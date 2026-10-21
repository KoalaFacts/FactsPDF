"""Future version/TFM fixtures exercise verifiers, not an installed .NET runtime."""
import base64
import hashlib
from pathlib import Path
import tempfile
import unittest
from xml.etree import ElementTree as ET
from zipfile import ZipFile

import verify_package as package_check
import verify_first_use as first_use

ROOT = Path(__file__).resolve().parents[1]
SOURCE = 'a' * 40


class DeclaredPackageContractTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(); self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        (self.root / 'LICENSE.md').write_text('license')
        (self.root / 'README.md').write_text('readme')

    def declaration(self, version='0.2.0', target='net11.0'):
        (self.root / 'Directory.Build.props').write_text(
            f'<Project><PropertyGroup><Version>{version}</Version>'
            f'<TargetFramework>{target}</TargetFramework></PropertyGroup></Project>')

    def package(self, version, target):
        path = self.root / f'FactsPDF.{version}.nupkg'
        with ZipFile(path, 'w') as archive:
            archive.writestr('FactsPDF.nuspec',
                f'<package><metadata><id>FactsPDF</id><version>{version}</version>'
                '<license type="file">LICENSE.md</license><readme>README.md</readme>'
                '<repository type="git" url="https://github.com/KoalaFacts/FactsPDF" '
                f'commit="{SOURCE}" /><dependencies><group targetFramework="{target}" />'
                '</dependencies></metadata></package>')
            archive.writestr(f'lib/{target}/FactsPDF.dll', b'fixture DLL, not executable')
            archive.writestr('LICENSE.md', b'license'); archive.writestr('README.md', b'readme')
        return path

    def test_declared_future_version_and_single_target_accept_matching_package(self):
        self.declaration()
        version, target = package_check.expected_contract(self.root)
        self.assertEqual((version, target), ('0.2.0', 'net11.0'))
        result = package_check.validate_package(self.package(version, target), self.root, version, SOURCE, target)
        self.assertEqual((result['version'], result['targetFramework']), (version, target))

    def test_package_cannot_choose_expected_version_or_target(self):
        self.declaration('0.1.0-dev', 'net10.0')
        version, target = package_check.expected_contract(self.root)
        for package_version, package_target in [('0.2.0', 'net10.0'), ('0.1.0-dev', 'net11.0')]:
            with self.subTest(version=package_version, target=package_target), self.assertRaises(AssertionError):
                package_check.validate_package(self.package(package_version, package_target), self.root, version, SOURCE, target)

    def test_ambiguous_conditional_or_nonliteral_declaration_fails_closed(self):
        for replacement in ['<TargetFrameworks>net10.0;net11.0</TargetFrameworks>',
                            '<TargetFramework>net11.0</TargetFramework><TargetFramework>net10.0</TargetFramework>',
                            '<TargetFramework Condition="x">net11.0</TargetFramework>',
                            '<TargetFramework>$(ReleaseTarget)</TargetFramework>']:
            self.declaration()
            path = self.root / 'Directory.Build.props'
            path.write_text(path.read_text().replace('<TargetFramework>net11.0</TargetFramework>', replacement))
            with self.subTest(replacement=replacement), self.assertRaises(AssertionError):
                package_check.expected_contract(self.root)
        self.declaration()
        path.write_text(path.read_text().replace('<PropertyGroup>', '<PropertyGroup Condition="x">'))
        with self.assertRaises(AssertionError): package_check.expected_contract(self.root)

    def test_local_selection_uses_expected_version_and_rejects_stale_extra_packages(self):
        self.declaration()
        packages = self.root / 'artifacts/packages'; packages.mkdir(parents=True)
        chosen = packages / 'FactsPDF.0.2.0.nupkg'; chosen.write_bytes(b'fixture')
        self.assertEqual(package_check.local_package(self.root, '0.2.0'), chosen)
        with self.assertRaises(AssertionError): package_check.local_package(self.root, '0.1.0-dev')
        (packages / 'FactsPDF.0.1.0-dev.nupkg').write_bytes(b'stale')
        with self.assertRaises(AssertionError): package_check.local_package(self.root, '0.2.0')

    def test_staged_consumer_uses_trusted_declarations_without_changing_example(self):
        source = ROOT / 'examples/first-use'
        original = (source / 'FirstUse.csproj').read_bytes()
        first_use.stage_consumer(source, self.root / 'consumer')
        first_use.configure_consumer(self.root / 'consumer/FirstUse.csproj', '0.2.0', 'net11.0')
        tree = ET.parse(self.root / 'consumer/FirstUse.csproj')
        self.assertEqual(tree.findtext('.//TargetFramework'), 'net11.0')
        self.assertEqual(tree.find('.//PackageReference').attrib, {'Include': 'FactsPDF', 'Version': '[0.2.0]'})
        self.assertFalse(tree.findall('.//ProjectReference'))
        self.assertEqual((source / 'FirstUse.csproj').read_bytes(), original)

    def test_future_assets_and_cache_are_checked_against_expected_target(self):
        version, target = '0.2.0', 'net11.0'
        assets = {'libraries': {'FactsPDF/'+version: {'type': 'package', 'path': 'factspdf/'+version}},
                  'packageFolders': {str(self.root / 'packages'): {}},
                  'project': {'restore': {'projectPath': str(self.root / 'consumer/FirstUse.csproj'),
                    'projectStyle': 'PackageReference', 'packagesPath': str(self.root / 'packages'),
                    'sources': {str(self.root / 'feed'): {}},
                    'configFilePaths': [str(self.root / 'consumer/NuGet.Config')],
                    'frameworks': {target: {'projectReferences': {}}}}}}
        first_use.validate_assets(assets, self.root, version, target)
        with self.assertRaises(AssertionError): first_use.validate_assets(assets, self.root, version, 'net10.0')
        package = self.package(version, target); dll = b'fixture DLL, not executable'
        cache = self.root / 'packages/factspdf' / version
        (cache / 'lib' / target).mkdir(parents=True)
        (cache / f'factspdf.{version}.nupkg').write_bytes(package.read_bytes())
        (cache / f'factspdf.{version}.nupkg.sha512').write_text(base64.b64encode(hashlib.sha512(package.read_bytes()).digest()).decode())
        (cache / 'lib' / target / 'FactsPDF.dll').write_bytes(dll)
        output = self.root / 'consumer/bin/Release' / target; output.mkdir(parents=True)
        (output / 'FactsPDF.dll').write_bytes(dll)
        first_use.verify_package_bytes(self.root, package, version, dll, target)
        (cache / 'lib' / target / 'FactsPDF.dll').write_bytes(b'wrong DLL')
        with self.assertRaisesRegex(AssertionError, 'cached assembly bytes'):
            first_use.verify_package_bytes(self.root, package, version, dll, target)


if __name__ == '__main__': unittest.main()
