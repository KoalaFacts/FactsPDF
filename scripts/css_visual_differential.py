#!/usr/bin/env python3
"""Native FactsPDF vs real Chrome print-to-PDF visual differential.

This workflow is intentionally diagnostic for pixel similarity: it rigorously
checks source text, page counts, page bounds and image completeness, but does
not falsely label an arbitrary pixel-change percentage as browser CSS parity.
Chrome receives documented print/font normalization only; this is necessary
because FactsPDF does not yet support @page/font-family/font-weight CSS.
Only synthetic public previews + JSON are uploaded, never standalone fonts.
"""
from html.parser import HTMLParser
from pathlib import Path
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

from PIL import Image
from css_visual_metrics import compare, save_visuals, align_raster_canvases

DPI = 120
WIDTH_PT, HEIGHT_PT, MARGIN_PT = 595.28, 841.89, 36.0
LATIN = Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf")
CJK = Path("/usr/share/fonts/truetype/droid/DroidSansFallbackFull.ttf")
FIXTURES = [
    ("latin", Path("examples/css-visual/latin-baseline.html"), 1),
    ("bilingual", Path("examples/css-visual/bilingual-baseline.html"), 1),
    ("pagination", Path("examples/css-visual/pagination-baseline.html"), 2),
]
# The following CSS is browser-only and never provided to FactsPDF. All
# supported CSS declarations in the fixture itself are unchanged.
BROWSER_NORMALIZATION = """
<style id="factspdf-browser-oracle-only">
  @page { size: 595.28pt 841.89pt; margin: 36pt; }
  html, body { margin: 0 !important; padding: 0 !important; }
  body {
    font-family: "DejaVu Sans", "Droid Sans Fallback", sans-serif !important;
    font-weight: 400 !important;
  }
  h1, h2, h3, h4, h5, h6 { font-weight: 400 !important; }
  * { font-kerning: none !important; font-feature-settings: "kern" 0 !important; }
</style>
"""

def run(*args, timeout=100):
    process = subprocess.run([str(arg) for arg in args], capture_output=True,
                             timeout=timeout)
    if process.returncode:
        error = process.stderr.decode("utf-8", errors="replace")
        stdout = process.stdout.decode("utf-8", errors="replace")
        raise RuntimeError(f"{args[0]} exited {process.returncode}: {error[-2200:]} {stdout[-800:]}")
    return process.stdout


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


