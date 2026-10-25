"""Reject provenance leaks and incorrect output in the external first-use verifier."""
import copy
import importlib.util
import os
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


class FirstUseTests(unittest.TestCase):
    def setUp(self):
        source = ROOT / 'scripts/verify_first_use.py'
        self.assertTrue(source.is_file(), 'Missing executable first-use verification')
        spec = importlib.util.spec_from_file_location('first_use', source)
        self.module = importlib.util.module_from_spec(spec); spec.loader.exec_module(self.module)
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.work = Path(self.temp.name)

    def test_workspace_rejects_repository_and_inherited_build_files(self):
        self.module.validate_workspace(self.work, ROOT)
        with self.assertRaisesRegex(ValueError, 'outside'):
            self.module.validate_workspace(ROOT / 'artifacts/consumer', ROOT)
        nested = self.work / 'consumer'; nested.mkdir()
        (self.work / 'Directory.Build.props').write_text('<Project/>')
        with self.assertRaisesRegex(ValueError, 'inherited'):
            self.module.validate_workspace(nested, ROOT)

    def assets(self):
        return {'libraries': {'FactsPDF/0.1.0-dev': {'type': 'package', 'path': 'factspdf/0.1.0-dev'}},
                'packageFolders': {str(self.work / 'packages'): {}},
                'project': {'restore': {'projectPath': str(self.work / 'consumer/FirstUse.csproj'),
                    'projectStyle': 'PackageReference', 'packagesPath': str(self.work / 'packages'),
                    'sources': {str(self.work / 'feed'): {}},
                    'configFilePaths': [str(self.work / 'consumer/NuGet.Config')],
                    'frameworks': {'net10.0': {'projectReferences': {}}}}}}

    def test_assets_require_only_explicit_feed_config_and_cache(self):
        valid = self.assets(); self.module.validate_assets(valid, self.work, '0.1.0-dev', 'net10.0')
        changes = [('sources', {'https://api.nuget.org/v3/index.json': {}}),
                   ('configFilePaths', [str(Path.home() / '.nuget/NuGet/NuGet.Config')]),
                   ('packagesPath', str(ROOT / 'artifacts/cache'))]
        for field, value in changes:
            bad = copy.deepcopy(valid); bad['project']['restore'][field] = value
            with self.subTest(field=field), self.assertRaises(AssertionError):
                self.module.validate_assets(bad, self.work, '0.1.0-dev', 'net10.0')
        bad = copy.deepcopy(valid); bad['packageFolders']['/shared/cache'] = {}
        with self.assertRaises(AssertionError): self.module.validate_assets(bad, self.work, '0.1.0-dev', 'net10.0')

    def test_assets_reject_project_references_extra_packages_and_wrong_version(self):
        for change in ('project', 'package', 'version'):
            bad = self.assets()
            if change == 'project': bad['project']['restore']['frameworks']['net10.0']['projectReferences']['/repo/FactsPDF.csproj'] = {}
            elif change == 'package': bad['libraries']['Other/1.0.0'] = {'type': 'package'}
            else: bad['libraries']['FactsPDF/0.2.0'] = bad['libraries'].pop('FactsPDF/0.1.0-dev')
            with self.subTest(change=change), self.assertRaises(AssertionError):
                self.module.validate_assets(bad, self.work, '0.1.0-dev', 'net10.0')

    def test_text_checker_preserves_words_and_page_order(self):
        wanted = ['First use\nLocal package', 'Second page\nEND-002']
        self.module.validate_text('First use\nLocal package\n\fSecond page\nEND-002\n\f', wanted)
        for actual in ['Firstuse Local package\fSecond page END-002\f',
                       'Second page END-002\fFirst use Local package\f',
                       'First use Local package\fSecond page END-003\f']:
            with self.assertRaisesRegex(AssertionError, 'text/page order'):
                self.module.validate_text(actual, wanted)

    def test_runtime_identity_rejects_missing_or_wrong_actual_runtime(self):
        self.assertTrue(hasattr(self.module, 'validate_runtime'), 'Missing actual consumer runtime check')
        runtime = dict(version='10.0.0', framework='.NET 10.0.0', os='Linux test host',
                       rid='linux-x64', architecture='X64', targetFramework='.NETCoreApp,Version=v10.0',
                       dynamicCodeSupported=True, dynamicCodeCompiled=True)
        self.module.validate_runtime(runtime, 'net10.0')
        for field, value in [('version', '11.0.0'), ('targetFramework', '.NETCoreApp,Version=v11.0'),
                             ('version', ''), ('os', ''), ('rid', ''), ('architecture', ''),
                             ('framework', ''), ('dynamicCodeSupported', False),
                             ('dynamicCodeCompiled', 'true')]:
            bad = dict(runtime); bad[field] = value
            with self.subTest(field=field, value=value), self.assertRaises(AssertionError):
                self.module.validate_runtime(bad, 'net10.0')
        for field in runtime:
            bad = dict(runtime); del bad[field]
            with self.subTest(missing=field), self.assertRaises(AssertionError):
                self.module.validate_runtime(bad, 'net10.0')
        # An SDK label is not evidence that the consumer ran on that runtime.
        with self.assertRaises(AssertionError):
            self.module.validate_runtime({'sdk': '11.0.100-rc.2.26504.105'}, 'net11.0')

    def test_staging_does_not_import_existing_build_outputs(self):
        import shutil
        source = self.work / 'source'; shutil.copytree(ROOT / 'examples/first-use', source)
        stale = source / 'bin/Release/net10.0'; stale.mkdir(parents=True)
        (stale / 'FirstUse.dll').write_bytes(b'stale executable')
        (source / 'Directory.Build.props').write_text('<Project/>')
        target = self.work / 'consumer'
        self.assertTrue(hasattr(self.module, 'stage_consumer'), 'Missing clean staging boundary')
        self.module.stage_consumer(source, target)
        self.assertFalse((target / 'bin').exists())
        self.assertFalse((target / 'Directory.Build.props').exists())
        self.assertTrue((target / 'Program.cs').is_file())

    def test_copyable_consumer_is_package_based_and_explicit(self):
        from xml.etree import ElementTree as ET
        import verify_package as package_check
        version, target = package_check.expected_contract(ROOT)
        project = ROOT / 'examples/first-use/FirstUse.csproj'
        self.assertTrue(project.is_file(), 'Missing independent consumer project')
        tree = ET.parse(project)
        self.assertEqual(tree.findtext('.//TargetFramework'), target)
        self.assertEqual(tree.findtext('.//OutputType'), 'Exe')
        self.assertEqual(tree.findtext('.//NuGetAudit'), 'false')
        self.assertFalse(tree.findall('.//ProjectReference'))
        self.assertEqual([p.attrib for p in tree.findall('.//PackageReference')],
                         [{'Include': 'FactsPDF', 'Version': f'[{version}]'}])

    def test_environment_drops_import_hooks_and_uses_empty_user_extensions(self):
        self.assertTrue(hasattr(self.module, 'build_environment'), 'Missing environment isolation')
        env, settings = self.module.build_environment({
            'PATH': '/usr/bin', 'HOME': '/existing-home',
            'CustomBeforeMicrosoftCommonTargets': '/repo/hook.targets',
            'DirectoryBuildPropsPath': '/repo/Directory.Build.props',
            'MSBuildUserExtensionsPath': '/existing-user-extensions',
            'DOTNET_STARTUP_HOOKS': '/hook.dll', 'NUGET_PLUGIN_PATHS': '/plugin'}, self.work)
        self.assertEqual(env['PATH'], '/usr/bin')
        self.assertEqual(env['HOME'], '/existing-home')
        self.assertFalse(set(env) & {'CustomBeforeMicrosoftCommonTargets', 'DirectoryBuildPropsPath',
                                     'DOTNET_STARTUP_HOOKS', 'NUGET_PLUGIN_PATHS'})
        self.assertEqual(env['MSBuildUserExtensionsPath'], str(self.work / 'msbuild-user'))
        self.assertEqual(settings['ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets'], 'false')

    def test_runtime_files_are_private_and_independent_of_ambient_home(self):
        env, settings = self.module.build_environment({
            'HOME': '/read-only-home', 'XDG_RUNTIME_DIR': '/ambient-runtime'}, self.work)
        self.assertIn('XDG_RUNTIME_DIR', env)
        runtime = Path(env['XDG_RUNTIME_DIR'])
        self.assertEqual(runtime.parent, self.work)
        self.assertEqual(settings['XDG_RUNTIME_DIR'], str(runtime))
        self.assertEqual(env['HOME'], '/read-only-home')
        self.assertTrue(runtime.is_dir())
        self.assertEqual(list(runtime.iterdir()), [])
        if os.name == 'posix':
            self.assertEqual(runtime.stat().st_mode & 0o777, 0o700)
        (runtime / 'probe').write_bytes(b'private runtime files')
        self.assertEqual((runtime / 'probe').read_bytes(), b'private runtime files')

    def test_preprocessed_import_graph_rejects_indirect_repository_import(self):
        self.assertTrue(hasattr(self.module, 'validate_imports'), 'Missing actual import graph validation')
        def project(paths):
            return '<Project>' + ''.join('<!--\n' + '='*20 + '\n' + str(p) + '\n' + '='*20 + '\n-->' for p in paths) + '</Project>'
        consumer = self.work / 'consumer/FirstUse.csproj'
        good = [consumer, self.work / 'sdk/Sdk.props']
        self.assertEqual(self.module.validate_imports(project(good), ROOT, consumer), list(map(str, good)))
        with self.assertRaisesRegex(AssertionError, 'repository build import'):
            self.module.validate_imports(project(good + [ROOT / 'Directory.Build.props']), ROOT, consumer)
        with self.assertRaisesRegex(AssertionError, 'import graph'):
            self.module.validate_imports('<Project/>', ROOT, consumer)


if __name__ == '__main__': unittest.main()
