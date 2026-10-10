#!/usr/bin/env python3
"""Summarise a deliberately selected, WPT-inspired NUnit syntax suite."""
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

trx = Path(sys.argv[1])
destination = Path(sys.argv[2])
root = ET.parse(trx).getroot()
ns = {"v": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
results = root.findall(".//v:UnitTestResult", ns)
if not results:
    raise AssertionError("CSS syntax conformance TRX contains no test results")

features = {}
for item in results:
    name = item.attrib["testName"]
    if "CssSyntaxWpt__" not in name:
        continue
    suffix = name.split("CssSyntaxWpt__", 1)[1]
    feature = suffix.split("__", 1)[0]
    outcome = item.attrib["outcome"]
    entry = features.setdefault(feature, {"passed": 0, "failed": 0, "skipped": 0})
    field = ("passed" if outcome == "Passed" else
             "skipped" if outcome in ("NotExecuted", "Inconclusive") else "failed")
    entry[field] += 1
    entry.setdefault("cases", []).append({"name": name, "outcome": outcome})

if sum(v["passed"] + v["failed"] + v["skipped"] for v in features.values()) != 12:
    raise AssertionError("Expected exactly 12 selected syntax cases: " + repr(features))

report = {
    "scope": "12 original NUnit assertions inspired by selected pinned WPT paths; not the full WPT suite",
    "wpt_revision": "521d168d63dbd206ea7fbe0b74ddd5760e6e668e",
    "normative_spec": "W3C CSS Syntax Level 3 CRD 2026-10-01",
    "features": features,
    "warning": "Only a selected syntax corpus was tested; this is not a browser CSS layout score or W3C certification."
}
destination.parent.mkdir(parents=True, exist_ok=True)
destination.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print(json.dumps({
    key: {name: value[name] for name in ("passed", "failed", "skipped")}
    for key, value in features.items()
}, indent=2))
if any(value["failed"] or value["skipped"] for value in features.values()):
    sys.exit(1)
