"""Validate local package metadata and license bytes; never publish an artifact."""
from __future__ import annotations

from pathlib import Path
from xml.etree import ElementTree as ET
from zipfile import ZipFile

root = Path(__file__).resolve().parents[1]
packages = list((root / "artifacts/packages").glob("FactsPDF.*.nupkg"))
assert len(packages) == 1, f"Expected exactly one local package, got {packages}"
with ZipFile(packages[0]) as package:
    names = package.namelist()
    assert package.read("LICENSE.md") == (root / "LICENSE.md").read_bytes(), "License changed during packing"
    assert package.read("README.md") == (root / "README.md").read_bytes(), "README missing or incorrect"
    nuspecs = [name for name in names if name.endswith(".nuspec")]
    assert len(nuspecs) == 1
    doc = ET.fromstring(package.read(nuspecs[0]))
    licenses = [node for node in doc.iter() if node.tag.split("}")[-1] == "license"]
    assert len(licenses) == 1 and licenses[0].attrib.get("type") == "file"
    assert licenses[0].text == "LICENSE.md"
    dependencies = [node for node in doc.iter() if node.tag.split("}")[-1] == "dependency"]
    assert not dependencies, f"Unexpected runtime package dependencies: {dependencies}"
print("Local package contains the exact license, README and no runtime package dependencies. Nothing was published.")
