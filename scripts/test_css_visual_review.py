"""Independent review regressions: preserve spaces and avoid ink-box dilution."""
import unittest
from pathlib import Path
from PIL import Image, ImageDraw
from css_visual_differential import normalized, BROWSER_NORMALIZATION
from css_visual_metrics import compare

class VisualReviewTests(unittest.TestCase):
    def test_text_guard_preserves_ascii_word_boundaries(self):
        self.assertNotEqual(normalized("Hello, world"), normalized("Hello,world"))
        self.assertNotEqual(normalized("English text"), normalized("Englishtext"))
        self.assertEqual(normalized("Hello,   world\nAgain"), "Hello, world Again")
        self.assertEqual(normalized("中文文字"), "中文文字")

    def test_ink_union_excludes_white_bridge_between_disjoint_words(self):
        left = Image.new("RGB", (20, 20), "white")
        right = left.copy()
        ImageDraw.Draw(left).rectangle((1, 1, 2, 2), fill="black")
        ImageDraw.Draw(right).rectangle((17, 17, 18, 18), fill="black")
        result = compare(left, right)
        self.assertEqual(result["ink_union_pixel_area"], 8)
        self.assertEqual(result["ink_union_change_fraction_over_16"], 1.0)
        self.assertEqual(result["pixels_changed_over_16"], 8)

    def test_real_latin_fixture_contains_accented_glyphs(self):
        fixture = Path("examples/css-visual/latin-baseline.html").read_text(encoding="utf-8")
        self.assertIn("café", fixture)
        self.assertIn("résumé", fixture)

    def test_chrome_reference_disables_ligatures_and_kerning(self):
        self.assertIn('"liga" 0', BROWSER_NORMALIZATION)
        self.assertIn('"clig" 0', BROWSER_NORMALIZATION)