class VisibleText(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.parts = []
        self.hide_depth = 0

    def handle_starttag(self, tag, attrs):
        if tag in ("head", "style", "script"):
            self.hide_depth += 1

    def handle_endtag(self, tag):
        if tag in ("head", "style", "script"):
            self.hide_depth -= 1

    def handle_data(self, data):
        if not self.hide_depth:
            self.parts.append(data)


def normalized(value):
    return "".join(value.split())


def visible_html(path):
    parser = VisibleText()
    parser.feed(path.read_text(encoding="utf-8"))
    return normalized("".join(parser.parts))


def pdfinfo(path):
    result = run("pdfinfo", path).decode("utf-8", errors="replace")
    page_match = re.search(r"^Pages:\s+(\d+)\s*$", result, re.M)
    dim_match = re.search(r"^Page size:\s+([\d.]+)\s+x\s+([\d.]+)\s+pts", result, re.M)
    if not page_match or not dim_match:
        raise AssertionError("Could not determine PDF pages and point geometry")
    pages = int(page_match.group(1))
    width, height = map(float, dim_match.groups())
    if abs(width - WIDTH_PT) > 0.5 or abs(height - HEIGHT_PT) > 0.5:
        raise AssertionError(f"Unexpected paper size: {width} x {height} pt")
    return pages, width, height


def assert_text_inside_page(pdf):
    root = ET.fromstring(run("pdftotext", "-bbox", pdf, "-"))
    pages, words = 0, 0
    for page in root.iter("{http://www.w3.org/1999/xhtml}page"):
        pages += 1
        width, height = float(page.attrib["width"]), float(page.attrib["height"])
        for word in page.iter("{http://www.w3.org/1999/xhtml}word"):
            words += 1
            x0, y0, x1, y1 = (float(word.attrib[k])
                               for k in ("xMin", "yMin", "xMax", "yMax"))
            if not (0 <= x0 <= x1 <= width + 0.01 and
                    0 <= y0 <= y1 <= height + 0.01):
                raise AssertionError(f"Text extends outside {Path(pdf).name} page {pages}")
    if words == 0:
        raise AssertionError(f"No extractable word geometry in {pdf}")
    return words


def images_for_pdf(pdf, prefix):
    run("pdftoppm", "-r", str(DPI), "-png", pdf, prefix)
    def page_number(path):
        return int(path.stem.rsplit("-", 1)[1])
    paths = sorted(Path(prefix).parent.glob(Path(prefix).name + "-*.png"),
                   key=page_number)
    if not paths:
        raise AssertionError(f"No page raster was created for {pdf}")
    return paths


def print_chrome(browser, source_html, pdf, profile):
    html = source_html.read_text(encoding="utf-8")
    if "</head>" not in html.lower():
        raise AssertionError("Fixture must contain explicit </head> for reference normalization")
    lowered = html.lower()
    boundary = lowered.index("</head>")
    wrapped = html[:boundary] + BROWSER_NORMALIZATION + html[boundary:]
    wrapper = pdf.with_suffix(".chrome-input.html")
    wrapper.write_text(wrapped, encoding="utf-8")
    run(browser, "--headless=new", "--no-sandbox", "--disable-gpu",
        "--disable-dev-shm-usage", "--disable-background-networking",
        "--disable-extensions", "--no-first-run", "--no-default-browser-check",
        "--no-pdf-header-footer", "--user-data-dir=" + str(profile),
        "--print-to-pdf=" + str(pdf), wrapper.as_uri(), timeout=100)
    if not pdf.is_file() or pdf.stat().st_size < 500:
        raise AssertionError("Chrome did not create a nonempty PDF")
    return sha256(wrapper)


def source_sha():
    event_path = os.environ.get("GITHUB_EVENT_PATH")
    if event_path:
        try:
            event = json.loads(Path(event_path).read_text(encoding="utf-8"))
            candidate = event.get("pull_request", {}).get("head", {}).get("sha")
            if candidate:
                return candidate
        except (OSError, ValueError, AttributeError):
            pass
    return run("git", "rev-parse", "HEAD").decode().strip()


def markdown_summary(report):
    lines = [
        "# FactsPDF / Chrome PDF visual differential",
        "",
        f"Source feature SHA: \`{report['source_sha']}\`  |  "
        f"Chrome: {report['browser']}  |  DPI: {report['dpi']}",
        "",
        "| Fixture | Pages | Whole-page changed pixels | Ink-region changed pixels (per-page average) | RGB MAE / 255 |",
        "| --- | ---: | ---: | ---: | ---: |",
    ]
    for case in report["cases"]:
        agg = case.get("aggregate")
        if agg is None:
            lines.append(f"| {case['id']} | — | — | — | ERROR |")
            continue
        ink = [p["metrics"]["ink_union_change_fraction_over_16"]
               for p in case["pages"]]
        ink_mean = sum(ink) / len(ink)
        lines.append(
            f"| {case['id']} | {len(case['pages'])} | "
            f"{agg['changed_pixel_fraction_over_16'] * 100:.3f}% | "
            f"{ink_mean * 100:.3f}% | {agg['rgb_mean_abs_error']:.3f} |")
    lines.extend([
        "",
        "**This is NOT a browser CSS conformance percentage.** "
        "Visual pixels are scored on unchanged page coordinates; Chrome uses "
        "documented browser-only @page/font/heading resets.",
        "Paper quantization differences of at most one raster pixel are "
        "right/bottom white-padded without image rescaling or registration.",
        "Full-page metrics contain whitespace; ink-region metrics cover the "
        "union of visibly occupied page areas and should be reviewed alongside "
        "the detail heatmap images.",
        "",
        f"Hard-check errors: {len(report['errors'])}. "
        "Chrome/FactsPDF extracted Unicode text, page counts, page "
        "bounds and qpdf syntax are individually tested.",
        "",
    ])
    return "\n".join(lines)


def main():
    if len(sys.argv) != 3:
        raise SystemExit("Usage: css_visual_differential.py <native-FactsPDF-Cli> <output-dir>")
    native_cli = Path(sys.argv[1]).resolve()
    output = Path(sys.argv[2]).resolve()
    output.mkdir(parents=True, exist_ok=True)
    browser = next((shutil.which(name) for name in
                    ("google-chrome", "chromium", "chromium-browser")
                    if shutil.which(name)), None)
    if browser is None:
        raise RuntimeError("Chrome/Chromium must be available for a real visual oracle")
    if not all(path.is_file() for path in (native_cli, LATIN, CJK)):
        raise RuntimeError("Missing native executable or complete OS-installed fixture fonts")

    report = {
        "tool": "FactsPDF Chrome PDF visual differential",
        "source_sha": source_sha(),
        "checkout_sha": run("git", "rev-parse", "HEAD").decode().strip(),
        "browser": run(browser, "--version").decode().strip(),
        "dpi": DPI,
        "reference_pdf_page_points": [WIDTH_PT, HEIGHT_PT],
        "page_margin_points": MARGIN_PT,
        "reference_only_css_sha256": hashlib.sha256(
            BROWSER_NORMALIZATION.encode("utf-8")).hexdigest(),
        "reference_only_css": BROWSER_NORMALIZATION.strip(),
        "font_input_sha256": {
            LATIN.name: sha256(LATIN), CJK.name: sha256(CJK)
        },
        "normalization_note": (
            "Chrome receives only @page, default body margin/padding reset, "
            "DejaVu/Droid font-family, normal heading weight and disabled kerning. "
            "FactsPDF receives the identical original fixture HTML; these browser-only "
            "adjustments cannot be parsed/rendered by FactsPDF yet."
        ),
        "visual_metric_policy": (
            "Purely diagnostic: unregistered exact-coordinate RGB MAE, pixels with "
            "maximum per-channel delta >16, both whole-page and text-ink-union "
            "fractions, image overlays, enlarged ink detail views, ink extents. "
            "No arbitrary similarity threshold used to claim browser parity."
        ),
        "checks": {
            "browser_and_native_pdfs_structurally_valid": False,
            "source_text_preserved_in_both": False,
            "same_page_count_as_fixture": False,
            "text_geometry_inside_pages": False,
            "all_page_rasters_produced": False,
        },
        "cases": [],
        "errors": [],
    }

    try:
        with tempfile.TemporaryDirectory(prefix="factspdf-visual-") as tmp:
            work = Path(tmp)
            for name, fixture, expected_pages in FIXTURES:
                case = {"id": name, "fixture": str(fixture),
                        "input_sha256": sha256(fixture),
                        "expected_pages": expected_pages,
                        "pages": []}
                report["cases"].append(case)
                try:
                    casework = work / name
                    casework.mkdir(parents=True)
                    original = visible_html(fixture)
                    chrome_pdf = casework / "chrome.pdf"
                    factspdf_pdf = casework / "factspdf.pdf"
                    wrapper_sha = print_chrome(browser, fixture, chrome_pdf,
                                               casework / "profile")
                    case["chrome_wrapper_sha256"] = wrapper_sha
                    run(native_cli, fixture, factspdf_pdf, "--font", LATIN, "--font",
                        CJK, "--subset-fonts")
                    for pdf in (chrome_pdf, factspdf_pdf):
                        run("qpdf", "--check", pdf)
                        assert_text_inside_page(pdf)
                    cpages, cw, ch = pdfinfo(chrome_pdf)
                    fpages, fw, fh = pdfinfo(factspdf_pdf)
                    case["chrome_pages"] = cpages
                    case["factspdf_pages"] = fpages
                    case["page_geometry_points"] = {
                        "chrome": [cw, ch], "factspdf": [fw, fh]
                    }
                    if cpages != fpages or cpages != expected_pages:
                        raise AssertionError(
                            f"Page count mismatch for {name}: Chrome={cpages}, "
                            f"FactsPDF={fpages}, expected={expected_pages}")
                    ct = normalized(run("pdftotext", "-raw", chrome_pdf, "-")
                                    .decode("utf-8"))
                    ft = normalized(run("pdftotext", "-raw", factspdf_pdf, "-")
                                    .decode("utf-8"))
                    case["text_equal"] = ct == ft == original
                    if not case["text_equal"]:
                        raise AssertionError(
                            f"Extracted text mismatch in {name}: "
                            f"source={original!r}; Chrome={ct!r}; FactsPDF={ft!r}")
                    chrome_images = images_for_pdf(chrome_pdf, casework / "chrome")
                    factspdf_images = images_for_pdf(factspdf_pdf, casework / "factspdf")
                    if len(chrome_images) != cpages or len(factspdf_images) != fpages:
                        raise AssertionError("Raster page count does not match PDF page count")
                    total_changed = 0
                    total_pixels = 0
                    total_absolute_error = 0.0
                    case_out = output / name
                    for index, (cpath, fpath) in enumerate(
                        zip(chrome_images, factspdf_images), start=1
                    ):
                        with Image.open(cpath) as cimage, Image.open(fpath) as fimage:
                            left, right, geometry_note = align_raster_canvases(
                                cimage, fimage)
                            metrics = compare(left, right)
                            if metrics["chrome_ink_bbox"] is None or metrics["factspdf_ink_bbox"] is None:
                                raise AssertionError("Blank page in a nonempty visual fixture")
                            files = save_visuals(left, right, case_out, f"page-{index:02}")
                        total_changed += metrics["pixels_changed_over_16"]
                        total_pixels += metrics["pixel_count"]
                        total_absolute_error += (metrics["rgb_mean_abs_error"] *
                                                 metrics["pixel_count"])
                        case["pages"].append({
                            "number": index, "metrics": metrics,
                            "raster_alignment": geometry_note,
                            "images": {key: str(Path(filename).relative_to(output))
                                       for key, filename in files.items()}
                        })
                    case["aggregate"] = {
                        "changed_pixel_fraction_over_16": round(
                            total_changed / total_pixels, 7),
                        "mean_page_ink_union_change_fraction_over_16": round(
                            sum(p["metrics"]["ink_union_change_fraction_over_16"]
                                for p in case["pages"]) / len(case["pages"]), 7),
                        "rgb_mean_abs_error": round(
                            total_absolute_error / total_pixels, 5),
                        "total_compared_pixels": total_pixels
                    }
                    print(f"CASE {name}: {cpages} page(s), "
                          f"change fraction={case['aggregate']['changed_pixel_fraction_over_16']:.6f}, "
                          f"mean RGB abs error={case['aggregate']['rgb_mean_abs_error']:.4f}",
                          flush=True)
                except Exception as exc:
                    case["error"] = str(exc)
                    report["errors"].append({"case": name, "message": str(exc)})
    finally:
        success = len(report["errors"]) == 0 and all(
            len(case.get("pages", [])) == case["expected_pages"]
            for case in report["cases"]) and len(report["cases"]) == len(FIXTURES)
        for key in report["checks"]:
            report["checks"][key] = success
        (output / "report.json").write_text(
            json.dumps(report, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8")
        (output / "summary.md").write_text(
            markdown_summary(report), encoding="utf-8")
    if report["errors"]:
        raise AssertionError(
            f"Visual-differential hard checks failed for {len(report['errors'])} cases. "
            "See report.json for exact errors."
        )
    print("Visual differential completed for all public fixtures.", flush=True)


if __name__ == "__main__":
    main()
