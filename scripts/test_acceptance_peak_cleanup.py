"""An aggregate deadline must not strand the nested GNU time/renderer session."""
import importlib
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch


class PeakCleanupTests(unittest.TestCase):
    @staticmethod
    def alive(pid):
        try:
            # A killed orphan may briefly be a zombie until its new parent reaps it.
            state = Path(f'/proc/{pid}/stat').read_text().rsplit(')', 1)[1].split()[0]
            return state not in {'Z', 'X'}
        except FileNotFoundError:
            return False

    def test_aggregate_timeout_terminates_nested_supervisor_and_renderer(self):
        p = importlib.import_module('acceptance_peak')
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            ready = root / 'pids.json'
            hung = ('import os,json,time;from pathlib import Path;'
                    f'Path({str(ready)!r}).write_text(json.dumps({{"renderer":os.getpid(),'
                    '"supervisor":os.getppid(),"group":os.getpgrp()}));time.sleep(60)')
            job = {'kind': 'documents', 'tasks': [{'id': 'hung',
                'argv': [sys.executable, '-c', hung],
                'expected_pdf_sha256': '0'*64, 'expected_pages': 1}]}
            real_run = p._run
            def shortened(argv, work, timeout, **kwargs):
                return real_run(argv, work, 3, **kwargs)
            ids = None
            try:
                with patch.object(p, '_run', side_effect=shortened):
                    with self.assertRaises(subprocess.TimeoutExpired):
                        p._workers(job, root/'run', Path('/usr/bin/time'), 1)
                self.assertTrue(ready.is_file(), 'The nested probe must have really started')
                ids = json.loads(ready.read_text())
                for _ in range(40):
                    if not any(self.alive(ids[k]) for k in ('renderer', 'supervisor')): break
                    time.sleep(.025)
                self.assertFalse(self.alive(ids['renderer']), 'Aggregate timeout stranded renderer')
                self.assertFalse(self.alive(ids['supervisor']), 'Aggregate timeout stranded GNU time')
            finally:
                if ids is None and ready.exists(): ids = json.loads(ready.read_text())
                if ids and any(self.alive(ids[k]) for k in ('renderer', 'supervisor')):
                    # Cleanup the old-code RED orphan too, never an unrelated group.
                    if ids['group'] != os.getpgrp():
                        try: os.killpg(ids['group'], signal.SIGKILL)
                        except ProcessLookupError: pass

    def test_nested_probe_timeout_also_terminates_supervisor_and_renderer(self):
        p = importlib.import_module('acceptance_peak')
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); ready = root/'pids.json'
            code = ('import os,json,time;from pathlib import Path;'
                    f'Path({str(ready)!r}).write_text(json.dumps({{"renderer":os.getpid(),'
                    '"supervisor":os.getppid(),"group":os.getpgrp()}));time.sleep(60)')
            job = {'kind':'documents','tasks':[{'id':'hung','argv':[sys.executable,'-c',code],
                'expected_pdf_sha256':'0'*64,'expected_pages':1}]}
            wrapper = root/'worker.py'
            wrapper.write_text('import sys\nfrom pathlib import Path\n'
                f'sys.path.insert(0,{str(Path(p.__file__).parent)!r})\n'
                'import acceptance_peak as p\n'
                'original=p.supervised\n'
                'def bounded(argv,work,timeout,supervisor,**kwargs):\n'
                '    return original(argv,work,.5,supervisor,**kwargs)\n'
                'p.supervised=bounded\n'
                f"p._worker({job!r},Path({str(root/'inner')!r}),Path('/usr/bin/time'),0,1)\n")
            ids = None
            try:
                with self.assertRaises(RuntimeError):
                    p._run([sys.executable,str(wrapper)],root/'outer',5)
                self.assertTrue(ready.is_file(), 'Nested renderer must really execute')
                ids = json.loads(ready.read_text())
                for _ in range(40):
                    if not any(self.alive(ids[k]) for k in ('renderer','supervisor')): break
                    time.sleep(.025)
                self.assertFalse(self.alive(ids['renderer']))
                self.assertFalse(self.alive(ids['supervisor']))
            finally:
                if ids is None and ready.exists(): ids=json.loads(ready.read_text())
                if ids and any(self.alive(ids[k]) for k in ('renderer','supervisor')) and ids['group']!=os.getpgrp():
                    try: os.killpg(ids['group'],signal.SIGKILL)
                    except ProcessLookupError: pass

    def test_shared_group_refuses_a_caller_that_does_not_own_the_session(self):
        p = importlib.import_module('acceptance_peak')
        with tempfile.TemporaryDirectory() as temp, \
             patch.object(p.os,'getpid',return_value=100), \
             patch.object(p.os,'getpgrp',return_value=200), \
             patch.object(p.os,'getsid',return_value=200):
            with self.assertRaisesRegex(ValueError,'worker-owned'):
                p._run(['/bin/true'],Path(temp)/'out',1,join_worker_group=True)


if __name__ == '__main__': unittest.main()
