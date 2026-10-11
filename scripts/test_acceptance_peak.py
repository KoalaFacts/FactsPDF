import importlib
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

class PeakTests(unittest.TestCase):
    def setUp(self):
        try: self.p = importlib.import_module('acceptance_peak')
        except ModuleNotFoundError: self.fail('Calibrated peak collector is not implemented')
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def test_strict_report_parser_keeps_zero_unknown_and_rejects_invalid_values(self):
        self.assertEqual(self.p.parse_usage('peak_rss_kib=123\nexit_code=0\n'), 123*1024)
        self.assertIsNone(self.p.parse_usage('peak_rss_kib=0\nexit_code=0\n'))
        for text in ('', 'peak_rss_kib=-1\nexit_code=0\n', 'peak_rss_kib=1\nexit_code=1\n',
                     'peak_rss_kib=nan\nexit_code=0\n', 'peak_rss_kib=1\nexit_code=0\nextra=2\n'):
            with self.subTest(text=text), self.assertRaises(ValueError): self.p.parse_usage(text)

    def test_failed_executable_and_timeout_never_become_peak_samples(self):
        with self.assertRaises(RuntimeError):
            self.p.supervised(['/bin/false'], self.root/'failed', 2)
        with self.assertRaises(TimeoutError):
            self.p.supervised(['/bin/sleep','30'], self.root/'timeout', .05)
        with self.assertRaises(RuntimeError):
            self.p.supervised(['/definitely-not-an-executable'], self.root/'missing', 2)

    def test_stdout_and_stderr_are_separate_from_measurement(self):
        row = self.p.supervised([sys.executable,'-c',
            'import sys;sys.stdout.buffer.write(b"%PDF-\\xff");sys.stderr.write("diagnostic")'],self.root/'io',5)
        self.assertEqual(Path(row['stdout_path']).read_bytes(),b'%PDF-\xff')
        self.assertEqual(Path(row['stderr_path']).read_text(),'diagnostic')
        self.assertEqual(row['peak_scope'],self.p.PEAK_SCOPE)

    def test_real_controls_are_sensitive_and_invariant_to_large_launcher(self):
        if not sys.platform.startswith('linux'): self.skipTest('Linux process metrics only')
        compiler=shutil.which('cc') or shutil.which('clang')
        self.assertIsNotNone(compiler, 'The Linux acceptance job requires a native compiler')
        report=self.p.calibrate(self.root/'calibration', repeats=1, compiler=compiler)
        self.assertTrue(report['controls_validated'])
        self.assertEqual(len(report['rows']),6)
        self.assertEqual({r['parent_mib'] for r in report['rows']},{0,192})
        self.assertEqual({r['child_mib'] for r in report['rows']},{0,16,128})
        self.assertFalse(report['budget_approved'])

    def test_control_revalidation_rejects_missing_duplicates_floor_and_flat_results(self):
        rows=[]
        for parent in (0,192):
            for child in (0,16,128):
                peak=(child+1)*1024*1024
                rows.append({'parent_mib':parent,'child_mib':child,'sample_index':0,
                             'peak_rss_bytes':peak,'vm_hwm_bytes':peak})
        self.p.validate_controls(rows,1)
        for broken in (rows[:-1], rows+[dict(rows[0])],
                       [{**r,'peak_rss_bytes':300*1024*1024} for r in rows],
                       [{**r,'peak_rss_bytes':r['peak_rss_bytes']+(100*1024*1024 if r['parent_mib'] else 0)} for r in rows]):
            with self.assertRaises(ValueError): self.p.validate_controls(broken,1)

if __name__=='__main__': unittest.main()
