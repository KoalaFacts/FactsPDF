#!/usr/bin/env python3
"""Author/check D1's synthetic JPEG corpus. Pillow is a verification tool only.

--generate is an explicit authoring operation, never a reference approval step.
Default/--check reads committed bytes and independently decodes positive cases.
No FactsPDF implementation is imported or invoked.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path

from PIL import Image, ImageDraw, features

ROOT = Path(__file__).resolve().parents[1]
CORPUS = ROOT / "tests/fixtures/jpeg"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def encode(image, **kwargs):
    output = io.BytesIO()
    image.save(output, "JPEG", quality=90, optimize=False, **kwargs)
    return output.getvalue()


def headers(data):
    """Locate length-delimited headers, stopping at the first scan payload."""
    assert data[:2] == b"\xff\xd8"
    offset = 2
    while offset < len(data):
        assert data[offset] == 255
        marker = data[offset + 1]
        length = int.from_bytes(data[offset + 2:offset + 4], "big")
        end = offset + 2 + length
        assert length >= 2 and end <= len(data)
        yield marker, offset, offset + 4, end
        if marker == 0xDA:
            return
        offset = end


def first(data, marker):
    return next(item for item in headers(data) if item[0] == marker)


def replace(data, offset, value):
    return data[:offset] + value + data[offset + len(value):]


def segment(marker, payload):
    return bytes((255, marker)) + (len(payload) + 2).to_bytes(2, "big") + payload


def generate():
    # These are newly authored test pixels: asymmetric corner colors, an upward
    # arrow, and bars. No source photos, downloaded artwork, or external fonts.
    chart = Image.new("RGB", (320, 160), "#f4f7fa")
    draw = ImageDraw.Draw(chart)
    for box, color in [((0, 0, 31, 23), "#ed3030"),
                       ((288, 0, 319, 23), "#25a34a"),
                       ((0, 136, 31, 159), "#2459db"),
                       ((288, 136, 319, 159), "#f0ce27")]:
        draw.rectangle(box, fill=color)
    draw.polygon([(160, 8), (148, 25), (156, 25), (156, 35),
                  (164, 35), (164, 25), (172, 25)], fill="#182d40")
    for index, height in enumerate([32, 56, 44, 80, 65, 94, 108]):
        x = 47 + index * 33
        draw.rectangle((x, 140 - height, x + 18, 140), fill="#246b92")
    draw.line((42, 142, 280, 142), fill="#182d40", width=2)
    gray = Image.new("L", (64, 32))
    gray.putdata([int(x / 63 * 255) for y in range(32) for x in range(64)])
    odd = Image.new("RGB", (17, 9))
    odd.putdata([((x * 31) % 256, (y * 59) % 256, (x * 13 + y * 7) % 256)
                 for y in range(9) for x in range(17)])
    cases = []

    def add(name, data, expected, reason, size=None, mode=None):
        row = {"file": name + ".jpg", "expected_load": expected, "reason": reason,
               "byte_length": len(data), "sha256": digest(data)}
        if size:
            row.update(width=size[0], height=size[1], mode=mode)
        if expected == "accept":
            with Image.open(io.BytesIO(data)) as image:
                image.load()
                assert image.size == tuple(size) and image.mode == mode
                row["independent_decoded_sha256"] = digest(image.tobytes())
        (CORPUS / row["file"]).write_bytes(data)
        cases.append(row)

    CORPUS.mkdir(parents=True, exist_ok=True)
    rgb = encode(chart, subsampling=2)
    _, _, jfif, jfif_end = first(rgb, 0xE0)

    def metadata(payload):
        return rgb[:jfif_end] + payload + rgb[jfif_end:]
    for suffix, sampling in [("444", 0), ("422", 1), ("420", 2)]:
        add("rgb-" + suffix, encode(chart, subsampling=sampling), "accept",
            "8-bit baseline JFIF YCbCr, sampling " + suffix, (320, 160), "RGB")
    add("gray", encode(gray), "accept", "8-bit baseline JFIF grayscale", (64, 32), "L")
    add("odd-420", encode(odd, subsampling=2), "accept", "Partial edge MCUs", (17, 9), "RGB")
    restart = encode(chart, subsampling=2, restart_marker_blocks=3)
    assert b"\xff\xdd" in restart and b"\xff\xd0" in restart
    add("restart-420", restart, "accept", "DRI=3, ordered restart markers", (320, 160), "RGB")
    _, quant_begin, _, _ = first(restart, 0xDB)
    filled = restart[:quant_begin] + b"\xff\xff" + restart[quant_begin:]
    filled = filled.replace(b"\xff\xd0", b"\xff\xff\xff\xd0")
    filled = filled.replace(b"\xff\xd9", b"\xff\xff\xff\xd9")
    add("marker-fill-420", filled, "accept", "Legal FF marker fill before DQT, RST0 and EOI",
        (320, 160), "RGB")
    exif = Image.Exif()
    exif[274] = 1
    upright = encode(chart, subsampling=2, exif=exif)
    add("exif-upright", upright, "accept", "IFD0 orientation=1", (320, 160), "RGB")
    add("density-300", encode(chart, subsampling=2, dpi=(300, 300)), "accept",
        "Equal JFIF densities do not change 96dpi layout convention", (320, 160), "RGB")
    add("comment", metadata(segment(0xFE, b"FactsPDF synthetic D1 fixture")),
        "accept", "Bounded comment is opaque metadata", (320, 160), "RGB")
    add("progressive", encode(chart, subsampling=2, progressive=True), "FPDF1602",
        "Progressive SOF2 is outside D1")
    add("cmyk", encode(chart.convert("CMYK")), "FPDF1602", "Four components are outside D1")
    exif[274] = 6
    add("exif-rotated", encode(chart, subsampling=2, exif=exif), "FPDF1602",
        "Non-upright orientation is rejected, never silently ignored")
    exif[274] = 0
    add("exif-invalid-orientation", encode(chart, subsampling=2, exif=exif), "FPDF1601",
        "Orientation is outside the Exif 1..8 domain")
    add("nonsquare-density", replace(rgb, jfif + 8, b"\x00\x02\x00\x01"), "FPDF1602",
        "JFIF non-square pixels are outside D1")
    add("no-jfif", rgb[:2] + rgb[jfif_end:], "FPDF1602", "No explicit JFIF color contract")
    add("adobe-transform", metadata(segment(0xEE, b"Adobe\x00\x64\x00\x00\x00\x00\x00")),
        "FPDF1602", "Adobe APP14 is outside D1 regardless of transform value")
    add("icc-marker", metadata(segment(0xE2, b"ICC_PROFILE\x00\x01\x01not-a-profile")),
        "FPDF1602", "APP2 ICC marker is rejected without parsing its opaque payload")
    add("bad-soi", replace(rgb, 1, b"\x00"), "FPDF1601", "Invalid SOI")
    _, _, quant, quant_end = first(rgb, 0xDB)
    add("truncated-segment", rgb[:quant_end - 1], "FPDF1601", "DQT payload ends early")
    add("zero-quantizer", replace(rgb, quant + 1, b"\x00"), "FPDF1601", "Zero quantizer")
    add("missing-eoi", rgb[:-2], "FPDF1601", "Missing EOI")
    _, _, scan, entropy = first(rgb, 0xDA)
    add("truncated-entropy", rgb[:entropy + (len(rgb) - entropy) // 2] + b"\xff\xd9",
        "FPDF1601", "Premature EOI despite a complete outer header")
    add("trailing-data", rgb + b"extra", "FPDF1601", "Data after EOI")
    _, _, frame, _ = first(rgb, 0xC0)
    add("zero-width", replace(rgb, frame + 3, b"\x00\x00"), "FPDF1601", "Zero frame width")
    add("over-dimension", replace(rgb, frame + 3, (8193).to_bytes(2, "big")), "FPDF1603",
        "Dimension limit must win before entropy traversal")
    add("over-pixels", replace(rgb, frame + 1, (4096).to_bytes(2, "big") + (4097).to_bytes(2, "big")),
        "FPDF1603", "Pixel limit must win before entropy traversal")
    add("duplicate-component", replace(rgb, frame + 9, rgb[frame + 6:frame + 7]),
        "FPDF1601", "Duplicate component IDs")
    missing_huffman = rgb
    for marker, begin, _, end in reversed(list(headers(rgb))):
        if marker == 0xC4:
            missing_huffman = missing_huffman[:begin] + missing_huffman[end:]
    add("missing-huffman", missing_huffman, "FPDF1601", "SOS references undefined Huffman tables")
    _, _, huffman, _ = first(rgb, 0xC4)
    counts = bytearray(rgb[huffman + 1:huffman + 17])
    original_total = sum(counts)
    assert counts[0] == 0 and counts[2] >= 3
    counts[0] = 3
    counts[2] -= 3
    assert sum(counts) == original_total
    add("oversubscribed-huffman", replace(rgb, huffman + 1, bytes(counts)), "FPDF1601",
        "Complete symbol payload but three one-bit codes exceed prefix-tree capacity")
    add("invalid-scan", replace(rgb, scan + 1 + rgb[scan] * 2, b"\x01"), "FPDF1601",
        "Baseline scan must start at spectral selection zero")
    add("unexpected-restart", rgb[:entropy] + b"\xff\xd0" + rgb[entropy:], "FPDF1601",
        "Restart marker without a DRI interval")
    restart_pos = restart.index(b"\xff\xd0")
    add("bad-restart-sequence", replace(restart, restart_pos + 1, b"\xd3"), "FPDF1601",
        "First restart must be RST0")
    _, _, app1, _ = first(upright, 0xE1)
    # Pillow writes a big-endian TIFF header: bytes 4..7 contain the IFD0 offset.
    assert upright[app1 + 6:app1 + 8] == b"MM"
    add("exif-outside-segment", replace(upright, app1 + 10, b"\xff\xff\xff\xff"), "FPDF1601",
        "IFD0 offset lies outside APP1")
    add("over-segments", metadata(segment(0xFE, b"x") * 257), "FPDF1603",
        "More than 256 length-delimited marker segments")
    manifest = {"status": "proposed-contract-not-engine-acceptance", "schema": 1,
                "origin": "Procedural pixels authored for FactsPDF; no external image assets",
                "generator": "scripts/jpeg_fixtures.py --generate",
                "pillow": Image.__version__, "jpeg_codec": features.version_codec("jpg"),
                "libjpeg_turbo": features.version_feature("libjpeg_turbo"),
                "cases": cases}
    (CORPUS / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")


def check():
    manifest = json.loads((CORPUS / "manifest.json").read_text())
    expected_files = {row["file"] for row in manifest["cases"]}
    assert expected_files == {file.name for file in CORPUS.glob("*.jpg")}
    by_name = {row["file"]: row for row in manifest["cases"]}
    observations = []
    for row in manifest["cases"]:
        data = (CORPUS / row["file"]).read_bytes()
        assert len(data) == row["byte_length"] and digest(data) == row["sha256"], row["file"]
        observed = {"file": row["file"], "expected_load": row["expected_load"]}
        if row["file"] == "oversubscribed-huffman.jpg":
            _, _, table, end = first(data, 0xC4)
            counts = list(data[table + 1:table + 17])
            assert sum(counts) == end - (table + 17), "Fixture must have a complete symbol payload"
            assert counts[0] > 2, "One-bit prefix tree must be oversubscribed"
            observed["isolated_huffman_overallocation"] = True
        if row["file"] == "marker-fill-420.jpg":
            assert all(bytes((255, 255, 255, marker)) in data for marker in (0xDB, 0xD0, 0xD9))
            assert row["independent_decoded_sha256"] == by_name["restart-420.jpg"]["independent_decoded_sha256"]
        # Do not decode the deliberately over-limit headers. No pixel buffer is
        # needed to verify their authored early-refusal property.
        if row["file"] in ("over-dimension.jpg", "over-pixels.jpg"):
            _, _, frame, _ = first(data, 0xC0)
            height = int.from_bytes(data[frame + 1:frame + 3], "big")
            width = int.from_bytes(data[frame + 3:frame + 5], "big")
            assert (width, height) == ((8193, 160) if row["file"] == "over-dimension.jpg" else (4097, 4096))
            observed["independent_decode"] = "not-attempted-over-limit-header"
        else:
            try:
                with Image.open(io.BytesIO(data)) as image:
                    image.load()
                    observed.update(independent_decode="accepted", size=list(image.size), mode=image.mode)
                    if row["expected_load"] == "accept":
                        assert image.size == (row["width"], row["height"]), row["file"]
                        assert image.mode == row["mode"], row["file"]
                        assert digest(image.tobytes()) == row["independent_decoded_sha256"], row["file"]
            except (OSError, ValueError) as error:
                assert row["expected_load"] != "accept", row["file"]
                observed.update(independent_decode="rejected", exception=type(error).__name__)
        observations.append(observed)
    print(json.dumps({"status": "fixture-check-only", "cases": len(observations),
                      "positive_cases": sum(r["expected_load"] == "accept" for r in observations),
                      "pillow": Image.__version__, "jpeg_codec": features.version_codec("jpg"),
                      "libjpeg_turbo": features.version_feature("libjpeg_turbo"),
                      "observations": observations}, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--generate", action="store_true")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    if args.generate:
        generate()
    check()
