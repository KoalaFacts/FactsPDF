"""Offline source-selection and fail-closed tests; never installs packages."""
import importlib.util
from pathlib import Path
import subprocess
import os
import tempfile
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location('ubuntu_packages', Path(__file__).with_name('ubuntu_packages.py'))
installer = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(installer)


class UbuntuPackagesTests(unittest.TestCase):
    def test_existing_formats_preserve_source_and_signature_options(self):
        sources = [
            ('sources.list', 'deb [signed-by=/keyring.gpg] http://archive.ubuntu.com/ubuntu jammy main universe\n'),
            ('ubuntu.sources', 'Types: deb\nURIs: http://azure.archive.ubuntu.com/ubuntu\nSuites: noble noble-updates\nComponents: main universe\nSigned-By: /keyring.gpg\n'),
        ]
        for name, content in sources:
            with self.subTest(name=name), tempfile.TemporaryDirectory() as directory:
                source = Path(directory) / name
                source.write_text(content)
                config = installer.source_config(source)
                self.assertIn(f'Dir::Etc::sourcelist "{source}";', config)
                self.assertIn('Dir::Etc::sourceparts "-";', config)
                self.assertEqual(source.read_text(), content)
                self.assertNotIn('AllowUnauthenticated', config)

    def test_unrelated_mixed_missing_or_empty_source_fails_before_execution(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / 'ubuntu.sources'
            with self.assertRaises((FileNotFoundError, ValueError)):
                installer.source_config(source)
            for content in ('', '# only a comment\n', 'deb https://packages.microsoft.com/ubuntu noble main\n',
                            'deb http://archive.ubuntu.com/ubuntu noble main\ndeb https://example.invalid noble main\n',
                            'deb tor+https://archive.ubuntu.com/ubuntu noble main\n'):
                source.write_text(content)
                with self.subTest(content=content), self.assertRaises(ValueError):
                    installer.source_config(source)

    def test_existing_local_ubuntu_mirrorlist_is_preserved_and_checked(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            mirrors = root / 'apt-mirrors.txt'
            mirrors.write_text('http://azure.archive.ubuntu.com/ubuntu priority:1\nhttp://archive.ubuntu.com/ubuntu priority:2\n')
            source = root / 'ubuntu.sources'
            source.write_text(f'Types: deb\nURIs: mirror+file:{mirrors}\nSuites: noble\nComponents: main\n')
            self.assertIn(str(source), installer.source_config(source))
            mirrors.write_text('https://packages.microsoft.com/ubuntu\n')
            with self.assertRaises(ValueError):
                installer.source_config(source)
            mirrors.write_text('')
            with self.assertRaises(ValueError):
                installer.source_config(source)

    def test_failed_update_stops_before_package_install(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / 'sources.list'
            source.write_text('deb http://archive.ubuntu.com/ubuntu jammy main\n')
            with patch.object(installer.subprocess, 'run', side_effect=subprocess.CalledProcessError(100, 'apt-get')) as run:
                with self.assertRaises(subprocess.CalledProcessError):
                    installer.install('packages', ['qpdf'], source)
                self.assertEqual(run.call_count, 1)
                self.assertEqual(run.call_args.args[0][-3:], ['apt-get', 'update', '-qq'])
                self.assertTrue(run.call_args.kwargs['check'])

    def test_real_apt_uri_selection_excludes_unrelated_source(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'ubuntu.sources'
            source.write_text('Types: deb\nURIs: http://archive.ubuntu.com/ubuntu\nSuites: noble\nComponents: main\n')
            parts = root / 'parts'; parts.mkdir()
            (parts / 'unrelated.list').write_text('deb https://unrelated.example.invalid/ubuntu noble main\n')
            base = ['apt-get', '-o', f'Dir::Etc::sourcelist={source}', '-o', f'Dir::Etc::sourceparts={parts}', '--print-uris', 'update']
            before = subprocess.run(base, check=True, capture_output=True, text=True).stdout
            self.assertIn('unrelated.example.invalid', before)
            config = root / 'apt.conf'; config.write_text(installer.source_config(source))
            after = subprocess.run(['apt-get', '--print-uris', 'update'], env=dict(os.environ, APT_CONFIG=str(config)), check=True, capture_output=True, text=True).stdout
            self.assertIn('archive.ubuntu.com', after)
            self.assertNotIn('unrelated.example.invalid', after)


if __name__ == '__main__':
    unittest.main()
