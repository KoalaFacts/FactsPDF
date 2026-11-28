"""Fail closed on mislabeled binaries, incorrect diagnostics and damaged outputs."""
import importlib.util
from pathlib import Path
import struct
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class NativeAotTests(unittest.TestCase):
    def setUp(self):
        source = ROOT / 'scripts/verify_native_aot.py'
        self.assertTrue(source.is_file(), 'Missing executable Native AOT acceptance')
        spec = importlib.util.spec_from_file_location('native_aot', source)
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

    def elf(self, machine=62):
        data = bytearray(128)
        data[:4] = b'ELF'; data[4] = 2; data[5] = 1
        struct.pack_into('<HH', data, 16, 3, machine)
        return data

    def macho(self, cpu=0x0100000c):
        data = bytearray(64)
        struct.pack_into('<IIII', data, 0, 0xfeedfacf, cpu, 0, 2)
        return data

    def test_requires_the_actual_host_of_the_target_rid(self):
        hosts = {'win-x64': ('Windows', 'AMD64'), 'win-arm64': ('Windows', 'ARM64'),
                 'linux-x64': ('Linux', 'x86_64'), 'linux-arm64': ('Linux', 'aarch64'),
                 'osx-x64': ('Darwin', 'x86_64'), 'osx-arm64': ('Darwin', 'arm64')}
        for rid, (system, machine) in hosts.items():
            with self.subTest(rid=rid):
                self.module.validate_platform(system, machine, rid)
                self.assertEqual(self.module.detect_rid(system, machine), rid)
                for other, (osys, omachine) in hosts.items():
                    if other != rid:
                        with self.assertRaisesRegex(AssertionError, 'Requires actual'):
                            self.module.validate_platform(osys, omachine, rid)
        with self.assertRaisesRegex(AssertionError, 'Windows x64'):
            self.module.validate_platform('Windows', 'ARM64')
        with self.assertRaises(AssertionError): self.module.detect_rid('Linux', 'riscv64')

    def test_elf_requires_the_target_machine_and_an_executable_type(self):
        self.assertEqual(self.module.validate_binary(self.elf(62), 'linux-x64')['machine'], 'x86-64')
        self.assertEqual(self.module.validate_binary(self.elf(183), 'linux-arm64')['machine'], 'AArch64')
        for rid in ('linux-x64', 'linux-arm64'):
            wrong = self.elf(183 if rid == 'linux-x64' else 62)
            with self.subTest(rid=rid), self.assertRaises(AssertionError): self.module.validate_binary(wrong, rid)
        for edit in [(4, '<B', 1), (5, '<B', 2), (16, '<H', 1), (16, '<H', 4)]:
            bad = self.elf(); struct.pack_into(edit[1], bad, edit[0], edit[2])
            with self.subTest(edit=edit), self.assertRaises(AssertionError): self.module.validate_elf(bad, 62)
        with self.assertRaises(AssertionError): self.module.validate_binary(bytes(self.pe()), 'linux-x64')

    def test_macho_requires_a_thin_executable_for_the_target_cpu(self):
        self.assertEqual(self.module.validate_binary(self.macho(0x0100000c), 'osx-arm64')['machine'], 'arm64')
        self.assertEqual(self.module.validate_binary(self.macho(0x01000007), 'osx-x64')['machine'], 'x86_64')
        with self.assertRaises(AssertionError): self.module.validate_binary(self.macho(0x01000007), 'osx-arm64')
        with self.assertRaises(AssertionError): self.module.validate_binary(self.macho(0x0100000c), 'osx-x64')
        dylib = self.macho(); struct.pack_into('<I', dylib, 12, 6)
        with self.assertRaises(AssertionError): self.module.validate_macho(dylib, 0x0100000c)
        fat = self.macho(); struct.pack_into('<I', fat, 0, 0xbebafeca)
        with self.assertRaisesRegex(AssertionError, 'fat'): self.module.validate_macho(fat, 0x0100000c)
        with self.assertRaises(AssertionError): self.module.validate_macho(bytes(self.macho())[:20], 0x0100000c)
        with self.assertRaises(AssertionError): self.module.validate_binary(bytes(self.elf()), 'osx-arm64')

    def test_pe_arm64_is_distinct_from_amd64(self):
        arm = self.pe(); struct.pack_into('<H', arm, 0x84, 0xaa64)
        self.assertEqual(self.module.validate_binary(arm, 'win-arm64')['machine'], 'ARM64')
        with self.assertRaises(AssertionError): self.module.validate_binary(arm, 'win-x64')
        with self.assertRaises(AssertionError): self.module.validate_binary(self.pe(), 'win-arm64')

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
        message = (b"FPDF1102: Element 'marquee' is not supported in this development slice.\r\n"
                   b"Location: line 1, column 1.\r\n")
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

    def test_staging_keeps_the_posix_name_and_is_executable(self):
        source = self.work / 'publish'; source.mkdir()
        executable = source / 'FactsPDF.Cli'; executable.write_bytes(self.elf())
        target = self.work / 'isolated'; target.mkdir()
        staged = self.module.stage_executable(executable, target)
        self.assertEqual([p.name for p in target.iterdir()], ['FactsPDF.Cli'])
        self.assertEqual(staged.read_bytes(), executable.read_bytes())

    def test_page_mutant_reverses_real_references_without_changing_offsets(self):
        data = b'%PDF-1.7\n2 0 obj << /Type /Pages /Kids [3 0 R 5 0 R] /Count 2 >>\nendobj\n'
        mutated = self.module.reverse_page_references(data)
        self.assertEqual(len(mutated), len(data))
        self.assertIn(b'/Kids [5 0 R 3 0 R]', mutated)
        for wrong in [b'%PDF-1.7', data.replace(b'5 0 R', b'5 0 R 7 0 R')]:
            with self.assertRaises(AssertionError): self.module.reverse_page_references(wrong)


if __name__ == '__main__': unittest.main()
