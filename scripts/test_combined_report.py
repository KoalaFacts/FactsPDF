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
        # DejaVu Sans strut under the inline chart image: 11pt minus half of (1901-483)/2048em.
        gap = 11 - (1901 - 483) * 11 / 4096
        self.assertEqual(module.row_groups(10, gap), [list(range(1, 11)), []])
        self.assertEqual([len(g) for g in module.row_groups(30, gap)], [10, 20, 0])
        self.assertEqual([len(g) for g in module.row_groups(100, gap)], [10, 21, 21, 21, 21, 6])
        # A block image (no gap) would keep one more row on the first page.
        self.assertEqual([len(g) for g in module.row_groups(30, 0)], [11, 19])
        self.assertEqual(module.row_height(10), 54)
        self.assertEqual(module.row_height(11), 32)
        self.assertEqual(module.reference_status(), 'pending-user-review')


if __name__ == '__main__': unittest.main()
