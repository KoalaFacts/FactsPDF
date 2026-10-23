"""Reject a real packaged net10 DLL retagged as net11; never download or publish."""
import argparse
from pathlib import Path
import tempfile
from xml.etree import ElementTree as ET
from zipfile import ZipFile
from verify_package import binary_reader, current_source, expected_contract, local_package, validate_package


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--assembly-inspector', type=Path, required=True)
    parser.add_argument('--dotnet', default='dotnet')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    source = current_source(root)
    version, target = expected_contract(root)
    assert target == 'net10.0', 'This regression requires the existing net10 package'
    package = local_package(root, version)
    reader = binary_reader(args.assembly_inspector, args.dotnet)
    validate_package(package, root, version, source, target, assembly_reader=reader)
    with ZipFile(package) as archive:
        files = {name: archive.read(name) for name in archive.namelist()}
    original = files['lib/net10.0/FactsPDF.dll']
    assert original.count(b'.NETCoreApp,Version=v10.0') == 1, 'Unique original TFM attribute'
    # Deliberately invalid test asset: only an equal-length attribute string is
    # changed; real AssemblyRef still points to System.Runtime 10.0.0.0.
    retagged = original.replace(b'.NETCoreApp,Version=v10.0', b'.NETCoreApp,Version=v11.0')
    observation = reader(retagged)
    assert observation['targetFramework'] == '.NETCoreApp,Version=v11.0'
    assert observation['runtimeReferences'] == ['10.0.0.0']
    files['lib/net11.0/FactsPDF.dll'] = retagged
    nuspec = next(name for name in files if name.endswith('.nuspec'))
    document = ET.fromstring(files[nuspec])
    namespace = document.tag.split('}')[0] + '}' if '}' in document.tag else ''
    dependencies = document.find('.//' + namespace + 'dependencies')
    assert dependencies is not None
    ET.SubElement(dependencies, namespace + 'group', targetFramework='net11.0')
    files[nuspec] = ET.tostring(document)
    with tempfile.TemporaryDirectory(prefix='factspdf-invalid-package-') as temporary:
        invalid = Path(temporary) / 'invalid-test-fixture.nupkg'
        with ZipFile(invalid, 'w') as archive:
            for name, data in files.items(): archive.writestr(name, data)
        try:
            validate_package(invalid, root, version, source, ('net10.0', 'net11.0'), assembly_reader=reader)
        except AssertionError as error:
            assert str(error) == 'Assembly runtime reference', str(error)
        else:
            raise AssertionError('Accepted a net10 binary with a forged net11 label')
    assert current_source(root) == source, 'Source changed during verification'
    print('Real net10 package accepted; retagged old binary rejected. No real net11 or AOT claim.')


if __name__ == '__main__': main()
