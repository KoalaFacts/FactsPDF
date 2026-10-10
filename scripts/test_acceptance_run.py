import importlib
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest


class RunnerTests(unittest.TestCase):
    def setUp(self):
        try: self.r = importlib.import_module('acceptance_run')
        except ModuleNotFoundError: self.fail('Task 2 acceptance_run is not implemented')
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = self.root / 'input.html'; self.source.write_text('<p>A</p>')
        self.case = {'id': 'B-test', 'kind': 'baseline', 'input_path': 'input.html', '_root': self.root,
                     'input_sha256': __import__('hashlib').sha256(self.source.read_bytes()).hexdigest(),
                     'entrypoints': {'cli': {'expected_exit': 0, 'expected_code': None}, 'api': {'expected_exit': 0, 'expected_code': None}},
                     'font_roles': [], 'requirements': ['text'], 'checks': {}}

    def exe(self, code):
        file = self.root / 'fake-native'
        file.write_text('#!' + sys.executable + '\n' + code)
        file.chmod(0o755); return file

    def test_cli_and_api_use_identical_inputs_and_font_order(self):
        fonts = [self.root/'first.ttf', self.root/'second.ttf']
        for p in fonts: p.write_bytes(p.name.encode())
        cli = self.r.command_for(self.case, 'cli', Path('/native'), fonts, self.root/'out.pdf')
        api = self.r.command_for(self.case, 'api', Path('/host'), fonts, self.root/'out.pdf')
        self.assertEqual(cli[1:3], api[2:4])
        self.assertEqual(cli[-4:], api[-4:])
        self.assertEqual(cli[-4:], ['--font', str(fonts[0]), '--font', str(fonts[1])])

    def test_host_only_case_rejects_cli(self):
        self.case['entrypoints'].pop('cli'); self.case['host_limits'] = {'MaxPages': 1}
        with self.assertRaises(ValueError):
            self.r.command_for(self.case, 'cli', Path('/native'), [], self.root/'out')
        cmd = self.r.command_for(self.case, 'api', Path('/host'), [], self.root/'out')
        self.assertIn('MaxPages=1', cmd)

    def test_timeout_has_no_success_record_and_child_is_reaped(self):
        exe = self.exe('import time\ntime.sleep(60)\n')
        row = self.r.run_case(self.case, 'cli', exe, [], self.root/'runs', 0.05)
        self.assertTrue(row['timed_out'])
        self.assertEqual(row['status'], 'infrastructure-error')
        with self.assertRaises(ProcessLookupError): os.kill(row['pid'], 0)

    def test_binary_stdout_is_not_decoded_as_text(self):
        exe = self.exe("import sys\nsys.stdout.buffer.write(b'%PDF-1.7\\n\\xff\\x00')\nsys.stderr.write('status only')\n")
        row = self.r.run_process([str(exe)], self.root/'process', 2)
        self.assertEqual(Path(row['stdout_path']).read_bytes(), b'%PDF-1.7\n\xff\x00')
        self.assertEqual(Path(row['stderr_path']).read_text(), 'status only')

    def test_output_guard_preserves_existing_bytes(self):
        exe = self.exe("import sys\nsys.stderr.write('FPDF1302: cannot fit')\nsys.exit(3)\n")
        self.case.update(kind='negative', expected_code='FPDF1302')
        self.case['entrypoints']['cli'] = {'expected_exit':3, 'expected_code':'FPDF1302'}
        row = self.r.run_case(self.case, 'cli', exe, [], self.root/'runs', 2)
        self.assertEqual(row['status'], 'expected-error')
        self.assertTrue(row['output_unchanged'])
        self.assertEqual((self.root/'runs/B-test/cli/result.pdf').read_bytes(), b'keep')

    def test_high_and_low_surrogates_are_constructed_inside_host(self):
        self.case['entrypoints'].pop('cli')
        for scalar in (0xD800,0xDC00):
            self.case['invalid_scalar'] = scalar
            cmd = self.r.command_for(self.case, 'api', Path('/host'), [], self.root/'out')
            self.assertEqual(cmd[cmd.index('--invalid-scalar')+1], str(scalar))
            ''.join(cmd).encode('utf-8')

    def test_success_exit_without_pdf_is_not_passed(self):
        row = self.r.run_case(self.case, 'cli', self.exe('pass\n'), [], self.root/'runs', 2)
        self.assertEqual(row['status'], 'infrastructure-error')

    def test_inapplicable_entry_is_rejected_not_silently_skipped(self):
        self.case['entrypoints'].pop('api')
        with self.assertRaises(ValueError): self.r.run_case(self.case, 'api', Path('/host'), [], self.root/'runs', 2)

if __name__ == '__main__': unittest.main()
