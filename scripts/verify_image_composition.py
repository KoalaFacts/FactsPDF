"""New multi-page C1/C2/D1 combinations; no browser printing or reference approval."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

from PIL import Image
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream


def run(command):
    return subprocess.run([str(v) for v in command], check=True, capture_output=True, timeout=120).stdout


def sha(data):
    return hashlib.sha256(data).hexdigest()


CASES = [
    {"name": "image-list-composition", "pages": 2, "image_page": 2, "image_x": 41,
     "tokens": [*[f"LEAD-{i:02d}" for i in range(1, 25)], "AUTO-AFTER", "AFTER-ONE", "NESTED-ONE", "AFTER-TWO", "AUTO-END"],
     "roles": {"LEAD-01": "DejaVuSans", "AUTO-AFTER": "DejaVuSans-Bold", "AFTER-TWO": "DejaVuSans-Oblique", "AUTO-END": "DejaVuSans-BoldOblique"}},
    {"name": "image-only-middle-page", "pages": 3, "image_page": 2, "image_x": 36,
     "tokens": ["ONLY-BEFORE", "ONLY-AFTER", "ONLY-NESTED", "ONLY-END"],
     "roles": {"ONLY-BEFORE": "DejaVuSans-Bold", "ONLY-AFTER": "DejaVuSans-Oblique", "ONLY-END": "DejaVuSans-BoldOblique"}},
]


def inspect(path, case, jpeg, raster=True):
    reader = PdfReader(path)
    assert len(reader.pages) == case["pages"], "page count"
    texts, fonts, draws = [], {}, []
    for index, page in enumerate(reader.pages, 1):
        def visitor(text, cm, tm, font, size):
            for token in case["roles"]:
                if token in text:
                    assert font is not None, "missing font resource"
                    fonts[token] = str(font["/BaseFont"]).split("+")[-1].lstrip("/")
        texts.append(page.extract_text(visitor_text=visitor))
        resources = page["/Resources"].get("/XObject", {})
        assert len(resources) == (1 if index == case["image_page"] else 0), "page-local image resources"
        matrix, stack = [1, 0, 0, 1, 0, 0], []
        for operands, operator in ContentStream(page.get_contents(), reader).operations:
            if operator == b"q": stack.append(matrix.copy())
            elif operator == b"Q": matrix = stack.pop()
            elif operator == b"cm":
                assert matrix == [1, 0, 0, 1, 0, 0], "unexpected nested transform"
                matrix = [float(v) for v in operands]
            elif operator == b"Do":
                image = resources[operands[0]].get_object()
                assert image["/Subtype"] == "/Image" and image["/Filter"] == "/DCTDecode"
                assert image._data == jpeg.read_bytes(), "original image bytes"
                draws.append((index, matrix.copy()))
    assert fonts == case["roles"], ("font role assignments", fonts)
    assert draws == [(case["image_page"], [400, 0, 0, 200, case["image_x"], 605.89])], ("image placement", draws)
    text = "\n".join(texts)
    assert all(text.count(token) == 1 for token in case["tokens"]), "missing/duplicated token"
    positions = [text.index(token) for token in case["tokens"]]
    assert positions == sorted(positions), "text order"
    if case["name"] == "image-list-composition":
        assert re.findall(r"LEAD-\d{2}", texts[0]) == case["tokens"][:24], "leading rows moved or missing"
        assert "AUTO-AFTER" in texts[1] and "AUTO-END" in texts[1]
        assert re.findall(r"(?m)^\s*(\d+)\.", texts[0]) == [str(i) for i in range(1, 25)], "leading markers"
        assert re.findall(r"(?m)^\s*(\d+)\.", texts[1]) == ["1", "2"], "following list counter"
    else:
        assert texts[1].strip() == "", "middle page must contain only the image"
        assert "ONLY-BEFORE" in texts[0] and "ONLY-AFTER" in texts[2]
    bbox = ET.fromstring(run(["pdftotext", "-bbox", path, "-"]))
    for index, page in enumerate([e for e in bbox.iter() if e.tag.endswith("page")], 1):
        for word in [e for e in page.iter() if e.tag.endswith("word")]:
            x0, y0, x1, y1 = [float(word.attrib[k]) for k in ("xMin", "yMin", "xMax", "yMax")]
            assert 36 - .01 <= x0 < x1 <= 559.28 + .01, "horizontal text bounds"
            assert 36 - .01 <= y0 < y1 <= 805.89 + .01, "vertical text bounds"
            if index == case["image_page"]:
                assert y0 >= 236 - .01, "text overlaps image"
    run([os.environ.get("FACTSPDF_QPDF", "qpdf"), "--check", path])
    if raster:
        prefix = path.with_suffix("")
        run(["pdftoppm", "-png", "-r", "96", path, prefix])
        png = Path(f"{prefix}-{case['image_page']}.png")
        with Image.open(png).convert("RGB") as page, Image.open(jpeg).convert("RGB") as original:
            for sx, sy in [(15, 10), (304, 10), (15, 148), (304, 148), (160, 20)]:
                pixel = page.getpixel((round((case["image_x"] + (sx + .5) / 320 * 400) * 96 / 72),
                                       round((36 + (sy + .5) / 160 * 200) * 96 / 72)))
                assert max(abs(a - b) for a, b in zip(pixel, original.getpixel((sx, sy)))) <= 12, "rendered image orientation/color"
    return {"pages": len(reader.pages), "bytes": path.stat().st_size, "sha256": sha(path.read_bytes()),
            "imageDraws": draws, "fontRoles": fonts, "qpdf": "pass", "textAndBounds": "pass"}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--dotnet", required=True)
    parser.add_argument("--cli-dll", type=Path, required=True)
    parser.add_argument("--native-cli", type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    manifest = json.loads(args.manifest.read_text())
    selected = {path for paths in manifest["roles"].values() for path in paths}
    recorded = {f["path"]: f["sha256"] for f in manifest["fonts"]}
    assert all(sha(Path(path).read_bytes()) == recorded[path] for path in selected), "font fingerprints"
    flags = {"normal": "--font", "bold": "--font-bold", "italic": "--font-italic", "boldItalic": "--font-bold-italic"}
    fonts = [v for role, paths in manifest["roles"].items() for path in paths for v in [flags[role], path]]
    jpeg = Path("tests/fixtures/jpeg/rgb-420.jpg").resolve()
    commands = [("managed", [args.dotnet, args.cli_dll])]
    if args.native_cli: commands.append(("native", [args.native_cli]))
    evidence = {"sourceHead": run(["git", "rev-parse", "HEAD"]).decode().strip(),
                "dirty": bool(run(["git", "status", "--porcelain"]).strip()), "cases": [], "referenceApproval": "pending-user-review"}
    for case in CASES:
        previous = None
        for entry, command in commands:
            output = args.output / f"{case['name']}-{entry}.pdf"
            run(command + [Path("examples") / f"{case['name']}.html", output, "--overwrite", "--subset-fonts", "--image", f"chart={jpeg}"] + fonts)
            inspected = inspect(output, case, jpeg, raster=entry == "managed")
            if previous: assert output.read_bytes() == previous.read_bytes(), "cross-entry bytes"
            previous = output
            evidence["cases"].append({"name": case["name"], "entry": entry, **inspected})
        # A reordered real PDF must fail the page/token/image checks.
        reader = PdfReader(previous); writer = PdfWriter()
        for page in reversed(reader.pages): writer.add_page(page)
        mutant = args.output / f"{case['name']}-reversed.pdf"
        with mutant.open("wb") as stream: writer.write(stream)
        try: inspect(mutant, case, jpeg, raster=False)
        except AssertionError: pass
        else: raise AssertionError("Inspector accepted reordered pages")
        mutant.unlink()
    evidence["reorderedPageMutantsRejected"] = len(CASES)
    (args.output / "results.json").write_text(json.dumps(evidence, indent=2) + "\n")
    print(json.dumps({"success": True, "cases": len(evidence["cases"])}))


if __name__ == "__main__": main()
