"""Fixed examples payload: actual Git bytes, deterministic ZIP, rejected drift."""
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
import zipfile

from private_examples_payload import assemble_examples, FILES, ARCHIVE


def git(root, *args):
    return subprocess.check_output(['git', *args], cwd=root, text=True).strip()


class ExamplesPayloadTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.base = Path(self.tmp.name)
        self.root = self.base / 'repo'
        self.root.mkdir()
        git(self.root, 'init', '-q')
        git(self.root, 'config', 'user.name', 'Fixture')
        git(self.root, 'config', 'user.email', 'fixture@example.invalid')
        for name in FILES:
            p = self.root / name
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_text('tracked fixture ' + name)
        (self.root / 'secret.env').write_text('must not bundle')
        git(self.root, 'add', '.')
        git(self.root, 'commit', '-qm', 'fixture')
        self.head = git(self.root, 'rev-parse', 'HEAD')

    def build(self, output='out', head=None):
        return assemble_examples(self.root, self.base / output, head or self.head)

    def test_deterministic_exact_members_and_manifest(self):
        a = self.build()
        self.build('repeat')
        self.assertEqual((self.base / 'out' / ARCHIVE).read_bytes(),
                         (self.base / 'repeat' / ARCHIVE).read_bytes())
        with zipfile.ZipFile(self.base / 'out' / ARCHIVE) as z:
            self.assertEqual(set(z.namelist()),
                             {'FactsPDF.Examples/' + p for p in FILES.values()} |
                             {'FactsPDF.Examples/EXAMPLES-MANIFEST.json'})
            m = json.loads(z.read('FactsPDF.Examples/EXAMPLES-MANIFEST.json'))
            self.assertEqual(m['sourceHead'], self.head)
            for x in m['trackedMembers']:
                b = z.read('FactsPDF.Examples/' + x['path'])
                self.assertEqual(b, (self.root / x['sourcePath']).read_bytes())
                self.assertEqual(hashlib.sha256(b).hexdigest(), x['sha256'])
            self.assertTrue(all(i.external_attr >> 16 == 0o100644 for i in z.infolist()))
        self.assertFalse(a['candidateAssembled'])
        self.assertFalse(a['published'])

    def test_reject_working_byte_drift(self):
        (self.root / 'LICENSE.md').write_text('changed')
        with self.assertRaisesRegex(ValueError, 'Git blob'):
            self.build()
        self.assertFalse((self.base / 'out').exists())

    def test_reject_missing(self):
        (self.root / 'LICENSE.md').unlink()
        with self.assertRaises(ValueError):
            self.build()

    def test_reject_symlink(self):
        p = self.root / 'LICENSE.md'
        b = p.read_bytes()
        p.unlink()
        target = self.base / 'license'
        target.write_bytes(b)
        p.symlink_to(target)
        with self.assertRaises(ValueError):
            self.build()

    def test_reject_wrong_head(self):
        with self.assertRaises(ValueError):
            self.build(head='0' * 40)

    def test_reject_existing_and_inside_output(self):
        self.build()
        with self.assertRaises(ValueError):
            self.build()
        with self.assertRaises(ValueError):
            assemble_examples(self.root, self.root / 'inside', self.head)

    def test_reject_untracked_required_file(self):
        git(self.root, 'rm', '--cached', 'LICENSE.md')
        git(self.root, 'commit', '-qm', 'remove')
        with self.assertRaises(ValueError):
            self.build(head=git(self.root, 'rev-parse', 'HEAD'))
