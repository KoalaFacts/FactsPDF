"""Offline unit tests for the visual-differential metric and artifact helpers."""
import tempfile
import unittest
from pathlib import Path

from PIL import Image
from css_visual_metrics import compare, save_visuals, align_raster_canvases


class VisualMetricTests(unittest.TestCase):
    def test_identical_pages_have_zero_difference(self):
        a = Image.new("RGB", (8, 8), "white")
        a.putpixel((3, 3), (0, 0, 0))
        result = compare(a, a)
        self.assertEqual(result["pixels_changed_over_16"], 0)
        self.assertEqual(result["change_fraction_over_16"], 0.0)
        self.assertEqual(result["rgb_mean_abs_error"], 0.0)
        self.assertEqual(result["ink_union_change_fraction_over_16"], 0.0)
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
        self.assertEqual(result["ink_union_change_fraction_over_16"], 1.0)

    def test_color_only_difference_is_detected_without_grayscale_masking(self):
        a = Image.new("RGB", (2, 2), (0, 0, 0))
        b = a.copy()
        b.putpixel((1, 1), (0, 0, 80))
        self.assertEqual(compare(a, b)["pixels_changed_over_16"], 1)

    def test_different_dimensions_are_never_compared_by_resizing(self):
        with self.assertRaises(ValueError):
            compare(Image.new("RGB", (2, 3), "white"), Image.new("RGB", (3, 2), "white"))

    def test_one_pixel_pdf_paper_rounding_is_padded_without_resampling(self):
        a = Image.new("RGB", (6, 8), "white")
        b = Image.new("RGB", (7, 8), "white")
        a.putpixel((3, 3), (0, 0, 0))
        b.putpixel((3, 3), (0, 0, 0))
        aa, bb, note = align_raster_canvases(a, b)
        self.assertEqual(aa.size, (7, 8))
        self.assertEqual(bb.size, (7, 8))
        self.assertEqual(aa.getpixel((3, 3)), (0, 0, 0))
        self.assertEqual(aa.getpixel((6, 3)), (255, 255, 255))
        self.assertEqual(note["chrome_original_pixels"], [6, 8])
        self.assertEqual(note["factspdf_original_pixels"], [7, 8])
        self.assertEqual(compare(aa, bb)["pixels_changed_over_16"], 0)

    def test_more_than_one_pixel_difference_is_not_hidden_by_padding(self):
        with self.assertRaises(ValueError):
            align_raster_canvases(Image.new("RGB", (4, 6), "white"),
                                  Image.new("RGB", (6, 6), "white"))

    def test_comparison_artifacts_are_real_images(self):
        a = Image.new("RGB", (12, 16), "white")
        b = a.copy()
        a.putpixel((1, 1), (0, 0, 0))
        b.putpixel((1, 2), (0, 0, 0))
        with tempfile.TemporaryDirectory() as temp:
            result = save_visuals(a, b, Path(temp), "01")
            self.assertEqual(len(result), 6)
            self.assertIn("detail", result)
            for path in result.values():
                self.assertTrue(Path(path).is_file(), path)
                with Image.open(path) as pic:
                    self.assertGreater(pic.width, 0)
                    self.assertGreater(pic.height, 0)


if __name__ == "__main__":
    unittest.main()
