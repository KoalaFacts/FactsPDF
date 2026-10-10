#!/usr/bin/env python3
"""Pinned box-model Chrome visual oracles for the next FactsPDF layout milestone.

Reference mode, used until FactsPDF implements the selected box-model CSS,
generates real Chrome PDFs and verifies StrictPdf rejects the unsupported CSS.
Comparison mode is a separate, explicit opt-in for the future renderer branch.
Neither mode calls a network URL or imports an external browser into FactsPDF.
"""
import argparse
from dataclasses import dataclass
from pathlib import Path
import hashlib
import json
import shutil
import subprocess
import tempfile

from PIL import Image
from css_visual_metrics import align_raster_canvases, compare, save_visuals
from css_visual_differential import (
    LATIN, CJK, DPI, WIDTH_PT, HEIGHT_PT, MARGIN_PT, BROWSER_NORMALIZATION,
    run, sha256, pdfinfo, assert_text_inside_page, visible_html, normalized,
    images_for_pdf, print_chrome, source_sha
)


@dataclass(frozen=True)
class BoxCase:
    name: str
    html: Path
    expected_pages: int
    expected_colors: tuple[tuple[str, int], ...]
    page_markers: tuple[str, ...]
    intent: str


CASES = (
    BoxCase(
        "background", Path("examples/css-visual/box-background.html"), 1,
        (("#cee8fb", 1000),), ("Background marker:",),
        "Flat, explicit background-color across a width/padding box"),
    BoxCase(
        "border", Path("examples/css-visual/box-border.html"), 1,
        (("#f1f4f6", 1000), ("#aa2233", 120),
         ("#1d4568", 120), ("#008000", 120), ("#a0601c", 120)),
        ("Four sides",),
        "Different top/right/bottom/left solid border widths and colors"),
    BoxCase(
        "padding", Path("examples/css-visual/box-padding.html"), 1,
        (("#e4e5e7", 700), ("#abd9e7", 700)), ("Padding top twelve",),
        "Four-value asymmetric outer padding with independent nested inset"),
    BoxCase(
        "nested", Path("examples/css-visual/box-nested.html"), 1,
        (("#dbeefa", 1000), ("#e4c76a", 1000), ("#ffb27d", 500)),
        ("Outer container",),
        "True nested sections, content widths, 70% child width and colored layers"),
    BoxCase(
        "fragmentation", Path("examples/css-visual/box-fragmentation.html"), 3,
        (("#e7f2fa", 1000), ("#1d4568", 120)),
        ("Fragment one:", "Fragment two:", "Fragment three:"),
        "Single nested box across 3 pages, continuous side edges and sliced decoration"),
)

# This style applies ONLY to Chrome's print reference. FactsPDF receives
# the unchanged fixture HTML. Chrome can otherwise suppress painted backgrounds
# under print defaults; CSS box references should have measurable solid fills.
CHROME_PRINT_COLOR = """
<style id="chrome-only-box-colors">
  html, body, body * {
    -webkit-print-color-adjust: exact !important;
    print-color-adjust: exact !important;
  }
</style>
"""

def count_color_pixels(image: Image.Image, hex_color: str, tolerance: int = 6) -> int:
    """Count near-exact filled pixels, a reproducible non-OCR visual oracle."""
    if not (len(hex_color) == 7 and hex_color.startswith("#")):
        raise ValueError("Color oracle must use #rrggbb")
    if not 0 <= tolerance <= 255:
        raise ValueError("Invalid RGB tolerance")
    rgb = tuple(int(hex_color[n:n + 2], 16) for n in (1, 3, 5))
    return sum(1 for pixel in image.convert("RGB").getdata()
               if all(abs(pixel[index] - rgb[index]) <= tolerance for index in range(3)))



def color_extent(image: Image.Image, hex_color: str, tolerance: int = 6):
    """Report a solid paint's original-coordinate bounding rectangle."""
    color = tuple(int(hex_color[n:n + 2], 16) for n in (1, 3, 5))
    pixels = image.convert("RGB")
    x0, y0, x1, y1 = pixels.width, pixels.height, -1, -1
    values = pixels.load()
    for y in range(pixels.height):
        for x in range(pixels.width):
            pixel = values[x, y]
            if all(abs(pixel[index] - color[index]) <= tolerance for index in range(3)):
                x0, y0 = min(x0, x), min(y0, y)
                x1, y1 = max(x1, x), max(y1, y)
    return None if x1 < x0 else [x0, y0, x1 + 1, y1 + 1]


