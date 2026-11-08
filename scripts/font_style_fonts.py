"""Verification-only pinned static font acquisition and explicit role manifest.
Never bundles standalone fonts into packages or public evidence. No engine dependency.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import urllib.error
import urllib.parse
import urllib.request
from fontTools.ttLib import TTFont

VERSION = '1.0.42'
ARCHIVE = f'SarasaGothicSC-TTF-Unhinted-{VERSION}.7z'
ARCHIVE_SHA = '06d6f9a2114d954276ccc23effb29bce7ba64dd210e400612bf0ae0c34bbb69c'
# Verified against upstream v1.0.42 release metadata. Same archive on both endpoints.
ARCHIVE_SIZE = 47647898
ARCHIVE_ASSET_ID = 590859097
STYLES = [
    ('normal', 'Regular', 'DejaVuSans.ttf', '3b45e27a4a85eb8683c05751ebb401286542c54cce0b251de6777e9158c0bc22'),
    ('bold', 'Bold', 'DejaVuSans-Bold.ttf', '22c28024fa931e8ae22479550f6b34b3d79019c947c814e8f5949129bf44924e'),
    ('italic', 'Italic', 'DejaVuSans-Oblique.ttf', 'bb95504cd338e78ac94b6de4e02a8969d0fa1aaf268496d0db20368332178d94'),
    ('boldItalic', 'BoldItalic', 'DejaVuSans-BoldOblique.ttf', '37dbd507a814dfcd07bc7938fd05dc17ef569b94d499b04f588c08870b468a83'),
]

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


class _SafeGitHubRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, message, headers, new_url):
        # GitHub asset API redirects to signed CDN URLs. Never forward its token.
        if urllib.parse.urlsplit(new_url).scheme != 'https':
            raise ValueError('Pinned archive redirect must use HTTPS')
        redirected = super().redirect_request(request, fp, code, message, headers, new_url)
        if redirected is not None and (
                urllib.parse.urlsplit(new_url).hostname != urllib.parse.urlsplit(request.full_url).hostname):
            redirected.remove_header('Authorization')
        return redirected


def acquire_archive(archive):
    """Try the same vendor release asset via two GitHub endpoints; fail closed on changed bytes."""
    if archive.is_file():
        if sha(archive) != ARCHIVE_SHA:
            raise ValueError('Pinned font archive hash mismatch')
        return
    sources = [
        f'https://github.com/be5invis/Sarasa-Gothic/releases/download/v{VERSION}/{ARCHIVE}',
        f'https://api.github.com/repos/be5invis/Sarasa-Gothic/releases/assets/{ARCHIVE_ASSET_ID}',
    ]
    failures = []
    for index, url in enumerate(sources):
        temp = archive.with_name(archive.name + '.partial')
        temp.unlink(missing_ok=True)
        headers = {
            'Accept': 'application/octet-stream',
            'User-Agent': 'FactsPDF-pinned-font-verification',
            'X-GitHub-Api-Version': '2022-11-28',
        }
        if index == 1 and os.environ.get('GITHUB_TOKEN'):
            headers['Authorization'] = 'Bearer ' + os.environ['GITHUB_TOKEN']
        request = urllib.request.Request(url, headers=headers)
        opener = urllib.request.build_opener(_SafeGitHubRedirect())
        try:
            with opener.open(request, timeout=90) as source, temp.open('wb') as target:
                size = 0
                while chunk := source.read(1024 * 1024):
                    size += len(chunk)
                    if size > ARCHIVE_SIZE:
                        raise ValueError('Pinned font archive exceeds expected size')
                    target.write(chunk)
            if size != ARCHIVE_SIZE or sha(temp) != ARCHIVE_SHA:
                raise ValueError('Pinned font archive size or SHA-256 mismatch')
            temp.replace(archive)
            return
        except (urllib.error.URLError, TimeoutError) as error:
            if isinstance(error, urllib.error.HTTPError) and error.code not in (429, 500, 502, 503, 504):
                raise
            failures.append(f'{index + 1}: {type(error).__name__} ({getattr(error, "code", "transport")})')
        finally:
            temp.unlink(missing_ok=True)
    raise RuntimeError('Pinned upstream font archive unavailable via both official GitHub endpoints: '
                       + '; '.join(failures))

def prepare(root, latin_root, download=False):
    root = root.resolve(); root.mkdir(parents=True, exist_ok=True)
    members = [f'SarasaGothicSC-{style}.ttf' for _, style, _, _ in STYLES]
    if download and not all((root / member).is_file() for member in members):
        archive = root / ARCHIVE
        acquire_archive(archive)
        subprocess.run(['7z', 'e', '-y', f'-o{root}', str(archive), *members], check=True, capture_output=True)
    roles = {}; details = []; outlines = {}
    for role, style, latin, expected in STYLES:
        cjk = root / f'SarasaGothicSC-{style}.ttf'
        if sha(cjk) != expected: raise ValueError('Pinned font file hash mismatch: ' + cjk.name)
        roles[role] = [str((latin_root / latin).resolve()), str(cjk)]
        for order, value in enumerate(roles[role]):
            path = Path(value)
            with TTFont(path) as font:
                assert 'glyf' in font and 'fvar' not in font and 'CFF ' not in font
                assert font['OS/2'].fsType == 0
                assert (font['OS/2'].usWeightClass >= 700) == ('bold' in role.lower())
                assert (font['post'].italicAngle != 0) == ('italic' in role.lower())
                if order == 1:
                    cmap = font.getBestCmap(); assert all(ord(c) in cmap for c in '中文字体样式验证')
                    outlines[role] = repr(tuple(map(list, font['glyf'][cmap[ord('中')]].getCoordinates(font['glyf']))))
                details.append({'role': role, 'order': order, 'path': str(path), 'bytes': path.stat().st_size,
                    'sha256': sha(path), 'weight': font['OS/2'].usWeightClass, 'italicAngle': font['post'].italicAngle,
                    'fsType': font['OS/2'].fsType, 'family': font['name'].getDebugName(1), 'style': font['name'].getDebugName(2),
                    'version': font['name'].getDebugName(5), 'postscript': font['name'].getDebugName(6)})
    assert len(set(outlines.values())) == 4, 'Vendor CJK outlines must differ across all four styles'
    assert sum(d['bytes'] for d in details) <= 67_108_864
    manifest = {'roles': roles, 'fonts': details, 'sarasaVersion': VERSION,
        'archiveSha256': ARCHIVE_SHA, 'source': f'https://github.com/be5invis/Sarasa-Gothic/releases/tag/v{VERSION}',
        'license': f'https://github.com/be5invis/Sarasa-Gothic/blob/v{VERSION}/LICENSE',
        'licenseName': 'Sarasa SIL Open Font License 1.1; DejaVu upstream license and distribution copyright',
        'dejavuSource': 'https://dejavu-fonts.github.io/',
        'dejavuLicense': 'https://dejavu-fonts.github.io/License.html',
        'scope': 'Explicit original static fonts, never engine synthesis; ordered Latin/CJK chains.'}
    return manifest

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--sarasa-root', type=Path, required=True)
    parser.add_argument('--latin-root', type=Path, default=Path('/usr/share/fonts/truetype/dejavu'))
    parser.add_argument('--download', action='store_true')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = prepare(args.sarasa_root, args.latin_root, args.download)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False)+'\n')
    print(json.dumps({'fonts': len(result['fonts']), 'bytes': sum(f['bytes'] for f in result['fonts']), 'manifest': str(args.output)}))
