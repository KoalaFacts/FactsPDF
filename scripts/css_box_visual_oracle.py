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
import PIL
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
  * { font-variant-ligatures: none !important;
      font-feature-settings: "kern" 0, "liga" 0, "clig" 0 !important; }
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



def validate_border_sides(image: Image.Image, dpi: int = 120,
                          dimension_tolerance: float = 3.0):
    """Check that colors form their DECLARED edges with correct thickness."""
    bounds = {
        "top": color_extent(image, "#aa2233"),
        "right": color_extent(image, "#1d4568"),
        "bottom": color_extent(image, "#008000"),
        "left": color_extent(image, "#a0601c"),
    }
    if any(b is None for b in bounds.values()):
        raise AssertionError("Missing one or more independent border-side colors")
    top, right, bottom, left = (bounds[k] for k in ("top", "right", "bottom", "left"))
    if not (top[1] <= min(left[1], right[1]) and
            bottom[3] >= max(left[3], right[3]) and
            left[0] <= top[0] and right[2] >= top[2]):
        raise AssertionError(f"Border colors appear on the wrong sides: {bounds}")
    def thick(box, side):
        return box[3] - box[1] if side in ("top", "bottom") else box[2] - box[0]
    declared = {"top": 6, "right": 5, "bottom": 4, "left": 7}
    for side, box in bounds.items():
        expected = declared[side] * dpi / 72
        if abs(thick(box, side) - expected) > dimension_tolerance:
            raise AssertionError(f"{side} border thickness does not match declared CSS: {box}")
        if side in ("top", "bottom"):
            if (box[2] - box[0]) <= 5 * (box[3] - box[1]):
                raise AssertionError(f"{side} must be a horizontal border")
        elif (box[3] - box[1]) <= 4 * (box[2] - box[0]):
            raise AssertionError(f"{side} must be a vertical border")
    return {"orientation": "correct", "side_bounds": bounds,
            "declared_widths_pt": declared, "dpi": dpi}


def validate_padding_geometry(image: Image.Image, dpi: int = 120):
    """Assert all four TRBL offsets, not just some nested containment."""
    outer = color_extent(image, "#e4e5e7")
    inner = color_extent(image, "#abd9e7")
    if outer is None or inner is None:
        raise AssertionError("Both solid padding backgrounds must be visible")
    insets = [inner[0] - outer[0], inner[1] - outer[1],
              outer[2] - inner[2], outer[3] - inner[3]]
    # The outer padding TRBL is 12pt 28pt 24pt 36pt.
    expected = [36 * dpi / 72, 12 * dpi / 72,
                28 * dpi / 72, 24 * dpi / 72]
    if any(abs(actual - target) > 3 for actual, target in zip(insets, expected)):
        raise AssertionError(f"Padding geometry does not match CSS TRBL: {insets} vs {expected}")
    return {"insets_px": insets, "css_padding_trbl_pt": [12, 28, 24, 36],
            "outer_bbox": outer, "inner_bbox": inner}


def validate_nested_percent_width(image: Image.Image, dpi: int = 120):
    """Measure content-box % width after deducting declared padding/borders."""
    outer = color_extent(image, "#dbeefa")
    inner = color_extent(image, "#e4c76a")
    leaf = color_extent(image, "#ffb27d")
    if outer is None or inner is None or leaf is None:
        raise AssertionError("Every nested background must have a painted area")
    if not (outer[0] < inner[0] < leaf[0] and
            leaf[2] < inner[2] < outer[2] and
            outer[1] < inner[1] < leaf[1] and
            leaf[3] < inner[3] < outer[3]):
        raise AssertionError("Nested boxes must be strictly inset")
    pt = dpi / 72
    parent_content = (outer[2] - outer[0]) - (2 * 12 + 2 * 2) * pt
    child_content = (inner[2] - inner[0]) - (2 * 16 + 2 * 2) * pt
    if parent_content <= 0 or child_content <= 0:
        raise AssertionError("Content box dimensions must be positive")
    ratio = child_content / parent_content
    if abs(ratio - 0.70) > 0.03:
        raise AssertionError(f"width:70% child measured as {ratio:.4f}")
    return {"content_width_ratio": round(ratio, 6),
            "declared_child_width_percent": 70,
            "outer_bbox": outer, "inner_bbox": inner, "leaf_bbox": leaf}


