import unittest

import ua_defaults_browser_differential as ua


class SnapshotTests(unittest.TestCase):
    def test_pixels_become_points_and_other_values_stay(self):
        self.assertEqual(ua.px_to_pt("16px"), "12pt")
        self.assertEqual(ua.px_to_pt("13.3333px"), "10pt")
        self.assertEqual(ua.px_to_pt("0px"), "0pt")
        self.assertEqual(ua.px_to_pt("normal"), "normal")

    def test_chrome_private_centring_keywords_become_center(self):
        self.assertEqual(ua.normalise("-internal-center"), "center")
        self.assertEqual(ua.normalise("-webkit-center"), "center")
        self.assertEqual(ua.normalise("start"), "start")

    def test_snapshot_diff_reports_changed_missing_and_extra_elements(self):
        committed = {"a/p#0": {name: "0pt" for name in ua.PROPERTIES}, "b/p#0": {name: "0pt" for name in ua.PROPERTIES}}
        live = {"a/p#0": {**committed["a/p#0"], "margin-top": "12pt"}, "c/p#0": committed["a/p#0"]}
        problems = ua.diff_snapshot(committed, live)
        self.assertEqual(len(problems), 3)
        self.assertTrue(any("a/p#0 margin-top" in problem for problem in problems))
        self.assertTrue(any(problem.startswith("b/p#0") for problem in problems))
        self.assertTrue(any(problem.startswith("c/p#0") for problem in problems))
        self.assertEqual(ua.diff_snapshot(committed, committed), [])


class GeometryTests(unittest.TestCase):
    def test_a_difference_within_the_tolerance_is_not_a_difference(self):
        expected = {"top": 100.0, "face:XXX": "B"}
        self.assertEqual(ua.geometry_mismatches(expected, {"top": 100.5, "face:XXX": "B"}), [])
        self.assertEqual(ua.geometry_mismatches(expected, {"top": 101.0, "face:XXX": "R"}),
                         [("face:XXX", "B", "R"), ("top", 100.0, 101.0)])

    def test_a_render_error_is_one_difference(self):
        self.assertEqual(ua.geometry_mismatches({"top": 1.0}, {"render": "error: FPDF1102"}),
                         [("render", "rendered", "error: FPDF1102")])

    def test_an_indent_missing_on_one_side_is_not_compared(self):
        self.assertEqual(ua.geometry_mismatches({"dx:XXX": 3.0}, {}), [])
        self.assertEqual(ua.geometry_mismatches({}, {"left": 20.0}), [])

    def test_unlisted_and_stale_differences_are_both_reported(self):
        known = [{"probe": "p", "metric": "top", "chrome": 556.5, "factspdf": 568.65, "reason": "body margin"},
                 {"probe": "p", "metric": "left", "chrome": 26.25, "factspdf": 20.0, "reason": "body margin"}]
        found = [("p", "top", 556.5, 568.65), ("p", "gap:AAA-XXX", 26.25, 22.4)]
        unexpected, stale = ua.check_geometry(found, known)
        self.assertEqual(unexpected, [("p", "gap:AAA-XXX", 26.25, 22.4)])
        self.assertEqual([entry["metric"] for entry in stale], ["left"])

    def test_a_listed_difference_may_drift_a_little_but_not_a_lot(self):
        known = [{"probe": "p", "metric": "top", "chrome": 556.5, "factspdf": 568.65, "reason": "r"}]
        self.assertEqual(ua.check_geometry([("p", "top", 556.6, 568.7)], known), ([], []))
        unexpected, stale = ua.check_geometry([("p", "top", 556.5, 570.0)], known)
        self.assertEqual(len(unexpected), 1)
        self.assertEqual(len(stale), 1)


class PdfReadingTests(unittest.TestCase):
    def test_matrix_product_applies_the_first_matrix_first(self):
        scale = (2.0, 0.0, 0.0, 2.0, 0.0, 0.0)
        move = (1.0, 0.0, 0.0, 1.0, 5.0, 7.0)
        self.assertEqual(ua.multiply(move, scale), (2.0, 0.0, 0.0, 2.0, 10.0, 14.0))
        self.assertEqual(ua.multiply(scale, move), (2.0, 0.0, 0.0, 2.0, 5.0, 7.0))

    def test_metrics_measure_gaps_and_indents_from_the_first_word(self):
        found = {"AAA": {"page": 0, "y": 100.0, "x": 20.0, "size": 12.0, "face": "R"},
                 "XXX": {"page": 0, "y": 70.0, "x": 44.0, "size": 12.0, "face": "B"},
                 "BBB": {"page": 1, "y": 700.0, "x": None, "size": 12.0, "face": "R"}}
        result = ua.metrics(found, ["AAA", "XXX", "BBB"])
        self.assertEqual(result["top"], 100.0)
        self.assertEqual(result["left"], 20.0)
        self.assertEqual(result["gap:AAA-XXX"], 30.0)
        self.assertEqual(result["gap:XXX-BBB"], "page")
        self.assertEqual(result["dx:XXX"], 24.0)
        self.assertNotIn("dx:BBB", result)
        self.assertEqual(result["face:XXX"], "B")


if __name__ == "__main__":
    unittest.main()
