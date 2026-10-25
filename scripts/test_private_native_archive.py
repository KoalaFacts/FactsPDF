"""Real private native software archive boundary, with tamper/mode controls."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

from stage_delivery_candidate import assemble_native, NATIVE_ARCHIVE_NAME


def valid_linux_elf() -> bytes:
    import struct
    data = bytearray(256)
    data[:16] = b'\x7fELF\x02\x01\x01' + bytes(9)
    struct.pack_into('<HHIQQQIHHHHHH', data, 16,
                     3, 62, 1, 0x400000, 64, 0, 0, 64, 56, 1, 0, 0, 0)
    struct.pack_into('<IIQQQQQQ', data, 64,
                     1, 5, 0, 0x400000, 0x400000, len(data), len(data), 0x1000)
    return bytes(data)


def valid_windows_pe() -> bytes:
    import struct
    data = bytearray(1024)
    data[:2] = b'MZ'
    struct.pack_into('<I', data, 0x3c, 0x80)
    data[0x80:0x84] = b'PE\x00\x00'
    struct.pack_into('<HHIIIHH', data, 0x84, 0x8664, 1, 0, 0, 0, 0xf0, 0x22)
    optional = 0x98
    struct.pack_into('<H', data, optional, 0x20b)
    struct.pack_into('<II', data, optional + 56, 0x2000, 0x200)
    struct.pack_into('<I', data, optional + 108, 16)
    section = optional + 0xf0
    data[section:section + 8] = b'.text\x00\x00\x00'
    struct.pack_into('<IIII', data, section + 8, 0x180, 0x1000, 0x200, 0x200)
    return bytes(data)


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
        self.payload = valid_linux_elf()
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
        exe.write_bytes(valid_windows_pe())
        self.record['files'] = [{'path': 'native/FactsPDF.Cli.exe',
                                 'bytes': exe.stat().st_size,
                                 'sha256': hashlib.sha256(exe.read_bytes()).hexdigest()}]
        self.save()
        report = self.assemble()
        self.assertFalse(report['identity']['binaryRetained'])
        with zipfile.ZipFile(self.out / NATIVE_ARCHIVE_NAME.format(rid='win-x64')) as z:
            self.assertEqual(z.read('FactsPDF.Native/FactsPDF.Cli.exe'), exe.read_bytes())


    def test_reject_truncated_wrong_architecture_and_invalid_elf_headers(self):
        import struct
        cases = {'truncated-magic': b'\x7fELF',
                 'elf32': bytearray(self.payload),
                 'arm64': bytearray(self.payload),
                 'missing-program-table': bytearray(self.payload),
                 'bad-segment-offset': bytearray(self.payload),
                 'no-loadable-segment': bytearray(self.payload)}
        cases['elf32'][4] = 1
        struct.pack_into('<H', cases['arm64'], 18, 183)
        struct.pack_into('<Q', cases['missing-program-table'], 32, len(self.payload))
        struct.pack_into('<Q', cases['bad-segment-offset'], 64 + 8, len(self.payload))
        struct.pack_into('<I', cases['no-loadable-segment'], 64, 2)
        for name, raw in cases.items():
            with self.subTest(name=name):
                data = bytes(raw)
                (self.build / 'native/FactsPDF.Cli').write_bytes(data)
                self.record['files'][0].update(bytes=len(data),
                    sha256=hashlib.sha256(data).hexdigest())
                self.save()
                with self.assertRaisesRegex(ValueError, 'ELF'):
                    self.assemble()
                self.assertFalse(self.out.exists())

    def test_reject_windows_truncated_wrong_architecture_and_sections(self):
        import struct
        self.selected = {'version': '0.1.0-preview.1',
                         'profile': 'win-x64-validation', 'binaryRetained': False}
        self.record['identity'] = self.selected
        (self.build / 'native/FactsPDF.Cli').unlink()
        exe = self.build / 'native/FactsPDF.Cli.exe'
        cases = {'truncated-magic': b'MZ',
                 'arm64': bytearray(valid_windows_pe()),
                 'pe32': bytearray(valid_windows_pe()),
                 'bad-signature': bytearray(valid_windows_pe()),
                 'bad-section': bytearray(valid_windows_pe()),
                 'not-executable': bytearray(valid_windows_pe())}
        struct.pack_into('<H', cases['arm64'], 0x84, 0xaa64)
        struct.pack_into('<H', cases['pe32'], 0x98, 0x10b)
        cases['bad-signature'][0x80:0x84] = b'JUNK'
        struct.pack_into('<I', cases['bad-section'], 0x98 + 0xf0 + 20, 9999)
        struct.pack_into('<H', cases['not-executable'], 0x84 + 18, 0x20)
        for name, raw in cases.items():
            with self.subTest(name=name):
                data = bytes(raw)
                exe.write_bytes(data)
                self.record['files'] = [{'path': 'native/FactsPDF.Cli.exe',
                    'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}]
                self.save()
                with self.assertRaisesRegex(ValueError, 'PE'):
                    self.assemble()
                self.assertFalse(self.out.exists())

if __name__ == '__main__':
    unittest.main()
