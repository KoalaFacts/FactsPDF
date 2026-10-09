"""Inspect the synthetic CI sample using an independent PDF reader, not the engine."""
from __future__ import annotations

import base64
import re
import subprocess
import sys
from pathlib import Path


def main() -> None:
    if len(sys.argv) not in (2, 3):
        raise SystemExit("Usage: verify_sample.py PDF [--emit-base64]")
    path = Path(sys.argv[1])
    data = path.read_bytes()
    if not data.startswith(b"%PDF-1.7\n"):
        raise AssertionError("Missing PDF header")
    info = subprocess.run(["pdfinfo", str(path)], check=True, capture_output=True, text=True)
    if info.stderr.strip():
        raise AssertionError(info.stderr)
    if not re.search(r"^Pages:\s+2$", info.stdout, re.MULTILINE):
        raise AssertionError("Expected exactly two pages: " + info.stdout)
    text_result = subprocess.run(["pdftotext", "-layout", str(path), "-"], check=True, capture_output=True, text=True)
    if text_result.stderr.strip():
        raise AssertionError(text_result.stderr)
    text = text_result.stdout
    for expected in ("FactsPDF", "The first renderer", "A second page", "Line two", "What comes next"):
        if expected not in text:
            raise AssertionError(f"Missing extracted text: {expected}")
    pages = text.split("\f")
    if "The first renderer" not in pages[0] or "A second page" not in pages[1]:
        raise AssertionError("Page break was not honored")
    if "A second page" in pages[0]:
        raise AssertionError("Second heading leaked onto the first page")
    subprocess.run(["pdftoppm", "-scale-to", "1000", "-png", str(path), str(path.with_suffix(""))], check=True)
    if len(list(path.parent.glob(path.stem + "-*.png"))) != 2:
        raise AssertionError("Expected two rendered page images")
    print(info.stdout)
    print("Independent PDF text and page-render checks passed.")
    if len(sys.argv) == 3:
        if sys.argv[2] != "--emit-base64":
            raise SystemExit("Unknown argument")
        if len(data) > 16_384:
            raise AssertionError("Only the small synthetic fixture may be emitted to CI logs")
        print("FACTSPDF_SAMPLE_BASE64=" + base64.b64encode(data).decode("ascii"))


if __name__ == "__main__":
    main()
