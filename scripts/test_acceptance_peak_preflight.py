"""Dependency diagnostics only; these tests do not validate RSS measurements."""
from contextlib import ExitStack
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

import acceptance_peak as peak


class PeakPreflightTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def assert_rejected_without_side_effects(self, supervisor, message, platform='linux'):
        job = {'kind': 'controls', 'tasks': [
            {'id': 'control', 'child_mib': 0, 'argv': [sys.executable]}]}
        entrypoints = {
            'supervised': lambda work: peak.supervised([sys.executable], work, 1, supervisor),
            'calibrate': lambda work: peak.calibrate(work, compiler=sys.executable, supervisor=supervisor),
            'workers': lambda work: peak._workers(job, work, supervisor, 1),
            'worker': lambda work: peak._worker(job, work, supervisor, 0, 1),
        }
        for name, invoke in entrypoints.items():
            work = self.root/name/'output'
            with self.subTest(entrypoint=name), ExitStack() as stack:
                stack.enter_context(patch.object(peak.sys, 'platform', platform))
                launches = [stack.enter_context(patch.object(owner, method,
                    side_effect=AssertionError('Dependency validation attempted to launch a process')))
                    for owner, method in ((peak, '_run'), (peak.subprocess, 'check_output'),
                                          (peak.subprocess, 'run'), (peak.subprocess, 'Popen'))]
                with self.assertRaisesRegex(RuntimeError, message):
                    invoke(work)
                for launch in launches:
                    launch.assert_not_called()
                self.assertFalse(work.parent.exists(), 'Dependency validation created output directories')

    def test_missing_supervisor_fails_before_directories_or_processes(self):
        self.assert_rejected_without_side_effects(self.root/'missing-supervisor', 'supervisor')

    def test_non_executable_supervisor_fails_before_directories_or_processes(self):
        supervisor = self.root/'non-executable-supervisor'
        supervisor.write_text('This is not a measurement tool.\n')
        supervisor.chmod(0o600)
        self.assert_rejected_without_side_effects(supervisor, 'not executable')

    def test_directory_supervisor_fails_before_directories_or_processes(self):
        self.assert_rejected_without_side_effects(self.root, 'supervisor')

    def test_non_linux_fails_before_directories_or_processes(self):
        self.assert_rejected_without_side_effects(Path(sys.executable), 'Linux', platform='darwin')

    def test_calibration_checks_native_protocol_before_output_creation(self):
        work = self.root/'output'
        supervisor = Path(sys.executable).resolve()
        # Simulate the version reply only; never execute a substitute measurement tool.
        with patch.object(peak.sys, 'platform', 'linux'), \
             patch.object(peak.subprocess, 'check_output', return_value=b'BSD time version 1.0\n') as version, \
             patch.object(peak.subprocess, 'run') as compile_control, \
             patch.object(peak, '_workers') as workers:
            with self.assertRaisesRegex(ValueError, 'wait4 supervisor'):
                peak.calibrate(work, compiler=sys.executable, supervisor=supervisor)
            version.assert_called_once_with([str(supervisor), '--version'], timeout=10)
            compile_control.assert_not_called()
            workers.assert_not_called()
        self.assertFalse(work.exists())

    def test_missing_compiler_fails_before_directories_or_processes(self):
        work = self.root/'output'
        with patch.object(peak.shutil, 'which', return_value=None), \
             patch.object(peak.subprocess, 'run') as compile_control, \
             patch.object(peak.subprocess, 'check_output') as version:
            with self.assertRaisesRegex(RuntimeError, 'compiler'):
                peak.calibrate(work)
            compile_control.assert_not_called()
            version.assert_not_called()
        self.assertFalse(work.exists())


if __name__ == '__main__': unittest.main()
