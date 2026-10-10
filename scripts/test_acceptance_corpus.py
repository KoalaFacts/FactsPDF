import copy
import importlib
import json
from pathlib import Path
import tempfile
import unittest


class CorpusTests(unittest.TestCase):
    def setUp(self):
        try:
            self.c = importlib.import_module('acceptance_corpus')
        except ModuleNotFoundError:
            self.fail('Task 1 acceptance_corpus is not implemented')
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.c.generate_corpus(self.root)
        self.path = self.root / 'manifest.json'

    def test_all_resource_boundaries_have_explicit_applicable_entries(self):
        m = self.c.load_manifest(self.path)
        negative = {c['id']: c for c in m['cases'] if c['kind'] == 'negative'}
        expected = {'N-wide', 'N-zero', 'N-pages', 'N-input', 'N-elements', 'N-depth',
                    'N-css-characters', 'N-css-declarations', 'N-display', 'N-output',
                    'N-utf16-high', 'N-utf16-low'}
        self.assertEqual(set(negative), expected)
        for ident, c in negative.items():
            entries = c.get('entrypoints', {})
            self.assertIn('api', entries)
            self.assertEqual(entries['api']['expected_code'], c['expected_code'])
            self.assertEqual(entries['api']['expected_exit'], 3)
            if ident not in {'N-wide', 'N-zero'}:
                self.assertNotIn('cli', entries)

    def test_generation_is_deterministic(self):
        before = {p.relative_to(self.root).as_posix(): p.read_bytes() for p in self.root.rglob('*') if p.is_file()}
        self.c.generate_corpus(self.root)
        after = {p.relative_to(self.root).as_posix(): p.read_bytes() for p in self.root.rglob('*') if p.is_file()}
        self.assertEqual(before, after)
        self.c.load_manifest(self.path)

    def test_record_variants_keep_every_id_once(self):
        m = self.c.load_manifest(self.path)
        for count in (10, 30, 100):
            case = next(x for x in m['cases'] if x['id'] == f'B02-{count}')
            self.assertEqual(case['records'], [f'R{i:04}' for i in range(1, count + 1)])
            text = (self.root / case['input_path']).read_text()
            for record in case['records']:
                self.assertEqual(text.count(record), 1)

    def test_target_errors_are_not_successes(self):
        m = self.c.load_manifest(self.path)
        rows = [{'case_id': x['id'], 'status': 'expected-error'} for x in m['cases'] if x['kind'] == 'target']
        with self.assertRaises(ValueError):
            self.c.summarize_results(m, rows)
        rows = [{'case_id': x['id'], 'status': 'unsupported'} for x in m['cases'] if x['kind'] == 'target' and x['input_path']]
        summary = self.c.summarize_results(m, rows)
        self.assertEqual(summary['target_verified_count'], 0)
        self.assertGreater(summary['target_total'], len(rows))
        self.assertFalse(summary['evidence_complete'])
        self.assertFalse(summary['preview_ready'])

    def test_duplicate_id_and_escaping_paths_fail(self):
        m = json.loads(self.path.read_text())
        for change in ('duplicate', '../escape.html', '/absolute.html', 'C:\\escape.html'):
            bad = copy.deepcopy(m)
            if change == 'duplicate': bad['cases'].append(copy.deepcopy(bad['cases'][0]))
            else: bad['cases'][0]['input_path'] = change
            self.path.write_text(json.dumps(bad))
            with self.assertRaises(ValueError): self.c.load_manifest(self.path)

    def test_target_without_defined_api_is_blocked_design(self):
        m = self.c.load_manifest(self.path)
        target = next(x for x in m['cases'] if x['id'] == 'T-page-furniture')
        self.assertIsNone(target['input_path'])
        self.assertEqual(target['initial_status'], 'blocked-design')
        self.assertTrue(target['requirements'])

    def test_verified_target_requires_its_own_complete_evidence(self):
        m = self.c.load_manifest(self.path)
        with self.assertRaises(ValueError):
            self.c.summarize_results(m, [{'case_id': 'T-list', 'status': 'verified'}])

    def test_changed_input_hash_or_symlink_escape_is_rejected(self):
        m = json.loads(self.path.read_text())
        p = self.root / m['cases'][0]['input_path']
        p.write_text('changed')
        with self.assertRaises(ValueError): self.c.load_manifest(self.path)


if __name__ == '__main__': unittest.main()