def validate_fragment_edges(raster_paths: list[Path], hex_color: str,
                            min_row_pixels: int = 300):
    """Assert CSS box-decoration-break:slice on three real print fragments.

    The first fragment has a full-width top border, the middle has only
    continuous side borders, and the last has the full-width bottom edge.
    Thin vertical side borders do not count as wide horizontal border rows.
    """
    if len(raster_paths) != 3:
        raise AssertionError("Expected exactly three fragment pages")
    color = tuple(int(hex_color[n:n + 2], 16) for n in (1, 3, 5))
    hits = []
    for path in raster_paths:
        with Image.open(path) as img:
            rgb = img.convert("RGB")
            px = rgb.load()
            rows = 0
            for y in range(rgb.height):
                observed = 0
                for x in range(rgb.width):
                    pixel = px[x, y]
                    if all(abs(pixel[k] - color[k]) <= 6 for k in range(3)):
                        observed += 1
                if observed >= min_row_pixels:
                    rows += 1
            hits.append(rows)
    if not (hits[0] > 0 and hits[1] == 0 and hits[2] > 0):
        raise AssertionError(
            f"Expected sliced first/middle/last border rows (>0, 0, >0), got {hits}")
    return {
        "first_top_rows": hits[0],
        "middle_horizontal_rows": hits[1],
        "last_bottom_rows": hits[2],
        "classification": "box-decoration-break slice (first top, middle sides, last bottom)",
        "wide_row_minimum_pixels": min_row_pixels,
    }

def validate_reference_pages(case: BoxCase, raster_paths: list[Path]):
    """Assert real colored browser fragments, not blank or color-suppressed PDFs."""
    if len(raster_paths) != case.expected_pages:
        raise AssertionError(
            f"{case.name}: Chrome produced {len(raster_paths)} pages; expected {case.expected_pages}")
    pages = []
    for page_no, raster in enumerate(raster_paths, 1):
        with Image.open(raster) as img:
            samples = {}
            bounds = {}
            for hex_code, minimum in case.expected_colors:
                observed = count_color_pixels(img, hex_code)
                if observed < minimum:
                    raise AssertionError(
                        f"{case.name} page {page_no}: color {hex_code} has {observed} pixels; "
                        f"requires {minimum}. Browser print background may be suppressed.")
                samples[hex_code] = observed
                bounds[hex_code] = color_extent(img, hex_code)
            if case.name in ("padding", "nested"):
                keys = ("#e4e5e7", "#abd9e7") if case.name == "padding" else ("#dbeefa", "#e4c76a", "#ffb27d")
                previous = None
                for key in keys:
                    bbox = bounds[key]
                    if bbox is None:
                        raise AssertionError(f"{case.name}: {key} missing bounding box")
                    if previous and not (
                        bbox[0] > previous[0] and bbox[1] > previous[1] and
                        bbox[2] < previous[2] and bbox[3] < previous[3]):
                        raise AssertionError(
                            f"{case.name}: nested painted bbox {bbox} must inset into {previous}")
                    previous = bbox
            pages.append({"number": page_no, "width_px": img.width,
                          "height_px": img.height, "color_pixels": samples,
                          "color_bounds": bounds})
    return {"pages": len(pages), "page_probes": pages}


def validate_unsupported(stderr: bytes, pdf_path: Path) -> str:
    """Explicitly gate current unsupported engine output; no pretending a diff passed."""
    if pdf_path.exists():
        raise AssertionError("StrictPdf unexpectedly wrote a box-model PDF.")
    decoded = stderr.decode("utf-8", errors="replace")
    if "FPDF1201" not in decoded:
        raise AssertionError(
            "Expected strict FPDF1201 for an unsupported box property; got " + repr(decoded[-700:]))
    return "expected-unsupported"


def chrome_reference_html(original: Path, target: Path) -> str:
    text = original.read_text(encoding="utf-8")
    location = text.lower().find("</head>")
    if location < 0:
        raise AssertionError("Every reference fixture needs a closing head tag")
    target.write_text(text[:location] + CHROME_PRINT_COLOR + text[location:],
                      encoding="utf-8")
    return sha256(target)


def page_marker_text(pdf: Path, page_number: int):
    return normalized(run("pdftotext", "-f", str(page_number), "-l",
                          str(page_number), "-raw", pdf, "-").decode("utf-8"))


