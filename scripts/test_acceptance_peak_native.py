"""Real Linux wait4 supervisor checks; no GNU time or synthetic RSS samples."""
from pathlib import Path
import json
import signal
import subprocess
import sys
import tempfile
import unittest

import acceptance_peak as peak


class NativeSupervisorTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        build = getattr(peak, 'build_supervisor', None)
        self.assertTrue(callable(build), 'The repository needs a native wait4 supervisor builder')
        self.supervisor = build(self.root/'build')

    def run_raw(self, argv, name='usage'):
        usage = self.root/name
        result = subprocess.run([str(self.supervisor), '--usage', str(usage), '--', *argv],
                                capture_output=True, timeout=5)
        return result, usage

    def test_exit_code_and_binary_streams_are_preserved(self):
        result, usage = self.run_raw([sys.executable, '-c',
            'import sys;sys.stdout.buffer.write(b"%PDF-\\xff");'
            'sys.stderr.write("child diagnostic");sys.exit(42)'])
        self.assertEqual(result.returncode, 42)
        self.assertEqual(result.stdout, b'%PDF-\xff')
        self.assertEqual(result.stderr, b'child diagnostic')
        self.assertRegex(usage.read_text(), r'^peak_rss_kib=[0-9]+\nexit_code=42\n$')
        with self.assertRaises(ValueError): peak.parse_usage(usage.read_text())

    def test_signal_exit_is_rejected_as_a_successful_sample(self):
        result, usage = self.run_raw([sys.executable, '-c',
            'import os,signal;os.kill(os.getpid(),signal.SIGTERM)'])
        self.assertEqual(result.returncode, 128 + signal.SIGTERM)
        self.assertIn('exit_code=143\n', usage.read_text())
        with self.assertRaises(ValueError): peak.parse_usage(usage.read_text())

    def test_exec_failures_preserve_126_and_127(self):
        non_executable = self.root/'not-executable'
        non_executable.write_text('not a runnable file')
        for executable, status in ((self.root/'absent', 127), (non_executable, 126)):
            with self.subTest(status=status):
                result, usage = self.run_raw([str(executable)], name=str(status))
                self.assertEqual(result.returncode, status)
                self.assertIn(f'exit_code={status}\n', usage.read_text())
                self.assertIn(b'exec', result.stderr)
                with self.assertRaises(ValueError): peak.parse_usage(usage.read_text())

    def test_released_transient_allocation_remains_in_peak_but_not_next_child(self):
        code = ('import json,mmap;from pathlib import Path;'
                'p=mmap.mmap(-1,64*1024*1024);'
                'p[::4096]=b"x"*(64*1024*1024//4096);p.close();'
                'rss=next(line for line in Path("/proc/self/status").read_text().splitlines()'
                ' if line.startswith("VmRSS:"));print(json.dumps({"rss":int(rss.split()[1])*1024}))')
        row = peak.supervised([sys.executable, '-c', code], self.root/'transient', 5, self.supervisor)
        final_rss = json.loads(Path(row['stdout_path']).read_bytes())['rss']
        self.assertGreaterEqual(row['peak_rss_bytes'], 64*1024*1024)
        self.assertGreater(row['peak_rss_bytes'] - final_rss, 32*1024*1024)
        small = peak.supervised([sys.executable, '-c', 'print("small")'],
                                self.root/'next', 5, self.supervisor)
        self.assertGreater(small['peak_rss_bytes'], 0)
        self.assertLess(small['peak_rss_bytes'], row['peak_rss_bytes']/2)

    def test_unwritable_report_does_not_launch_child_or_replace_existing_file(self):
        usage = self.root/'usage'
        usage.write_text('keep')
        marker = self.root/'child-started'
        result, _ = self.run_raw([sys.executable, '-c',
            f'from pathlib import Path;Path({str(marker)!r}).write_text("started")'])
        self.assertEqual(result.returncode, 125)
        self.assertEqual(usage.read_text(), 'keep')
        self.assertFalse(marker.exists())


if __name__ == '__main__': unittest.main()
