"""Corresponding-source product payload: committed bytes, safe inventory and fail-closed behavior."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import subprocess
import tarfile
import tempfile
import unittest

from private_source_payload import assemble_source, SOURCE_ROOT, REQUIRED


def git(cwd: Path, *args: str) -> str:
    return subprocess.check_output(['git', *args], cwd=cwd, text=True).strip()


class SourceArchiveTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        base = Path(self.tmp.name)
        self.root = base / 'checkout'
        self.root.mkdir()
        git(self.root, 'init', '-q')
        git(self.root, 'config', 'user.email', 'source-test@example.invalid')
        git(self.root, 'config', 'user.name', 'Source Test')
        self.files = set(REQUIRED) | {
            'src/FactsPDF/Api.cs', 'src/FactsPDF.Cli/Program.cs',
            'examples/quick-start.html', 'examples/no-bundle.ttf',
            'docs/development.md', 'COMMERCIAL.md', 'LICENSING.md',
        }
        for path in self.files:
            target = self.root / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text('fixture: ' + path + '\n')
        for project, body in {
            'src/FactsPDF/FactsPDF.csproj': '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><None Include="../../LICENSE.md"/><None Include="../../README.md"/></ItemGroup></Project>',
            'src/FactsPDF.Cli/FactsPDF.Cli.csproj': '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><ProjectReference Include="../FactsPDF/FactsPDF.csproj"/><Compile Include="../../shared/PageFurnitureJson.cs"/></ItemGroup></Project>',
            'tools/FactsPDF.PackageInspector/FactsPDF.PackageInspector.csproj': '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><Compile Include="../../scripts/inspect_candidate_assembly.cs"/></ItemGroup></Project>',
        }.items():
            (self.root / project).write_text(body)
        git(self.root, 'add', '-A')
        git(self.root, 'commit', '-qm', 'fixture')
        self.head = git(self.root, 'rev-parse', 'HEAD')
        self.out = base / 'product'

    def test_real_archive_contains_git_objects_but_not_font(self):
        report = assemble_source(self.root, self.out, self.head)
        archive = self.out / report['archive']['filename']
        self.assertEqual(hashlib.sha256(archive.read_bytes()).hexdigest(), report['archive']['sha256'])
        with tarfile.open(archive, 'r:gz') as stream:
            files = {entry.name: stream.extractfile(entry).read()
                     for entry in stream if entry.isfile()}
        self.assertNotIn(SOURCE_ROOT + '/examples/no-bundle.ttf', files)
        self.assertEqual(files[SOURCE_ROOT + '/LICENSE.md'], (self.root / 'LICENSE.md').read_bytes())
        manifest = json.loads(files[SOURCE_ROOT + '/SOURCE-MANIFEST.json'])
        self.assertEqual(manifest['sourceHead'], self.head)
        self.assertIn('src/FactsPDF/Api.cs', [r['path'] for r in manifest['trackedMembers']])
        self.assertNotIn('SOURCE-BUILD.md', [r['path'] for r in manifest['trackedMembers']])
        self.assertFalse(report['independentlyValidated'])
        self.assertFalse(report['candidateAssembled'])
        self.assertFalse(report['published'])

    def test_reproduces_identical_archives_in_distinct_destinations(self):
        first = assemble_source(self.root, self.out, self.head)
        second = assemble_source(self.root, self.out.parent / 'second', self.head)
        self.assertEqual(first['archive']['sha256'], second['archive']['sha256'])

    def test_tracked_file_mutation_is_rejected_before_output(self):
        (self.root / 'LICENSE.md').write_text('tampered\n')
        with self.assertRaisesRegex(ValueError, 'differs from committed Git blob'):
            assemble_source(self.root, self.out, self.head)
        self.assertFalse(self.out.exists())

    def test_missing_linked_decoder_is_rejected(self):
        (self.root / 'shared/PageFurnitureJson.cs').unlink()
        with self.assertRaisesRegex(ValueError, 'Missing or symlinked'):
            assemble_source(self.root, self.out, self.head)
        self.assertFalse(self.out.exists())

    def test_missing_explicit_project_link_is_rejected(self):
        project = self.root / 'src/FactsPDF.Cli/FactsPDF.Cli.csproj'
        project.write_text('<Project Sdk="Microsoft.NET.Sdk"><ItemGroup>'
                           '<Compile Include="../../missing/Hidden.cs"/></ItemGroup></Project>')
        git(self.root, 'add', '-A')
        git(self.root, 'commit', '-qm', 'unreviewed source link')
        newer = git(self.root, 'rev-parse', 'HEAD')
        with self.assertRaisesRegex(ValueError, 'Missing linked source'):
            assemble_source(self.root, self.out, newer)

    def test_refuses_existing_and_source_tree_outputs(self):
        self.out.mkdir()
        with self.assertRaisesRegex(ValueError, 'Fresh external'):
            assemble_source(self.root, self.out, self.head)
        with self.assertRaisesRegex(ValueError, 'Fresh external'):
            assemble_source(self.root, self.root / 'artifacts', self.head)

    def test_symlinked_source_member_is_rejected(self):
        path = self.root / 'src/FactsPDF/Api.cs'
        path.unlink()
        path.symlink_to(self.root / 'LICENSE.md')
        with self.assertRaisesRegex(ValueError, 'symlinked'):
            assemble_source(self.root, self.out, self.head)


if __name__ == '__main__':
    unittest.main()
