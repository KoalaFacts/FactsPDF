"""Observe pinned Playwright's real apt children offline, including a negative control.

A fake sudo models environment scrubbing, a Node preload models uid 0, and a
recording apt executable replaces package operations. No elevation/downloads.
"""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

from ubuntu_packages import install


def verify(cli):
    node = shutil.which('node')
    if not node:
        raise RuntimeError('Node is required')
    with tempfile.TemporaryDirectory(prefix='factspdf-apt-observation-') as directory:
        root = Path(directory)
        source = root / 'ubuntu.sources'
        source.write_text('Types: deb\nURIs: http://archive.ubuntu.com/ubuntu\nSuites: noble\nComponents: main\n')
        log = root / 'calls.jsonl'
        preload = root / 'root.cjs'
        preload.write_text('process.getuid = () => 0;\n')
        def executable(name, content):
            p = root / name
            p.write_text(content)
            p.chmod(0o755)
        executable('sudo', '#!/bin/sh\nexec env -i PATH="$PATH" "$@"\n')
        # node's absolute path prevents recursion through this test wrapper.
        executable('node', '#!/bin/sh\nexec ' + json.dumps(node) + ' --require ' + json.dumps(str(preload)) + ' "$@"\n')
        executable('apt-get', '#!' + sys.executable + '\n' +
                   'import json, os, pathlib, sys\n' +
                   'p = os.environ.get("APT_CONFIG")\n' +
                   'entry = {"args": sys.argv[1:], "config": pathlib.Path(p).read_text() if p else None}\n' +
                   'with open(' + repr(str(log)) + ', "a") as f: f.write(json.dumps(entry) + "\\n")\n')
        old_path = os.environ['PATH']
        os.environ['PATH'] = str(root) + os.pathsep + old_path
        try:
            install('packages', ['qpdf'], source)
            direct = [json.loads(line) for line in log.read_text().splitlines()]
            assert [item['args'] for item in direct] == [['update', '-qq'], ['install', '-y', '--no-install-recommends', 'qpdf']], direct
            assert all('Dir::Etc::sourceparts \"-\";' in item['config'] and str(source) in item['config'] for item in direct)
            log.unlink()
            install('playwright', [str(Path(cli).resolve())], source)
            observed = [json.loads(line) for line in log.read_text().splitlines()]
            assert len(observed) == 2, observed
            assert observed[0]['args'] == ['update'], observed
            assert observed[1]['args'][:2] == ['install', '-y'], observed
            assert all('Dir::Etc::sourceparts "-";' in item['config'] and str(source) in item['config'] for item in observed)
            log.unlink()
            # Omitting the explicit env assignment after sudo loses APT_CONFIG.
            env = dict(os.environ, APT_CONFIG=str(root / 'unused.conf'))
            subprocess.run(['sudo', str(root / 'node'), str(Path(cli).resolve()), 'install-deps', 'chromium'], env=env, check=True)
            negative = [json.loads(line) for line in log.read_text().splitlines()]
            assert len(negative) == 2 and all(item['config'] is None for item in negative), negative
            print('PASS: real Playwright apt update/install inherit scoped config; negative control loses it. No network or elevation.')
        finally:
            os.environ['PATH'] = old_path


if __name__ == '__main__':
    verify(sys.argv[1])
