#!/usr/bin/env python3
"""Compare FactsPDF's user-agent defaults with real Chrome, element by element.

The corpus (tests/fixtures/ua-defaults/corpus.json) holds one tiny synthetic document per supported element
or context; only font-family is authored, so every value is a user-agent default. This script needs a real
Chrome and is CI-only; the committed Chrome snapshot lets `dotnet test` check the computed values without one.

1. Snapshot freshness: getComputedStyle of every probe element is read from the live Chrome and compared
   with tests/fixtures/ua-defaults/chrome-computed.json. Any difference fails and is printed, which is how
   a Chrome change or a stale snapshot is caught. `--update-snapshot` rewrites the file.
2. Rendered geometry: every probe is printed to PDF by Chrome and by the FactsPDF CLI with the same DejaVu Sans
   faces, and compared per probe: first baseline, baseline-to-baseline gaps before/at/after the element,
   the x of text that starts a text run, font size and face. Differences must be listed in the geometry
   section of tests/fixtures/ua-defaults/known-differences.json; a listed difference that no longer occurs
   fails too, so the list can only shrink. `--update-known` rewrites the geometry section.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
from html.parser import HTMLParser
import json
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import zlib

FIXTURES = Path(__file__).resolve().parent.parent / "tests" / "fixtures" / "ua-defaults"
PROPERTIES = [
    "display",
    "margin-top", "margin-right", "margin-bottom", "margin-left",
    "padding-top", "padding-right", "padding-bottom", "padding-left",
    "border-top-width", "border-right-width", "border-bottom-width", "border-left-width",
    "border-top-style", "border-right-style", "border-bottom-style", "border-left-style",
    "font-size", "font-weight", "font-style", "line-height",
    "text-align", "text-decoration-line", "vertical-align", "list-style-type", "white-space",
]
FACES = {
    "DejaVuSans.ttf": ("normal", "normal"),
    "DejaVuSans-Bold.ttf": ("bold", "normal"),
    "DejaVuSans-Oblique.ttf": ("normal", "italic"),
    "DejaVuSans-BoldOblique.ttf": ("bold", "italic"),
}
FAILURE_SEPARATOR = "\n\n"
CLI_FONT_FLAGS =["--font", "--font-bold", "--font-italic", "--font-bold-italic"]
MISMATCH_TOLERANCE = 0.75   # pt: a geometry difference smaller than this is not a difference
KNOWN_TOLERANCE = 0.3       # pt: how far a listed difference may drift and still be that difference
CHROME_NAMES = ("google-chrome", "google-chrome-stable", "chromium", "chromium-browser")
CAPTURE_SCRIPT = """
window.addEventListener('load', function () {
  var props = %s, targets = %s, out = {};
  targets.forEach(function (key) {
    var parts = key.split('#'), tag = parts[0], n = parts.length > 1 ? +parts[1] : 0;
    var el = tag === 'html' ? document.documentElement : tag === 'body' ? document.body
      : document.body.querySelectorAll(tag)[n];
    var style = getComputedStyle(el); out[key] = {};
    props.forEach(function (p) { out[key][p] = style.getPropertyValue(p); });
  });
  document.documentElement.setAttribute('data-ua', JSON.stringify(out));
});
"""


def load_json(name):
    return json.loads((FIXTURES / name).read_text(encoding="utf-8"))


def compact(value):
    return json.dumps(value, separators=(",", ":"), ensure_ascii=False)


def find_chrome(explicit):
    candidates = [explicit, os.environ.get("FACTSPDF_CHROME")]
    candidates += [shutil.which(name) for name in CHROME_NAMES]
    for candidate in candidates:
        if candidate and Path(candidate).is_file():
            return candidate
    raise RuntimeError("A real Chrome or Chromium is required: pass --chrome or set FACTSPDF_CHROME")


def png_bytes(width=24, height=12):
    """A small deterministic RGB PNG for the probe image."""
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
    rows = b"".join(bytes(1) + b"".join(bytes([(x * 9) % 256, (y * 20) % 256, 140]) for x in range(width))
                    for y in range(height))
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)) +
            chunk(b"IDAT", zlib.compress(rows, 9)) + chunk(b"IEND", b""))


# ---------------------------------------------------------------- computed values (Chrome)

class DataReader(HTMLParser):
    """Reads the data-ua attribute the capture script left on the html element of a dumped DOM."""
    def __init__(self):
        super().__init__()
        self.data = None

    def handle_starttag(self, tag, attrs):
        if tag == "html":
            self.data = dict(attrs).get("data-ua")


def px_to_pt(value):
    match = re.fullmatch(r"(-?\d+(?:\.\d+)?)px", value)
    if not match:
        return value
    points = round(float(match.group(1)) * 0.75, 3)
    return f"{points:.3f}".rstrip("0").rstrip(".") + "pt"


def normalise(value):
    """Lengths in pt; Chrome's two private spellings of a centred th and caption become center."""
    if value in ("-internal-center", "-webkit-center"):
        return "center"
    return px_to_pt(value)