def run_case(case: BoxCase, *, browser: str, cli: Path, output: Path,
             scratch: Path, mode: str):
    case_work = scratch / case.name
    case_work.mkdir(parents=True, exist_ok=True)
    result = {
        "name": case.name, "intent": case.intent,
        "fixture": str(case.html),
        "fixture_sha256": sha256(case.html),
        "expected_pages": case.expected_pages,
        "status": None,
        "page_reference_probes": [],
        "page_differences": []
    }

    original_text = visible_html(case.html)
    browser_fixture = case_work / "chrome-source.html"
    result["chrome_only_color_source_sha256"] = chrome_reference_html(
        case.html, browser_fixture)
    chrome_pdf = case_work / "chrome.pdf"
    result["browser_print_input_sha256"] = print_chrome(
        browser, browser_fixture, chrome_pdf, case_work / "chrome-profile")
    run("qpdf", "--check", chrome_pdf)
    chrome_pages, cw, ch = pdfinfo(chrome_pdf)
    if chrome_pages != case.expected_pages:
        raise AssertionError(f"{case.name}: expected {case.expected_pages} browser pages but got {chrome_pages}")
    assert_text_inside_page(chrome_pdf)
    browser_text = normalized(run("pdftotext", "-raw", chrome_pdf, "-").decode("utf-8"))
    if browser_text != original_text:
        raise AssertionError(
            f"{case.name}: Chrome text does not match HTML: {browser_text!r} vs {original_text!r}")
    for page, marker in enumerate(case.page_markers, 1):
        if normalized(marker) not in page_marker_text(chrome_pdf, page):
            raise AssertionError(f"{case.name}: marker {marker!r} missing on printed page {page}")
    browser_rasters = images_for_pdf(chrome_pdf, case_work / "chrome-raster")
    probe = validate_reference_pages(case, browser_rasters)
    result["page_reference_probes"] = probe["page_probes"]
    if case.name == "fragmentation":
        result["sliced_edges"] = validate_fragment_edges(
            browser_rasters, "#1d4568")
    result["chrome_paper_points"] = [cw, ch]
    target_dir = output / case.name
    target_dir.mkdir(parents=True, exist_ok=True)

    pdf = case_work / "factspdf.pdf"
    command = [str(cli), str(case.html), str(pdf),
               "--font", str(LATIN), "--font", str(CJK), "--subset-fonts"]
    execution = subprocess.run(command, capture_output=True, timeout=120)

    if mode == "reference":
        if execution.returncode != 3:
            raise AssertionError(
                f"{case.name}: expected current unsupported exit 3, got "
                f"{execution.returncode}; stderr={execution.stderr.decode(errors='replace')[-700:]}")
        result["status"] = validate_unsupported(execution.stderr, pdf)
        for number, path in enumerate(browser_rasters, 1):
            reference = target_dir / f"page-{number:02}-chrome-reference.png"
            shutil.copyfile(path, reference)
            result["page_differences"].append({
                "number": number,
                "status": "NOT_COMPARED_FACTSPDF_BOX_MODEL_NOT_IMPLEMENTED",
                "chrome_image": str(reference.relative_to(output))
            })
        return result

    if execution.returncode:
        raise AssertionError(
            f"{case.name}: model compare mode requires real FactsPDF support; "
            f"exit={execution.returncode}, stderr={execution.stderr.decode(errors='replace')[-700:]}")
    run("qpdf", "--check", pdf)
    native_pages, fw, fh = pdfinfo(pdf)
    assert_text_inside_page(pdf)
    if native_pages != chrome_pages or native_pages != case.expected_pages:
        raise AssertionError(
            f"{case.name}: page counts Chrome={chrome_pages}, FactsPDF={native_pages} "
            f"vs expected {case.expected_pages}")
    native_text = normalized(run("pdftotext", "-raw", pdf, "-").decode("utf-8"))
    if native_text != browser_text or native_text != original_text:
        raise AssertionError(
            f"{case.name}: PDF text differs; native={native_text!r}, Chrome={browser_text!r}")
    native_rasters = images_for_pdf(pdf, case_work / "native-raster")
    if len(native_rasters) != len(browser_rasters):
        raise AssertionError("PDF raster page count does not match")
    result["factspdf_paper_points"] = [fw, fh]
    for number, (cpath, fpath) in enumerate(zip(browser_rasters, native_rasters), 1):
        with Image.open(cpath) as cimg, Image.open(fpath) as fimg:
            chrome, native, alignment = align_raster_canvases(cimg, fimg)
            metrics = compare(chrome, native)
            imgs = save_visuals(chrome, native, target_dir, f"page-{number:02}")
        result["page_differences"].append({
            "number": number, "status": "COMPARED",
            "metrics": metrics, "alignment": alignment,
            "images": {key: str(Path(filename).relative_to(output))
                       for key, filename in imgs.items()}
        })
    result["status"] = "compared"
    return result


