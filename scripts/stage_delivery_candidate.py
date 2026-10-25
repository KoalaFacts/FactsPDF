"""Source-clean, bounded preflight for the future private FactsPDF candidate producer.

This phase creates only a source-inspection receipt. It builds no package, native
binary or installable candidate, and never uploads/publishes software.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from xml.etree import ElementTree as ET

from verify_package import current_source, expected_contract

VERSION = '0.1.0-preview.1'
TARGET = 'net10.0'
SDK = '10.0.402'
RUNTIME = '10.0.13'
PROFILES = {'local-linux': 'linux-x64', 'win-x64-validation': 'win-x64'}
CONFIG_NAMES = ('Directory.Build.props', 'Directory.Build.targets',
                'Directory.Packages.props', 'Directory.Build.rsp')
INSPECTOR = Path('tools/FactsPDF.PackageInspector')
INSPECTOR_OUTPUT_EXCLUDES = '$(DefaultItemExcludes);bin/**;obj/**'
LINK = Path('scripts/inspect_candidate_assembly.cs')


def _git(root: Path, *args: str) -> bytes:
    return subprocess.check_output(['git', *args], cwd=root)


def _sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def identity(root: Path, profile: str) -> dict:
    if profile not in PROFILES:
        raise ValueError('Unknown delivery profile')
    version, target = expected_contract(root)
    if (version, target) != (VERSION, TARGET):
        raise ValueError('Fixed candidate source version/target mismatch')
    sdk = json.loads((root / 'global.json').read_text(encoding='utf-8'))
    if sdk != {'sdk': {'version': SDK, 'rollForward': 'disable', 'allowPrerelease': False}}:
        raise ValueError('Fixed candidate SDK roll-forward mismatch')
    data = json.loads((root / 'eng/delivery-candidate.json').read_text(encoding='utf-8'))
    fields = {'schemaVersion': 1, 'packageId': 'FactsPDF', 'version': VERSION,
              'targetFramework': TARGET}
    if any(data.get(k) != v for k, v in fields.items()):
        raise ValueError('Candidate identity declaration mismatch')
    selected_sdk = data.get('sdk', {})
    if any(selected_sdk.get(k) != v for k, v in sdk['sdk'].items()):
        raise ValueError('Candidate SDK declaration mismatch')
    if data.get('managedRuntime') != {'framework': 'Microsoft.NETCore.App',
                                      'version': RUNTIME, 'rollForward': 'Disable'}:
        raise ValueError('Candidate managed-runtime declaration mismatch')
    if data.get('nativePacks') != {'runtimeVersion': RUNTIME, 'compilerVersion': RUNTIME}:
        raise ValueError('Candidate native-pack declaration mismatch')
    if data.get('publication') != {'published': False, 'distributionAuthorized': False}:
        raise ValueError('Candidate is not authorized for distribution')
    profiles = data.get('profiles')
    if not isinstance(profiles, list) or {x.get('name'): (x.get('rid'), x.get('binaryRetained'))
                                                 for x in profiles if isinstance(x, dict)} != {
            'local-linux': ('linux-x64', True), 'win-x64-validation': ('win-x64', False)} or len(profiles) != 2:
        raise ValueError('Candidate platform/retention mismatch')
    return {'packageId': 'FactsPDF', 'version': VERSION, 'targetFramework': TARGET,
            'sdkVersion': SDK, 'managedRuntimeVersion': RUNTIME,
            'nativeRuntimePackVersion': RUNTIME, 'nativeCompilerPackVersion': RUNTIME,
            'profile': profile, 'rid': PROFILES[profile],
            'binaryRetained': profile == 'local-linux'}


def inspector_inputs(root: Path) -> list[dict]:
    """Freeze actual SDK implicit source/resource globs plus explicit link/import closure.

    The inspector is a build-time metadata oracle; ignored sources under its own
    default glob must never become trusted merely because git status is clean.
    """
    tracked = set(filter(None, _git(root, 'ls-files', '-z').decode().split('\0')))
    project = root / INSPECTOR
    # MSBuild imports this user project automatically, even when git ignores it.
    # A missing or dangling symlink is not a trustworthy empty input either.
    user_project = project / 'FactsPDF.PackageInspector.csproj.user'
    if user_project.exists() or user_project.is_symlink():
        raise ValueError('Untracked/ignored inspector user project import: ' + str(user_project))
    doc = ET.parse(project / 'FactsPDF.PackageInspector.csproj').getroot()
    if doc.attrib != {'Sdk': 'Microsoft.NET.Sdk'}:
        raise ValueError('Unexpected inspector SDK or project import')
    if doc.findall('.//Import') or doc.findall('.//ProjectReference') or doc.findall('.//EmbeddedResource'):
        raise ValueError('Unexpected inspector imports or explicit resources')
    if any(node.attrib for node in doc if node.tag in ('PropertyGroup', 'ItemGroup')):
        raise ValueError('Conditional inspector build group')
    if any(node.tag not in ('PropertyGroup', 'ItemGroup') for node in doc):
        raise ValueError('Unexpected inspector build target or project node')
    if any(node.tag not in ('OutputType', 'IsPackable', 'DefaultItemExcludes') for group in doc.findall('PropertyGroup') for node in group):
        raise ValueError('Unexpected inspector build property')
    if any(node.tag != 'Compile' for group in doc.findall('ItemGroup') for node in group):
        raise ValueError('Unexpected inspector build item')
    if any(node.attrib for group in doc for node in group if node.tag != 'Compile'):
        raise ValueError('Conditional inspector build item/property')
    explicit = doc.findall('.//Compile')
    if len(explicit) != 1 or explicit[0].attrib != {
            'Include': '../../scripts/inspect_candidate_assembly.cs', 'Link': 'Program.cs'}:
        raise ValueError('Unexpected inspector source-link closure')
    if any(node.tag in ('EnableDefaultCompileItems', 'DefaultItemExcludes',
                        'DefaultItemExcludesInProjectFolder', 'Compile') and node is not explicit[0]
           for node in doc.iter()):
        raise ValueError('Unexpected inspector glob override')
    # External BaseOutputPath/BaseIntermediateOutputPath change SDK implicit
    # exclusions; retain source-tree bin/obj exclusions in the project itself.
    # This is checked as part of the committed input closure, not an ambient flag.
    exclusions = doc.findall('./PropertyGroup/DefaultItemExcludes')
    if len(exclusions) != 1 or exclusions[0].text != INSPECTOR_OUTPUT_EXCLUDES:
        raise ValueError('Inspector source-tree bin/obj exclusions are not pinned')
    selected = {INSPECTOR / 'FactsPDF.PackageInspector.csproj', LINK}
    for directory in (Path('.'), Path('tools'), INSPECTOR):
        selected.update(directory / name for name in CONFIG_NAMES
                        if (root / directory / name).exists() or (root / directory / name).is_symlink())
    # SDK default Compile/**.cs and EmbeddedResource/**.resx, excluding only
    # conventional top-level build outputs. Do not ban unrelated ignored shared/*.
    for candidate in project.rglob('*'):
        relative = candidate.relative_to(project)
        if relative.parts[0] in ('bin', 'obj'):
            continue
        if candidate.is_symlink():
            raise ValueError('Symlink in inspector project inputs')
        if candidate.is_file() and candidate.suffix.lower() in ('.cs', '.resx', '.props', '.targets', '.rsp', '.csproj'):
            selected.add(INSPECTOR / relative)
    result = []
    for rel in sorted(selected, key=lambda p: p.as_posix()):
        name = rel.as_posix()
        if name.startswith('./'):
            name = name[2:]
        path = root / rel
        if name not in tracked or not path.is_file() or path.is_symlink():
            raise ValueError('Untracked/ignored/missing inspector input: ' + name)
        if name.endswith(('.props', '.targets')) and ET.fromstring(path.read_bytes()).findall('.//Import'):
            raise ValueError('Unreviewed inspector configuration import: ' + name)
        raw = path.read_bytes()
        result.append({'path': name, 'bytes': len(raw), 'sha256': _sha(raw)})
    return result


def preflight(root: Path, profile: str, output: Path) -> dict:
    root = root.resolve(strict=True)
    output = output.absolute()
    # No existing output can be overwritten; staging must stay outside checkout.
    resolved = output.resolve()
    if resolved == root or root in resolved.parents or output.exists() or output.is_symlink():
        raise ValueError('Fresh external candidate preflight directory required')
    if not output.parent.is_dir():
        raise ValueError('Output parent must already exist')
    source = current_source(root)
    if not re.fullmatch(r'[0-9a-f]{40}', source):
        raise ValueError('Expected exact Git source SHA')
    selected = identity(root, profile)
    inputs = inspector_inputs(root)
    if current_source(root) != source or inspector_inputs(root) != inputs:
        raise ValueError('Source or inspector inputs changed during preflight')
    receipt = {'schemaVersion': 1, 'phase': 'source-preflight', 'sourceHead': source,
               'identity': selected, 'inspectorInputs': inputs,
               'candidateBuilt': False, 'independentlyValidated': False,
               'published': False, 'distributionAuthorized': False}
    output.mkdir(mode=0o700)
    (output / 'source-preflight.json').write_text(
        json.dumps(receipt, indent=2, sort_keys=True) + '\n', encoding='utf-8')
    return receipt



def _host_toolchain(dotnet: str, root: Path) -> dict:
    """Require the exact SDK and installed managed runtime, not just a requested pin."""
    def invoke(*args: str) -> str:
        result = subprocess.run([dotnet, *args], cwd=root, text=True,
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                timeout=60, check=False)
        if result.returncode:
            raise RuntimeError('dotnet ' + ' '.join(args) + ' failed: ' + result.stdout[-2000:])
        return result.stdout.strip()
    sdk = invoke('--version')
    if sdk != SDK:
        raise ValueError('Observed SDK version differs from fixed candidate SDK: ' + sdk)
    runtimes = invoke('--list-runtimes')
    if not any(re.match(r'^Microsoft\.NETCore\.App\s+' + re.escape(RUNTIME) + r'\s+\[', line)
               for line in runtimes.splitlines()):
        raise ValueError('Exact candidate managed runtime is not installed: ' + RUNTIME)
    info = invoke('--info')
    return {'sdk': sdk, 'managedRuntime': RUNTIME, 'runtimeList': runtimes,
            'infoSha256': _sha(info.encode('utf-8'))}


def _build_command(dotnet: str, root: Path, work: Path, label: str,
                   arguments: list[str]) -> None:
    """Retain a private real command log, fail on every non-zero exit."""
    logdir = work / 'logs'
    logdir.mkdir(exist_ok=True)
    log = logdir / (label + '.log')
    with log.open('w', encoding='utf-8') as stream:
        result = subprocess.run([dotnet, *arguments], cwd=root, stdout=stream,
                                stderr=subprocess.STDOUT, timeout=600, check=False)
    if result.returncode:
        # CI must surface the actual compiler diagnostic: keeping it solely
        # under RUNNER_TEMP makes the failure impossible to triage remotely.
        # The complete log stays private; only a bounded tail reaches CI.
        tail = log.read_text(encoding='utf-8', errors='replace')[-5000:]
        raise RuntimeError('Private candidate build failed: ' + label +
                           ' (exit ' + str(result.returncode) + '); see ' + str(log) +
                           '\nCompiler output (last 5000 characters):\n' + tail)


def _clear_known_build_outputs(root: Path) -> None:
    """Only four specific SDK output directories may be cleared after clean-source preflight."""
    import shutil
    for project in ('src/FactsPDF', 'src/FactsPDF.Cli'):
        for directory in ('bin', 'obj'):
            candidate = root / project / directory
            if candidate.is_symlink():
                raise ValueError('Symlink build output is forbidden: ' + str(candidate))
            if candidate.exists():
                if not candidate.is_dir():
                    raise ValueError('Unexpected build output type: ' + str(candidate))
                shutil.rmtree(candidate)


def _member_record(work: Path, relative: str) -> dict:
    import os
    path = work / relative
    if not path.is_file() or path.is_symlink() or not path.stat().st_size:
        raise ValueError('Missing/invalid private build payload: ' + relative)
    h = hashlib.sha256()
    with path.open('rb') as source:
        while block := source.read(1024 * 1024):
            h.update(block)
    return {'path': relative, 'bytes': path.stat().st_size, 'sha256': h.hexdigest(),
            'executable': os.access(path, os.X_OK)}


def _check_managed_runtimeconfig(path: Path) -> None:
    """Refuse a managed CLI that can override the fixed runtime with another framework."""
    data = json.loads(path.read_text(encoding='utf-8'))
    if not isinstance(data, dict):
        raise ValueError('Generated managed CLI runtimeconfig must be an object')
    options = data.get('runtimeOptions')
    if not isinstance(options, dict) or options.get('tfm') != TARGET:
        raise ValueError('Generated managed CLI runtimeconfig TFM is not strictly pinned')
    if ('frameworks' in options or 'includedFrameworks' in options or
            'frameworks' in data or 'includedFrameworks' in data):
        raise ValueError('Generated managed CLI runtimeconfig contains alternative frameworks')
    framework = options.get('framework')
    if (not isinstance(framework, dict) or
            framework != {'name': 'Microsoft.NETCore.App', 'version': RUNTIME} or
            options.get('rollForward') != 'Disable'):
        raise ValueError('Generated managed CLI runtimeconfig is not strictly pinned')


def private_build(root: Path, profile: str, output: Path, dotnet: str = 'dotnet') -> dict:
    """Perform real private builds; do NOT assemble/distribute an accepted candidate.

    One trusted, clean checkout and fresh external directory. The caller must
    independently verify extracted archives before candidate finalization.
    """
    import shutil
    if profile not in PROFILES:
        raise ValueError('Unknown delivery profile')
    dotnet_path = shutil.which(dotnet)
    if not dotnet_path:
        raise ValueError('dotnet executable unavailable')
    receipt = preflight(root, profile, output)
    root = root.resolve(strict=True)
    work = output.absolute()
    source = receipt['sourceHead']
    try:
        observed = _host_toolchain(dotnet_path, root)
        (work / 'logs').mkdir()
        # Record actual runtime and SDK metadata without mistaking a requested pack
        # version for the version resolved by the .NET compiler.
        _build_command(dotnet_path, root, work, 'dotnet-info', ['--info'])
        _clear_known_build_outputs(root)
        common = ['-p:SourceRevisionId=' + source,
                  '-p:ContinuousIntegrationBuild=true',
                  '-p:RuntimeFrameworkVersion=' + RUNTIME]
        records = []
        if profile == 'local-linux':
            inspector_obj = work / 'inspector' / 'obj'
            inspector_bin = work / 'inspector' / 'bin'
            inspector_obj.mkdir(parents=True)
            inspector_bin.mkdir(parents=True)
            # Never use a pre-existing inspector DLL or the source project's
            # implicit obj: its compilation inputs were frozen by preflight.
            _build_command(dotnet_path, root, work, 'inspector-build', [
                'build', 'tools/FactsPDF.PackageInspector/FactsPDF.PackageInspector.csproj',
                '-c', 'Release',
                '-p:BaseIntermediateOutputPath=' + str(inspector_obj) + '/',
                '-p:MSBuildProjectExtensionsPath=' + str(inspector_obj) + '/',
                '-p:BaseOutputPath=' + str(inspector_bin) + '/',
                *common])
            inspector_dlls = list(inspector_bin.rglob('FactsPDF.PackageInspector.dll'))
            if len(inspector_dlls) != 1:
                raise ValueError('Expected exactly one newly compiled PackageInspector assembly')
            records.append(_member_record(work, inspector_dlls[0].relative_to(work).as_posix()))
            (work / 'packages').mkdir()
            _build_command(dotnet_path, root, work, 'package-pack', [
                'pack', 'src/FactsPDF/FactsPDF.csproj', '-c', 'Release',
                '-o', str(work / 'packages'), *common])
            package = 'packages/FactsPDF.' + VERSION + '.nupkg'
            records.append(_member_record(work, package))
            (work / 'managed').mkdir()
            _build_command(dotnet_path, root, work, 'managed-publish', [
                'publish', 'src/FactsPDF.Cli/FactsPDF.Cli.csproj', '-c', 'Release',
                '--no-self-contained', '-p:UseAppHost=false',
                '-p:RollForward=Disable', '-o', str(work / 'managed'), *common])
            _check_managed_runtimeconfig(work / 'managed/FactsPDF.Cli.runtimeconfig.json')
            for name in ('FactsPDF.Cli.dll', 'FactsPDF.dll',
                         'FactsPDF.Cli.deps.json', 'FactsPDF.Cli.runtimeconfig.json'):
                records.append(_member_record(work, 'managed/' + name))
        (work / 'native').mkdir()
        native_args = ['publish', 'src/FactsPDF.Cli/FactsPDF.Cli.csproj',
                       '-c', 'Release', '-r', PROFILES[profile],
                       '-p:PublishAot=true', '-p:IlcVersion=' + RUNTIME,
                       '-o', str(work / 'native'), *common]
        _build_command(dotnet_path, root, work, 'native-publish', native_args)
        native = 'native/FactsPDF.Cli' + ('.exe' if profile == 'win-x64-validation' else '')
        records.append(_member_record(work, native))
        if current_source(root) != source or inspector_inputs(root) != receipt['inspectorInputs']:
            raise ValueError('Tracked source or inspector closure changed during build')
        # The build receipt binds real bytes only. Runtime/NativeAOT compiler pack
        # provenance, extracted package validation and owner authorization remain open.
        result = {'schemaVersion': 1, 'phase': 'private-build-unverified',
                  'sourceHead': source, 'identity': receipt['identity'],
                  'observedHost': observed, 'files': sorted(records, key=lambda x: x['path']),
                  'nativeCompilerPackVerified': False, 'candidateBuilt': True,
                  'candidateAssembled': False, 'independentlyValidated': False,
                  'published': False, 'distributionAuthorized': False}
        (work / 'private-build.json').write_text(
            json.dumps(result, indent=2, sort_keys=True) + '\n', encoding='utf-8')
        return result
    except Exception:
        # Retain private failure logs/preflight evidence for recovery. A failed
        # build never writes a success receipt or silently reuses old payloads.
        raise


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    check = commands.add_parser('preflight', help='Validate candidate inputs without building software')
    check.add_argument('--profile', required=True, choices=sorted(PROFILES))
    check.add_argument('--output', required=True, type=Path)
    build = commands.add_parser('build', help='Build private unverified candidate inputs; no publication')
    build.add_argument('--profile', required=True, choices=sorted(PROFILES))
    build.add_argument('--output', required=True, type=Path)
    build.add_argument('--dotnet', default='dotnet')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    if args.command == 'build':
        result = private_build(root, args.profile, args.output, args.dotnet)
        print(f"Private build: {result['sourceHead']} / {args.profile}; not verified or published.")
    else:
        receipt = preflight(root, args.profile, args.output)
        print(f"Preflight: {receipt['sourceHead']} / {args.profile}; no binary or candidate built.")


if __name__ == '__main__':
    main()