def chrome_args(chrome, profile):
    return [chrome, "--headless=new", "--no-sandbox", "--disable-gpu", "--disable-dev-shm-usage",
            "--disable-background-networking", "--disable-extensions", "--no-first-run",
            f"--user-data-dir={profile}"]


def probe_document(corpus, probe, style):
    return corpus["documentTemplate"].replace("{{style}}", style).replace("{{body}}", probe["body"])


def capture_computed(chrome, corpus, probe, work):
    targets = [target["key"] for target in probe["targets"]]
    script = "<script>" + CAPTURE_SCRIPT % (json.dumps(PROPERTIES), json.dumps(targets)) + "</script>"
    document = probe_document(corpus, probe, corpus["authoredStyle"]).replace("</head>", script + "</head>")
    folder = work / ("computed-" + probe["id"])
    folder.mkdir(parents=True)
    page = folder / "probe.html"
    page.write_text(document, encoding="utf-8")
    (folder / corpus["imageName"]).write_bytes(png_bytes())
    result = subprocess.run(chrome_args(chrome, folder / "profile") +
                            ["--virtual-time-budget=5000", "--dump-dom", page.as_uri()],
                            capture_output=True, text=True, timeout=120)
    reader = DataReader()
    reader.feed(result.stdout)
    if result.returncode or not reader.data:
        raise RuntimeError(f"Chrome returned no computed values for {probe['id']}: {result.stderr[-500:]}")
    return {f"{probe['id']}/{key}": {name: normalise(values[name]) for name in PROPERTIES}
            for key, values in json.loads(reader.data).items()}


def live_snapshot(chrome, corpus, work, workers):
    with ThreadPoolExecutor(max_workers=workers) as pool:
        parts = list(pool.map(lambda probe: capture_computed(chrome, corpus, probe, work), corpus["probes"]))
    elements = {}
    for part in parts:
        elements.update(part)
    return elements


def write_snapshot(elements):
    lines = [f" {json.dumps(key)}:{compact(values)}" for key, values in elements.items()]
    text = '{\n "properties":' + compact(PROPERTIES) + ',\n "elements":{\n' + ",\n".join(lines) + "\n }\n}\n"
    (FIXTURES / "chrome-computed.json").write_text(text, encoding="utf-8", newline="\n")


def diff_snapshot(committed, live):
    problems = []
    for key in sorted(set(committed) | set(live)):
        if key not in live:
            problems.append(f"{key}: in the snapshot but not produced by Chrome")
        elif key not in committed:
            problems.append(f"{key}: produced by Chrome but missing from the snapshot")
        else:
            for name in PROPERTIES:
                if committed[key].get(name) != live[key].get(name):
                    problems.append(f"{key} {name}: snapshot {committed[key].get(name)!r}, Chrome {live[key].get(name)!r}")
    return problems


# ---------------------------------------------------------------- rendered geometry

WORD = re.compile(r"[A-Za-z0-9]+")