def validate_native_box_visual(case: BoxCase, raster_paths: list[Path]):
    """In compare mode, native PDF must actually paint supported box CSS."""
    probe = validate_reference_pages(case, raster_paths)
    if case.name == "fragmentation":
        probe["sliced_edges"] = validate_fragment_edges(raster_paths, "#1d4568")
    return probe


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
    wide_rows = []
    ink_edges = []
    for path in raster_paths:
        with Image.open(path) as img:
            rgb = img.convert("RGB")
            px = rgb.load()
            rows = []
            ymin, ymax = rgb.height, -1
            for y in range(rgb.height):
                observed = 0
                for x in range(rgb.width):
                    pixel = px[x, y]
                    if all(abs(pixel[k] - color[k]) <= 6 for k in range(3)):
                        observed += 1
                if observed:
                    ymin, ymax = min(ymin, y), max(ymax, y)
                if observed >= min_row_pixels:
                    rows.append(y)
            wide_rows.append(rows)
            ink_edges.append((ymin, ymax))
    tolerance = 10  # 4pt border at 120DPI plus antialiasing
    first, middle, last = wide_rows
    if not (first and not middle and last):
        raise AssertionError(
            f"Expected sliced first/middle/last border rows (>0, 0, >0), got {list(map(len,wide_rows))}")
    if any(y > ink_edges[0][0] + tolerance for y in first):
        raise AssertionError(f"First fragment has a false bottom/opposite edge: {first}")
    if any(y < ink_edges[2][1] - tolerance for y in last):
        raise AssertionError(f"Last fragment has a false top/opposite edge: {last}")
    return {
        "first_top_rows": len(first),
        "middle_horizontal_rows": len(middle),
        "last_bottom_rows": len(last),
        "horizontal_border_positions": wide_rows,
        "side_border_vertical_extents": ink_edges,
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
            geometry = None
            if case.name == "border":
                geometry = validate_border_sides(img)
            elif case.name == "padding":
                geometry = validate_padding_geometry(img)
            elif case.name == "nested":
                geometry = validate_nested_percent_width(img)
            pages.append({"number": page_no, "width_px": img.width,
                          "height_px": img.height, "color_pixels": samples,
                          "color_bounds": bounds, "geometry": geometry})
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

    if mode == "m7" and case.name == "fragmentation":
        stderr = execution.stderr.decode("utf-8", errors="replace")
        if execution.returncode != 3 or "FPDF1302" not in stderr or pdf.exists():
            raise AssertionError(
                f"M7 must explicitly reject painted multi-page boxes before output; "
                f"exit={execution.returncode}, stderr={stderr[-700:]}")
        for number, path in enumerate(browser_rasters, 1):
            reference = target_dir / f"page-{number:02}-chrome-reference.png"
            shutil.copyfile(path, reference)
            result["page_differences"].append({
                "number": number, "status": "M8_FRAGMENTATION_DEFERRED",
                "chrome_image": str(reference.relative_to(output))
            })
        result["status"] = "M8-fragmentation-deferred"
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
    result["native_paint_verification"] = validate_native_box_visual(
        case, native_rasters)
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
    if record["mode"] == "reference":
        text.extend([
            "",
            "**Reference mode does NOT run a paired cross-engine comparison.**",
            "StrictPdf must explicitly reject unsupported box properties without creating a PDF.",
        ])
    elif record["mode"] == "m7":
        text.extend([
            "",
            "**M7 genuinely compares four single-page Chrome/FactsPDF PDFs** for",
            "extracted text, visible colored fills, independent border geometry,",
            "and raster differences. The three-page fragment remains an explicit",
            "M8 unsupported-result gate (FPDF1302/no PDF), NOT a comparison.",
        ])
    else:
        text.extend([
            "",
            "**Compare mode requires real paired Chrome/FactsPDF PDFs**, matching"
            " text and page counts, plus native box-paint and fragmentation probes.",
            "Pixel difference values remain diagnostics, not universal CSS conformance.",
        ])
    text.extend([
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
    parser.add_argument("--mode", choices=["reference", "m7", "compare"], required=True)
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
        "checkout_sha": run("git", "rev-parse", "HEAD").decode().strip(),
        "chrome": run(browser, "--version").decode().strip(),
        "tool_versions": {
            "pdftoppm": subprocess.run(["pdftoppm", "-v"], capture_output=True,
                                       text=True, timeout=15).stderr.strip().splitlines()[0],
            "pdftotext": subprocess.run(["pdftotext", "-v"], capture_output=True,
                                        text=True, timeout=15).stderr.strip().splitlines()[0],
            "pillow": PIL.__version__,
            "qpdf": run("qpdf", "--version").decode().splitlines()[0],
        },
        "raster_dpi": DPI,
        "expected_paper_pt": [WIDTH_PT, HEIGHT_PT],
        "margin_pt": MARGIN_PT,
        "os_font_sha256": {LATIN.name: sha256(LATIN), CJK.name: sha256(CJK)},
        "chrome_only_print_color_css": CHROME_PRINT_COLOR.strip(),
        "chrome_only_base_normalization_css": BROWSER_NORMALIZATION.strip(),
        "note": (
            "Reference mode is an historic unsupported-feature gate. M7 validates "
            "four real single-page painted PDFs against independent Chrome pages "
            "and explicitly rejects three-page painting (M8). Compare mode "
            "requires all five native PDFs, including true sliced fragments. "
            "No global similarity percentage is implied."),
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
    print(f"All box-model {args.mode} checks passed; "
          "M7 is limited to single-page painting." if args.mode == "m7" else
          "All Chrome reference/comparison checks passed.", flush=True)


if __name__ == "__main__":
    main()
