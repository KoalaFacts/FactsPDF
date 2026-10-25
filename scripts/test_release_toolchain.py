"""Strict entrypoint tests use synthetic metadata, not .NET 11 execution."""
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

import verify_first_use as first_use

ROOT = Path(__file__).resolve().parents[1]


class ReleaseToolchainTests(unittest.TestCase):
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