def probe_words(body):
    text = re.sub(r"<[^>]*>", " ", body)
    seen = []
    for word in WORD.findall(text):
        if word not in seen:
            seen.append(word)
    return seen


IDENTITY = (1.0, 0.0, 0.0, 1.0, 0.0, 0.0)


def multiply(first, second):
    """The PDF product first x second of two six-number matrices."""
    a1, b1, c1, d1, e1, f1 = first
    a2, b2, c2, d2, e2, f2 = second
    return (a1 * a2 + b1 * c2, a1 * b2 + b1 * d2, c1 * a2 + d1 * c2, c1 * b2 + d1 * d2,
            e1 * a2 + f1 * c2 + e2, e1 * b2 + f1 * d2 + f2)


def to_unicode_map(font):
    """code -> text from a font's ToUnicode CMap (bfchar and bfrange), or None without one."""
    if "/ToUnicode" not in font:
        return None
    data = font["/ToUnicode"].get_object().get_data().decode("latin-1")
    mapping = {}

    def text_of(hex_digits):
        return bytes.fromhex(hex_digits).decode("utf-16-be")

    for block in re.findall(r"beginbfchar(.*?)endbfchar", data, re.S):
        for source, target in re.findall(r"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>", block):
            mapping[int(source, 16)] = text_of(target)
    for block in re.findall(r"beginbfrange(.*?)endbfrange", data, re.S):
        for low, high, target in re.findall(r"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>", block):
            start = text_of(target)
            for offset, code in enumerate(range(int(low, 16), int(high, 16) + 1)):
                mapping[code] = start[:-1] + chr(ord(start[-1]) + offset)
    return mapping


def raw_bytes(item):
    return getattr(item, "original_bytes", None) or bytes(item)


def decode_string(raw, mapping):
    if mapping is None:
        return raw.decode("latin-1")
    return "".join(mapping.get(int.from_bytes(raw[i:i + 2], "big"), "") for i in range(0, len(raw), 2))


def show_operations(pdf):
    """Each show-text operation of the PDF: page, text, device x (None unless positioned), baseline y, size, face.

    A small content-stream interpreter instead of pypdf's text extraction, whose reported positions
    depend on the pypdf version and lag behind after list markers and images. Fonts are the Type0
    fonts both engines embed, decoded through their ToUnicode maps."""
    from pypdf import PdfReader
    from pypdf.generic import ContentStream
    shows = []
    reader = PdfReader(str(pdf))
    for index, page in enumerate(reader.pages):
        fonts = {}
        for name, ref in page["/Resources"].get_object().get("/Font", {}).items():
            font = ref.get_object()
            fonts[name] = (str(font.get("/BaseFont", "")), to_unicode_map(font))
        contents = page.get_contents()
        if contents is None:
            continue
        state = {"ctm": IDENTITY, "tm": IDENTITY, "tlm": IDENTITY, "font": None, "size": 0.0,
                 "positioned": False, "leading": 0.0}
        stack = []

        def show(raw, index=index, state=state):
            base, mapping = fonts.get(state["font"], ("", None))
            matrix = multiply(state["tm"], state["ctm"])
            size = state["size"] * (matrix[2] ** 2 + matrix[3] ** 2) ** 0.5
            face = ("B" if "Bold" in base else "") + ("I" if "Oblique" in base or "Italic" in base else "")
            shows.append((index, decode_string(raw, mapping), round(matrix[4], 2) if state["positioned"] else None,
                          round(matrix[5], 2), round(size, 2), face or "R"))
            state["positioned"] = False

        def move(tx, ty, state=state):
            state["tlm"] = multiply((1.0, 0.0, 0.0, 1.0, tx, ty), state["tlm"])
            state["tm"] = state["tlm"]
            state["positioned"] = True

        for operands, operator in ContentStream(contents, reader).operations:
            op = operator.decode("latin-1") if isinstance(operator, bytes) else str(operator)
            if op == "q":
                stack.append(state["ctm"])
            elif op == "Q" and stack:
                state["ctm"] = stack.pop()
            elif op == "cm":
                state["ctm"] = multiply(tuple(float(v) for v in operands), state["ctm"])
            elif op == "BT":
                state["tm"] = state["tlm"] = IDENTITY
                state["positioned"] = True
            elif op == "Tf":
                state["font"], state["size"] = str(operands[0]), float(operands[1])
            elif op == "Tm":
                state["tm"] = state["tlm"] = tuple(float(v) for v in operands)
                state["positioned"] = True
            elif op == "Td":
                move(float(operands[0]), float(operands[1]))
            elif op == "TD":
                state["leading"] = -float(operands[1])
                move(float(operands[0]), float(operands[1]))
            elif op == "TL":
                state["leading"] = float(operands[0])
            elif op == "T*":
                move(0.0, -state["leading"])
            elif op == "Tj":
                show(raw_bytes(operands[0]))
            elif op == "TJ":
                show(b"".join(raw_bytes(item) for item in operands[0] if not isinstance(item, (int, float))))
            elif op in ("'", '"'):
                move(0.0, -state["leading"])
                show(raw_bytes(operands[-1]))
    return shows


