"""Finalize a privately staged five-component candidate only against external evidence.

This is a source/byte linker, NOT the independent PDF/installer verifier, legal
authorization, human page approval, or a public publishing entrypoint. The
independent verifier must create the successful payload report first.
"""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import re
import tempfile

VERSION = "0.1.0-preview.1"
EXPECTED = {
    "package": f"FactsPDF.{VERSION}.nupkg",
    "managed": f"FactsPDF.Cli.{VERSION}.managed.zip",
    "native": f"FactsPDF.Cli.{VERSION}.native-linux-x64.zip",
    "source": f"FactsPDF.{VERSION}.source.tar.gz",
    "examples": f"FactsPDF.{VERSION}.examples.zip",
}
REQUIRED_CHECKS = frozenset({
    "source-offline-rebuild", "examples-extraction",
    "staged-archive-allowlists", "staged-package-inspector",
    "clean-installed-api-and-cli", "linux-native-feature-and-recovery",
})
# Explicit committed expectations, not rendered-PDF output or generated receipts.
FROZEN_INPUTS = (
    ".github/workflows/ci.yml", "Directory.Build.props",
    "eng/delivery-candidate.json", "global.json",
    "scripts/stage_delivery_candidate.py", "scripts/private_five_finalization.py",
    "scripts/private_source_payload.py",
    "scripts/private_examples_payload.py", "scripts/verify_first_use.py",
    "scripts/verify_native_feature_paths.py",
    "examples/business-page-furniture.json",
    "examples/first-use/expected-pages.json",
)
HEX64 = re.compile(r"[0-9a-f]{64}\Z")
HEX40 = re.compile(r"[0-9a-f]{40}\Z")


def _read_json(path: Path) -> tuple[dict, bytes]:
    if path.is_symlink() or not path.is_file() or not 0 < path.stat().st_size <= 1024 * 1024:
        raise ValueError("Missing, symlinked, empty or oversized verification JSON")
    raw = path.read_bytes()

    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise ValueError("Duplicate verification JSON key: " + key)
            result[key] = value
        return result

    obj = json.loads(raw, object_pairs_hook=pairs)
    if not isinstance(obj, dict):
        raise ValueError("Verification receipt must be a JSON object")
    return obj, raw


def _sha(path: Path) -> tuple[int, str]:
    if path.is_symlink() or not path.is_file() or not 0 < path.stat().st_size <= 256 * 1024 * 1024:
        raise ValueError("Missing, symlinked, empty or oversized verified file")
    digest = hashlib.sha256()
    size = 0
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
            size += len(block)
    return size, digest.hexdigest()


