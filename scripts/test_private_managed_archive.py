"""Negative/positive software-archive boundary checks without a mock .NET build."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

from stage_delivery_candidate import assemble_managed, MANAGED_ARCHIVE_NAME, MANAGED_MEMBERS


class ManagedPrivateArchiveTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.base = Path(self.tmp.name)
        self.root = self.base / 'source'
        self.build = self.base / 'build'
        self.root.mkdir()
        self.build.mkdir()
        self.identity = {'packageId': 'FactsPDF', 'version': '0.1.0-preview.1',
                         'profile': 'local-linux'}
        self.source = 'f' * 40
        records = []
        for path, _ in MANAGED_MEMBERS:
            target = (self.build if path.startswith('managed/') else self.root) / path
            target.parent.mkdir(parents=True, exist_ok=True)
            data = ('real bytes for ' + path).encode()
            target.write_bytes(data)
            if path.startswith('managed/'):
                records.append({'path': path, 'bytes': len(data),
                                'sha256': hashlib.sha256(data).hexdigest()})
        receipt = {'schemaVersion': 1, 'phase': 'private-build-unverified',
                   'sourceHead': self.source, 'identity': self.identity,
                   'candidateBuilt': True, 'candidateAssembled': False,
                   'independentlyValidated': False, 'published': False,
                   'distributionAuthorized': False, 'files': records}
        (self.build / 'private-build.json').write_text(json.dumps(receipt))
        self.out = self.base / 'software'

    def assemble(self):
        with patch('stage_delivery_candidate.current_source', return_value=self.source), \
             patch('stage_delivery_candidate.identity', return_value=self.identity):
            return assemble_managed(self.root, self.build, self.out)

    def test_real_software_members_only_and_exact_receipt(self):
        report = self.assemble()
        with zipfile.ZipFile(self.out / MANAGED_ARCHIVE_NAME) as archive:
            self.assertEqual(sorted(archive.namelist()),
                             sorted(name for _, name in MANAGED_MEMBERS))
            self.assertIsNone(archive.testzip())
            for original, destination in MANAGED_MEMBERS:
                base = self.build if original.startswith('managed/') else self.root
                self.assertEqual(archive.read(destination), (base / original).read_bytes())
        self.assertFalse(report['candidateAssembled'])
        self.assertFalse(report['independentlyValidated'])
        self.assertEqual(report['sourceHead'], self.source)

    def test_mutated_dll_rejected_before_archive(self):
        (self.build / 'managed/FactsPDF.dll').write_bytes(b'tampered')
        with self.assertRaisesRegex(ValueError, 'changed after receipt'):
            self.assemble()
        self.assertFalse(self.out.exists())

    def test_changed_source_or_license_symlink_rejected(self):
        (self.root / 'LICENSE.md').unlink()
        (self.root / 'LICENSE.md').symlink_to('docs/private-managed-cli-readme.md')
        with self.assertRaisesRegex(ValueError, 'symlinked'):
            self.assemble()
        self.assertFalse(self.out.exists())

    def test_duplicate_build_records_rejected(self):
        path = self.build / 'private-build.json'
        receipt = json.loads(path.read_text())
        receipt['files'].append(receipt['files'][0])
        path.write_text(json.dumps(receipt))
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            self.assemble()

    def test_existing_target_never_overwritten(self):
        self.out.mkdir()
        (self.out / 'important.txt').write_text('preserve')
        with self.assertRaisesRegex(ValueError, 'Fresh external'):
            self.assemble()
        self.assertEqual((self.out / 'important.txt').read_text(), 'preserve')


if __name__ == '__main__':
    unittest.main()
