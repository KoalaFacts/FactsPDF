"""F4 authored corpus and acceptance boundaries; PDFs tested by the native inspector."""
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]


class CombinedReportTests(unittest.TestCase):
    def test_executable_corpus_is_reproducible(self):
        source = ROOT / 'scripts/combined_report_corpus.py'
        self.assertTrue(source.exists(), 'F4 executable corpus is missing')
        spec = importlib.util.spec_from_file_location('corpus', source)
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        for count in (10, 30, 100):
            html = (ROOT / f'examples/combined-report-{count}.html').read_text()
            self.assertEqual(html, module.render(count))
            self.assertEqual(html.count('<tr>'), count + 1)
            for fragment in ('<strong>', '<em>', '<ol>', '<ul>', '<img ', '<colgroup>', '<thead>', 'break-after:avoid', f'END-{count:03}'):
                self.assertIn(fragment, html)

    def test_independent_layout_contract(self):
        source = ROOT / 'scripts/verify_combined_report.py'
        self.assertTrue(source.exists(), 'F4 independent PDF inspector is missing')
        spec = importlib.util.spec_from_file_location('inspector', source)
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        self.assertEqual(module.row_groups(10), [list(range(1, 11)), []])
        self.assertEqual([len(g) for g in module.row_groups(30)], [11, 19])
        self.assertEqual([len(g) for g in module.row_groups(100)], [11, 21, 21, 21, 21, 5])
        self.assertEqual(module.row_height(10), 54)
        self.assertEqual(module.row_height(11), 32)
        self.assertEqual(module.reference_status(), 'pending-user-review')


if __name__ == '__main__': unittest.main()
