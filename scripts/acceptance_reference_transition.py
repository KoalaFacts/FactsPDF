"""Permit opt-in reference-only commits without pretending new code was reviewed.

Review source remains immutable. This is a trusted-checkout policy, not a signature.
"""
from pathlib import Path
import re
import subprocess

POLICY = 'reference-files-only-v1'
_ALLOWED = re.compile(r'docs/acceptance/references/[A-Za-z0-9_-]+/(?:native\.pdf|text\.txt|page-[0-9]+\.png)')


def validate_source_transition(review: dict, candidate_sha: str, repo_root: Path) -> None:
    approved = review.get('source_sha')
    if not all(isinstance(v, str) and re.fullmatch(r'[a-f0-9]{40}', v) for v in (approved, candidate_sha)):
        raise ValueError('Source transition requires exact commit IDs')
    if approved != candidate_sha and review.get('source_transition_policy') != POLICY:
        raise ValueError('Different source commit has no explicit reference-only transition policy')

    def git(*args):
        result = subprocess.run(['git', '-C', str(repo_root.resolve()), *args],
                                capture_output=True, timeout=15)
        if result.returncode:
            raise ValueError('Source transition requires available committed history and a clean checkout')
        return result.stdout

    if git('rev-parse', 'HEAD').decode().strip() != candidate_sha:
        raise ValueError('Candidate source differs from the actual checkout')
    if git('status', '--porcelain', '--untracked-files=normal'):
        raise ValueError('Source transition refuses an uncommitted checkout')
    git('merge-base', '--is-ancestor', approved, candidate_sha)
    if approved == candidate_sha:
        return
    changed = git('diff', '--no-renames', '--name-only', '-z', approved, candidate_sha, '--')
    for raw in changed.split(b'\0'):
        if not raw:
            continue
        path = raw.decode('utf-8', errors='strict')
        if path != 'docs/acceptance/reference-reviews.json' and not _ALLOWED.fullmatch(path):
            raise ValueError('Source changed outside approved reference-only paths: ' + path)
        # Deleted, executable, symlink and submodule artifacts cannot qualify.
        entry = git('ls-tree', '-z', candidate_sha, '--', path).split(b'\0')[0]
        if not entry.startswith(b'100644 blob '):
            raise ValueError('Reference transition requires regular committed files: ' + path)
