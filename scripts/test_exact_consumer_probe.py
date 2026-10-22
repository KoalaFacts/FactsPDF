"""Exact probe guards; RC-shaped values here are synthetic, not RC execution."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from xml.etree import ElementTree as ET

import verify_first_use as first_use

ROOT = Path(__file__).resolve().parents[1]


class ExactConsumerProbeTests(unittest.TestCase):
    def test_explicit_versions_reject_floating_empty_or_build_metadata(self):
        self.assertTrue(hasattr(first_use, 'probe_version'), 'Missing exact probe version parser')
        for value in ['10.0.100', '11.0.100-rc.2.12345.6']:
            self.assertEqual(first_use.probe_version(value), value)
        for value in ['', None, '11.0', '11.0.x', 'latest', '11.0.100+build', '11.0.100-rc.*', '011.0.100', '11.0.100\n']:
            with self.subTest(value=value), self.assertRaises(ValueError): first_use.probe_version(value)

    def test_temporary_project_and_sdk_are_pinned_without_changing_repository(self):
        self.assertTrue(hasattr(first_use, 'configure_probe_toolchain'), 'Missing isolated SDK/runtime pin')
        original = (ROOT/'global.json').read_bytes()
        with tempfile.TemporaryDirectory() as temporary:
            work = Path(temporary)
            first_use.stage_consumer(ROOT/'examples/first-use', work/'consumer')
            first_use.configure_consumer(work/'consumer/FirstUse.csproj', '0.1.0-dev', 'net11.0')
            first_use.configure_probe_toolchain(work, '11.0.100-rc.2.12345.6', '11.0.0-rc.2.12345.6')
            self.assertEqual(json.loads((work/'global.json').read_text()), {'sdk': {
                'version': '11.0.100-rc.2.12345.6', 'rollForward': 'disable', 'allowPrerelease': True}})
            tree = ET.parse(work/'consumer/FirstUse.csproj')
            self.assertEqual(tree.findtext('.//RuntimeFrameworkVersion'), '11.0.0-rc.2.12345.6')
            self.assertEqual(tree.findtext('.//RollForward'), 'Disable')
            self.assertEqual(tree.findtext('.//TargetFramework'), 'net11.0')
            self.assertEqual(tree.find('.//PackageReference').get('Version'), '[0.1.0-dev]')
        self.assertEqual((ROOT/'global.json').read_bytes(), original)

    def test_live_identity_requires_exact_sdk_numeric_runtime_and_full_rc_suffix(self):
        self.assertTrue(hasattr(first_use, 'validate_probe_toolchain'), 'Missing exact observed probe toolchain check')
        sdk = '11.0.100-rc.2.12345.6'; version = '11.0.0-rc.2.12345.6'
        runtime = {'version': '11.0.0', 'framework': '.NET '+version}
        first_use.validate_probe_toolchain(sdk, runtime, sdk, version)
        for actual_sdk, actual in [(sdk+'7', runtime), (sdk, dict(runtime, version='10.0.0')),
                                  (sdk, dict(runtime, framework='.NET 11.0.0')),
                                  (sdk, dict(runtime, framework='.NET 11.0.0-rc.2.12345.7')),
                                  (sdk, {}), ('', runtime)]:
            with self.subTest(sdk=actual_sdk, runtime=actual), self.assertRaises(AssertionError):
                first_use.validate_probe_toolchain(actual_sdk, actual, sdk, version)
        first_use.validate_probe_toolchain('10.0.100', {'version': '10.0.0', 'framework': '.NET 10.0.0'}, '10.0.100', '10.0.0')
        with self.assertRaises(AssertionError):
            first_use.validate_assembly_source('0.1.0-dev+'+'b'*40, '0.1.0-dev', 'a'*40)

    def test_generated_runtimeconfig_must_retain_exact_request(self):
        self.assertTrue(hasattr(first_use, 'validate_probe_runtimeconfig'), 'Missing exact runtimeconfig pin check')
        config = {'runtimeOptions': {'framework': {'name': 'Microsoft.NETCore.App', 'version': '10.0.0'}, 'rollForward': 'Disable'}}
        first_use.validate_probe_runtimeconfig(config, '10.0.0')
        for bad in [{}, {'runtimeOptions': {'framework': {'version': '10.0.1'}, 'rollForward': 'Disable'}},
                    {'runtimeOptions': {'framework': {'version': '10.0.0'}, 'rollForward': 'LatestPatch'}},
                    {'runtimeOptions': {'framework': {'version': '10.0.0'}}}]:
            with self.subTest(config=bad), self.assertRaises(AssertionError):
                first_use.validate_probe_runtimeconfig(bad, '10.0.0')

    def test_probe_arguments_must_be_paired_and_not_enter_stable_release_mode(self):
        base = [sys.executable, str(ROOT/'scripts/verify_first_use.py'), '--cli-dll', 'unused', '--output', 'unused']
        for args in [['--probe-sdk', '10.0.100'], ['--probe-runtime', '10.0.0']]:
            result = subprocess.run(base+args, capture_output=True, text=True)
            self.assertEqual(result.returncode, 2)
            self.assertIn('must be supplied together', result.stderr)
        release = [sys.executable, str(ROOT/'scripts/verify_release_candidate.py'), '--cli-dll', 'unused', '--output', 'unused',
                   '--expected-sdk', '10.0.100', '--expected-runtime', '10.0.0', '--probe-sdk', '11.0.100-rc.2.12345.6', '--probe-runtime', '11.0.0-rc.2.12345.6']
        result = subprocess.run(release, capture_output=True, text=True)
        self.assertEqual(result.returncode, 2)
        self.assertIn('unrecognized arguments', result.stderr)


if __name__ == '__main__': unittest.main()
