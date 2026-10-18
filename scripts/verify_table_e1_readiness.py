"""Verify authored E1 fixture arithmetic and CURRENT rejection, never table support."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dotnet", required=True)
    parser.add_argument("--cli-dll", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    root = Path("tests/fixtures/tables-e1")
    manifest = json.loads((root / "manifest.json").read_text())
    assert manifest["status"] == "proposed-not-implemented"
    args.output.mkdir(parents=True, exist_ok=True)
    records = []
    for case in manifest["cases"]:
        source = root / case["file"]
        assert hashlib.sha256(source.read_bytes()).hexdigest() == case["sha256"], "fixture hash"
        proposal = case["proposed"]
        if "code" not in proposal:
            widths = proposal["column_outer_widths"]
            assert all(width > 0 for width in widths) and sum(widths) == proposal["table_width"]
            decoration = 2 * (proposal["cell_padding"] + proposal["cell_border"])
            assert [width - decoration for width in widths] == proposal["content_widths"]
            assert all(len(row) == len(widths) for row in proposal["row_line_counts"])
            heights = [max(row) * proposal["line_height"] + decoration for row in proposal["row_line_counts"]]
            assert heights == proposal["row_heights"] and sum(heights) == proposal["table_height"]
        target = args.output / (case["id"] + ".pdf")
        target.write_bytes(b"unchanged-e1-sentinel")
        result = subprocess.run([args.dotnet, str(args.cli_dll), str(source), str(target), "--overwrite"],
                                capture_output=True, timeout=30)
        assert result.returncode == 3 and case["current_expected_code"].encode() in result.stderr, result.stderr.decode()
        assert target.read_bytes() == b"unchanged-e1-sentinel", "current rejection changed destination"
        target.unlink()
        records.append({"id": case["id"], "inputSha256": case["sha256"], "actualCode": case["current_expected_code"],
                        "outputUnchanged": True, "futureOutcome": proposal.get("code", "proposed-success")})
    result = {"status": "readiness-only-no-table-support", "cases": records,
              "sourceHead": subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip(),
              "dirty": bool(subprocess.check_output(["git", "status", "--porcelain"]).strip()),
              "designApproval": "pending-colgroup-contract-review"}
    (args.output / "results.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps({"readinessCases": len(records), "tableFunctionalityVerified": False}))


if __name__ == "__main__": main()
