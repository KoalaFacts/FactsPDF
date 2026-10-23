"""Validate local package metadata and license bytes; never publish an artifact."""
from __future__ import annotations

import argparse
import json
import tempfile
from pathlib import Path
import re
import subprocess
from xml.etree import ElementTree as ET
from zipfile import ZipFile

def current_source(root):
    build_configuration = ('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'Directory.Build.rsp')
    inherited = [str(parent / name) for parent in root.resolve().parents for name in build_configuration if (parent / name).exists()]
    assert not inherited, 'Ancestor build configuration: ' + ', '.join(inherited)
    tracked = subprocess.check_output(['git', 'ls-files', '-v', '-z'], cwd=root).split(b'\0')
    assert not any(entry[:1].islower() or entry[:1] == b'S' for entry in tracked if entry), 'Hidden tracked inputs: clear assume-unchanged/skip-worktree flags'
    assert not subprocess.check_output(['git', 'status', '--porcelain', '--untracked-files=all'], cwd=root).strip(), 'Source checkout must be clean'
    # These two SDK projects include source/resources by glob. Git's ignore rules
    # do not affect those globs. Only their top-level bin/obj are build outputs;
    # a nested ignored bin directory can still contain a real compile input.
    projects = ('src/FactsPDF', 'src/FactsPDF.Cli')
    parent_inputs = tuple(prefix + name for prefix in ('', 'src/') for name in build_configuration)
    ignored = subprocess.check_output(['git', 'ls-files', '--others', '--ignored', '--exclude-standard', '-z',
                                       '--', *projects, *parent_inputs], cwd=root).decode().split('\0')
    inputs = [name for name in ignored if name and not any(
        name.startswith(project + '/' + output + '/') for project in projects for output in ('bin', 'obj'))]
    assert not inputs, 'Ignored project inputs: ' + ', '.join(inputs)
    return subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()


def expected_contract(root):
    """Read the repository's explicit single-target declaration, never the package.

    This is deliberately not an MSBuild evaluator. Ambiguous/conditional or
    computed declarations need a reviewed verifier update rather than guessing.
    """
    doc = ET.parse(root / 'Directory.Build.props').getroot()
    assert not doc.findall('.//TargetFrameworks'), 'Expected a single target framework'
    def literal(name, pattern):
        nodes = doc.findall('.//' + name)
        assert len(nodes) == 1, 'Expected one declared ' + name
        node = nodes[0]
        groups = [group for group in doc.findall('PropertyGroup') if node in list(group)]
        assert len(groups) == 1 and not groups[0].attrib and not node.attrib, 'Expected unconditional ' + name
        value = node.text or ''
        assert re.fullmatch(pattern, value), 'Expected literal ' + name
        return value
    return (literal('Version', r'(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-[0-9a-z]+(?:[.-][0-9a-z]+)*)?'),
            literal('TargetFramework', r'net[1-9][0-9]*\.0'))


def local_package(root, version):
    packages = list((root / 'artifacts/packages').glob('FactsPDF.*.nupkg'))
    expected = root / 'artifacts/packages' / f'FactsPDF.{version}.nupkg'
    assert packages == [expected], f'Expected only local package {expected}, got {packages}'
    return expected


def validate_package(path, root, version, source, target, *, assembly_reader=None):
    targets = (target,) if isinstance(target, str) else tuple(target)
    assert targets and len(set(targets)) == len(targets), "Explicit unique target configuration"
    assert all(re.fullmatch(r"net[1-9][0-9]*\.0", t) for t in targets), "Target configuration"
    assert len(targets) == 1 or targets == ("net10.0", "net11.0"), "Supported candidate target set"
    assert len(targets) == 1 or callable(assembly_reader), "Candidate requires binary metadata reader"
    assert version and re.fullmatch(r'[0-9a-f]{40}', source), 'Expected package version/source'
    with ZipFile(path) as package:
        names = package.namelist()
        assert len(names) == len(set(names)), 'Duplicate package entries'
        nuspecs = [name for name in names if name.endswith('.nuspec')]
        assert len(nuspecs) == 1, 'Package nuspec count'
        doc = ET.fromstring(package.read(nuspecs[0]))
        def single(name):
            matches = [node for node in doc.iter() if node.tag.split('}')[-1] == name]
            assert len(matches) == 1, 'Package metadata: ' + name
            return matches[0]
        assert single('id').text == 'FactsPDF', 'Package identity'
        assert single('version').text == version, 'Package version'
        repository = dict(single('repository').attrib)
        assert repository == dict(type='git', url='https://github.com/KoalaFacts/FactsPDF', commit=source), 'Package repository source'
        assert single('license').attrib == {'type': 'file'} and single('license').text == 'LICENSE.md', 'Package license metadata'
        assert single('readme').text == 'README.md', 'Package README metadata'
        assert package.read('LICENSE.md') == (root / 'LICENSE.md').read_bytes(), 'License changed during packing'
        assert package.read('README.md') == (root / 'README.md').read_bytes(), 'README missing or incorrect'
        assert not any(node.tag.split('}')[-1] == 'dependency' for node in doc.iter()), 'Runtime package dependencies'
        groups = list(single('dependencies'))
        assert [n for n in doc.iter() if n.tag.split('}')[-1] == 'group'] == groups, 'Unexpected dependency group'
        assert len(groups) == len(targets), 'Package dependency group count'
        assert all(g.tag.split('}')[-1] == 'group' and not list(g) for g in groups), 'Package dependency group'
        assert sorted(tuple(g.attrib.items()) for g in groups) == sorted((('targetFramework', t),) for t in targets), 'Package target framework'
        assert not any(node.tag.split('}')[-1] in ('frameworkReferences', 'frameworkAssemblies')
                       for node in doc.iter()), 'Framework dependencies'
        consumer_folders = ('lib/', 'ref/', 'runtimes/', 'build/', 'buildtransitive/',
                            'buildmultitargeting/', 'analyzers/', 'contentfiles/', 'content/', 'tools/')
        assets = [name for name in names if name.replace('\\', '/').lower().startswith(consumer_folders)
                  or name.replace('\\', '/').lower() == 'runtime.json']
        assert sorted(assets) == sorted(f'lib/{t}/FactsPDF.dll' for t in targets), 'Package consumer assets'
        assemblies = {t: package.read(f'lib/{t}/FactsPDF.dll') for t in targets}
        if assembly_reader is not None:
            for t, data in assemblies.items():
                metadata = assembly_reader(data)
                assert metadata['name'] == 'FactsPDF', 'Assembly identity'
                assert metadata['culture'] == '' and metadata['publicKey'] == '', 'Assembly culture/signing identity'
                assert metadata['assemblyVersion'] == version.split('-', 1)[0] + '.0', 'Assembly version'
                assert metadata['targetFramework'] == '.NETCoreApp,Version=v' + t[3:], 'Assembly target framework'
                assert metadata.get('runtimeReferences') == [t[3:] + '.0.0'], 'Assembly runtime reference'
                validate_assembly_source(metadata['informationalVersion'], version, source)
        result = dict(id='FactsPDF', version=version, targetFrameworks=targets, repository=repository, assemblies=assemblies)
        if len(targets) == 1:
            result.update(targetFramework=targets[0], assembly=assemblies[targets[0]])
        return result


def validate_selected_asset(package, consumer_target, selected, loaded_bytes):
    # Exact matching assets only: do not accept NuGet fallback for this candidate.
    assert consumer_target in package['assemblies'], 'Unconfigured consumer target'
    expected = [f'lib/{consumer_target}/FactsPDF.dll']
    assert selected.get('compile') == expected and selected.get('runtime') == expected, 'Consumer asset selection'
    assert loaded_bytes == package['assemblies'][consumer_target], 'Consumed assembly bytes'



def validate_assembly_source(actual, version, source):
    assert actual == version + '+' + source, 'Loaded assembly version/source'


def binary_reader(inspector, dotnet='dotnet'):
    inspector = Path(inspector).resolve()
    assert inspector.is_file(), 'Missing compiled metadata inspector'
    def read(data):
        with tempfile.TemporaryDirectory(prefix='factspdf-metadata-') as temporary:
            path = Path(temporary) / 'assembly.dll'
            path.write_bytes(data)
            result = subprocess.run([str(dotnet), str(inspector), str(path)],
                                    capture_output=True, timeout=60, check=True)
            return json.loads(result.stdout)
    return read


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--assembly-inspector', type=Path)
    parser.add_argument('--dotnet', default='dotnet')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    source = current_source(root)
    version, target = expected_contract(root)
    validate_package(local_package(root, version), root, version, source, target,
                     assembly_reader=binary_reader(args.assembly_inspector, args.dotnet) if args.assembly_inspector else None)
    assert current_source(root) == source, 'Source changed during verification'
    print(f'Local package identity, version, source, {target} assets, license/README and absence of dependencies verified. Nothing was published.')


if __name__ == '__main__': main()
