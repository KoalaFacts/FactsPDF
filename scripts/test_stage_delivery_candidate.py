"""Candidate producer input boundaries, tested without packaging or a .NET host."""
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

from stage_delivery_candidate import inspector_inputs, identity, preflight


class CandidateInputTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(prefix='factspdf-stage-test-')
        self.addCleanup(temp.cleanup)
        self.parent = Path(temp.name)
        self.root = self.parent / 'repo'
        self.root.mkdir()
        for name in ('eng', 'tools/FactsPDF.PackageInspector', 'scripts'):
            (self.root / name).mkdir(parents=True)
        (self.root / 'Directory.Build.props').write_text('<Project><PropertyGroup>'
            '<Version>0.1.0-preview.1</Version><TargetFramework>net10.0</TargetFramework>'
            '</PropertyGroup></Project>')
        (self.root / 'global.json').write_text(json.dumps({'sdk': {'version': '10.0.402',
            'rollForward': 'disable', 'allowPrerelease': False}}))
        (self.root / 'eng/delivery-candidate.json').write_text(json.dumps({
            'schemaVersion': 1, 'packageId': 'FactsPDF', 'version': '0.1.0-preview.1',
            'targetFramework': 'net10.0', 'sdk': {'version': '10.0.402',
                'rollForward': 'disable', 'allowPrerelease': False},
            'managedRuntime': {'framework': 'Microsoft.NETCore.App', 'version': '10.0.13',
                               'rollForward': 'Disable'},
            'nativePacks': {'runtimeVersion': '10.0.13', 'compilerVersion': '10.0.13'},
            'publication': {'published': False, 'distributionAuthorized': False},
            'profiles': [{'name': 'local-linux', 'rid': 'linux-x64', 'binaryRetained': True},
                         {'name': 'win-x64-validation', 'rid': 'win-x64', 'binaryRetained': False}]}))
        (self.root / 'tools/FactsPDF.PackageInspector/FactsPDF.PackageInspector.csproj').write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
            '<DefaultItemExcludes>$(DefaultItemExcludes);bin/**;obj/**</DefaultItemExcludes>'
            '</PropertyGroup><ItemGroup>'
            '<Compile Include="../../scripts/inspect_candidate_assembly.cs" Link="Program.cs" />'
            '</ItemGroup></Project>')
        (self.root / 'scripts/inspect_candidate_assembly.cs').write_text('Console.WriteLine("metadata");')
        self.git('init', '-q')
        self.git('add', '.')
        self.git('-c', 'user.name=Test', '-c', 'user.email=test@example.invalid',
                 'commit', '-qm', 'source')

    def git(self, *args):
        return subprocess.check_output(['git', *args], cwd=self.root).decode().strip()

    def test_valid_profile_receipt_is_only_preflight_not_candidate(self):
        expected = self.git('rev-parse', 'HEAD')
        receipt = preflight(self.root, 'local-linux', self.parent / 'output')
        self.assertEqual(receipt['sourceHead'], expected)
        self.assertFalse(receipt['candidateBuilt'])
        self.assertFalse(receipt['independentlyValidated'])
        self.assertEqual(len(receipt['inspectorInputs']), 3)
        self.assertEqual(receipt, json.loads((self.parent / 'output/source-preflight.json').read_text()))
        self.assertEqual(sorted(x.name for x in (self.parent / 'output').iterdir()), ['source-preflight.json'])

    def test_refuse_existing_or_in_tree_output(self):
        (self.parent / 'output').mkdir()
        with self.assertRaisesRegex(ValueError, 'Fresh external'):
            preflight(self.root, 'local-linux', self.parent / 'output')
        with self.assertRaisesRegex(ValueError, 'Fresh external'):
            preflight(self.root, 'local-linux', self.root / 'artifacts/out')

    def test_ignore_injected_csharp_source_is_rejected(self):
        (self.root / '.gitignore').write_text('tools/FactsPDF.PackageInspector/**/*.cs\n')
        self.git('add', '.gitignore')
        self.git('-c', 'user.name=Test', '-c', 'user.email=test@example.invalid', 'commit', '-qm', 'ignore')
        injected = self.root / 'tools/FactsPDF.PackageInspector/sub/Evil.cs'
        injected.parent.mkdir()
        injected.write_text('namespace Evil {}')
        self.assertFalse(self.git('status', '--porcelain'))  # old clean-source check misses it
        with self.assertRaisesRegex(ValueError, 'Untracked/ignored'):
            inspector_inputs(self.root)
        self.assertFalse((self.parent / 'output').exists())

    def test_ignored_tools_parent_config_is_rejected(self):
        (self.root / '.gitignore').write_text('tools/Directory.Build.props\n')
        self.git('add', '.gitignore')
        self.git('-c', 'user.name=Test', '-c', 'user.email=test@example.invalid', 'commit', '-qm', 'ignore')
        (self.root / 'tools/Directory.Build.props').write_text('<Project/>')
        self.assertFalse(self.git('status', '--porcelain'))
        with self.assertRaisesRegex(ValueError, 'Untracked/ignored'):
            inspector_inputs(self.root)

    def test_unrelated_ignored_shared_file_not_rejected(self):
        (self.root / '.gitignore').write_text('shared/*.cs\n')
        self.git('add', '.gitignore')
        self.git('-c', 'user.name=Test', '-c', 'user.email=test@example.invalid', 'commit', '-qm', 'ignore')
        (self.root / 'shared').mkdir()
        (self.root / 'shared/unrelated.cs').write_text('no build glob')
        self.assertEqual(len(inspector_inputs(self.root)), 3)

    def test_sdk_and_pack_identity_mismatch_rejected(self):
        data = json.loads((self.root / 'eng/delivery-candidate.json').read_text())
        data['nativePacks']['compilerVersion'] = '10.0.14'
        (self.root / 'eng/delivery-candidate.json').write_text(json.dumps(data))
        self.git('add', '.')
        self.git('-c', 'user.name=Test', '-c', 'user.email=test@example.invalid', 'commit', '-qm', 'change')
        with self.assertRaisesRegex(ValueError, 'native-pack'):
            identity(self.root, 'local-linux')

    def test_unexpected_project_import_is_rejected(self):
        project = self.root / 'tools/FactsPDF.PackageInspector/FactsPDF.PackageInspector.csproj'
        project.write_text(project.read_text().replace('</Project>',
            '<Import Project="../../untracked.targets" /></Project>'))
        self.git('add', '.')
        self.git('-c', 'user.name=Test', '-c', 'user.email=test@example.invalid', 'commit', '-qm', 'change')
        with self.assertRaisesRegex(ValueError, 'imports'):
            inspector_inputs(self.root)


    def test_reject_ignored_msbuild_user_project_even_with_clean_git(self):
        (self.root / '.gitignore').write_text('*.user\n')
        self.git('add', '.gitignore')
        self.git('-c', 'user.name=Test', '-c', 'user.email=test@example.invalid',
                 'commit', '-qm', 'ignore user project')
        user_project = self.root / 'tools/FactsPDF.PackageInspector/FactsPDF.PackageInspector.csproj.user'
        user_project.write_text('<Project><Target Name="Injected" BeforeTargets="Build" /></Project>')
        self.assertFalse(self.git('status', '--porcelain'))
        with self.assertRaisesRegex(ValueError, 'user project import'):
            inspector_inputs(self.root)
        self.assertFalse((self.parent / 'candidate').exists())

    def test_reject_removed_source_tree_exclusions(self):
        project = self.root / 'tools/FactsPDF.PackageInspector/FactsPDF.PackageInspector.csproj'
        project.write_text(project.read_text().replace(';obj/**', ''))
        self.git('add', '.')
        self.git('-c', 'user.name=Test', '-c', 'user.email=test@example.invalid',
                 'commit', '-qm', 'remove exclusions')
        with self.assertRaisesRegex(ValueError, 'source-tree bin/obj exclusions'):
            inspector_inputs(self.root)

    def test_private_build_rejects_conflicting_runtimeconfig_before_native_publish(self):
        from unittest.mock import patch
        from stage_delivery_candidate import private_build
        for name in ('framework-rollforward', 'framework-array', 'included-frameworks', 'wrong-tfm'):
            with self.subTest(name=name):
                runtime = {'runtimeOptions': {
                    'tfm': 'net10.0', 'rollForward': 'Disable',
                    'framework': {'name': 'Microsoft.NETCore.App', 'version': '10.0.13'}}}
                if name == 'framework-rollforward':
                    runtime['runtimeOptions']['framework']['rollForward'] = 'LatestPatch'
                elif name == 'framework-array':
                    runtime['runtimeOptions']['frameworks'] = [{'name': 'Other', 'version': '10.0.13'}]
                elif name == 'included-frameworks':
                    runtime['runtimeOptions']['includedFrameworks'] = [{'name': 'Other', 'version': '10.0.13'}]
                else:
                    runtime['runtimeOptions']['tfm'] = 'net9.0'
                commands = []

                def fake_build(dotnet, root, work, label, args):
                    commands.append(label)
                    if label == 'inspector-build':
                        dll = work / 'inspector/bin/Release/net10.0/FactsPDF.PackageInspector.dll'
                        dll.parent.mkdir(parents=True, exist_ok=True)
                        dll.write_bytes(b'inspected candidate metadata')
                    elif label == 'package-pack':
                        (work / 'packages/FactsPDF.0.1.0-preview.1.nupkg').write_bytes(b'package')
                    elif label == 'managed-publish':
                        (work / 'managed/FactsPDF.Cli.runtimeconfig.json').write_text(json.dumps(runtime))

                output = self.parent / ('build-' + name)
                with (patch('shutil.which', return_value='/fake/dotnet'),
                      patch('stage_delivery_candidate._host_toolchain',
                            return_value={'sdk': '10.0.402', 'managedRuntime': '10.0.13'}),
                      patch('stage_delivery_candidate._build_command', side_effect=fake_build)):
                    with self.assertRaisesRegex(ValueError, 'runtimeconfig'):
                        private_build(self.root, 'local-linux', output)
                self.assertNotIn('native-publish', commands)
                self.assertFalse((output / 'private-build.json').exists())


