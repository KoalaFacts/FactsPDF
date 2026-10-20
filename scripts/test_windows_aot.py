"""Fail closed on mislabeled binaries, incorrect diagnostics and damaged outputs."""
import importlib.util
from pathlib import Path
import struct
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class WindowsAotTests(unittest.TestCase):
    def setUp(self):
        source = ROOT / 'scripts/verify_windows_aot.py'
        self.assertTrue(source.is_file(), 'Missing executable Windows AOT acceptance')
        spec = importlib.util.spec_from_file_location('windows_aot', source)
        self.module = importlib.util.module_from_spec(spec); spec.loader.exec_module(self.module)
        temporary = tempfile.TemporaryDirectory(); self.addCleanup(temporary.cleanup)
        self.work = Path(temporary.name)

    def pe(self):
        data = bytearray(512)
        data[:2] = b'MZ'; struct.pack_into('<I', data, 0x3c, 0x80)
        data[0x80:0x84] = b'PE\0\0'
        struct.pack_into('<H', data, 0x84, 0x8664)  # AMD64 COFF machine
        struct.pack_into('<H', data, 0x94, 240)  # optional-header size
        struct.pack_into('<H', data, 0x96, 2)  # executable, not DLL
        struct.pack_into('<H', data, 0x98, 0x20b)  # PE32+
        struct.pack_into('<I', data, 0x98 + 108, 16)  # data-directory count
        return data

    def test_requires_actual_windows_x64_host(self):
        self.module.validate_platform('Windows', 'AMD64')
        for system, machine in [('Linux', 'x86_64'), ('Windows', 'ARM64'), ('Windows', 'x86')]:
            with self.subTest(system=system, machine=machine), self.assertRaisesRegex(AssertionError, 'Windows x64'):
                self.module.validate_platform(system, machine)

    def test_pe_accepts_native_shape_but_rejects_wrong_architecture_and_managed_header(self):
        self.assertTrue(self.module.validate_pe(self.pe())['managedHeaderAbsent'])
        for offset, format_, value in [(0x84, '<H', 0x14c), (0x98, '<H', 0x10b),
                                       (0x96, '<H', 0x2002), (0x98 + 108, '<I', 14),
                                       (0x98 + 112 + 14*8, '<I', 0x1000)]:
            bad = self.pe(); struct.pack_into(format_, bad, offset, value)
            with self.subTest(offset=offset), self.assertRaises(AssertionError):
                self.module.validate_pe(bad)

    def test_pe_rejects_truncated_or_unrelated_files(self):
        for data in [b'', b'\x7fELF' + bytes(600), self.pe()[:100], self.pe()[:300]]:
            with self.subTest(length=len(data)), self.assertRaises(AssertionError):
                self.module.validate_pe(data)
        bad = self.pe(); struct.pack_into('<I', bad, 0x3c, 0xfffffff0)
        with self.assertRaises(AssertionError): self.module.validate_pe(bad)

    def test_diagnostic_requires_exact_error_exit_and_empty_stdout(self):
        message = b"FPDF1102: Element 'script' is not supported in this development slice.\r\n"
        self.module.validate_diagnostic(subprocess.CompletedProcess([], 3, b'', message))
        for code, out, error in [(0, b'', message), (3, b'%PDF', message),
                                 (3, b'', message.replace(b'1102', b'1103')),
                                 (3, b'', message + b'extra diagnostic\n')]:
            with self.subTest(code=code, stdout=out), self.assertRaises(AssertionError):
                self.module.validate_diagnostic(subprocess.CompletedProcess([], code, out, error))

    def test_success_accepts_existing_cli_status_on_stderr(self):
        self.assertTrue(hasattr(self.module, 'validate_success'), 'Missing existing CLI success contract')
        message = b'Wrote 2 page(s), 4258 bytes.\r\n'
        self.module.validate_success(subprocess.CompletedProcess([], 0, b'', message), 4258)
        for code, out, error in [(3, b'', message), (0, b'%PDF', message), (0, b'', b''),
                                 (0, b'', message.replace(b'4258', b'4259'))]:
            with self.subTest(code=code, stdout=out), self.assertRaises(AssertionError):
                self.module.validate_success(subprocess.CompletedProcess([], code, out, error), 4258)

    def test_destination_requires_preservation_and_no_temporary_leak(self):
        target = self.work / 'original output.pdf'
        target.write_bytes(b'original')
        self.module.validate_destination(target, b'original')
        target.write_bytes(b'changed')
        with self.assertRaises(AssertionError): self.module.validate_destination(target, b'original')
        target.unlink(); self.module.validate_destination(target, None)
        target.write_bytes(b'partial')
        with self.assertRaises(AssertionError): self.module.validate_destination(target, None)
        target.unlink(); (self.work / '.factspdf-leak.tmp').write_bytes(b'partial')
        with self.assertRaises(AssertionError): self.module.validate_destination(target, None)

    def test_staging_copies_only_the_executable(self):
        source = self.work / 'publish'; source.mkdir()
        executable = source / 'FactsPDF.Cli.exe'; executable.write_bytes(self.pe())
        for name in ('FactsPDF.Cli.dll', 'FactsPDF.dll', 'FactsPDF.Cli.runtimeconfig.json'):
            (source / name).write_bytes(b'managed sidecar')
        target = self.work / 'isolated executable'; target.mkdir()
        staged = self.module.stage_executable(executable, target)
        self.assertEqual(staged.read_bytes(), executable.read_bytes())
        self.assertEqual([p.name for p in target.iterdir()], ['FactsPDF.Cli.exe'])

    def test_page_mutant_reverses_real_references_without_changing_offsets(self):
        data = b'%PDF-1.7\n2 0 obj << /Type /Pages /Kids [3 0 R 5 0 R] /Count 2 >>\nendobj\n'
        mutated = self.module.reverse_page_references(data)
        self.assertEqual(len(mutated), len(data))
        self.assertIn(b'/Kids [5 0 R 3 0 R]', mutated)
        for wrong in [b'%PDF-1.7', data.replace(b'5 0 R', b'5 0 R 7 0 R')]:
            with self.assertRaises(AssertionError): self.module.reverse_page_references(wrong)


if __name__ == '__main__': unittest.main()
