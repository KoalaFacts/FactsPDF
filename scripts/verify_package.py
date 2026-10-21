"""Validate local package metadata and license bytes; never publish an artifact."""
from __future__ import annotations

from pathlib import Path
import re
import subprocess
from xml.etree import ElementTree as ET
from zipfile import ZipFile

def current_source(root):
    tracked = subprocess.check_output(['git', 'ls-files', '-v', '-z'], cwd=root).split(b'\0')
    assert not any(entry[:1].islower() or entry[:1] == b'S' for entry in tracked if entry), 'Hidden tracked inputs: clear assume-unchanged/skip-worktree flags'
    assert not subprocess.check_output(['git', 'status', '--porcelain', '--untracked-files=all'], cwd=root).strip(), 'Source checkout must be clean'
    # These two SDK projects include source/resources by glob. Git's ignore rules
    # do not affect those globs. Only their top-level bin/obj are build outputs;
    # a nested ignored bin directory can still contain a real compile input.
    projects = ('src/FactsPDF', 'src/FactsPDF.Cli')
    parent_inputs = tuple(prefix + name for prefix in ('', 'src/')
                          for name in ('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props',
                                       'Directory.Build.rsp'))
    ignored = subprocess.check_output(['git', 'ls-files', '--others', '--ignored', '--exclude-standard', '-z',
                                       '--', *projects, *parent_inputs], cwd=root).decode().split('\0')
    inputs = [name for name in ignored if name and not any(
        name.startswith(project + '/' + output + '/') for project in projects for output in ('bin', 'obj'))]
    assert not inputs, 'Ignored project inputs: ' + ', '.join(inputs)
    return subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()


def validate_package(path, root, version, source):
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
        assert single('group').attrib == {'targetFramework': 'net10.0'}, 'Package target framework'
        consumer_folders = ('lib/', 'ref/', 'runtimes/', 'build/', 'buildtransitive/',
                            'buildmultitargeting/', 'analyzers/', 'contentfiles/', 'content/', 'tools/')
        assets = [name for name in names if name.replace('\\', '/').lower().startswith(consumer_folders)]
        assert assets == ['lib/net10.0/FactsPDF.dll'], 'Package consumer assets'
        return dict(id='FactsPDF', version=version, targetFramework='net10.0', repository=repository,
                    assembly=package.read(assets[0]))


def validate_assembly_source(actual, version, source):
    assert actual == version + '+' + source, 'Loaded assembly version/source'


def main():
    root = Path(__file__).resolve().parents[1]
    source = current_source(root)
    version = ET.parse(root / 'Directory.Build.props').findtext('PropertyGroup/Version')
    packages = list((root / 'artifacts/packages').glob('FactsPDF.*.nupkg'))
    assert len(packages) == 1, f'Expected exactly one local package, got {packages}'
    validate_package(packages[0], root, version, source)
    assert current_source(root) == source, 'Source changed during verification'
    print('Local package identity, version, source, net10.0 assets, license/README and absence of dependencies verified. Nothing was published.')


if __name__ == '__main__': main()
