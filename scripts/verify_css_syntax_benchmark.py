#!/usr/bin/env python3
"""Validate the narrow, native CSS syntax measurement without performance claims."""
import json
from pathlib import Path
import sys

record = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
assert record["dynamic_code_supported"] is False, "Bench must execute as Native AOT"
assert record["rules"] == 96
assert record["warmups"] == 10 and record["samples"] == 30
assert record["css_utf16_characters"] > 1000
assert record["median_ms"] >= 0
assert record["median_current_thread_allocated_bytes"] >= 0
assert record["peak_process_working_set_bytes"] > 0
assert len(record["raw"]) == record["samples"]
assert all(x["ms"] >= 0 and x["current_thread_allocated_bytes"] >= 0 for x in record["raw"])
assert "Excludes HTML, cascade, layout and PDF" in record["scope"]
print("Native AOT CSS syntax measurement validated: "
      + str(record["samples"]) + " samples, median "
      + str(record["median_ms"]) + " ms")
