#!/usr/bin/env python3
"""Cross-check an expanded, pinned-WPT-pattern CSS syntax corpus against real Chrome CSSOM.

Browser-side parsing is independent of FactsPDF; this is a syntax-structure
projection, NOT a complete WPT run, browser cascade/layout oracle, or a general
percentage of CSS coverage. The browser is used only in this CI script.
"""
from collections import Counter
from html.parser import HTMLParser
import html
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import tempfile

WPT_REV = "521d168d63dbd206ea7fbe0b74ddd5760e6e668e"
WPT_DIR = "css/css-syntax/"
SOURCE = {
    "numbers": "decimal-points-in-numbers.html",
    "urls": "url-whitespace-consumption.html",
    "unicode": "non-ascii-codepoints.html",
    "escapes": "escaped-eof.html",
    "nested": "invalid-nested-rules.html",
    "at-rules": "at-rule-in-declaration-list.html",
    "variables": "var-with-blocks.html",
    "unclosed": "unclosed-constructs.html",
    "recovery": "at-rule-in-declaration-list.html",
}
def cases():
    output = []
    def add(family, context, css, wpt=False):
        output.append({
            "id": f"{family}-{len(output):03d}",
            "family": family,
            "context": context,
            "css": css,
            "reference": WPT_DIR + SOURCE[family] if wpt and family in SOURCE else None,
            "derivation": "adapted WPT behavior pattern" if wpt else "generated complementary case"
        })
    selectors = [
        "p", ".note", "#summary", "section p", "div > p", "h1, h2",
        "article.note > p", "p.note.strong", ":is(p, .note)",
        ":where(.note)", "p:not(.disabled)", "section:has(p)"
    ]
    properties = [
        "color:red", "color:#12aaff", "font-size:12px", "width:40%",
        "opacity:.5", "display:grid", "background-color:blue",
        "line-height:1.25"
    ]
    # Generated, independent CSSOM/AST rule structures: 12 x 8 = 96.
    for selector in selectors:
        for value in properties:
            add("rules", "stylesheet", selector + "{" + value + "}")
    # Decimal syntax patterns, adapted from WPT decimals / dimensions.
    for value in ["1", "1.0", ".1", "0.25", "1e0", "1E2", "+2", "1.5",
                  "0", "2e+0", "1e-1", "2.75"]:
        add("numbers", "inline", "line-height:" + value, True)
    for value in ["1px", ".5em", "1.25pt", "2e1px", "3%", "0"]:
        add("numbers", "inline", "width:" + value, True)
    # Exact WPT URL whitespace variations plus additional valid URL forms.
    for value in ['url("foo")', 'url( "foo")', 'url("foo" )',
                  "url(foo.png)", "url( foo.png )", "url(a\\)b)",
                  "url(test.svg)", 'url("a b.svg")',
                  "url(abc-def)", "url(a/b/c.png)"]:
        add("urls", "inline", "background-image:" + value, True)
    # Escaped identifiers and non-ASCII names; no URL/network access.
    for css in [".café{color:red}", ".中文{color:red}", "#über{color:red}",
                ".\\61{color:blue}", ".\\6eote{color:red}",
                "café{width:2px}", ".a\\+b{color:green}",
                ".\\31 23{color:blue}", ".foo\\:bar{color:blue}",
                ".\\1F600{color:blue}", ".aa\\26 bb{color:blue}"]:
        add("unicode", "stylesheet", css, True)
    for css in ["col\\6fr:red", "--Theme:var(--x, red)", "--theme:blue",
                "--name:hello\\", "color:blue", "--文字:green"]:
        add("escapes", "inline", css, True)
    # Comment token boundaries, including no invented space.
    for sep in ["", " ", "\n", "/*x*/", "/**/", " /*x*/ ", "/*x*/\n"]:
        add("comments", "stylesheet", "p" + sep + ".note{color:red}")
        add("comments", "inline", "color:" + sep + "red")
    # Nested component grammar held as valid custom properties.
    for value in ["var(--a)", "var(--a, red)", "var(--a, {x:y})",
                  "calc(100% - 2px)", "'hello'", '"a b"',
                  "(1 + 2)", "[x, y]", "{a:b}", "var(--a, var(--b, red))"]:
        add("variables", "inline", "--payload:" + value, True)
    for value in ["var(--a)", "calc(100% - 2px)", "var(--a, red)",
                  "min(1px, 2px)", "clamp(1px, 50%, 100px)"]:
        add("variables", "inline", "width:" + value, True)
    # At-rules and nested rule trees, neither evaluated nor fetched.
    for prefix in ["@media print", "@media screen", "@media all",
                   "@supports (display: grid)", "@supports (color: red)"]:
        for rule in ["p{color:red}", "p{color:red}div{color:blue}",
                     ".note{width:10px}"]:
            add("at-rules", "stylesheet", prefix + "{" + rule + "}", True)
    for selector in [".a", "p", "article", "#item"]:
        for child in ["& .b", "&>span", "&:hover"]:
            add("nested", "stylesheet", selector + "{color:red;" +
                child + "{color:blue}}", True)
    # WPT unclosed constructs (only browser-comparable parsed structures).
    for value in ["p{color:red", "p{color:blue",
                  "p{width:12px", "p{opacity:.5",
                  "p{color:red;font-size:12px",
                  "p{--payload:var(--a)", "p{display:grid"]:
        add("unclosed", "stylesheet", value, True)
    # CSS recovery: malformed declaration followed by an intact declaration.
    for bad in ["invalid value", "!invalid", "*color:red",
                "missing", ":bad", "/bad", "color",
                "@unknown x;", "broken()"]:
        add("recovery", "inline", bad + "; color:red", True)
    # Source dedup is intentional: even test inputs must be distinct.
    assert len(output) >= 175, len(output)
    assert len({(x["context"], x["css"]) for x in output}) == len(output), "duplicate case"
    return output


