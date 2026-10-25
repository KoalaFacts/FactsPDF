"""Real private native software archive boundary, with tamper/mode controls."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

from stage_delivery_candidate import assemble_native, NATIVE_ARCHIVE_NAME


class NativeArchiveTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.base = Path(self.tmp.name)
        self.root = self.base / 'root'
        self.build = self.base / 'build'
        self.root.mkdir()
        self.build.mkdir()
        self.source = 'f' * 40
        self.selected = {'version': '0.1.0-preview.1', 'profile': 'local-linux'}
        self.payload = b'\x7fELF' + b'\x00' * 12 + b'compiled payload'
        native = self.build / 'native/FactsPDF.Cli'
        native.parent.mkdir()
        native.write_bytes(self.payload)
        native.chmod(0o755)
        (self.root / 'LICENSE.md').write_text('unchanged license')
        doc = self.root / 'docs/private-native-cli-readme.md'
        doc.parent.mkdir()
        doc.write_text('native usage instructions')
        self.record = {'schemaVersion': 1, 'phase': 'private-build-unverified',
                       'sourceHead': self.source, 'identity': self.selected,
                       'candidateBuilt': True, 'candidateAssembled': False,
                       'independentlyValidated': False, 'published': False,
                       'distributionAuthorized': False,
                       'files': [{'path': 'native/FactsPDF.Cli',
                                  'bytes': len(self.payload),
                                  'sha256': hashlib.sha256(self.payload).hexdigest()}]}
        self.save()
        self.out = self.base / 'delivery'

    def save(self):
        (self.build / 'private-build.json').write_text(json.dumps(self.record))

    def assemble(self):
        with patch('stage_delivery_candidate.current_source', return_value=self.source), \
             patch('stage_delivery_candidate.identity', return_value=self.selected):
            return assemble_native(self.root, self.build, self.out)

    def test_real_three_member_native_software_archive_and_modes(self):
        report = self.assemble()
        with zipfile.ZipFile(self.out / NATIVE_ARCHIVE_NAME.format(rid='linux-x64')) as z:
            self.assertEqual(sorted(z.namelist()), ['FactsPDF.Native/CLI-README.md',
                'FactsPDF.Native/FactsPDF.Cli', 'FactsPDF.Native/LICENSE.md'])
            self.assertEqual(z.read('FactsPDF.Native/FactsPDF.Cli'), self.payload)
            self.assertEqual(z.getinfo('FactsPDF.Native/FactsPDF.Cli').external_attr >> 16, 0o100755)
            self.assertIsNone(z.testzip())
        self.assertFalse(report['candidateAssembled'])
        self.assertFalse(report['independentlyValidated'])

    def test_mutated_real_binary_fails_before_output(self):
        (self.build / 'native/FactsPDF.Cli').write_bytes(self.payload + b'tampered')
        with self.assertRaisesRegex(ValueError, 'changed after receipt'):
            self.assemble()
        self.assertFalse(self.out.exists())

    def test_non_elf_even_matching_hash_rejected(self):
        changed = b'FAKE' + self.payload[4:]
        (self.build / 'native/FactsPDF.Cli').write_bytes(changed)
        self.record['files'][0]['sha256'] = hashlib.sha256(changed).hexdigest()
        self.save()
        with self.assertRaisesRegex(ValueError, 'executable ELF'):
            self.assemble()

    def test_non_executable_linux_elf_rejected(self):
        (self.build / 'native/FactsPDF.Cli').chmod(0o644)
        with self.assertRaisesRegex(ValueError, 'executable ELF'):
            self.assemble()

    def test_license_symlink_and_duplicate_receipt_rejected(self):
        license = self.root / 'LICENSE.md'
        license.unlink()
        license.symlink_to('docs/private-native-cli-readme.md')
        with self.assertRaisesRegex(ValueError, 'symlinked'):
            self.assemble()
        license.unlink()
        license.write_text('unchanged license')
        self.record['files'].append(self.record['files'][0])
        self.save()
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            self.assemble()

    def test_existing_output_never_replaced(self):
        self.out.mkdir()
        (self.out / 'keep').write_bytes(b'keep')
        with self.assertRaisesRegex(ValueError, 'Fresh external'):
            self.assemble()
        self.assertEqual((self.out / 'keep').read_bytes(), b'keep')

    def test_windows_pe_profile_is_not_retained(self):
        self.selected = {'version': '0.1.0-preview.1',
                         'profile': 'win-x64-validation', 'binaryRetained': False}
        self.record['identity'] = self.selected
        (self.build / 'native/FactsPDF.Cli').unlink()
        exe = self.build / 'native/FactsPDF.Cli.exe'
        exe.write_bytes(b'MZ' + self.payload[2:])
        self.record['files'] = [{'path': 'native/FactsPDF.Cli.exe',
                                 'bytes': exe.stat().st_size,
                                 'sha256': hashlib.sha256(exe.read_bytes()).hexdigest()}]
        self.save()
        report = self.assemble()
        self.assertFalse(report['identity']['binaryRetained'])
        with zipfile.ZipFile(self.out / NATIVE_ARCHIVE_NAME.format(rid='win-x64')) as z:
            self.assertEqual(z.read('FactsPDF.Native/FactsPDF.Cli.exe'), exe.read_bytes())


if __name__ == '__main__':
    unittest.main()
