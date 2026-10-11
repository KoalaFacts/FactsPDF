"""A comparison-only margin observation never permits ink outside paper."""
import unittest
from acceptance_inspect import classify_page_bounds


class PhysicalBoundsTests(unittest.TestCase):
    @staticmethod
    def xml(x0, y0, x1, y1):
        return (f'<html><page width="595.28" height="841.89">'
                f'<word xMin="{x0}" yMin="{y0}" xMax="{x1}" yMax="{y1}">A</word>'
                '</page></html>').encode()

    def test_subpoint_overflow_is_rejected_at_each_physical_edge(self):
        for box in [(-0.4, 40, 20, 52), (40, -0.4, 70, 12),
                    (580, 40, 595.68, 52), (40, 830, 70, 842.29)]:
            with self.subTest(box=box), self.assertRaises(ValueError):
                classify_page_bounds(self.xml(*box), 36, True)

    def test_exact_physical_edges_remain_valid_but_not_margin_approved(self):
        pages, observation = classify_page_bounds(self.xml(0, 0, 595.28, 841.89), 36, True)
        self.assertEqual(len(pages), 1)
        self.assertIsNotNone(observation)

    def test_inward_content_margin_observation_is_still_reported(self):
        pages, observation = classify_page_bounds(self.xml(40, 35.322919, 70, 47), 36, True)
        self.assertEqual(len(pages), 1)
        self.assertIn('35.322919', observation)
        with self.assertRaises(ValueError):
            classify_page_bounds(self.xml(40, 35.322919, 70, 47), 36, False)


if __name__ == '__main__':
    unittest.main()