class PreReader(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.depth = 0
        self.text = []
    def handle_starttag(self, tag, attrs):
        if tag == "pre" and ("id", "results") in attrs: self.depth += 1
    def handle_endtag(self, tag):
        if tag == "pre" and self.depth: self.depth -= 1
    def handle_data(self, data):
        if self.depth: self.text.append(data)


SCRIPT = r"""
"use strict";
const input = JSON.parse(document.getElementById("cases").textContent);
function decls(style) {
  if (!style) return [];
  return Array.from(style).map(name => ({
    name: name,
    important: style.getPropertyPriority(name) === "important"
  }));
}
function rules(items) {
  return Array.from(items).map(rule => {
    const className = rule.constructor.name;
    let kind;
    if (className === "CSSStyleRule") kind = "style";
    else if (className === "CSSMediaRule") kind = "@media";
    else if (className === "CSSSupportsRule") kind = "@supports";
    else kind = "@unsupported:" + className;
    return {
      kind: kind,
      declarations: decls(rule.style),
      children: rule.cssRules ? rules(rule.cssRules) : []
    };
  });
}
const result = [];
for (const item of input) {
  try {
    let data;
    if (item.context === "stylesheet") {
      const sheet = new CSSStyleSheet();
      sheet.replaceSync(item.css);
      data = { rules: rules(sheet.cssRules) };
    } else {
      const el = document.createElement("span");
      el.style.cssText = item.css;
      data = { declarations: decls(el.style) };
    }
    result.push({id:item.id, data:data});
  } catch(error) {
    result.push({id:item.id, error:String(error)});
  }
}
document.getElementById("results").textContent = JSON.stringify(result);
"""


def chrome_oracle(binary, entries, work):
    embedded = json.dumps(entries, ensure_ascii=False, separators=(",", ":"))
    # Cannot allow JSON to close the non-executable <script type=json> element.
    embedded = embedded.replace("<", "\\u003c").replace(">", "\\u003e").replace("&", "\\u0026")
    content = ("<!doctype html><html><meta charset=utf-8><title>CSS Syntax oracle</title>"
               '<script type="application/json" id="cases">' + embedded + "</script>"
               '<pre id="results"></pre><script>' + SCRIPT + "</script></html>")
    page = work / "reference.html"
    page.write_text(content, encoding="utf-8")
    args = [binary, "--headless=new", "--no-sandbox", "--disable-gpu",
            "--disable-dev-shm-usage", "--disable-background-networking",
            "--disable-extensions", "--no-first-run",
            "--user-data-dir=" + str(work / "chrome-profile"),
            "--virtual-time-budget=5000", "--dump-dom", page.as_uri()]
    proc = subprocess.run(args, capture_output=True, text=True, timeout=80)
    if proc.returncode:
        raise RuntimeError("Chrome failed: " + proc.stderr[-2500:])
    pre = PreReader()
    pre.feed(proc.stdout)
    if not pre.text:
        raise RuntimeError("Chrome did not return oracle JSON; stderr: " + proc.stderr[-1500:])
    decoded = json.loads("".join(pre.text))
    if len(decoded) != len(entries):
        raise AssertionError("Browser oracle omitted cases")
    return decoded


def main():
    if len(sys.argv) != 3:
        raise SystemExit("Usage: css_syntax_browser_differential.py <probe-dll> <artifact-dir>")
    probe = Path(sys.argv[1]).resolve()
    target = Path(sys.argv[2]); target.mkdir(parents=True, exist_ok=True)
    browser = next((shutil.which(x) for x in ("google-chrome", "chromium", "chromium-browser") if shutil.which(x)), None)
    if not browser:
        raise RuntimeError("CI must provide a real Chrome/Chromium browser")
    browser_version = subprocess.check_output([browser, "--version"], text=True).strip()
    entries = cases()
    with tempfile.TemporaryDirectory(prefix="factspdf-css-oracle-") as temp:
        work = Path(temp)
        fixture = work / "cases.json"
        fixture.write_text(json.dumps(entries, ensure_ascii=False), encoding="utf-8")
        browser_data = chrome_oracle(browser, entries, work)
        engine_process = subprocess.run(["dotnet", str(probe), str(fixture)],
                                        capture_output=True, text=True, timeout=80)
        if engine_process.returncode:
            raise RuntimeError("FactsPDF probe failed: " + engine_process.stderr[-2000:])
        engine_data = json.loads(engine_process.stdout)
    a = {item["id"]: item for item in browser_data}
    b = {item["id"]: item for item in engine_data}
    categories = {}
    mismatches = []
    for item in entries:
        name = item["family"]
        stat = categories.setdefault(name, {"passed": 0, "failed": 0, "wpt_pattern_cases": 0})
        if item["reference"]: stat["wpt_pattern_cases"] += 1
        oracle = a[item["id"]]
        actual = b[item["id"]]
        if oracle.get("error") is None and oracle.get("data") == actual.get("data"):
            stat["passed"] += 1
        else:
            stat["failed"] += 1
            mismatches.append({
                "id": item["id"], "family": name, "context": item["context"],
                "css": item["css"], "reference": item["reference"],
                "browser": oracle, "factspdf": actual
            })
    report = {
        "wpt_revision": WPT_REV,
        "scope": "Deterministic WPT-pattern-inspired plus generated CSSOM syntax-tree structural projections. Not the full upstream WPT harness, computed-style equivalence, or PDF rendering compatibility.",
        "browser_version": browser_version,
        "dotnet_probe": str(probe.name),
        "runner_os": platform.platform(),
        "cases": len(entries),
        "wpt_pattern_cases": sum(1 for x in entries if x["reference"]),
        "passed": sum(x["passed"] for x in categories.values()),
        "failed": len(mismatches),
        "families": categories,
        "mismatches": mismatches,
        "note": "CSSOM discards invalid declarations and performs property validation; AST preserves syntactically valid unknown properties. Only directly comparable projections are used. Detailed failures remain in this artifact."
    }
    dest = target / "browser-differential.json"
    dest.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k:report[k] for k in ("browser_version","cases","wpt_pattern_cases","passed","failed","families")}, indent=2))
    for case in mismatches[:8]:
        print("DIFF", case["id"], case["css"], case["browser"], case["factspdf"], file=sys.stderr)
    if mismatches:
        raise AssertionError(str(len(mismatches)) + " browser oracle disagreements")


if __name__ == "__main__":
    main()
