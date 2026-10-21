"""Package acceptance must bind identity and the loaded binary to the tested source."""
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
SOURCE = 'a' * 40
VERSION = '0.1.0-dev'


class PackageContractTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location('package_contract', ROOT / 'scripts/verify_package.py')
        self.module = importlib.util.module_from_spec(spec)
        try:
            spec.loader.exec_module(self.module)
        except (AssertionError, FileNotFoundError) as error:
            self.fail('Package checker must be import-safe: ' + str(error))
        self.assertTrue(hasattr(self.module, 'validate_package'), 'Missing package source contract')
        temporary = tempfile.TemporaryDirectory(); self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        (self.root / 'LICENSE.md').write_bytes(b'expected license')
        (self.root / 'README.md').write_bytes(b'expected readme')

    def package(self, change=None, extra=None):
        xml = ('<package><metadata><id>FactsPDF</id><version>0.1.0-dev</version>'
               '<license type="file">LICENSE.md</license><readme>README.md</readme>'
               '<repository type="git" url="https://github.com/KoalaFacts/FactsPDF" commit="' + SOURCE + '" />'
               '<dependencies><group targetFramework="net10.0" /></dependencies></metadata></package>')
        if change: xml = change(xml)
        path = self.root / 'FactsPDF.0.1.0-dev.nupkg'
        files = {'FactsPDF.nuspec': xml.encode(), 'lib/net10.0/FactsPDF.dll': b'test assembly bytes',
                 'LICENSE.md': b'expected license', 'README.md': b'expected readme'}
        files.update(extra or {})
        with ZipFile(path, 'w') as archive:
            for name, data in files.items(): archive.writestr(name, data)
        return path

    def test_accepts_bound_package_and_returns_its_provenance(self):
        result = self.module.validate_package(self.package(), self.root, VERSION, SOURCE)
        self.assertEqual(result['id'], 'FactsPDF')
        self.assertEqual(result['repository']['commit'], SOURCE)
        self.assertEqual(result['targetFramework'], 'net10.0')
        self.assertEqual(result['assembly'], b'test assembly bytes')

    def test_rejects_wrong_identity_version_or_source_metadata(self):
        for old, new in [('<id>FactsPDF</id>', '<id>Other</id>'),
                         ('0.1.0-dev', '0.2.0'), (SOURCE, 'b' * 40),
                         (' commit="' + SOURCE + '"', ''),
                         ('KoalaFacts/FactsPDF', 'Other/FactsPDF'),
                         ('<version>0.1.0-dev</version>', '<version>0.1.0-dev</version><version>0.2.0</version>')]:
            with self.subTest(replacement=new), self.assertRaises(AssertionError):
                self.module.validate_package(self.package(lambda s: s.replace(old, new)), self.root, VERSION, SOURCE)

    def test_rejects_changed_consumer_assets_dependencies_or_notices(self):
        for old, new in [('net10.0', 'net11.0'),
                         ('<group targetFramework="net10.0" />', '<group targetFramework="net10.0"><dependency id="Other" version="1.0.0" /></group>'),
                         ('<readme>README.md</readme>', '<readme>Other.md</readme>'),
                         ('type="file">LICENSE.md', 'type="expression">MIT')]:
            with self.subTest(replacement=new), self.assertRaises(AssertionError):
                self.module.validate_package(self.package(lambda s: s.replace(old, new)), self.root, VERSION, SOURCE)
        for extra in [{'lib/net11.0/FactsPDF.dll': b'extra target'},
                      {'runtimes/linux-x64/native/other.so': b'extra runtime'},
                      {'LICENSE.md': b'changed'}, {'README.md': b'changed'}]:
            with self.subTest(files=list(extra)), self.assertRaises(AssertionError):
                self.module.validate_package(self.package(extra=extra), self.root, VERSION, SOURCE)

    def test_rejects_relabelled_assembly_from_another_source(self):
        self.assertTrue(hasattr(self.module, 'validate_assembly_source'), 'Missing loaded binary source check')
        self.module.validate_assembly_source(VERSION + '+' + SOURCE, VERSION, SOURCE)
        for actual in [None, VERSION, VERSION + '+' + 'b' * 40, '0.2.0+' + SOURCE]:
            with self.subTest(actual=actual), self.assertRaises(AssertionError):
                self.module.validate_assembly_source(actual, VERSION, SOURCE)

    def test_rejects_additional_nuget_consumer_asset_categories(self):
        for folder in ['build', 'buildTransitive', 'buildMultiTargeting', 'analyzers', 'contentFiles', 'content', 'tools']:
            with self.subTest(folder=folder), self.assertRaisesRegex(AssertionError, 'consumer assets'):
                self.module.validate_package(self.package(extra={folder + '/extra.targets': b'extra'}), self.root, VERSION, SOURCE)

    def test_current_source_cannot_hide_untracked_source_with_git_configuration(self):
        subprocess.run(['git', 'init', '-q', str(self.root)], check=True)
        subprocess.run(['git', '-C', str(self.root), 'add', '.'], check=True)
        subprocess.run(['git', '-C', str(self.root), '-c', 'user.name=Test', '-c', 'user.email=test@example.invalid',
                        'commit', '-qm', 'fixture'], check=True)
        subprocess.run(['git', '-C', str(self.root), 'config', 'status.showUntrackedFiles', 'no'], check=True)
        (self.root / 'Extra.cs').write_text('class Extra {}')
        with self.assertRaisesRegex(AssertionError, 'clean'):
            self.module.current_source(self.root)

    def test_current_source_rejects_uncommitted_source_changes(self):
        self.assertTrue(hasattr(self.module, 'current_source'), 'Missing clean source check')
        subprocess.run(['git', 'init', '-q', str(self.root)], check=True)
        subprocess.run(['git', '-C', str(self.root), 'add', '.'], check=True)
        subprocess.run(['git', '-C', str(self.root), '-c', 'user.name=Test', '-c', 'user.email=test@example.invalid',
                        'commit', '-qm', 'fixture'], check=True)
        expected = subprocess.check_output(['git', '-C', str(self.root), 'rev-parse', 'HEAD'], text=True).strip()
        self.assertEqual(self.module.current_source(self.root), expected)
        (self.root / 'README.md').write_text('uncommitted change')
        with self.assertRaisesRegex(AssertionError, 'clean'):
            self.module.current_source(self.root)

    def test_current_source_rejects_ignored_project_inputs_but_allows_build_outputs(self):
        subprocess.run(['git', 'init', '-q', str(self.root)], check=True)
        subprocess.run(['git', '-C', str(self.root), 'add', '.'], check=True)
        subprocess.run(['git', '-C', str(self.root), '-c', 'user.name=Test', '-c', 'user.email=test@example.invalid',
                        'commit', '-qm', 'fixture'], check=True)
        rules = 'Extra.cs\nDirectory.Build.targets\n**/bin/\n**/obj/\n'
        info_exclude = self.root / '.git/info/exclude'
        info_exclude.write_text(rules)
        for project in ['FactsPDF', 'FactsPDF.Cli']:
            path = self.root / 'src' / project; path.mkdir(parents=True)
            for folder in ['bin', 'obj']:
                output = path / folder; output.mkdir(); (output / 'Generated.cs').write_text('generated output')
        self.module.current_source(self.root)
        for name in ['src/FactsPDF/Extra.cs', 'src/FactsPDF.Cli/Extra.cs',
                     'src/FactsPDF/Nested/bin/Extra.cs', 'src/Directory.Build.targets']:
            path = self.root / name; path.parent.mkdir(parents=True, exist_ok=True); path.write_text('ignored build input')
            with self.subTest(name=name), self.assertRaisesRegex(AssertionError, 'Ignored project inputs'):
                self.module.current_source(self.root)
            path.unlink()
        info_exclude.write_text('')
        global_ignore = self.root / '.git/global-ignore'; global_ignore.write_text(rules)
        subprocess.run(['git', '-C', str(self.root), 'config', 'core.excludesFile', str(global_ignore)], check=True)
        self.module.current_source(self.root)
        (self.root / 'src/FactsPDF/Extra.cs').write_text('globally ignored compile input')
        with self.assertRaisesRegex(AssertionError, 'Ignored project inputs'):
            self.module.current_source(self.root)


if __name__ == '__main__': unittest.main()
