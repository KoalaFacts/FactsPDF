"""Validate local package metadata and license bytes; never publish an artifact."""
from __future__ import annotations

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


def validate_package(path, root, version, source, target):
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
        group = single('group')
        assert group.attrib == {'targetFramework': target}, 'Package target framework'
        assert list(single('dependencies')) == [group] and not list(group), 'Package dependency group'
        assert not any(node.tag.split('}')[-1] in ('frameworkReferences', 'frameworkAssemblies')
                       for node in doc.iter()), 'Framework dependencies'
        consumer_folders = ('lib/', 'ref/', 'runtimes/', 'build/', 'buildtransitive/',
                            'buildmultitargeting/', 'analyzers/', 'contentfiles/', 'content/', 'tools/')
        assets = [name for name in names if name.replace('\\', '/').lower().startswith(consumer_folders)
                  or name.replace('\\', '/').lower() == 'runtime.json']
        assert assets == [f'lib/{target}/FactsPDF.dll'], 'Package consumer assets'
        return dict(id='FactsPDF', version=version, targetFramework=target, repository=repository,
                    assembly=package.read(assets[0]))


def validate_assembly_source(actual, version, source):
    assert actual == version + '+' + source, 'Loaded assembly version/source'


def main():
    root = Path(__file__).resolve().parents[1]
    source = current_source(root)
    version, target = expected_contract(root)
    validate_package(local_package(root, version), root, version, source, target)
    assert current_source(root) == source, 'Source changed during verification'
    print(f'Local package identity, version, source, {target} assets, license/README and absence of dependencies verified. Nothing was published.')


if __name__ == '__main__': main()
