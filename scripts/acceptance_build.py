"""Build both acceptance executables and bind their hashes to a clean checkout.

This is a trusted-workspace build receipt, not a signature or hostile-build sandbox.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess

from acceptance_corpus import digest, write_json

MODE = 'linux-x64-native-aot'


def validate_build_provenance(executables: dict[str, Path], source: str) -> dict:
    if not re.fullmatch(r'[a-f0-9]{40}', source or '') or set(executables) != {'cli', 'api'}:
        raise ValueError('Build provenance requires exact source and both entrypoints')
    records = []
    for path in executables.values():
        try:
            records.append(json.loads((path.parent/'build-provenance.json').read_text()))
        except (OSError, ValueError) as ex:
            raise ValueError('Missing or invalid build-provenance.json; rebuild both executables') from ex
    record = records[0]
    if (not isinstance(record, dict) or any(item != record for item in records) or
            record.get('schema_version') != 1 or record.get('source_sha') != source or
            record.get('build_mode') != MODE):
        raise ValueError('Build source/mode differs or executables come from mixed builds')
    expected = {role: {'name': path.name, 'sha256': digest(path.read_bytes())}
                for role, path in executables.items()}
    if record.get('executables') != expected:
        raise ValueError('Executable bytes differ from the recorded source build')
    return record


def checkout() -> str:
    source = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
    dirty = subprocess.check_output(['git', 'status', '--porcelain', '--untracked-files=normal'], text=True)
    if dirty or not re.fullmatch(r'[a-f0-9]{40}', source):
        raise ValueError('Build receipt requires a clean committed checkout, including untracked sources')
    return source


def build(cli_dir: Path, host_dir: Path) -> dict:
    source = checkout()
    if cli_dir.resolve() == host_dir.resolve():
        raise ValueError('CLI and host need separate fresh output directories')
    # Reserve both fresh directories before running any build. Never reuse old outputs.
    for directory in (cli_dir, host_dir):
        if directory.exists():
            raise ValueError('Build output already exists; choose fresh directories')
    for directory in (cli_dir, host_dir): directory.mkdir(parents=True)
    targets = [('src/FactsPDF.Cli/FactsPDF.Cli.csproj', cli_dir),
               ('tools/FactsPDF.Acceptance/FactsPDF.Acceptance.csproj', host_dir)]
    commands = []
    for project, directory in targets:
        command = ['dotnet', 'publish', project, '-c', 'Release', '-r', 'linux-x64',
                   '-p:PublishAot=true', '-o', str(directory.resolve())]
        subprocess.run(command, check=True, timeout=900)
        commands.append(command)
    if checkout() != source:
        raise ValueError('Checkout changed while building; no build receipt issued')
    executables = {'cli': cli_dir/'FactsPDF.Cli', 'api': host_dir/'FactsPDF.Acceptance'}
    info = json.loads(subprocess.check_output([str(executables['api'].resolve()), 'info'], timeout=30))
    if info.get('source_sha') != source or info.get('dynamic_code_supported') is not False:
        raise ValueError('Built host does not identify the expected compiled Native AOT source')
    record = {'schema_version': 1, 'source_sha': source, 'build_mode': MODE,
              'executables': {role: {'name': path.name, 'sha256': digest(path.read_bytes())}
                              for role, path in executables.items()},
              'sdk': subprocess.check_output(['dotnet', '--version'], text=True).strip(),
              'commands': commands, 'scope': 'trusted clean-checkout build receipt, not cryptographic attestation'}
    for directory in (cli_dir, host_dir): write_json(directory/'build-provenance.json', record)
    return record


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cli-output', type=Path, required=True)
    parser.add_argument('--host-output', type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(build(args.cli_output, args.host_output), indent=2))