def measure_pdf(pdf, words):
    """Page, baseline y, x (only when the word starts a show operation), font size and face of each word."""
    shows = show_operations(pdf)
    stream = "".join(show[1] for show in shows)
    owner, first = [], []
    for position, show in enumerate(shows):
        owner.extend([position] * len(show[1]))
        first.extend([True] + [False] * (len(show[1]) - 1) if show[1] else [])
    found = {}
    for word in words:
        start = stream.find(word)  # the probe's words are distinct, and adjacent cells or markers run together
        if start < 0:
            continue
        page, _, x, y, size, face = shows[owner[start]]
        found[word] = {"page": page, "y": y, "x": x if first[start] else None, "size": size, "face": face}
    return found


def metrics(found, words):
    """The comparable numbers and labels of one render, keyed by metric name.

    top and left are the first word's baseline and x; gap is the baseline distance between consecutive
    words (before, at and after the element); dx is a word's x relative to the first word, so an indent
    shows once and a shared page offset (the body margin) only in left."""
    result = {}
    present = [word for word in words if word in found]
    for word in words:
        if word not in found:
            result[f"missing:{word}"] = "missing"
    if present:
        result["top"] = found[present[0]]["y"]
        if found[present[0]]["x"] is not None:
            result["left"] = found[present[0]]["x"]
    for before, after in zip(present, present[1:]):
        a, b = found[before], found[after]
        result[f"gap:{before}-{after}"] = round(a["y"] - b["y"], 2) if a["page"] == b["page"] else "page"
    for word in present:
        entry = found[word]
        if entry["x"] is not None and found[present[0]]["x"] is not None and word != present[0]:
            result[f"dx:{word}"] = round(entry["x"] - found[present[0]]["x"], 2)
        result[f"size:{word}"] = entry["size"]
        result[f"face:{word}"] = entry["face"]
    return result


def render_chrome(chrome, corpus, probe, work, fonts_dir):
    folder = work / ("chrome-" + probe["id"])
    folder.mkdir(parents=True)
    faces = ""
    for file, (weight, style) in FACES.items():
        shutil.copyfile(fonts_dir / file, folder / file)
        faces += f"@font-face{{font-family:F;src:url('{file}');font-weight:{weight};font-style:{style}}}"
    page = folder / "probe.html"
    page.write_text(probe_document(corpus, probe, faces + corpus["pageStyle"] + corpus["authoredStyle"]), encoding="utf-8")
    (folder / corpus["imageName"]).write_bytes(png_bytes())
    pdf = folder / "probe.pdf"
    subprocess.run(chrome_args(chrome, folder / "profile") +
                   ["--no-pdf-header-footer", f"--print-to-pdf={pdf}", page.as_uri()],
                   capture_output=True, text=True, timeout=120)
    if not pdf.exists():
        raise RuntimeError(f"Chrome printed no PDF for {probe['id']}")
    return pdf


