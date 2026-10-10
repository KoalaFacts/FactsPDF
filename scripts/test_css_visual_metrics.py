"""Offline unit tests for the visual-differential metric and artifact helpers."""
import tempfile
import unittest
from pathlib import Path

from PIL import Image
from css_visual_metrics import compare, save_visuals


class VisualMetricTests(unittest.TestCase):
    def test_identical_pages_have_zero_difference(self):
        a = Image.new("RGB", (8, 8), "white")
        a.putpixel((3, 3), (0, 0, 0))
        result = compare(a, a)
        self.assertEqual(result["pixels_changed_over_16"], 0)
        self.assertEqual(result["change_fraction_over_16"], 0.0)
        self.assertEqual(result["rgb_mean_abs_error"], 0.0)
        self.assertEqual(result["chrome_ink_bbox"], [3, 3, 4, 4])
        self.assertEqual(result["factspdf_ink_bbox"], [3, 3, 4, 4])

    def test_single_changed_pixel_is_reported(self):
        a = Image.new("RGB", (4, 4), "white")
        b = a.copy()
        b.putpixel((1, 2), (0, 0, 0))
        result = compare(a, b)
        self.assertEqual(result["pixels_changed_over_16"], 1)
        self.assertAlmostEqual(result["change_fraction_over_16"], 1 / 16)
        self.assertGreater(result["rgb_mean_abs_error"], 0)

    def test_color_only_difference_is_detected_without_grayscale_masking(self):
        a = Image.new("RGB", (2, 2), (0, 0, 0))
        b = a.copy()
        b.putpixel((1, 1), (0, 0, 80))
        self.assertEqual(compare(a, b)["pixels_changed_over_16"], 1)

    def test_different_dimensions_are_never_compared_by_resizing(self):
        with self.assertRaises(ValueError):
            compare(Image.new("RGB", (2, 3), "white"), Image.new("RGB", (3, 2), "white"))

    def test_comparison_artifacts_are_real_images(self):
        a = Image.new("RGB", (12, 16), "white")
        b = a.copy()
        a.putpixel((1, 1), (0, 0, 0))
        b.putpixel((1, 2), (0, 0, 0))
        with tempfile.TemporaryDirectory() as temp:
            result = save_visuals(a, b, Path(temp), "01")
            self.assertEqual(len(result), 5)
            for path in result.values():
                self.assertTrue(Path(path).is_file(), path)
                with Image.open(path) as pic:
                    self.assertGreater(pic.width, 0)
                    self.assertGreater(pic.height, 0)


if __name__ == "__main__":
    unittest.main()
