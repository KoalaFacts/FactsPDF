"""Five-payload staging tests. Individual archive/member and installed-PDF acceptance are separate CI gates."""
import hashlib
import json
import queue
import subprocess
import sys
import threading
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from private_examples_payload import FILES
from private_five_finalization import REQUIRED_CHECKS, finalize_five
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


    def verified_receipt(self):
        """Fixture stands in for an independently generated, successful report.

        These unit tests exercise final linkage only; they do not claim to have
        run an actual verifier, or to authorize any synthetic payload.
        """
        self.stage()
        parent = self.root.parent / 'verification'
        reports = parent / 'reports'
        reports.mkdir(parents=True)
        evidence = {}
        for name in sorted(REQUIRED_CHECKS):
            data = ('FAKE INDEPENDENT UNIT EVIDENCE: ' + name).encode()
            target = reports / (name + '.json')
            target.write_bytes(data)
            evidence[name] = {
                'status': 'passed', 'evidenceFile': 'reports/' + target.name,
                'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(),
            }
        payloads = []
        staging = json.loads((self.output / 'candidate-staging.json').read_text())
        for item in staging['payloads']:
            archive_name = item['filename']
            member = {
                'path': {'package': 'lib/net10.0/FactsPDF.dll',
                         'managed': 'FactsPDF.Cli/FactsPDF.Cli.dll',
                         'native': 'FactsPDF.Native/FactsPDF.Cli'}.get(item['role'], ''),
                'bytes': item['bytes'], 'sha256': item['sha256'],
                'identity': {'sourceHead': SOURCE},
            }
            payloads.append({
                'filename': archive_name, 'bytes': item['bytes'],
                'sha256': item['sha256'],
                'inventoryDigest': hashlib.sha256(('fixture:' + archive_name).encode()).hexdigest(),
                'binaryMembers': [member] if member['path'] else [],
            })
        proof = {
            'schemaVersion': 1, 'phase': 'payloads', 'status': 'passed',
            'candidateStagingSha256': hashlib.sha256(
                (self.output / 'candidate-staging.json').read_bytes()).hexdigest(),
            'identity': {
                'sourceHead': SOURCE, 'profile': 'local-linux',
                'packageId': 'FactsPDF', 'packageVersion': VERSION,
                'targetFramework': 'net10.0', 'sdkVersion': '10.0.402',
                'selectedRuntimeVersion': '10.0.13',
            },
            'actualToolchain': {
                'sdkVersion': '10.0.402', 'managedRuntimeVersion': '10.0.13',
                'nativeRuntimePackVersion': '10.0.13',
                'nativeCompilerPackVersion': '10.0.13',
                'officialSignatureVerified': False,
                'nativeCompilerPackVerified': False,
            },
            'frozenContractDigest': 'd' * 64,
            'payloads': payloads, 'checks': evidence,
        }
        path = parent / 'payload-verification.json'
        path.write_text(json.dumps(proof))
        return path

    def finalize_fixture(self, verification):
        with patch('stage_delivery_candidate.current_source', return_value=SOURCE):
            with patch('stage_delivery_candidate.identity', return_value=self.selected):
                with patch('private_five_finalization.frozen_contract_digest',
                           return_value='d' * 64):
                    return finalize_five(self.root, self.output, verification)

    def paused_finalizer(self, verification, crash=False, before_link=False):
        # Real child and filesystem, with only fixture identity mocked. The pipe
        # barrier fixes the interleaving; timeouts only bound a broken test.
        code = r"""
import json, os, sys
from pathlib import Path
from unittest.mock import patch
from test_private_five_payloads import FivePayloadStagingTests
case = FivePayloadStagingTests()
case.root, case.output, proof = map(Path, sys.argv[1:4])
case.selected = json.loads(sys.argv[4])
link = os.link

def checkpoint(src, dst):
    before = sys.argv[5] == 'before-link'
    if not before or Path(dst).name != 'candidate-manifest.json':
        link(src, dst)
    if Path(dst).name == 'candidate-manifest.json':
        print('manifest-barrier', flush=True)
        if sys.argv[5] == 'crash':
            os._exit(77)
        if sys.stdin.readline().strip() != 'continue':
            raise RuntimeError('Test barrier abandoned')
        if before:
            link(src, dst)

with patch('private_five_finalization.os.link', side_effect=checkpoint):
    case.finalize_fixture(proof)
"""
        child = subprocess.Popen(
            [sys.executable, '-c', code, str(self.root), str(self.output),
             str(verification), json.dumps(self.selected),
             'crash' if crash else 'before-link' if before_link else 'overlap'],
            cwd=Path(__file__).resolve().parent,
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            text=True)

        def cleanup():
            if child.poll() is None:
                child.kill()
            child.communicate(timeout=10)
        self.addCleanup(cleanup)
        ready = queue.Queue()
        threading.Thread(target=lambda: ready.put(child.stdout.readline()),
                         daemon=True).start()
        self.assertEqual(ready.get(timeout=30).strip(), 'manifest-barrier')
        return child

    def assert_final_pair(self):
        manifest = self.output / 'candidate-manifest.json'
        sums = (self.output / 'SHA256SUMS').read_text()
        self.assertIn(hashlib.sha256(manifest.read_bytes()).hexdigest() +
                      '  candidate-manifest.json\n', sums)

    def test_live_recovery_cannot_be_rolled_back_by_original_caller(self):
        verification = self.verified_receipt()
        child = self.paused_finalizer(verification)
        self.finalize_fixture(verification)
        self.assert_final_pair()
        before = {name: (self.output / name).read_bytes()
                  for name in ('candidate-manifest.json', 'SHA256SUMS')}
        _, error = child.communicate('continue\n', timeout=30)
        self.assertEqual(child.returncode, 0, error)
        self.assertEqual(before, {name: (self.output / name).read_bytes()
                                  for name in before})
        self.assert_final_pair()

    def test_two_initial_callers_accept_only_matching_completion(self):
        verification = self.verified_receipt()
        child = self.paused_finalizer(verification, before_link=True)
        self.finalize_fixture(verification)
        before = {name: (self.output / name).read_bytes()
                  for name in ('candidate-manifest.json', 'SHA256SUMS')}
        _, error = child.communicate('continue\n', timeout=30)
        self.assertEqual(child.returncode, 0, error)
        self.assertEqual(before, {name: (self.output / name).read_bytes()
                                  for name in before})
        self.assert_final_pair()

    def test_process_exit_after_manifest_link_is_recoverable(self):
        verification = self.verified_receipt()
        child = self.paused_finalizer(verification, crash=True)
        child.communicate(timeout=30)
        self.assertEqual(child.returncode, 77)
        self.assertFalse((self.output / 'SHA256SUMS').exists())
        self.finalize_fixture(verification)
        self.assert_final_pair()

    def test_concurrent_mismatched_completion_is_rejected_and_preserved(self):
        verification = self.verified_receipt()
        child = self.paused_finalizer(verification)
        manifest = (self.output / 'candidate-manifest.json').read_bytes()
        (self.output / 'SHA256SUMS').write_bytes(b'conflicting completion')
        child.communicate('continue\n', timeout=30)
        self.assertNotEqual(child.returncode, 0)
        self.assertEqual((self.output / 'candidate-manifest.json').read_bytes(), manifest)
        self.assertEqual((self.output / 'SHA256SUMS').read_bytes(), b'conflicting completion')

    def test_final_checksums_bind_successful_external_payload_report(self):
        verification = self.verified_receipt()
        result = self.finalize_fixture(verification)
        self.assertTrue(result['independentlyValidated'])
        self.assertFalse(result['distributionAuthorized'])
        self.assertFalse(result['humanPdfApproved'])
        self.assertFalse(result['published'])
        self.assertEqual(result['verificationReceiptSha256'],
                         hashlib.sha256(verification.read_bytes()).hexdigest())
        manifest = self.output / 'candidate-manifest.json'
        self.assertEqual(json.loads(manifest.read_text()), result)
        sums = dict(line.split('  ', 1)[::-1]
                    for line in (self.output / 'SHA256SUMS').read_text().splitlines())
        self.assertEqual(len(sums), 6)
        for filename, hexdigest in sums.items():
            self.assertEqual(hexdigest,
                             hashlib.sha256((self.output / filename).read_bytes()).hexdigest())
        with self.assertRaisesRegex(ValueError, 'Unexpected, missing or already finalized'):
            self.finalize_fixture(verification)

    def test_modified_payload_after_same_source_success_is_rejected(self):
        verification = self.verified_receipt()
        payload = self.output / ('FactsPDF.' + VERSION + '.nupkg')
        payload.write_bytes(payload.read_bytes() + b'AFTER-EXTERNAL-VERIFICATION')
        with self.assertRaisesRegex(ValueError, 'Payload changed after successful'):
            self.finalize_fixture(verification)
        self.assertFalse((self.output / 'candidate-manifest.json').exists())
        self.assertFalse((self.output / 'SHA256SUMS').exists())

    def test_missing_or_modified_independent_evidence_is_rejected(self):
        verification = self.verified_receipt()
        report = next((verification.parent / 'reports').iterdir())
        report.write_bytes(report.read_bytes() + b'POST-VERIFICATION-CHANGE')
        with self.assertRaisesRegex(ValueError, 'Independent acceptance evidence was changed'):
            self.finalize_fixture(verification)
        self.assertFalse((self.output / 'candidate-manifest.json').exists())

    def test_staging_cannot_alias_checkout_through_symlinked_parent(self):
        import shutil
        verification = self.verified_receipt()
        shutil.copytree(self.output, self.root / 'candidate-inside-checkout')
        parent_alias = self.root.parent / 'aliased-source'
        parent_alias.symlink_to(self.root, target_is_directory=True)
        self.output = parent_alias / 'candidate-inside-checkout'
        with self.assertRaisesRegex(ValueError, 'physically external'):
            self.finalize_fixture(verification)

    def test_success_report_must_also_be_physically_outside_checkout(self):
        import shutil
        verification = self.verified_receipt()
        shutil.copytree(verification.parent, self.root / 'ignored-report')
        with self.assertRaisesRegex(ValueError, 'physically external'):
            self.finalize_fixture(self.root / 'ignored-report/payload-verification.json')


    def test_nested_symlink_evidence_escape_is_rejected(self):
        verification = self.verified_receipt()
        outside = self.root.parent / 'outside-evidence'
        nested = outside / 'nested'
        nested.mkdir(parents=True)
        external = nested / 'proof.json'
        data = b'FAKE UNIT OUTSIDE EVIDENCE'
        external.write_bytes(data)
        (verification.parent / 'reports/escape').symlink_to(
            outside, target_is_directory=True)
        proof = json.loads(verification.read_text())
        key = next(iter(proof['checks']))
        proof['checks'][key] = {
            'status': 'passed',
            'evidenceFile': 'reports/escape/nested/proof.json',
            'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(),
        }
        verification.write_text(json.dumps(proof))
        with self.assertRaisesRegex(ValueError, 'Symlinked independent evidence path'):
            self.finalize_fixture(verification)
        self.assertFalse((self.output / 'candidate-manifest.json').exists())

    def test_evidence_mutated_during_last_revalidation_is_rejected(self):
        verification = self.verified_receipt()
        evidence = next((verification.parent / 'reports').iterdir())
        calls = 0

        def changing_source(_root):
            nonlocal calls
            calls += 1
            if calls == 2:
                evidence.write_bytes(evidence.read_bytes() + b'CHANGED')
            return SOURCE

        with patch('stage_delivery_candidate.current_source', side_effect=changing_source):
            with patch('stage_delivery_candidate.identity', return_value=self.selected):
                with patch('private_five_finalization.frozen_contract_digest',
                           return_value='d' * 64):
                    with self.assertRaisesRegex(
                            ValueError, 'Independent acceptance evidence changed before finalization'):
                        finalize_five(self.root, self.output, verification)
        self.assertGreaterEqual(calls, 2)
        self.assertFalse((self.output / 'candidate-manifest.json').exists())
        self.assertFalse((self.output / 'SHA256SUMS').exists())

    def test_interrupted_checksum_installation_recovers_only_intact_manifest(self):
        verification = self.verified_receipt()
        original = self.finalize_fixture(verification)
        expected_manifest = (self.output / 'candidate-manifest.json').read_bytes()
        expected_sums = (self.output / 'SHA256SUMS').read_bytes()
        (self.output / 'SHA256SUMS').unlink()
        replay = self.finalize_fixture(verification)
        self.assertEqual(replay, original)
        self.assertEqual((self.output / 'candidate-manifest.json').read_bytes(),
                         expected_manifest)
        self.assertEqual((self.output / 'SHA256SUMS').read_bytes(), expected_sums)
        (self.output / 'SHA256SUMS').unlink()
        (self.output / 'candidate-manifest.json').write_bytes(b'INVALID PARTIAL MANIFEST')
        with self.assertRaisesRegex(ValueError, 'Interrupted finalization manifest is stale'):
            self.finalize_fixture(verification)
        self.assertFalse((self.output / 'SHA256SUMS').exists())

    def test_wrong_staged_receipt_or_incomplete_check_set_is_rejected(self):
        verification = self.verified_receipt()
        proof = json.loads(verification.read_text())
        proof['candidateStagingSha256'] = '0' * 64
        verification.write_text(json.dumps(proof))
        with self.assertRaisesRegex(ValueError, 'Missing successful'):
            self.finalize_fixture(verification)
        proof['candidateStagingSha256'] = hashlib.sha256(
            (self.output / 'candidate-staging.json').read_bytes()).hexdigest()
        proof['checks'].pop(next(iter(proof['checks'])))
        verification.write_text(json.dumps(proof))
        with self.assertRaisesRegex(ValueError, 'Missing required independent'):
            self.finalize_fixture(verification)
        self.assertFalse((self.output / 'candidate-manifest.json').exists())


if __name__ == '__main__':
    unittest.main()
