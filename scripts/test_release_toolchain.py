"""Strict entrypoint tests use synthetic metadata, not .NET 11 execution."""
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

import verify_first_use as first_use

ROOT = Path(__file__).resolve().parents[1]


class ReleaseToolchainTests(unittest.TestCase):
    @staticmethod
    def _strict_route_fixture(temporary):
        root = Path(temporary)
        published = root / 'published'
        published.mkdir()
        cli = published / 'FactsPDF.Cli.dll'
        cli.write_bytes(b'synthetic-cli')
        (published / 'FactsPDF.dll').write_bytes(b'synthetic-core')
        cli.with_suffix('.deps.json').write_text('{}')
        cli.with_suffix('.runtimeconfig.json').write_text('{}')
        package = root / 'FactsPDF.0.1.0-dev.nupkg'
        package.write_bytes(b'synthetic-package')
        output = root / 'evidence'
        argv = ['verify_release_candidate.py', '--cli-dll', str(cli),
                '--package', str(package), '--output', str(output), '--dotnet', 'dotnet',
                '--expected-sdk', '10.0.402', '--expected-runtime', '10.0.13']
        return argv, output

    def test_strict_entrypoint_stages_exact_pins_before_dotnet(self):
        import json
        from unittest import mock
        from xml.etree import ElementTree as ET

        class ObservedStrictRoute(Exception):
            pass

        with tempfile.TemporaryDirectory() as temporary:
            argv, output = self._strict_route_fixture(temporary)

            def inspect_before_dotnet(_ambient, work):
                sdk = json.loads((work / 'global.json').read_text())['sdk']
                self.assertEqual(sdk, {'version': '10.0.402',
                                       'rollForward': 'disable', 'allowPrerelease': False})
                project = ET.parse(work / 'consumer/FirstUse.csproj')
                self.assertEqual(project.findtext('.//RuntimeFrameworkVersion'), '10.0.13')
                self.assertEqual(project.findtext('.//RollForward'), 'Disable')
                self.assertEqual(project.findtext('.//UseAppHost'), 'false')
                raise ObservedStrictRoute

            with (mock.patch.object(sys, 'argv', argv),
                  mock.patch.object(first_use, 'current_source', return_value='a' * 40),
                  mock.patch.object(first_use, 'expected_contract', return_value=('0.1.0-dev', 'net10.0')),
                  mock.patch.object(first_use, 'validate_package', return_value={'assembly': b'synthetic-core'}),
                  mock.patch.object(first_use, 'build_environment', side_effect=inspect_before_dotnet)):
                with self.assertRaises(ObservedStrictRoute):
                    first_use.main(strict_toolchain=True)

            recorded = json.loads((output / 'consumer.global.json').read_text())
            self.assertEqual(recorded['sdk']['version'], '10.0.402')
            self.assertEqual(recorded['sdk']['rollForward'], 'disable')

    def test_strict_entrypoint_rejects_wrong_generated_runtimeconfig_before_pdf(self):
        import json
        from unittest import mock

        with tempfile.TemporaryDirectory() as temporary:
            argv, output = self._strict_route_fixture(temporary)
            calls = []

            def fake_dotnet(args, **kwargs):
                command = list(map(str, args))
                calls.append(command)
                self.assertEqual(command[0], 'dotnet')
                work = Path(kwargs['cwd']).parent
                result = b''
                if command[1] == '--version':
                    result = b'10.0.402\\n'
                elif command[1] == 'restore':
                    assets = work / 'consumer/obj/project.assets.json'
                    assets.parent.mkdir(parents=True)
                    assets.write_text('{}')
                elif command[1] == 'msbuild':
                    if any(part.startswith('-getProperty:') for part in command):
                        result = json.dumps({'Properties': {
                            'TargetFramework': 'net10.0',
                            'DirectoryBuildPropsPath': '',
                            'DirectoryBuildTargetsPath': '',
                            'DirectoryPackagesPropsPath': ''},
                            'Items': {'ProjectReference': []}}).encode()
                    else:
                        target = next(part.split(':', 1)[1] for part in command
                                      if part.startswith('-preprocess:'))
                        Path(target).write_text('<Project/>')
                elif command[1] == 'build':
                    destination = work / 'consumer/bin/Release/net10.0'
                    destination.mkdir(parents=True)
                    (destination / 'FirstUse.dll').write_bytes(b'synthetic-consumer')
                    (destination / 'FirstUse.runtimeconfig.json').write_text(json.dumps({
                        'runtimeOptions': {'tfm': 'net10.0',
                                           'framework': {'name': 'Microsoft.NETCore.App',
                                                         'version': '10.0.12'},
                                           'rollForward': 'LatestPatch'}}))
                else:
                    raise AssertionError('Unexpected PDF execution or command: ' + repr(command))
                return subprocess.CompletedProcess(command, 0, result, b'')

            with (mock.patch.object(sys, 'argv', argv),
                  mock.patch.object(first_use, 'current_source', return_value='a' * 40),
                  mock.patch.object(first_use, 'expected_contract', return_value=('0.1.0-dev', 'net10.0')),
                  mock.patch.object(first_use, 'validate_package', return_value={'assembly': b'synthetic-core'}),
                  mock.patch.object(first_use, 'validate_assets'),
                  mock.patch.object(first_use, 'validate_imports', return_value=['independent-import']),
                  mock.patch.object(first_use, 'verify_package_bytes'),
                  mock.patch.object(first_use.subprocess, 'run', side_effect=fake_dotnet)):
                with self.assertRaisesRegex(AssertionError, 'runtimeconfig'):
                    first_use.main(strict_toolchain=True)

            self.assertEqual([command[1] for command in calls],
                             ['--version', 'restore', 'msbuild', 'msbuild', 'build'])
            self.assertFalse((output / 'consumer.pdf').exists(), 'Conversion must not start')

    def test_requires_explicit_canonical_stable_versions(self):
        self.assertTrue(hasattr(first_use, 'stable_version'), 'Missing exact stable version validation')
        for value in [None, '', ' ', '10', '10.0', '10.0.x', '*', 'latest', '10.0.0-rc.2',
                      '10.0.100-preview.1', '10.0.0+build', '10.0.0\n', ' 10.0.0', '010.0.0', '١٠.0.0']:
            with self.subTest(value=value), self.assertRaises(ValueError):
                first_use.stable_version(value)
        self.assertEqual(first_use.stable_version('10.0.100'), '10.0.100')
        self.assertEqual(first_use.stable_version('10.0.0'), '10.0.0')

    def test_exact_actual_sdk_and_runtime_match(self):
        self.assertTrue(hasattr(first_use, 'validate_release_toolchain'), 'Missing exact toolchain check')
        first_use.validate_release_toolchain('10.0.100', {'version': '10.0.0', 'framework': '.NET 10.0.0'}, '10.0.100', '10.0.0')

    def test_numeric_runtime_version_cannot_hide_a_prerelease_framework(self):
        # Environment.Version is numeric even on prerelease runtimes. The
        # FrameworkDescription carries the RC/preview identity separately.
        for framework in ['.NET 10.0.0-rc.2.25502.107', '.NET 10.0.0-preview.1',
                          '.NET 10.0.1', '.NET 10.0.0+build', '', None]:
            with self.subTest(framework=framework), self.assertRaisesRegex(AssertionError, 'runtime'):
                first_use.validate_release_toolchain('10.0.100', {'version': '10.0.0', 'framework': framework},
                                                     '10.0.100', '10.0.0')
        with self.assertRaisesRegex(AssertionError, 'runtime'):
            first_use.validate_release_toolchain('10.0.100', {'version': '10.0.0'}, '10.0.100', '10.0.0')

    def test_wrong_sdk_patch_prerelease_or_missing_actual_sdk_fails(self):
        self.assertTrue(hasattr(first_use, 'validate_release_toolchain'), 'Missing exact toolchain check')
        for actual in ['10.0.101', '10.0.100-rc.2', '', None]:
            with self.subTest(actual=actual), self.assertRaisesRegex(AssertionError, 'SDK'):
                first_use.validate_release_toolchain(actual, {'version': '10.0.0'}, '10.0.100', '10.0.0')

    def test_wrong_runtime_patch_prerelease_or_missing_actual_runtime_fails(self):
        self.assertTrue(hasattr(first_use, 'validate_release_toolchain'), 'Missing exact toolchain check')
        for actual in ['10.0.1', '10.0.0-rc.2', '', None]:
            with self.subTest(actual=actual), self.assertRaisesRegex(AssertionError, 'runtime'):
                first_use.validate_release_toolchain('10.0.100', {'version': actual}, '10.0.100', '10.0.0')
        with self.assertRaisesRegex(AssertionError, 'runtime'):
            first_use.validate_release_toolchain('10.0.100', {}, '10.0.100', '10.0.0')

    def test_invalid_expectations_cannot_match_invalid_observations(self):
        self.assertTrue(hasattr(first_use, 'validate_release_toolchain'), 'Missing exact toolchain check')
        for sdk, runtime in [('10.0.x', '10.0.0'), ('10.0.100', '10.0.0-rc.2'), ('', '')]:
            with self.subTest(sdk=sdk, runtime=runtime), self.assertRaises(ValueError):
                first_use.validate_release_toolchain(sdk, {'version': runtime}, sdk, runtime)

    def test_release_entry_requires_both_expectations_before_consumption(self):
        entry = ROOT / 'scripts/verify_release_candidate.py'
        self.assertTrue(entry.is_file(), 'Missing release-only entrypoint')
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / 'output'
            base = [sys.executable, str(entry), '--cli-dll', str(Path(temporary) / 'missing.dll'), '--output', str(output)]
            for arguments in [[], ['--expected-sdk', '10.0.100'], ['--expected-runtime', '10.0.0'],
                              ['--expected-sdk', '', '--expected-runtime', '10.0.0'],
                              ['--expected-sdk', '10.0.x', '--expected-runtime', '10.0.0'],
                              ['--expected-sdk', '10.0.100', '--expected-runtime', '10.0.0-rc.2']]:
                with self.subTest(arguments=arguments):
                    result = subprocess.run(base + arguments, capture_output=True, text=True)
                    self.assertEqual(result.returncode, 2, result.stderr)
                    self.assertIn('--expected-', result.stderr)
                    self.assertFalse(output.exists())

    def test_strict_consumer_sdk_and_runtime_are_generated_not_copied_from_repo(self):
        import json
        import shutil
        from xml.etree import ElementTree as ET
        with tempfile.TemporaryDirectory() as tmp:
            work = Path(tmp)
            (work / 'consumer').mkdir()
            shutil.copy2(ROOT / 'examples/first-use/FirstUse.csproj', work / 'consumer/FirstUse.csproj')
            (work / 'global.json').write_text('{"sdk":{"version":"11.0.100","rollForward":"latestFeature"}}')
            first_use.configure_consumer_toolchain(work, ROOT, '10.0.402', '10.0.13', strict_toolchain=True)
            self.assertEqual(json.loads((work / 'global.json').read_text())['sdk'],
                             {'version': '10.0.402', 'rollForward': 'disable', 'allowPrerelease': False})
            tree = ET.parse(work / 'consumer/FirstUse.csproj')
            self.assertEqual(tree.findtext('.//RuntimeFrameworkVersion'), '10.0.13')
            self.assertEqual(tree.findtext('.//RollForward'), 'Disable')
            self.assertEqual(tree.findtext('.//UseAppHost'), 'false')
            with self.assertRaises(ValueError):
                first_use.configure_consumer_toolchain(work, ROOT, '10.0.x', '10.0.13', strict_toolchain=True)

    def test_strict_release_runtimeconfig_rejects_floating_or_wrong_runtime(self):
        good = {'runtimeOptions': {'framework': {'version': '10.0.13'}, 'rollForward': 'Disable'}}
        first_use.validate_probe_runtimeconfig(good, '10.0.13')
        for bad in [
            {'runtimeOptions': {'framework': {'version': '10.0.13'}}},
            {'runtimeOptions': {'framework': {'version': '10.0.12'}, 'rollForward': 'Disable'}},
            {'runtimeOptions': {'framework': {'version': '10.0.13'}, 'rollForward': 'LatestPatch'}},
            {'runtimeOptions': {'frameworks': [{'name':'Microsoft.NETCore.App','version':'10.0.13'}],
                                'rollForward': 'Disable'}},
        ]:
            with self.subTest(bad=bad), self.assertRaises(AssertionError):
                first_use.validate_probe_runtimeconfig(bad, '10.0.13')

    def test_dev_entry_keeps_family_runtime_validation(self):
        runtime = dict(version='10.0.1-rc.synthetic', framework='.NET synthetic', os='test', rid='linux-x64',
                       architecture='X64', targetFramework='.NETCoreApp,Version=v10.0',
                       dynamicCodeSupported=True, dynamicCodeCompiled=True)
        first_use.validate_runtime(runtime, 'net10.0')

    def test_exact_toolchain_does_not_allow_another_source_identity(self):
        self.assertTrue(hasattr(first_use, 'validate_release_toolchain'), 'Missing exact toolchain check')
        first_use.validate_release_toolchain('10.0.100', {'version': '10.0.0', 'framework': '.NET 10.0.0'}, '10.0.100', '10.0.0')
        with self.assertRaisesRegex(AssertionError, 'version/source'):
            first_use.validate_assembly_source('0.1.0-dev+' + 'b' * 40, '0.1.0-dev', 'a' * 40)


if __name__ == '__main__': unittest.main()