def make_summary(record: dict) -> str:
    text = [
        "# FactsPDF box-model Chrome visual references",
        "",
        f"Mode: **{record['mode']}** — 5 fixtures, "
        f"{sum(c['expected_pages'] for c in record['cases'])} expected reference pages.",
        "",
        "| Fixture | Chrome pages | Colored probes | FactsPDF status |",
        "| --- | ---: | ---: | --- |"
    ]
    for case in record["cases"]:
        probes = case["page_reference_probes"]
        text.append(
            f"| {case['name']} | {len(probes)} | "
            f"{sum(len(p['color_pixels']) for p in probes)} | {case.get('status') or 'ERROR'} |")
    text.extend([
        "",
        "**Reference mode does NOT run a passing cross-engine visual comparison.**",
        "StrictPdf must explicitly reject unsupported width/padding/border/background "
        "properties without creating a PDF. The future compare mode generates both "
        "PDFs and full/ink region visual diffs for exactly the same fixture input.",
        "",
        f"Hard failures: {len(record['errors'])}. No font files, PDFs or executable "
        "programs are included in the upload; only public reference PNGs and this report.",
        "Chrome uses browser-only print normalization and print-color-adjust:exact "
        "to expose solid backgrounds; it is disclosed in report.json.",
        "",
    ])
    return "\n".join(text)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=["reference", "compare"], required=True)
    parser.add_argument("--native", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    if not args.native.is_file():
        raise RuntimeError("Actual native FactsPDF CLI executable is missing")
    if not all(x.is_file() for x in (LATIN, CJK)):
        raise RuntimeError("OS-installed test fonts are missing")
    browser = next((shutil.which(binary) for binary in
                    ("google-chrome", "chromium", "chromium-browser")
                    if shutil.which(binary)), None)
    if not browser:
        raise RuntimeError("Chrome/Chromium binary is required for an independent oracle")
    args.output.mkdir(parents=True, exist_ok=True)
    record = {
        "run": "FactsPDF box model Chrome PDF visual reference",
        "mode": args.mode,
        "css_scope": "background-color, border, padding, width%, nesting, 3-page sliced box",
        "source_head": source_sha(),
        "chrome": run(browser, "--version").decode().strip(),
        "raster_dpi": DPI,
        "expected_paper_pt": [WIDTH_PT, HEIGHT_PT],
        "margin_pt": MARGIN_PT,
        "os_font_sha256": {LATIN.name: sha256(LATIN), CJK.name: sha256(CJK)},
        "chrome_only_print_color_css": CHROME_PRINT_COLOR.strip(),
        "chrome_only_base_normalization_css": BROWSER_NORMALIZATION.strip(),
        "note": (
            "Only Chrome reference PDFs are rendered in 'reference' mode; "
            "the current FactsPDF renderer is intentionally unimplemented for box CSS, "
            "and must return FPDF1201. In 'compare' mode only, both PDFs are required, "
            "tested for identical extracted text and pages, and rendered into pixel "
            "and ink-region maps. No global similarity percentage claimed."),
        "cases": [],
        "errors": []
    }

    try:
        with tempfile.TemporaryDirectory(prefix="factspdf-box-chrome-") as tmp:
            for case in CASES:
                try:
                    entry = run_case(case, browser=browser, cli=args.native,
                                     output=args.output, scratch=Path(tmp), mode=args.mode)
                    record["cases"].append(entry)
                    print(f"BOX REFERENCE {case.name}: {entry['status']} "
                          f"{len(entry['page_reference_probes'])} page(s)", flush=True)
                except Exception as ex:
                    record["errors"].append({"case": case.name, "reason": str(ex)})
                    record["cases"].append({
                        "name": case.name, "expected_pages": case.expected_pages,
                        "status": "ERROR", "page_reference_probes": [],
                        "error": str(ex)
                    })
    finally:
        (args.output / "report.json").write_text(
            json.dumps(record, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        (args.output / "summary.md").write_text(
            make_summary(record), encoding="utf-8")
    if record["errors"]:
        raise AssertionError(
            f"{len(record['errors'])} box-model oracle fixtures failed; "
            "see report.json for exact case and cause.")
    if len(record["cases"]) != len(CASES):
        raise AssertionError("A box-model fixture was omitted")
    print("All box-model Chrome reference checks passed; "
          "FactsPDF box-model comparison remains future work.", flush=True)


if __name__ == "__main__":
    main()
