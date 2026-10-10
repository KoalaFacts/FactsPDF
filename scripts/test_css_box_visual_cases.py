"""Tests define the browser box-model oracle contract before implementation."""
import tempfile
import unittest
from pathlib import Path
from PIL import Image

from css_box_visual_oracle import CASES, count_color_pixels, validate_unsupported, validate_reference_pages, validate_fragment_edges, color_extent


class BoxVisualContractTests(unittest.TestCase):
    def test_all_five_families_are_present_and_pinned(self):
        self.assertEqual([case.name for case in CASES], [
            "background", "border", "padding", "nested", "fragmentation"
        ])
        self.assertEqual([case.expected_pages for case in CASES], [1, 1, 1, 1, 3])
        self.assertTrue(all(case.html.is_file() for case in CASES))
        self.assertTrue(all(case.expected_colors for case in CASES))

    def test_fixture_grammar_uses_only_planned_box_subset(self):
        for case in CASES:
            content = case.html.read_text(encoding="utf-8")
            self.assertIn("background-color:", content)
            self.assertNotIn("box-sizing:", content)
            self.assertNotIn("@page", content)
            self.assertNotIn("position:", content)
            self.assertNotIn("display:grid", content)
        self.assertIn("width:70%", CASES[3].html.read_text(encoding="utf-8"))
        self.assertIn("break-before:page", CASES[4].html.read_text(encoding="utf-8"))

    def test_image_color_probe_detects_solid_fill_with_tolerance(self):
        image = Image.new("RGB", (15, 10), "#ffffff")
        for y in range(4):
            for x in range(5):
                image.putpixel((x, y), (206, 232, 251))
        self.assertEqual(count_color_pixels(image, "#cee8fb", tolerance=0), 20)
        self.assertEqual(count_color_pixels(image, "#cee8fb", tolerance=8), 20)
        self.assertEqual(count_color_pixels(image, "#aa2233", tolerance=16), 0)

    def test_near_white_rgb_does_not_count_as_solid_background(self):
        white = Image.new("RGB", (10, 10), "white")
        self.assertEqual(count_color_pixels(white, "#f1f4f6"), 0)

    def test_nested_color_extents_can_measure_child_inset(self):
        image = Image.new("RGB", (60, 60), "white")
        for y in range(5, 45):
            for x in range(5, 55):
                image.putpixel((x, y), (219, 238, 250))
        for y in range(10, 35):
            for x in range(13, 40):
                image.putpixel((x, y), (228, 199, 106))
        outer = color_extent(image, "#dbeefa")
        inner = color_extent(image, "#e4c76a")
        self.assertEqual(outer, [5, 5, 55, 45])
        self.assertEqual(inner, [13, 10, 40, 35])
        self.assertLess(outer[0], inner[0])
        self.assertLess(inner[2], outer[2])

    def test_first_middle_last_fragment_top_and_bottom_slicing(self):
        from PIL import ImageDraw
        pages = [Image.new("RGB", (65, 70), "white") for _ in range(3)]
        for img in pages:
            brush = ImageDraw.Draw(img)
            brush.rectangle((4, 0, 6, 69), fill="#1d4568")
            brush.rectangle((58, 0, 60, 69), fill="#1d4568")
        ImageDraw.Draw(pages[0]).rectangle((4, 0, 60, 3), fill="#1d4568")
        ImageDraw.Draw(pages[2]).rectangle((4, 65, 60, 69), fill="#1d4568")
        with tempfile.TemporaryDirectory() as temp:
            rasters = []
            for i, image in enumerate(pages):
                path = Path(temp) / f"page-{i+1}.png"
                image.save(path)
                rasters.append(path)
            verified = validate_fragment_edges(rasters, "#1d4568", min_row_pixels=30)
            self.assertGreater(verified["first_top_rows"], 0)
            self.assertEqual(verified["middle_horizontal_rows"], 0)
            self.assertGreater(verified["last_bottom_rows"], 0)

    def test_reference_validation_checks_every_page_and_expected_color(self):
        case = CASES[0]
        with tempfile.TemporaryDirectory() as tmp:
            first = Path(tmp) / "page-1.png"
            Image.new("RGB", (50, 50), "#cee8fb").save(first)
            self.assertEqual(validate_reference_pages(case, [first])["pages"], 1)
            with self.assertRaises(AssertionError):
                validate_reference_pages(case, [])

    def test_unsupported_css_rejects_output_before_box_feature_is_ready(self):
        with tempfile.TemporaryDirectory() as tmp:
            candidate = Path(tmp) / "unwritten.pdf"
            self.assertEqual(validate_unsupported(b"FPDF1201: width is not supported", candidate),
                             "expected-unsupported")
            candidate.write_bytes(b"not a pdf")
            with self.assertRaises(AssertionError):
                validate_unsupported(b"FPDF1201: width is not supported", candidate)
            candidate.unlink()
            with self.assertRaises(AssertionError):
                validate_unsupported(b"Conversion succeeded", candidate)


if __name__ == "__main__":
    unittest.main()