def frozen_contract_digest(root: Path) -> str:
    """Canonical hash over source-committed configuration and independent fixtures."""
    files = []
    for name in sorted(FROZEN_INPUTS):
        path = root / name
        size, digest = _sha(path)
        if size > 8 * 1024 * 1024:
            raise ValueError("Frozen candidate input exceeds budget")
        files.append({"path": name, "bytes": size, "sha256": digest})
    data = {"schemaVersion": 1, "recipeVersion": 1, "files": files}
    return hashlib.sha256(json.dumps(
        data, ensure_ascii=True, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")).hexdigest()


def _check_member(member: dict) -> None:
    if not isinstance(member, dict) or set(member) != {"path", "bytes", "sha256", "identity"}:
        raise ValueError("Incomplete independently verified binary member")
    if (not isinstance(member["path"], str) or not member["path"] or
            member["path"].startswith("/") or "\\" in member["path"] or
            any(part in ("", ".", "..") for part in member["path"].split("/")) or
            not isinstance(member["bytes"], int) or isinstance(member["bytes"], bool) or
            member["bytes"] <= 0 or not isinstance(member["sha256"], str) or
            not HEX64.fullmatch(member["sha256"]) or
            not isinstance(member["identity"], dict) or not member["identity"]):
        raise ValueError("Invalid independently verified binary member")


def finalize_five(root: Path, candidate: Path, verification: Path) -> dict:
    """Bind final checksums to an external successful payload verification report.

    No final file is written until every current payload, reported binary member
    inventory, committed contract and genuine evidence-file byte hash agrees.
    """
    from stage_delivery_candidate import current_source, identity

    root = root.resolve(strict=True)
    candidate = candidate.absolute()
    verification = verification.absolute()
    if (candidate.is_symlink() or not candidate.is_dir() or
            verification.is_symlink() or not verification.is_file()):
        raise ValueError("Expected separate external stage and verification files")
    physical_candidate = candidate.resolve(strict=True)
    physical_verification = verification.resolve(strict=True)
    if (physical_candidate == root or root in physical_candidate.parents or
            physical_verification == root or root in physical_verification.parents or
            physical_candidate == physical_verification.parent or
            physical_candidate in physical_verification.parents):
        raise ValueError("Expected physically external stage and verification files")
    source_head = current_source(root)
    if not HEX40.fullmatch(source_head):
        raise ValueError("Expected a complete source commit")
    selected = identity(root, "local-linux")
    staging, stage_bytes = _read_json(candidate / "candidate-staging.json")
    if (staging.get("schemaVersion") != 1 or
            staging.get("phase") != "five-payloads-staged-awaiting-independent-verification" or
            staging.get("sourceHead") != source_head or staging.get("identity") != selected or
            staging.get("retainedProfile") != "local-linux" or
            staging.get("windowsBinaryRetained") is not False or
            staging.get("notFinalCandidate") is not True or
            staging.get("candidateBuilt") is not True or
            staging.get("candidateAssembled") is not True or
            any(staging.get(key) is not False for key in (
                "independentlyValidated", "published", "distributionAuthorized"))):
        raise ValueError("Invalid or stale staged candidate identity/status")
    stage_payloads = staging.get("payloads")
    if not isinstance(stage_payloads, list) or len(stage_payloads) != 5:
        raise ValueError("Expected exactly five staged payloads")
    staged_names = set(EXPECTED.values()) | {"candidate-staging.json"}
    current_names = {entry.name for entry in candidate.iterdir()}
    # A process may terminate after linking the manifest but before SHA256SUMS.
    # Only that exact partial state is recoverable, after full re-verification.
    interrupted = current_names == staged_names | {"candidate-manifest.json"}
    if current_names != staged_names and not interrupted:
        raise ValueError("Unexpected, missing or already finalized candidate file")
    if interrupted and (candidate / "candidate-manifest.json").is_symlink():
        raise ValueError("Symlinked interrupted manifest cannot be resumed")

    proof, proof_bytes = _read_json(verification)
    expected_id = {
        "sourceHead": source_head, "profile": "local-linux",
        "packageId": "FactsPDF", "packageVersion": VERSION,
        "targetFramework": "net10.0", "sdkVersion": "10.0.402",
        "selectedRuntimeVersion": "10.0.13",
    }
    if (proof.get("schemaVersion") != 1 or proof.get("phase") != "payloads" or
            proof.get("status") != "passed" or proof.get("identity") != expected_id or
            proof.get("candidateStagingSha256") != hashlib.sha256(stage_bytes).hexdigest()):
        raise ValueError("Missing successful source/profile/staging-bound external verification")
    observed = proof.get("actualToolchain")
    if not isinstance(observed, dict) or any(observed.get(k) != value for k, value in {
        "sdkVersion": "10.0.402", "managedRuntimeVersion": "10.0.13",
        "nativeRuntimePackVersion": "10.0.13",
        "nativeCompilerPackVersion": "10.0.13",
        "officialSignatureVerified": False,
        "nativeCompilerPackVerified": False,
    }.items()):
        raise ValueError("Unverified or incompatible observed candidate toolchain")
    digest = frozen_contract_digest(root)
    if proof.get("frozenContractDigest") != digest:
        raise ValueError("Stale independent expectation/contract digest")

    stage_by_role = {}
    for rec in stage_payloads:
        if (not isinstance(rec, dict) or rec.get("role") not in EXPECTED or
                rec.get("role") in stage_by_role or
                rec.get("filename") != EXPECTED[rec["role"]]):
            raise ValueError("Unexpected or duplicated staged payload role")
        stage_by_role[rec["role"]] = rec
    if set(stage_by_role) != set(EXPECTED):
        raise ValueError("Incomplete staged payload roles")
    verified = proof.get("payloads")
    if not isinstance(verified, list) or len(verified) != 5:
        raise ValueError("Incomplete external payload verification")
    verified_by_name = {}
    for rec in verified:
        if (not isinstance(rec, dict) or
                set(rec) != {"filename", "bytes", "sha256", "inventoryDigest", "binaryMembers"} or
                rec.get("filename") not in EXPECTED.values() or
                rec["filename"] in verified_by_name or
                not isinstance(rec.get("inventoryDigest"), str) or
                not HEX64.fullmatch(rec["inventoryDigest"]) or
                not isinstance(rec.get("binaryMembers"), list)):
            raise ValueError("Invalid independent payload/member record")
        if rec["filename"] in (EXPECTED[k] for k in ("package", "managed", "native")) and not rec["binaryMembers"]:
            raise ValueError("Executable/member identity missing")
        seen = set()
        for member in rec["binaryMembers"]:
            _check_member(member)
            if member["path"] in seen:
                raise ValueError("Duplicated independently verified member")
            seen.add(member["path"])
        verified_by_name[rec["filename"]] = rec
    if set(verified_by_name) != set(EXPECTED.values()):
        raise ValueError("Wrong independent payload set")
    for role, name in EXPECTED.items():
        rec, external = stage_by_role[role], verified_by_name[name]
        actual_size, actual_digest = _sha(candidate / name)
        if (actual_size != rec.get("bytes") or actual_digest != rec.get("sha256") or
                actual_size != external.get("bytes") or actual_digest != external.get("sha256") or
                not isinstance(rec.get("producerReceiptSha256"), str) or
                not HEX64.fullmatch(rec["producerReceiptSha256"])):
            raise ValueError("Payload changed after successful independent verification: " + name)

    checks = proof.get("checks")
    if not isinstance(checks, dict) or set(checks) != REQUIRED_CHECKS:
        raise ValueError("Missing required independent acceptance checks")
    evidence_root = verification.parent.resolve(strict=True)
    evidence_records = []
    for name, item in checks.items():
        if not isinstance(item, dict) or set(item) != {"status", "evidenceFile", "bytes", "sha256"} or item["status"] != "passed":
            raise ValueError("Incomplete acceptance evidence for " + name)
        rel = item["evidenceFile"]
        if (not isinstance(rel, str) or not rel.startswith("reports/") or
                "\\" in rel or any(x in ("", ".", "..") for x in rel.split("/")) or
                not isinstance(item["sha256"], str) or not HEX64.fullmatch(item["sha256"])):
            raise ValueError("Invalid independent evidence path/digest for " + name)
        path = evidence_root / rel
        # Check each component, not just the immediate parent. An intermediate
        # symlink can otherwise redirect reports/x/y.json outside the evidence root.
        component = evidence_root
        for segment in rel.split("/"):
            component = component / segment
            if component.is_symlink():
                raise ValueError("Symlinked independent evidence path")
        physically_resolved = path.resolve(strict=True)
        reports_root = (evidence_root / "reports").resolve(strict=True)
        if (physically_resolved == physical_candidate or
                physical_candidate in physically_resolved.parents or
                reports_root not in physically_resolved.parents):
            raise ValueError("Evidence path leaves verified report folder")
        size, evidence_digest = _sha(path)
        if size != item["bytes"] or evidence_digest != item["sha256"]:
            raise ValueError("Independent acceptance evidence was changed")
        evidence_records.append((path, size, evidence_digest))
    if current_source(root) != source_head:
        raise ValueError("Source changed during final verification linkage")

    manifest = {
        "schemaVersion": 1, "phase": "private-linux-technically-verified-not-authorized",
        "sourceHead": source_head, "identity": selected, "payloads": verified,
        "stagingReceiptSha256": hashlib.sha256(stage_bytes).hexdigest(),
        "verificationReceiptSha256": hashlib.sha256(proof_bytes).hexdigest(),
        "frozenContractDigest": digest, "actualToolchain": observed,
        "acceptanceChecks": checks, "candidateAssembled": True,
        "independentlyValidated": True, "published": False,
        "distributionAuthorized": False, "humanPdfApproved": False,
        "fullLegacyReplayApproved": False, "customerObtained": False,
        "windowsBinaryRetained": False,
    }
    manifest_bytes = (json.dumps(manifest, sort_keys=True, indent=2) + "\n").encode("utf-8")
    manifest_digest = hashlib.sha256(manifest_bytes).hexdigest()
    sums = "".join(
        f"{verified_by_name[name]['sha256']}  {name}\n"
        for name in sorted(EXPECTED.values())
    ) + f"{manifest_digest}  candidate-manifest.json\n"

    # Stage both files privately, then install without replacing any existing
    # entry. Leave an interrupted manifest recoverable; never roll back a
    # matching completion installed by another live caller.
    with tempfile.TemporaryDirectory(prefix="factspdf-final-", dir=candidate.parent) as temporary:
        scratch = Path(temporary)
        (scratch / "candidate-manifest.json").write_bytes(manifest_bytes)
        (scratch / "SHA256SUMS").write_text(sums, encoding="ascii")
        for name in EXPECTED.values():
            original = stage_by_role[next(k for k, v in EXPECTED.items() if v == name)]
            size, actual = _sha(candidate / name)
            if size != original["bytes"] or actual != original["sha256"]:
                raise ValueError("Payload mutated before final checksum installation")
        if hashlib.sha256((candidate / "candidate-staging.json").read_bytes()).hexdigest() != manifest["stagingReceiptSha256"]:
            raise ValueError("Staging receipt changed before final checksum installation")
        if hashlib.sha256(verification.read_bytes()).hexdigest() != manifest["verificationReceiptSha256"]:
            raise ValueError("Independent verification changed during finalization")
        # Catch a mutation between initial evidence verification and final files.
        for path, recorded_size, recorded_sha in evidence_records:
            if _sha(path) != (recorded_size, recorded_sha):
                raise ValueError("Independent acceptance evidence changed before finalization")
        previous = candidate / "candidate-manifest.json"
        if interrupted:
            if previous.is_symlink() or previous.read_bytes() != manifest_bytes:
                raise ValueError("Interrupted finalization manifest is stale or modified")
        else:
            try:
                os.link(scratch / "candidate-manifest.json", previous)
            except FileExistsError:
                if previous.is_symlink() or previous.read_bytes() != manifest_bytes:
                    raise ValueError("Concurrent finalization manifest is stale or modified")
        # SHA256SUMS is the last-installed completion marker. Another invocation
        # may have completed this identical pair since our initial inventory.
        # On other errors leave the manifest for fully revalidated retry, not
        # a check-then-unlink rollback that can delete a concurrent completion.
        try:
            os.link(scratch / "SHA256SUMS", candidate / "SHA256SUMS")
        except FileExistsError:
            checksum = candidate / "SHA256SUMS"
            if (previous.is_symlink() or checksum.is_symlink() or
                    previous.read_bytes() != manifest_bytes or
                    checksum.read_bytes() != (scratch / "SHA256SUMS").read_bytes()):
                raise ValueError("Concurrent finalization completion is stale or modified")
    return manifest


if __name__ == "__main__":
    import argparse
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--candidate", type=Path, required=True)
    p.add_argument("--verification", type=Path, required=True)
    args = p.parse_args()
    result = finalize_five(Path(__file__).resolve().parents[1],
                           args.candidate, args.verification)
    print("Technically verified private candidate: " + result["sourceHead"] +
          "; public distribution, human approval and customer delivery remain unauthorized.")