def render_factspdf(cli, corpus, probe, work, fonts_dir):
    folder = work / ("factspdf-" + probe["id"])
    folder.mkdir(parents=True)
    page = folder / "probe.html"
    page.write_text(probe_document(corpus, probe, corpus["pageStyle"] + corpus["authoredStyle"]), encoding="utf-8")
    image = folder / corpus["imageName"]
    image.write_bytes(png_bytes())
    pdf = folder / "probe.pdf"
    command = ["dotnet", cli] if cli.endswith(".dll") else [cli]
    flags = [item for flag, file in zip(CLI_FONT_FLAGS, FACES) for item in (flag, str(fonts_dir / file))]
    result = subprocess.run(command + [str(page), str(pdf), "--overwrite", "--strict=false", *flags,
                                       "--image", f"{corpus['imageName']}={image}"],
                            capture_output=True, text=True, timeout=120)
    if result.returncode or not pdf.exists():
        message = (result.stderr or result.stdout).strip().splitlines()
        return None, "error: " + (message[0][:90] if message else f"exit {result.returncode}")
    return pdf, None


def measure_probe(chrome, cli, corpus, probe, work, fonts_dir):
    words = probe_words(probe["body"])
    expected = metrics(measure_pdf(render_chrome(chrome, corpus, probe, work, fonts_dir), words), words)
    pdf, error = render_factspdf(cli, corpus, probe, work, fonts_dir)
    actual = {"render": error} if error else metrics(measure_pdf(pdf, words), words)
    return probe["id"], expected, actual


def close(expected, actual, tolerance):
    if isinstance(expected, (int, float)) and isinstance(actual, (int, float)):
        return abs(expected - actual) <= tolerance
    return expected == actual


def geometry_mismatches(expected, actual):
    """(metric, chrome, factspdf) for every metric that differs beyond the tolerance."""
    result = []
    if "render" in actual:
        return [("render", "rendered", actual["render"])]
    for metric in sorted(set(expected) | set(actual)):
        want, got = expected.get(metric, "absent"), actual.get(metric, "absent")
        if metric.startswith(("dx:", "left")) and "absent" in (want, got):
            continue  # the text did not start a run on one side, so its x cannot be compared
        if not close(want, got, MISMATCH_TOLERANCE):
            result.append((metric, want, got))
    return result


def same_difference(entry, probe, metric, chrome, factspdf):
    return (entry["probe"] == probe and entry["metric"] == metric and
            close(entry["chrome"], chrome, KNOWN_TOLERANCE) and close(entry["factspdf"], factspdf, KNOWN_TOLERANCE))


def check_geometry(found, known):
    """Returns (unexpected, stale): differences not listed, and listed differences that no longer occur."""
    unexpected = [m for m in found
                  if not any(same_difference(entry, *m) for entry in known)]
    stale = [entry for entry in known
             if not any(same_difference(entry, *m) for m in found)]
    return unexpected, stale


def write_known(known_document):
    def section(name):
        entries = known_document.get(name, [])
        return f' "{name}":[\n' + ",\n".join("  " + compact(entry) for entry in entries) + "\n ]"
    text = "{\n" + ",\n".join(section(name) for name in ("computed", "geometry")) + "\n}\n"
    (FIXTURES / "known-differences.json").write_text(text, encoding="utf-8", newline="\n")


def update_geometry_section(known_document, found):
    reasons = {(e["probe"], e["metric"]): e["reason"] for e in known_document.get("geometry", [])}
    known_document["geometry"] = [
        {"probe": probe, "metric": metric, "chrome": chrome, "factspdf": factspdf,
         "reason": reasons.get((probe, metric), "unclassified")}
        for probe, metric, chrome, factspdf in found]
    write_known(known_document)


# ---------------------------------------------------------------- main