class PrivateBuildContractTests(unittest.TestCase):

    def test_native_compiler_origin_binds_assets_cache_and_exact_versions(self):
        import base64
        import hashlib
        from subprocess import CompletedProcess
        from unittest.mock import patch
        from stage_delivery_candidate import _native_compiler_origin
        with tempfile.TemporaryDirectory() as temp:
            parent = Path(temp)
            root = parent / 'checkout'
            assets_file = root / 'src/FactsPDF.Cli/obj/project.assets.json'
            assets_file.parent.mkdir(parents=True)
            cache = parent / 'packages'
            ids = ('Microsoft.DotNet.ILCompiler',
                   'runtime.linux-x64.Microsoft.DotNet.ILCompiler')
            libraries = {}
            for i, package_id in enumerate(ids):
                package_key = package_id.lower()
                package_dir = cache / package_key / '10.0.13'
                package_dir.mkdir(parents=True)
                (package_dir / (package_key + '.nuspec')).write_text(
                    '<package><metadata><id>' + package_id +
                    '</id><version>10.0.13</version></metadata></package>')
                digest = base64.b64encode(bytes([i + 1]) * 64).decode('ascii')
                (package_dir / (package_key + '.10.0.13.nupkg.sha512')).write_text(digest)
                libraries[package_id + '/10.0.13'] = {
                    'type': 'package', 'path': package_key + '/10.0.13', 'sha512': 'sha512-' + digest}
            def save():
                assets_file.write_text(json.dumps({'libraries': libraries}))
            save()
            with patch('stage_delivery_candidate.subprocess.run',
                       return_value=CompletedProcess([], 0, 'global-packages: ' + str(cache) + '\n', '')):
                record = _native_compiler_origin('dotnet', root, 'local-linux')
                self.assertEqual([p['packageId'] for p in record['packages']], list(ids))
                self.assertEqual([p['version'] for p in record['packages']], ['10.0.13'] * 2)
                self.assertEqual(record['projectAssetsSha256'],
                                 hashlib.sha256(assets_file.read_bytes()).hexdigest())
                self.assertFalse(record['officialSignatureVerified'])
                # NuGet assets require the SRI-style prefix; cache has bare base64.
                libraries[ids[0] + '/10.0.13']['sha512'] = record['packages'][0]['packageSha512']
                save()
                with self.assertRaisesRegex(ValueError, 'asset/cache SHA512 mismatch'):
                    _native_compiler_origin('dotnet', root, 'local-linux')
                libraries[ids[0] + '/10.0.13']['sha512'] = (
                    'sha512-' + record['packages'][0]['packageSha512'])
                save()

                changed = libraries.pop(ids[1] + '/10.0.13')
                libraries[ids[1] + '/10.0.14'] = changed
                save()
                with self.assertRaisesRegex(ValueError, 'version mismatch'):
                    _native_compiler_origin('dotnet', root, 'local-linux')
                libraries.pop(ids[1] + '/10.0.14')
                libraries[ids[1] + '/10.0.13'] = changed
                save()
                sha = cache / ids[1].lower() / '10.0.13' / (
                    ids[1].lower() + '.10.0.13.nupkg.sha512')
                sha.write_text(base64.b64encode(b'x' * 64).decode('ascii'))
                with self.assertRaisesRegex(ValueError, 'asset/cache SHA512 mismatch'):
                    _native_compiler_origin('dotnet', root, 'local-linux')
                sha.unlink()
                with self.assertRaisesRegex(ValueError, 'package cache'):
                    _native_compiler_origin('dotnet', root, 'local-linux')

    def test_host_toolchain_requires_real_exact_runtime(self):
        from unittest.mock import patch
        from subprocess import CompletedProcess
        from stage_delivery_candidate import _host_toolchain
        outputs = ['10.0.402\n', 'Microsoft.NETCore.App 10.0.13 [/sdk]\n', '.NET SDK 10.0.402\n']
        with patch('stage_delivery_candidate.subprocess.run',
                   side_effect=[CompletedProcess([], 0, out, '') for out in outputs]) as called:
            result = _host_toolchain('dotnet', Path('.'))
        self.assertEqual(result['sdk'], '10.0.402')
        self.assertEqual(result['managedRuntime'], '10.0.13')
        self.assertEqual(called.call_count, 3)

    def test_host_toolchain_rejects_implicit_runtime_roll_forward(self):
        from unittest.mock import patch
        from subprocess import CompletedProcess
        from stage_delivery_candidate import _host_toolchain
        outputs = ['10.0.402\n', 'Microsoft.NETCore.App 10.0.14 [/sdk]\n']
        with patch('stage_delivery_candidate.subprocess.run',
                   side_effect=[CompletedProcess([], 0, out, '') for out in outputs]):
            with self.assertRaisesRegex(ValueError, 'not installed'):
                _host_toolchain('dotnet', Path('.'))

    def test_host_toolchain_rejects_newer_sdk(self):
        from unittest.mock import patch
        from subprocess import CompletedProcess
        from stage_delivery_candidate import _host_toolchain
        with patch('stage_delivery_candidate.subprocess.run',
                   return_value=CompletedProcess([], 0, '10.0.403\n', '')):
            with self.assertRaisesRegex(ValueError, 'Observed SDK'):
                _host_toolchain('dotnet', Path('.'))

    def test_compiler_failure_surfaces_bounded_diagnostic(self):
        from unittest.mock import patch
        from subprocess import CompletedProcess
        from stage_delivery_candidate import _build_command
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            def fail(command, **kwargs):
                kwargs['stdout'].write('MSB1234: diagnostic for this actual failed build\n')
                return CompletedProcess(command, 1)
            with patch('stage_delivery_candidate.subprocess.run', side_effect=fail):
                with self.assertRaisesRegex(RuntimeError, 'MSB1234: diagnostic'):
                    _build_command('dotnet', root, root, 'inspector-build', ['build'])

    def test_record_rejects_symlinks_and_missing_payload(self):
        from stage_delivery_candidate import _member_record
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            (root / 'real.dll').write_bytes(b'actual sample')
            self.assertEqual(_member_record(root, 'real.dll')['bytes'], 13)
            (root / 'alias.dll').symlink_to('real.dll')
            with self.assertRaisesRegex(ValueError, 'Missing/invalid'):
                _member_record(root, 'alias.dll')
            with self.assertRaisesRegex(ValueError, 'Missing/invalid'):
                _member_record(root, 'missing.dll')

if __name__ == '__main__':
    unittest.main()
