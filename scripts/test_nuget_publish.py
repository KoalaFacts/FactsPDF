"""Publisher boundary tests. ZIP fixtures are synthetic, not .NET build evidence."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[1]
VERSION = '0.1.0-preview.1'
SHA = 'a' * 40


class PublisherTests(unittest.TestCase):
    def setUp(self):
        path = ROOT / 'scripts/nuget_publish.py'
        self.assertTrue(path.is_file(), 'NuGet publisher implementation must exist')
        spec = importlib.util.spec_from_file_location('nuget_publish', path)
        self.module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.module)
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def request(self, **changes):
        args = dict(declared=VERSION, requested=VERSION, event='workflow_dispatch',
                    ref='refs/heads/main', publish=True, approved=True, source=SHA)
        args.update(changes)
        return self.module.validate_request(**args)

    def package(self, extra=None):
        package = self.root / f'FactsPDF.{VERSION}.nupkg'
        members = {'FactsPDF.nuspec': b'<package/>', 'LICENSE.md': b'unchanged licence',
                   'README.md': b'preview docs', 'lib/net10.0/FactsPDF.dll': b'synthetic assembly'}
        members.update(extra or {})
        with zipfile.ZipFile(package, 'w') as archive:
            for name, data in members.items():
                archive.writestr(name, data)
        return package

    def run_record(self, **changes):
        run = dict(id=5, path='.github/workflows/ci.yml', head_sha=SHA,
                   event='push', head_branch='main', status='completed', conclusion='success',
                   head_repository={'full_name': 'KoalaFacts/FactsPDF'})
        run.update(changes)
        return run

    def test_dotted_preview_is_accepted_without_changing_source_version(self):
        self.assertEqual(self.request(), VERSION)

    def test_stable_malformed_and_shell_versions_are_rejected(self):
        for version in ('0.1.0', 'v0.1.0-preview.1', '0.1.0-preview.01',
                        '0.1.0-preview.1+build', '0.1.0-preview.1\nX=1', '$(id)', ''):
            with self.subTest(version=version), self.assertRaises(ValueError):
                self.request(declared=version, requested=version)

    def test_requested_version_must_equal_source(self):
        with self.assertRaises(ValueError):
            self.request(requested='0.1.0-preview.2')

    def test_publication_requires_manual_main_and_explicit_gate_confirmation(self):
        for changes in ({'event': 'pull_request'}, {'ref': 'refs/heads/feature'},
                        {'ref': 'refs/tags/v0.1.0-preview.1'}, {'approved': False}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                self.request(**changes)

    def test_pr_dry_run_needs_no_publication_permission(self):
        self.assertEqual(self.request(event='pull_request', ref='refs/pull/102/merge',
                                     publish=False, approved=False, requested=''), VERSION)

    def test_invalid_source_sha_is_rejected(self):
        for source in ('main', 'a'*39, 'A'*40, SHA+'\n'):
            with self.subTest(source=source), self.assertRaises(ValueError):
                self.request(source=source)

    def test_exact_successful_main_ci_is_accepted(self):
        self.assertEqual(self.module.verify_main_ci([{'workflow_runs': [self.run_record()]}], SHA), 5)

    def test_stale_foreign_pr_failed_or_incomplete_ci_cannot_authorize(self):
        for changes in ({'head_sha': 'b'*40}, {'event': 'pull_request'},
                        {'head_branch': 'feature'}, {'path': '.github/workflows/other.yml'},
                        {'head_repository': {'full_name': 'other/FactsPDF'}},
                        {'status': 'in_progress', 'conclusion': None}, {'conclusion': 'failure'}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                self.module.verify_main_ci([{'workflow_runs': [self.run_record(**changes)]}], SHA)

    def test_newer_failed_ci_is_not_masked_by_old_success_or_pagination(self):
        pages = [{'workflow_runs': [self.run_record()]},
                 {'workflow_runs': [self.run_record(id=6, conclusion='failure')]}]
        with self.assertRaises(ValueError):
            self.module.verify_main_ci(pages, SHA)

    def test_seal_and_readback_binds_exact_package_version_source_and_bytes(self):
        bundle = self.root / 'bundle'
        self.module.seal(self.package(), bundle, VERSION, SHA)
        result = self.module.verify_bundle(bundle, VERSION, SHA)
        self.assertEqual(result.name, f'FactsPDF.{VERSION}.nupkg')
        with self.assertRaises(ValueError):
            self.module.verify_bundle(bundle, VERSION, 'b'*40)
        result.write_bytes(result.read_bytes()+b'tamper')
        with self.assertRaises(ValueError):
            self.module.verify_bundle(bundle, VERSION, SHA)

    def test_fonts_extra_payloads_and_traversal_are_not_distributed(self):
        for name in ('font.ttf', 'tools/FactsPDF.Cli.dll', '../LICENSE.md', '.signature.p7s'):
            with self.subTest(name=name), self.assertRaises(ValueError):
                self.module.seal(self.package({name: b'extra'}), self.root/'bundle', VERSION, SHA)

    def test_wrong_filename_and_existing_staging_directory_fail(self):
        package = self.package()
        renamed = package.rename(self.root/'Other.nupkg')
        with self.assertRaises(ValueError):
            self.module.seal(renamed, self.root/'bundle', VERSION, SHA)
        package = self.package()
        bundle = self.root/'bundle'
        bundle.mkdir()
        with self.assertRaises(ValueError):
            self.module.seal(package, bundle, VERSION, SHA)

    def test_signed_public_package_may_add_signature_but_not_change_content(self):
        bundle = self.root/'bundle'
        package = self.package()
        self.module.seal(package, bundle, VERSION, SHA)
        manifest = json.loads((bundle/'manifest.json').read_text())
        signed = self.package({'.signature.p7s': b'official signature fixture'})
        self.module.verify_published(signed, manifest)
        with self.assertRaises(ValueError):
            self.module.verify_published(self.package({'LICENSE.md': b'altered'}), manifest)

    def test_duplicate_zip_entries_are_rejected(self):
        import warnings
        package = self.package()
        with warnings.catch_warnings():
            warnings.simplefilter('ignore', UserWarning)
            with zipfile.ZipFile(package, 'a') as archive:
                archive.writestr('LICENSE.md', b'second')
        with self.assertRaises(ValueError):
            self.module.seal(package, self.root/'bundle', VERSION, SHA)

    def test_publish_rerun_downloads_the_artifact_named_by_the_successful_verify_job(self):
        workflow = (ROOT/'.github/workflows/publish-nuget.yml').read_text()
        self.assertIn('name: ${{ needs.verify.outputs.artifact }}', workflow)
        self.assertIn('artifact: ${{ steps.bundle.outputs.artifact }}', workflow)

    def test_extra_staged_file_and_manifest_version_change_are_rejected(self):
        bundle = self.root/'bundle'
        self.module.seal(self.package(), bundle, VERSION, SHA)
        extra = bundle/'surprise.nupkg'
        extra.write_bytes(b'extra')
        with self.assertRaises(ValueError):
            self.module.verify_bundle(bundle, VERSION, SHA)
        extra.unlink()
        manifest_path = bundle/'manifest.json'
        manifest = json.loads(manifest_path.read_text())
        manifest['version'] = '0.1.0-preview.2'
        manifest_path.write_text(json.dumps(manifest))
        with self.assertRaises(ValueError):
            self.module.verify_bundle(bundle, VERSION, SHA)

    def test_workflow_keeps_oidc_and_binary_upload_behind_explicit_publication(self):
        path = ROOT/'.github/workflows/publish-nuget.yml'
        self.assertTrue(path.is_file(), 'Workflow must exist')
        workflow = path.read_text()
        self.assertNotIn('pull_request_target:', workflow)
        self.assertNotIn('--skip-duplicate', workflow)
        self.assertNotIn('NUGET_API_KEY }}', workflow.split('run:')[0])
        self.assertIn('secrets.NUGET_USERNAME', workflow)
        self.assertIn('NuGet/login@8d196754b4036150537f80ac539e15c2f1028841', workflow)
        self.assertIn("github.ref == 'refs/heads/main'", workflow)
        self.assertIn('inputs.release_gates_confirmed', workflow)
        self.assertIn('scripts/verify_release_candidate.py', workflow)
        self.assertIn('scripts/verify_package.py --assembly-inspector', workflow)
        self.assertIn('nuget_publish.py gate-ci', workflow)
        self.assertIn('nuget_publish.py verify-public', workflow)


if __name__ == '__main__':
    unittest.main()