def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--cli", required=True, help="the built FactsPDF CLI (executable or FactsPDF.Cli.dll)")
    parser.add_argument("--chrome", help="Chrome or Chromium binary (default: FACTSPDF_CHROME, else found on PATH)")
    parser.add_argument("--fonts-dir", default=os.environ.get("FACTSPDF_DEJAVU_DIR", "/usr/share/fonts/truetype/dejavu"),
                        help="directory with the four DejaVu Sans faces (default: FACTSPDF_DEJAVU_DIR or the Ubuntu path)")
    parser.add_argument("--work", help="working directory (default: a temporary one)")
    parser.add_argument("--workers", type=int, default=4)
    parser.add_argument("--snapshot-only", action="store_true", help="check (or update) the Chrome snapshot and skip the geometry layer")
    parser.add_argument("--update-snapshot", action="store_true", help="rewrite chrome-computed.json from the live Chrome")
    parser.add_argument("--update-known", action="store_true", help="rewrite the geometry section of known-differences.json")
    args = parser.parse_args(argv)

    chrome = find_chrome(args.chrome)
    cli = str(Path(args.cli).resolve())
    fonts_dir = Path(args.fonts_dir).resolve()
    missing = [file for file in FACES if not (fonts_dir / file).is_file()]
    if missing:
        raise RuntimeError(f"Missing DejaVu faces in {fonts_dir}: {', '.join(missing)}")
    corpus = load_json("corpus.json")
    keep = tempfile.TemporaryDirectory(prefix="ua-defaults-") if not args.work else None
    work = Path(args.work or keep.name).resolve()
    work.mkdir(parents=True, exist_ok=True)
    failures = []

    live = live_snapshot(chrome, corpus, work, args.workers)
    snapshot_path = FIXTURES / "chrome-computed.json"
    if args.update_snapshot:
        write_snapshot(live)
        print(f"Wrote {len(live)} elements to {snapshot_path.name}")
    else:
        problems = diff_snapshot(load_json("chrome-computed.json")["elements"], live)
        print(f"Chrome snapshot: {len(live)} elements, {len(problems)} differences")
        if problems:
            failures.append("The committed Chrome snapshot differs from the live Chrome "
                            "(run with --update-snapshot after reviewing):\n  " + "\n  ".join(problems))

    if args.snapshot_only:
        if keep:
            keep.cleanup()
        if failures:
            print(FAILURE_SEPARATOR.join(failures), file=sys.stderr)
        return 1 if failures else 0

    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        results = list(pool.map(lambda probe: measure_probe(chrome, cli, corpus, probe, work, fonts_dir),
                                corpus["probes"]))
    found = [(probe, metric, chrome_value, factspdf_value)
             for probe, expected, actual in results
             for metric, chrome_value, factspdf_value in geometry_mismatches(expected, actual)]
    known_document = load_json("known-differences.json")
    if args.update_known:
        update_geometry_section(known_document, found)
        print(f"Wrote {len(found)} geometry differences to known-differences.json")
    else:
        known = known_document.get("geometry", [])
        unexpected, stale = check_geometry(found, known)
        unclassified = [e for e in known if e["reason"] in ("", "unclassified")]
        clean = len({probe for probe, *_ in found})
        print(f"Geometry: {len(results)} probes, {len(results) - clean} identical to Chrome, "
              f"{len(found)} differences ({len(known)} listed)")
        if unexpected:
            failures.append("Geometry differences not in known-differences.json:\n  " +
                            "\n  ".join(f"{p} {m}: Chrome {c}, FactsPDF {f}" for p, m, c, f in unexpected))
        if stale:
            failures.append("Listed geometry differences that no longer occur (remove them):\n  " +
                            "\n  ".join(f"{e['probe']} {e['metric']}" for e in stale))
        if unclassified:
            failures.append("Geometry differences without a reason:\n  " +
                            "\n  ".join(f"{e['probe']} {e['metric']}" for e in unclassified))
    if keep:
        keep.cleanup()
    if failures:
        print(FAILURE_SEPARATOR.join(failures), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
