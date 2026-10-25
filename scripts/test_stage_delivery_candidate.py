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
            '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup>'
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


if __name__ == '__main__':
    unittest.main()
