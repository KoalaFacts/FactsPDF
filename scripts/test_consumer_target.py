"""Synthetic cross-target guards; these tests do not execute .NET 11."""
import base64
import hashlib
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

import verify_first_use as first_use

ROOT = Path(__file__).resolve().parents[1]


class ConsumerTargetTests(unittest.TestCase):
    def runtime(self, major=11):
        return dict(version=f'{major}.0.0', framework=f'.NET {major}.0.0', os='fixture',
                    rid='linux-x64', architecture='X64', targetFramework=f'.NETCoreApp,Version=v{major}.0',
                    dynamicCodeSupported=True, dynamicCodeCompiled=True)

    def config(self, major=11):
        return {'runtimeOptions': {'tfm': f'net{major}.0',
                'framework': {'name': 'Microsoft.NETCore.App', 'version': f'{major}.0.0'}}}

    def validator(self):
        self.assertTrue(hasattr(first_use, 'validate_consumer_execution'), 'Missing runtimeconfig/live identity check')
        return first_use.validate_consumer_execution

    def test_selected_target_requires_runtimeconfig_and_live_runtime(self):
        check = self.validator()
        check(self.runtime(), 'net11.0', self.config())
        # A hypothetical SDK 11 build that actually targets/runs 10 is not 11 evidence.
        old = self.runtime(10); old['sdk'] = '11.0.100-rc.synthetic'
        for runtime, config in [(old, self.config(10)), (old, self.config()),
                                (self.runtime(), self.config(10))]:
            with self.subTest(runtime=runtime, config=config), self.assertRaises(AssertionError):
                check(runtime, 'net11.0', config)

    def test_runtimeconfig_missing_or_wrong_identity_is_rejected(self):
        check = self.validator()
        bad_configs = [{}, {'runtimeOptions': {}}, self.config(10)]
        for key, value in [('tfm', ''), ('framework', {}),
                           ('framework', {'name': 'Microsoft.AspNetCore.App', 'version': '11.0.0'}),
                           ('framework', {'name': 'Microsoft.NETCore.App', 'version': '10.0.0'}),
                           ('framework', {'name': 'Microsoft.NETCore.App', 'version': '11.0.x'})]:
            config = self.config(); config['runtimeOptions'][key] = value; bad_configs.append(config)
        for key in ['frameworks', 'includedFrameworks']:
            config = self.config(); config['runtimeOptions'][key] = []; bad_configs.append(config)
        for config in bad_configs:
            with self.subTest(config=config), self.assertRaises(AssertionError):
                check(self.runtime(), 'net11.0', config)

    def test_framework_description_must_match_observed_numeric_runtime(self):
        check = self.validator()
        for description in ['.NET 10.0.0', '.NET 11.0.1', 'SDK 11.0.100', '', None]:
            runtime = self.runtime(); runtime['framework'] = description
            with self.subTest(description=description), self.assertRaises(AssertionError):
                check(runtime, 'net11.0', self.config())
        runtime = self.runtime(); runtime['framework'] += '-rc.synthetic'
        check(runtime, 'net11.0', self.config())  # dev probe only, not strict GA acceptance
        with self.assertRaises(AssertionError):
            first_use.validate_release_toolchain('11.0.100', runtime, '11.0.100', '11.0.0')
        runtime.pop('targetFramework')
        with self.assertRaises(AssertionError): check(runtime, 'net11.0', self.config())

    def test_default_net10_and_patch_roll_forward_remain_valid(self):
        check = self.validator()
        check(self.runtime(10), 'net10.0', self.config(10))
        runtime = self.runtime(10); runtime.update(version='10.0.1', framework='.NET 10.0.1')
        check(runtime, 'net10.0', self.config(10))
        config = self.config(10); config['runtimeOptions']['framework']['version'] = '10.0.2'
        with self.assertRaises(AssertionError): check(runtime, 'net10.0', config)

    def test_canonical_consumer_target_selection(self):
        self.assertTrue(hasattr(first_use, 'consumer_tfm'), 'Missing explicit consumer target parser')
        for value in ['net10.0', 'net11.0']:
            self.assertEqual(first_use.consumer_tfm(value), value)
        for value in ['', ' ', 'net11', 'net11.0-windows', 'net11.0;net10.0', 'net011.0', 'net11.x', None]:
            with self.subTest(value=value), self.assertRaises(ValueError): first_use.consumer_tfm(value)

    def test_entry_rejects_invalid_target_before_consumption(self):
        for entry in ['verify_first_use.py', 'verify_release_candidate.py']:
            command = [sys.executable, str(ROOT/'scripts'/entry), '--cli-dll', 'missing.dll', '--output', 'unused']
            if entry == 'verify_release_candidate.py':
                command += ['--expected-sdk', '10.0.100', '--expected-runtime', '10.0.0']
            result = subprocess.run(command + ['--consumer-tfm', 'net11.x'], capture_output=True, text=True)
            self.assertEqual(result.returncode, 2)
            self.assertIn('argument --consumer-tfm:', result.stderr)

    def test_package_cache_target_is_independent_of_consumer_output_target(self):
        self.assertIn('consumer_target', __import__('inspect').signature(first_use.verify_package_bytes).parameters,
                      'Missing separate package and consumer byte paths')
        with tempfile.TemporaryDirectory() as temporary:
            work = Path(temporary); package = work/'package.nupkg'; package.write_bytes(b'fixture package')
            cache = work/'packages/factspdf/0.1.0-dev'; (cache/'lib/net10.0').mkdir(parents=True)
            (cache/'factspdf.0.1.0-dev.nupkg').write_bytes(package.read_bytes())
            (cache/'factspdf.0.1.0-dev.nupkg.sha512').write_text(base64.b64encode(hashlib.sha512(package.read_bytes()).digest()).decode())
            (cache/'lib/net10.0/FactsPDF.dll').write_bytes(b'fixture DLL')
            output = work/'consumer/bin/Release/net11.0'; output.mkdir(parents=True)
            (output/'FactsPDF.dll').write_bytes(b'fixture DLL')
            first_use.verify_package_bytes(work, package, '0.1.0-dev', b'fixture DLL', 'net10.0', consumer_target='net11.0')
            (output/'FactsPDF.dll').write_bytes(b'wrong DLL')
            with self.assertRaisesRegex(AssertionError, 'consumer assembly bytes'):
                first_use.verify_package_bytes(work, package, '0.1.0-dev', b'fixture DLL', 'net10.0', consumer_target='net11.0')


if __name__ == '__main__': unittest.main()
