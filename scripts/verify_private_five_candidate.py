"""Independently verify the five *actual* staged private payloads and read back their final manifest.

This is a CI/local technical acceptance gate, not a public release, human PDF
approval, a font distribution, or a commercial evaluation licence. It operates
outside the source checkout and never uploads software.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tarfile
import zipfile

from private_five_finalization import EXPECTED, REQUIRED_CHECKS, frozen_contract_digest
from verify_package import binary_reader, current_source, validate_package

VERSION = "0.1.0-preview.1"
MANAGED = {
    "FactsPDF.Cli/FactsPDF.Cli.dll", "FactsPDF.Cli/FactsPDF.dll",
    "FactsPDF.Cli/FactsPDF.Cli.deps.json",
    "FactsPDF.Cli/FactsPDF.Cli.runtimeconfig.json",
    "FactsPDF.Cli/LICENSE.md", "FactsPDF.Cli/CLI-README.md",
}
NATIVE = {
    "FactsPDF.Native/FactsPDF.Cli", "FactsPDF.Native/LICENSE.md",
    "FactsPDF.Native/CLI-README.md",
    "FactsPDF.Native/notices/dotnet/LICENSE.TXT",
    "FactsPDF.Native/notices/dotnet/THIRD-PARTY-NOTICES.TXT",
}
EXAMPLES = {
    "FactsPDF.Examples/first-use/" + name for name in (
        "FirstUse.csproj", "Program.cs", "NuGet.Config",
        "input.html", "unsupported.html", "expected-pages.json")
} | {
    "FactsPDF.Examples/global.json", "FactsPDF.Examples/LICENSE.md",
    "FactsPDF.Examples/README.md", "FactsPDF.Examples/EXAMPLES-MANIFEST.json",
}
HEX = re.compile(r"[0-9a-f]{64}\Z")


def require(condition: bool, description: str) -> None:
    if not condition:
        raise ValueError(description)


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def file_bytes(path: Path, maximum: int = 256 * 1024 * 1024) -> bytes:
    require(not path.is_symlink() and path.is_file(), "Missing or symlinked file: " + str(path))
    require(0 < path.stat().st_size <= maximum, "Invalid file byte budget: " + str(path))
    data = path.read_bytes()
    require(len(data) == path.stat().st_size, "File changed during read: " + str(path))
    return data


def load(path: Path) -> dict:
    data = json.loads(file_bytes(path, 4 * 1024 * 1024))
    require(isinstance(data, dict), "Expected JSON object: " + str(path))
    return data


def run(*args: str) -> str:
    result = subprocess.run(args, capture_output=True, text=True, timeout=120)
    require(result.returncode == 0, "Command failed: " + " ".join(args) + " " + result.stderr[-600:])
    return result.stdout


def pdf_witness(path: Path, pages: int, markers: tuple[str, ...]) -> dict:
    data = file_bytes(path, 16 * 1024 * 1024)
    require(data.startswith(b"%PDF-"), "Not a PDF: " + str(path))
    run("qpdf", "--check", str(path))
    info = run("pdfinfo", str(path))
    match = re.search(r"(?m)^Pages:\s+(\d+)\s*$", info)
    require(match is not None and int(match.group(1)) == pages, "Wrong PDF page count: " + str(path))
    extracted = run("pdftotext", "-layout", str(path), "-")
    for marker in markers:
        require(marker in extracted, "Missing searchable PDF text: " + marker)
    return {"bytes": len(data), "sha256": digest(data), "pages": pages, "markers": list(markers)}


def safe_member(name: str) -> None:
    require(bool(name) and name[0] != "/" and "\\" not in name and ":" not in name
            and all(piece not in ("", ".", "..") for piece in name.split("/")),
            "Unsafe archive member: " + name)


def zip_inventory(path: Path, role: str) -> tuple[list[dict], dict[str, bytes]]:
    budget = {"package": 64, "managed": 64, "native": 96, "examples": 4}[role] * 1024 * 1024
    found: dict[str, bytes] = {}
    records = []
    total = 0
    with zipfile.ZipFile(path) as archive:
        infos = archive.infolist()
        require(len(infos) == len({i.filename for i in infos}), "Duplicate ZIP members")
        if role in ("managed", "native", "examples"):
            expected = {"managed": MANAGED, "native": NATIVE, "examples": EXAMPLES}[role]
            require({i.filename for i in infos} == expected, "Wrong private archive allowlist: " + role)
        for item in infos:
            safe_member(item.filename)
            require(not item.is_dir() and 0 < item.file_size <= budget, "Invalid ZIP member size")
            total += item.file_size
            require(total <= budget, "ZIP expanded size exceeded: " + role)
            if role in ("managed", "native", "examples"):
                mode = (item.external_attr >> 16)
                expected_mode = 0o100755 if item.filename == "FactsPDF.Native/FactsPDF.Cli" else 0o100644
                require(item.create_system == 3 and mode == expected_mode, "Unsafe ZIP member type/mode")
            raw = archive.read(item)
            require(len(raw) == item.file_size, "ZIP decompression changed member length")
            found[item.filename] = raw
            records.append({"path": item.filename, "bytes": len(raw), "sha256": digest(raw)})
        require(archive.testzip() is None, "ZIP CRC failed")
    return sorted(records, key=lambda x: x["path"]), found


def source_inventory(path: Path, head: str, extracted: Path) -> tuple[list[dict], dict[str, bytes]]:
    prefix = "FactsPDF." + VERSION + ".source/"
    records, contents = [], {}
    total = 0
    with tarfile.open(path, "r:gz") as archive:
        members = archive.getmembers()
        require(60 < len(members) <= 513 and members[0].isdir()
                and members[0].name == prefix[:-1], "Unexpected source root")
        for member in members[1:]:
            require(member.isfile() and member.name.startswith(prefix), "Unsafe source TAR entry")
            name = member.name[len(prefix):]
            safe_member(name)
            require(name not in contents and member.mode == 0o644
                    and 0 < member.size <= 8 * 1024 * 1024, "Invalid source member")
            total += member.size
            require(total <= 64 * 1024 * 1024, "Source expanded size exceeded")
            raw = archive.extractfile(member).read()
            require(len(raw) == member.size, "Incomplete source payload")
            contents[name] = raw
            records.append({"path": name, "bytes": len(raw), "sha256": digest(raw)})
    manifest = json.loads(contents["SOURCE-MANIFEST.json"])
    require(manifest["sourceHead"] == head and manifest["packageVersion"] == VERSION,
            "Corresponding source identity mismatch")
    declared = manifest["trackedMembers"]
    require(len(declared) == len({r["path"] for r in declared})
            and {r["path"] for r in declared} == set(contents) - {"SOURCE-MANIFEST.json", "SOURCE-BUILD.md"},
            "Incomplete corresponding source manifest")
    for rec in declared:
        name = rec["path"]
        require(rec["bytes"] == len(contents[name]) and rec["sha256"] == digest(contents[name]),
                "Source manifest file mismatch")
        require(run_bytes_git(head, name) == contents[name], "Source differs from committed Git object")
    require(not (extracted / ".git").exists()
            and (extracted / "SOURCE-MANIFEST.json").read_bytes() == contents["SOURCE-MANIFEST.json"],
            "Extracted source not isolated or wrong")
    return sorted(records, key=lambda x: x["path"]), contents


def run_bytes_git(head: str, path: str) -> bytes:
    root = Path(__file__).resolve().parents[1]
    return subprocess.check_output(["git", "cat-file", "blob", head + ":" + path],
                                   cwd=root, timeout=30)


def evidence_path(folder: Path, name: str, obj: dict) -> dict:
    filename = "reports/" + name + ".json"
    path = folder / filename
    raw = (json.dumps(obj, sort_keys=True, indent=2) + "\n").encode()
    path.write_bytes(raw)
    return {"status": "passed", "evidenceFile": filename,
            "bytes": len(raw), "sha256": digest(raw)}


def verify(root: Path, candidate: Path, runner: Path, output: Path) -> dict:
    require(not candidate.is_symlink() and not runner.is_symlink() and
            not output.is_symlink(), "Symlinked private delivery root")
    root = root.resolve(strict=True)
    candidate = candidate.resolve(strict=True)
    runner = runner.resolve(strict=True)
    require(candidate != root and root not in candidate.parents and candidate.parent == runner,
            "Expected isolated private candidate in runner")
    require(not output.exists() and output.parent.is_dir() and
            root not in output.resolve().parents and candidate not in output.resolve().parents,
            "Expected fresh external independent verification directory")
    head = current_source(root)
    staging_bytes = file_bytes(candidate / "candidate-staging.json", 1024 * 1024)
    staged = json.loads(staging_bytes)
    require(staged["sourceHead"] == head and staged["identity"]["profile"] == "local-linux"
            and staged["candidateBuilt"] is True and staged["candidateAssembled"] is True
            and staged["independentlyValidated"] is False and staged["windowsBinaryRetained"] is False,
            "Wrong staged candidate source or phase")
    require({p.name for p in candidate.iterdir()} ==
            set(EXPECTED.values()) | {"candidate-staging.json"},
            "Staged candidate contains missing/extra final files")
    by_role = {p["role"]: p for p in staged["payloads"]}
    require(len(by_role) == 5 and set(by_role) == set(EXPECTED), "Wrong candidate roles")
    inventories, unpacked, payloads = {}, {}, []
    for role, filename in EXPECTED.items():
        archive = candidate / filename
        raw = file_bytes(archive)
        record = by_role[role]
        require(record["filename"] == filename and record["bytes"] == len(raw)
                and record["sha256"] == digest(raw), "Staged payload identity changed: " + role)
        if role == "source":
            inventory, contents = source_inventory(
                archive, head, runner / "factspdf-private-source-extracted")
        else:
            inventory, contents = zip_inventory(archive, role)
        inventories[role], unpacked[role] = inventory, contents
        members = []
        binary = {
            "package": ["lib/net10.0/FactsPDF.dll"],
            "managed": ["FactsPDF.Cli/FactsPDF.Cli.dll", "FactsPDF.Cli/FactsPDF.dll"],
            "native": ["FactsPDF.Native/FactsPDF.Cli"],
        }.get(role, [])
        for name in binary:
            raw_member = contents[name]
            members.append({"path": name, "bytes": len(raw_member),
                            "sha256": digest(raw_member),
                            "identity": {"sourceHead": head, "version": VERSION, "role": role}})
        inventory_digest = digest(json.dumps(inventory, sort_keys=True, separators=(",", ":")).encode())
        payloads.append({"filename": filename, "bytes": len(raw),
                         "sha256": digest(raw), "inventoryDigest": inventory_digest,
                         "binaryMembers": members})

    examples_root = runner / "factspdf-staged-examples-extracted/FactsPDF.Examples"
    examples_manifest = json.loads(unpacked["examples"]["FactsPDF.Examples/EXAMPLES-MANIFEST.json"])
    require(examples_manifest["sourceHead"] == head and examples_manifest["version"] == VERSION,
            "Extracted examples source differs")
    for name, data in unpacked["examples"].items():
        require((examples_root / name.removeprefix("FactsPDF.Examples/")).read_bytes() == data,
                "Extracted example differs from final ZIP")

    managed_root = runner / "factspdf-staged-managed-extracted"
    native_root = runner / "factspdf-staged-native-extracted"
    for role, base in (("managed", managed_root), ("native", native_root)):
        for name, data in unpacked[role].items():
            require((base / name).read_bytes() == data, "Extracted executable archive differs: " + role)
    require(unpacked["native"]["FactsPDF.Native/FactsPDF.Cli"][:4] == b"\x7fELF",
            "Native Linux binary missing ELF signature")
    original_license = file_bytes(root / "LICENSE.md", 1024 * 1024)
    for role, member in (
        ("managed", "FactsPDF.Cli/LICENSE.md"),
        ("native", "FactsPDF.Native/LICENSE.md"),
        ("examples", "FactsPDF.Examples/LICENSE.md"),
    ):
        require(unpacked[role][member] == original_license,
                "Shipped license differs from committed license: " + role)
    # Independently frozen upstream Linux NativeAOT 10.0.13 notice hashes.
    # The runtime-pack texts are not the SDK's or compiler-pack's notices.
    for filename, expected in {
        "LICENSE.TXT": "cfc21f5e8bd655ae997eec916138b707b1d290b83272c02a95c9f821b8c87310",
        "THIRD-PARTY-NOTICES.TXT": "66f1d4e44973185519bb4aa8a9718eb22fc7af2cc532e3ae9cfc4c127ee7fc54",
    }.items():
        require(digest(unpacked["native"]["FactsPDF.Native/notices/dotnet/" + filename]) == expected,
                "Native runtime-pack legal notice differs from independently pinned bytes: " + filename)
    require(unpacked["managed"]["FactsPDF.Cli/FactsPDF.dll"] ==
            unpacked["package"]["lib/net10.0/FactsPDF.dll"],
            "Managed and installed library assemblies differ")

    build = load(runner / "factspdf-private-build/private-build.json")
    inspector_paths = list((runner / "factspdf-private-build/inspector/bin").rglob("FactsPDF.PackageInspector.dll"))
    require(build["sourceHead"] == head and len(inspector_paths) == 1, "Wrong compiled inspector")
    inspector = inspector_paths[0]
    inspector_rel = inspector.relative_to(runner / "factspdf-private-build").as_posix()
    inspector_record, = (item for item in build["files"] if item["path"] == inspector_rel)
    require(inspector_record["sha256"] == digest(file_bytes(inspector))
            and inspector_record["bytes"] == inspector.stat().st_size,
            "Compiled inspector not source-bound")
    package_meta = validate_package(candidate / EXPECTED["package"], root, VERSION, head,
                                    "net10.0", assembly_reader=binary_reader(inspector, "dotnet"))
    require(package_meta["assembly"] == unpacked["managed"]["FactsPDF.Cli/FactsPDF.dll"],
            "Inspected NuGet PE is not consumed assembly")

    source_pdf = pdf_witness(runner / "factspdf-private-source-rebuilt.pdf", 1,
                             ("SERVICE REPORT", "Example Client", "Records reviewed"))
    require((runner / "factspdf-private-source-extracted/src/FactsPDF.Cli/bin/Release/net10.0/FactsPDF.Cli.dll").is_file(),
            "No real rebuilt source CLI")
    consumer_dir = runner / "factspdf-staged-five-installed-consumer"
    installed = load(consumer_dir / "report.json")
    require(installed["sourceHead"] == head and installed["sdk"] == "10.0.402"
            and installed["crossEntryBytesEqual"] is True
            and installed["recoveryBytesEqual"] is True
            and installed["reorderedPdfRejected"] is True
            and installed["wrongCachedAssemblyRejected"] is True,
            "Installed API/CLI acceptance missing")
    require(installed["package"]["sha256"] == digest(file_bytes(candidate / EXPECTED["package"]))
            and installed["cliSha256"] == digest(unpacked["managed"]["FactsPDF.Cli/FactsPDF.Cli.dll"]),
            "Installed package/CLI differs from final staged bytes")
    require(installed["toolchainConstraint"]["verified"] is True
            and installed["toolchainConstraint"]["expectedRuntime"] == "10.0.13"
            and installed["toolchainConstraint"]["expectedSdk"] == "10.0.402",
            "Clean consumer toolchain not strictly pinned")
    require({x["entry"] for x in installed["cases"]} == {"consumer", "cli"}
            and len(installed["negativeCases"]) == 5
            and all(x["code"] == "FPDF1102" and x["outputPreserved"] is True
                    for x in installed["negativeCases"]), "Negative consumer paths not complete")
    consumer_pdfs = {}
    for entry in ("consumer", "cli"):
        witness = pdf_witness(consumer_dir / (entry + ".pdf"), 2,
                              ("LOCAL-PACKAGE-001", "END-002"))
        reported, = (x for x in installed["cases"] if x["entry"] == entry)
        require(reported["pdfSha256"] == witness["sha256"] and reported["pages"] == 2
                and reported["qpdf"] is True and reported["textAndBounds"] is True,
                "Installed PDF receipt differs from actual PDF")
        require(digest(file_bytes(consumer_dir / (entry + "-recovery.pdf"))) == witness["sha256"],
                "Installed API/CLI recovery differs from real output")
        consumer_pdfs[entry] = witness
    require(consumer_pdfs["consumer"]["sha256"] == consumer_pdfs["cli"]["sha256"],
            "Installed API and CLI outputs differ")

    native_dir = runner / "factspdf-staged-five-native-features"
    native = load(native_dir / "report.json")
    native_binary = unpacked["native"]["FactsPDF.Native/FactsPDF.Cli"]
    require(native["sourceHead"] == head and native["nativeExecutableSha256"] == digest(native_binary)
            and native["gates"]["nativeProcess"] is True
            and native["gates"]["independentPoppler"] is True,
            "Actual staged native executable not verified")
    furniture = native["furniture"]
    continuation = native["continuedRow"]
    require(all(furniture[k] is True for k in (
            "bodyWordGeometryUnchanged", "leftRightAndMarginPositions", "repeatBytesEqual",
            "invalidConfigDestinationPreserved", "failureRecoveryBytesEqual"))
            and continuation["notes"] == 24 and continuation["pages"] == 2
            and continuation["completeNoteContents"] is True
            and continuation["repeatBytesEqual"] is True, "Native feature/recovery evidence incomplete")
    native_pdfs = {
        "furniture": pdf_witness(native_dir / "page-furniture.pdf", 2, ("Business report", "Page 1 of 2")),
        "continued": pdf_witness(native_dir / "continued-row.pdf", 2, ("SRV-001", "SRV-002")),
    }
    require(furniture["sha256"] == native_pdfs["furniture"]["sha256"]
            and continuation["sha256"] == native_pdfs["continued"]["sha256"],
            "Native feature PDF differs from run receipt")

    sdk = run("dotnet", "--version").strip()
    runtimes = run("dotnet", "--list-runtimes")
    require(sdk == "10.0.402" and re.search(r"(?m)^Microsoft\.NETCore\.App 10\.0\.13 \[", runtimes),
            "SDK or managed runtime not installed at pinned version")
    require(current_source(root) == head, "Source changed during independent check")
    output.mkdir(mode=0o700)
    (output / "reports").mkdir()
    checks_data = {
        "source-offline-rebuild": {"sourceCommit": head, "pdf": source_pdf,
                                    "builtCliPresent": True, "inspectorPresent": True},
        "examples-extraction": {"sourceCommit": head,
                                 "memberCount": len(inventories["examples"]),
                                 "memberInventorySha256": digest(json.dumps(
                                     inventories["examples"], sort_keys=True).encode())},
        "staged-archive-allowlists": {"sourceCommit": head,
                                     "memberCounts": {k: len(v) for k, v in inventories.items()},
                                     "inventoryDigests": {p["filename"]: p["inventoryDigest"] for p in payloads}},
        "staged-package-inspector": {"sourceCommit": head, "packageVersion": VERSION,
                                     "target": "net10.0", "assemblySha256": digest(package_meta["assembly"]),
                                     "inspectorSha256": digest(file_bytes(inspector))},
        "clean-installed-api-and-cli": {"sourceCommit": head, "sdk": sdk,
                                         "realPdfs": consumer_pdfs, "negativeCases": len(installed["negativeCases"]),
                                         "recoveryBytesEqual": True},
        "linux-native-feature-and-recovery": {"sourceCommit": head,
                                               "nativeBinarySha256": digest(native_binary),
                                               "realPdfs": native_pdfs,
                                               "continuationNotes": 24,
                                               "invalidConfigDestinationPreserved": True},
    }
    require(set(checks_data) == REQUIRED_CHECKS, "Incomplete independent evidence types")
    checks = {name: evidence_path(output, name, body) for name, body in checks_data.items()}
    verification = {
        "schemaVersion": 1, "phase": "payloads", "status": "passed",
        "candidateStagingSha256": digest(staging_bytes),
        "identity": {"sourceHead": head, "profile": "local-linux", "packageId": "FactsPDF",
                     "packageVersion": VERSION, "targetFramework": "net10.0",
                     "sdkVersion": sdk, "selectedRuntimeVersion": "10.0.13"},
        "actualToolchain": {"sdkVersion": sdk, "managedRuntimeVersion": "10.0.13",
                            "nativeRuntimePackVersion": "10.0.13",
                            "nativeCompilerPackVersion": "10.0.13",
                            "officialSignatureVerified": False,
                            "nativeCompilerPackVerified": False},
        "frozenContractDigest": frozen_contract_digest(root),
        "payloads": sorted(payloads, key=lambda x: x["filename"]), "checks": checks,
    }
    (output / "payload-verification.json").write_text(
        json.dumps(verification, sort_keys=True, indent=2) + "\n")
    return verification


def readback(candidate: Path, verification: Path) -> dict:
    candidate = candidate.resolve(strict=True)
    report = load(verification)
    manifest_bytes = file_bytes(candidate / "candidate-manifest.json", 4 * 1024 * 1024)
    manifest = json.loads(manifest_bytes)
    require({p.name for p in candidate.iterdir()} ==
            set(EXPECTED.values()) | {"candidate-staging.json", "candidate-manifest.json", "SHA256SUMS"},
            "Extra or missing finalized private candidate file")
    require(manifest["phase"] == "private-linux-technically-verified-not-authorized"
            and manifest["independentlyValidated"] is True
            and manifest["candidateAssembled"] is True
            and all(manifest[k] is False for k in (
                "published", "distributionAuthorized", "humanPdfApproved",
                "fullLegacyReplayApproved", "customerObtained", "windowsBinaryRetained")),
            "Final candidate misstates verification or distribution rights")
    require(manifest["verificationReceiptSha256"] == digest(file_bytes(verification, 4 * 1024 * 1024))
            and manifest["stagingReceiptSha256"] == digest(file_bytes(candidate / "candidate-staging.json")),
            "Final manifest detached from independently verified inputs")
    require(manifest["payloads"] == report["payloads"]
            and manifest["acceptanceChecks"] == report["checks"],
            "Final manifest does not match independently witnessed payloads")
    checksum_lines = file_bytes(candidate / "SHA256SUMS", 8 * 1024).decode("ascii").splitlines()
    expected = {p["filename"]: p["sha256"] for p in report["payloads"]}
    expected["candidate-manifest.json"] = digest(manifest_bytes)
    require(len(checksum_lines) == 6, "Wrong final SHA256SUMS line count")
    observed = {}
    for line in checksum_lines:
        require(re.fullmatch(r"[0-9a-f]{64}  [A-Za-z0-9_.-]+", line) is not None,
                "Malformed final checksum record")
        hexdigest, filename = line.split("  ", 1)
        require(filename not in observed, "Duplicate final checksum record")
        observed[filename] = hexdigest
    require(observed == expected and
            checksum_lines == [expected[name] + "  " + name for name in sorted(expected)],
            "Final manifest/checksum allowlist or ordering mismatch")
    for name, wanted in expected.items():
        require(digest(file_bytes(candidate / name)) == wanted,
                "Finalized private candidate bytes changed: " + name)
    return {"sourceHead": manifest["sourceHead"], "files": len(expected),
            "phase": manifest["phase"], "published": False,
            "distributionAuthorized": False, "fullLegacyReplayApproved": False}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("verify", "readback"))
    parser.add_argument("--candidate", type=Path, required=True)
    parser.add_argument("--runner", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--verification", type=Path)
    options = parser.parse_args()
    if options.mode == "verify":
        require(options.runner is not None and options.output is not None,
                "Verification requires actual runner and fresh output directory")
        result = verify(Path(__file__).resolve().parents[1], options.candidate,
                        options.runner, options.output)
        print("Independently verified exact private five-component payloads: " +
              result["identity"]["sourceHead"] + "; no publication or user page approval")
    else:
        require(options.verification is not None, "Final readback requires independent verification path")
        print(json.dumps(readback(options.candidate, options.verification), sort_keys=True))


if __name__ == "__main__":
    main()
