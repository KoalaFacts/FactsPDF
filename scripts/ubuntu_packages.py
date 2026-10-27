"""Scope CI dependency installation to the runner's existing Ubuntu sources.

No source/key files are changed. Both direct apt and Playwright's root child
receive the same command-local configuration; signature checks remain enabled.
"""
import argparse
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
from urllib.parse import urlsplit


def ubuntu_archive(uri):
    url = urlsplit(uri)
    host = url.hostname or ''
    return (url.scheme in {'http', 'https'} and
            (host in {'archive.ubuntu.com', 'security.ubuntu.com'} or host.endswith('.archive.ubuntu.com')) and
            url.path.rstrip('/') == '/ubuntu' and not url.username and not url.password and
            not url.query and not url.fragment)


def valid_source_uri(uri):
    if ubuntu_archive(uri):
        return True
    url = urlsplit(uri)
    if url.scheme != 'mirror+file' or url.netloc or url.query or url.fragment:
        return False
    # Hosted images may use local mirrorlists. Inspect them unchanged; never
    # replace them with a new mirror or allow a remote mirrorlist fetch.
    mirror_file = Path(url.path)
    if not mirror_file.is_absolute():
        return False
    entries = [line.split('#', 1)[0].split() for line in mirror_file.read_text().splitlines()]
    mirrors = [entry[0] for entry in entries if entry]
    return bool(mirrors) and all(ubuntu_archive(mirror) for mirror in mirrors)


def source_config(source):
    source = source.resolve()
    text = '\n'.join(line.split('#', 1)[0] for line in source.read_text().splitlines())
    urls = re.findall(r'\b[a-zA-Z][a-zA-Z0-9+.-]*:[^\s\]]+', text)
    if not urls or not all(valid_source_uri(uri) for uri in urls):
        raise ValueError(f'Expected only existing Ubuntu archive sources in {source}')
    if '"' in str(source) or '\n' in str(source):
        raise ValueError('Unsupported source path')
    return (f'Dir::Etc::sourcelist "{source}";\n'
            'Dir::Etc::sourceparts "-";\n'
            'APT::Update::Error-Mode "any";\n')


def existing_source():
    # Ubuntu 24.04 uses deb822; 22.04 uses the traditional source list.
    for source in (Path('/etc/apt/sources.list.d/ubuntu.sources'), Path('/etc/apt/sources.list')):
        if source.is_file() and source.stat().st_size:
            source_config(source)  # Fail closed, never fall back from an unrecognized file.
            return source
    raise ValueError('No existing Ubuntu source file found')


def install(mode, arguments, source):
    with tempfile.TemporaryDirectory(prefix='factspdf-apt-') as directory:
        config = Path(directory) / 'apt.conf'
        config.write_text(source_config(source))
        prefix = ['sudo', 'env', f'APT_CONFIG={config}']
        if mode == 'packages':
            subprocess.run(prefix + ['apt-get', 'update', '-qq'], check=True)
            subprocess.run(prefix + ['apt-get', 'install', '-y', '--no-install-recommends', *arguments], check=True)
        else:
            cli = Path(arguments[0]).resolve(strict=True)
            node = shutil.which('node')
            if node is None:
                raise ValueError('Node executable not found')
            # Start Playwright as root so it does not invoke a second sudo that
            # would discard APT_CONFIG. Only dependency installation is elevated.
            subprocess.run(prefix + [node, str(cli), 'install-deps', 'chromium'], check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['packages', 'playwright'])
    parser.add_argument('arguments', nargs='+')
    args = parser.parse_args()
    if args.mode == 'playwright' and len(args.arguments) != 1:
        parser.error('playwright requires one installed CLI path')
    install(args.mode, args.arguments, existing_source())


if __name__ == '__main__':
    main()
