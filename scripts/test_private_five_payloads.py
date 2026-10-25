"""Five-payload staging tests. Individual archive/member and installed-PDF acceptance are separate CI gates."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from private_examples_payload import FILES
from stage_delivery_candidate import (
    MANAGED_ARCHIVE_NAME, MANAGED_MEMBERS, NATIVE_ARCHIVE_NAME,
    assemble_five_payloads,
)

VERSION = '0.1.0-preview.1'
SOURCE = 'a' * 40


def stamp(data):
    return {'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}


class FivePayloadStagingTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(prefix='factspdf-five-stage-')
        self.addCleanup(temp.cleanup)
        parent = Path(temp.name)
        self.root = parent / 'repo'
        self.root.mkdir()
        self.dirs = {k: parent / k for k in ('build', 'managed', 'native', 'source', 'examples')}
        for directory in self.dirs.values():
            directory.mkdir()
        self.output = parent / 'final'
        self.selected = {'profile': 'local-linux', 'packageId': 'FactsPDF',
                         'version': VERSION, 'binaryRetained': True}
        native_origin = {'nativePack': '10.0.13'}
        compiler_origin = {'compiler': '10.0.13'}

        def blob(label, directory, filename):
            value = ('FAKE UNIT FIXTURE: ' + label).encode()
            file = directory / filename
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(value)
            return {'filename': filename, **stamp(value)}

        package = 'FactsPDF.' + VERSION + '.nupkg'
        package_file = blob('package', self.dirs['build'] / 'packages', package)
        self.reports = {}
        self.reports['build'] = {
            'schemaVersion': 1, 'phase': 'private-build-unverified', 'sourceHead': SOURCE,
            'identity': self.selected, 'candidateBuilt': True,
            'files': [{'path': 'packages/' + package, **{
                k: package_file[k] for k in ('bytes', 'sha256')}}],
            'nativeCompilerOrigin': compiler_origin, 'nativeNoticeOrigin': native_origin,
        }
        managed = blob('managed', self.dirs['managed'], MANAGED_ARCHIVE_NAME)
        native = blob('native', self.dirs['native'],
                      NATIVE_ARCHIVE_NAME.format(rid='linux-x64'))
        source_name = 'FactsPDF.' + VERSION + '.source.tar.gz'
        example_name = 'FactsPDF.' + VERSION + '.examples.zip'
        source = blob('source', self.dirs['source'], source_name)
        examples = blob('examples', self.dirs['examples'], example_name)
        members = {
            'managed': [x[1] for x in MANAGED_MEMBERS],
            'native': [
                'FactsPDF.Native/FactsPDF.Cli', 'FactsPDF.Native/LICENSE.md',
                'FactsPDF.Native/CLI-README.md',
                'FactsPDF.Native/notices/dotnet/LICENSE.TXT',
                'FactsPDF.Native/notices/dotnet/THIRD-PARTY-NOTICES.TXT'],
            'source': ['LICENSE.md', 'SOURCE-MANIFEST.json', 'SOURCE-BUILD.md',
                       'src/FactsPDF.Cli/FactsPDF.Cli.csproj'] +
                      ['src/fixture-' + str(i) for i in range(7)],
            'examples': list(FILES.values()) + ['EXAMPLES-MANIFEST.json'],
        }
        for key, arch in [('managed', managed), ('native', native),
                          ('source', source), ('examples', examples)]:
            self.reports[key] = {
                'schemaVersion': 1,
                'phase': {
                    'managed': 'partial-managed-software-archive-unverified',
                    'native': 'partial-native-software-archive-unverified',
                    'source': 'partial-source-archive-unverified',
                    'examples': 'partial-examples-archive-unverified',
                }[key],
                'sourceHead': SOURCE, 'archive': arch,
                'members': [{'path': v} for v in members[key]],
            }
            if key in ('managed', 'native'):
                self.reports[key]['identity'] = self.selected
            else:
                self.reports[key]['version'] = VERSION
        self.reports['native']['nativeNoticeOrigin'] = native_origin
        self.reports['native']['nativeCompilerOrigin'] = compiler_origin
        self.write_receipts()

    def write_receipts(self):
        for key, report in self.reports.items():
            report.update(candidateAssembled=False, independentlyValidated=False,
                          published=False, distributionAuthorized=False)
            (self.dirs[key] / {
                'build': 'private-build.json',
                'managed': 'managed-archive.json',
                'native': 'native-archive.json',
                'source': 'source-archive.json',
                'examples': 'examples-archive.json',
            }[key]).write_text(json.dumps(report))

    def stage(self):
        with patch('stage_delivery_candidate.current_source', return_value=SOURCE), \
                patch('stage_delivery_candidate.identity', return_value=self.selected):
            return assemble_five_payloads(self.root, *(self.dirs[k] for k in (
                'build', 'managed', 'native', 'source', 'examples')), self.output)

    def test_five_real_payload_paths_are_copied_with_exact_receipts(self):
        report = self.stage()
        self.assertTrue(report['candidateAssembled'])
        self.assertFalse(report['independentlyValidated'])
        self.assertFalse(report['published'])
        self.assertFalse(report['distributionAuthorized'])
        self.assertEqual(report['sourceHead'], SOURCE)
        self.assertEqual(len(report['payloads']), 5)
        self.assertEqual(set(x['role'] for x in report['payloads']),
                         {'package', 'managed', 'native', 'source', 'examples'})
        for item in report['payloads']:
            path = self.output / item['filename']
            self.assertTrue(path.is_file())
            self.assertEqual(stamp(path.read_bytes()), {
                k: item[k] for k in ('bytes', 'sha256')})
        self.assertEqual(
            json.loads((self.output / 'candidate-staging.json').read_text()), report)
        self.assertFalse((self.output / 'candidate-manifest.json').exists())
        self.assertFalse((self.output / 'SHA256SUMS').exists())

    def test_mutated_payload_rejected_before_directory_created(self):
        (self.dirs['examples'] / ('FactsPDF.' + VERSION + '.examples.zip')).write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError, 'no longer matches'):
            self.stage()
        self.assertFalse(self.output.exists())

    def test_wrong_source_or_profile_is_rejected(self):
        self.reports['source']['sourceHead'] = 'b' * 40
        self.write_receipts()
        with self.assertRaisesRegex(ValueError, 'Mismatched source'):
            self.stage()
        self.reports['source']['sourceHead'] = SOURCE
        self.reports['native']['identity'] = {'profile': 'win-x64-validation'}
        self.write_receipts()
        with self.assertRaisesRegex(ValueError, 'Mismatched source'):
            self.stage()

    def test_incomplete_member_inventory_is_rejected(self):
        self.reports['examples']['members'].pop()
        self.write_receipts()
        with self.assertRaisesRegex(ValueError, 'component member inventory'):
            self.stage()

    def test_native_origin_mismatch_is_rejected(self):
        self.reports['native']['nativeCompilerOrigin'] = {'compiler': 'unknown'}
        self.write_receipts()
        with self.assertRaisesRegex(ValueError, 'compiler/notice provenance'):
            self.stage()

    def test_missing_or_symlinked_payload_is_rejected(self):
        path = self.dirs['native'] / NATIVE_ARCHIVE_NAME.format(rid='linux-x64')
        path.unlink()
        with self.assertRaisesRegex(ValueError, 'Missing or mismatched native'):
            self.stage()
        path.symlink_to(self.dirs['source'] / ('FactsPDF.' + VERSION + '.source.tar.gz'))
        with self.assertRaisesRegex(ValueError, 'Missing or mismatched native'):
            self.stage()

    def test_existing_or_in_checkout_output_is_rejected(self):
        self.output.mkdir()
        with self.assertRaisesRegex(ValueError, 'Fresh external'):
            self.stage()
        self.output.rmdir()
        self.output = self.root / 'candidate'
        with self.assertRaisesRegex(ValueError, 'Fresh external'):
            self.stage()


if __name__ == '__main__':
    unittest.main()
