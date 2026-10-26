"""No-.NET regression tests for the final private-candidate read-back boundary."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from private_five_finalization import EXPECTED
from verify_private_five_candidate import readback


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


class FinalCandidateReadbackTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="factspdf-final-readback-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.candidate = self.root / "private-candidate"
        self.candidate.mkdir()
        self.verification = self.root / "independent-verification.json"
        source_head = "a" * 40
        identity = {
            "sourceHead": source_head,
            "profile": "local-linux",
            "packageId": "FactsPDF",
            "packageVersion": "0.1.0-preview.1",
            "targetFramework": "net10.0",
            "sdkVersion": "10.0.402",
            "selectedRuntimeVersion": "10.0.13",
        }
        toolchain = {
            "sdkVersion": "10.0.402",
            "managedRuntimeVersion": "10.0.13",
            "nativeRuntimePackVersion": "10.0.13",
            "nativeCompilerPackVersion": "10.0.13",
            "officialSignatureVerified": False,
            "nativeCompilerPackVerified": False,
        }
        frozen = "b" * 64
        staged = (json.dumps(
            {"sourceHead": source_head, "identity": identity}, sort_keys=True
        ) + "\n").encode()
        (self.candidate / "candidate-staging.json").write_bytes(staged)
        payloads = []
        for role, filename in EXPECTED.items():
            content = ("test-only-not-software:" + role).encode()
            (self.candidate / filename).write_bytes(content)
            payloads.append({"filename": filename, "sha256": sha(content), "bytes": len(content)})
        report = {
            "identity": identity,
            "candidateStagingSha256": sha(staged),
            "frozenContractDigest": frozen,
            "actualToolchain": toolchain,
            "payloads": payloads,
            "checks": {"control": True},
        }
        self.verification.write_text(json.dumps(report, sort_keys=True) + "\n")
        self.manifest = {
            "schemaVersion": 1,
            "phase": "private-linux-technically-verified-not-authorized",
            "sourceHead": source_head, "identity": identity,
            "frozenContractDigest": frozen, "actualToolchain": toolchain,
            "payloads": payloads, "acceptanceChecks": {"control": True},
            "verificationReceiptSha256": sha(self.verification.read_bytes()),
            "stagingReceiptSha256": sha(staged),
            "candidateAssembled": True, "independentlyValidated": True,
            "published": False, "distributionAuthorized": False,
            "humanPdfApproved": False, "fullLegacyReplayApproved": False,
            "customerObtained": False, "windowsBinaryRetained": False,
        }
        self.write_manifest()

    def write_manifest(self):
        data = (json.dumps(self.manifest, sort_keys=True) + "\n").encode()
        (self.candidate / "candidate-manifest.json").write_bytes(data)
        expected = {p["filename"]: p["sha256"] for p in self.manifest["payloads"]}
        expected["candidate-manifest.json"] = sha(data)
        (self.candidate / "SHA256SUMS").write_text(
            "".join(expected[name] + "  " + name + "\n" for name in sorted(expected)))

    def test_exact_private_candidate_readback(self):
        result = readback(self.candidate, self.verification)
        self.assertEqual(result["files"], 6)
        self.assertFalse(result["distributionAuthorized"])

    def test_tampered_payload_is_rejected(self):
        (self.candidate / EXPECTED["package"]).write_bytes(b"tampered")
        with self.assertRaisesRegex(ValueError, "bytes changed"):
            readback(self.candidate, self.verification)

    def test_checksum_manifest_mutation_is_rejected(self):
        path = self.candidate / "SHA256SUMS"
        path.write_text(path.read_text().replace("  candidate-manifest.json", "  unrelated.json"))
        with self.assertRaisesRegex(ValueError, "mismatch"):
            readback(self.candidate, self.verification)

    def test_distribution_grant_cannot_be_smuggled_into_manifest(self):
        self.manifest["distributionAuthorized"] = True
        self.write_manifest()
        with self.assertRaisesRegex(ValueError, "rights"):
            readback(self.candidate, self.verification)

    def test_changed_source_head_with_recomputed_checksums_is_rejected(self):
        self.manifest["sourceHead"] = "0" * 40
        self.write_manifest()
        with self.assertRaisesRegex(ValueError, "detached"):
            readback(self.candidate, self.verification)

    def test_changed_frozen_contract_with_recomputed_checksums_is_rejected(self):
        self.manifest["frozenContractDigest"] = "c" * 64
        self.write_manifest()
        with self.assertRaisesRegex(ValueError, "detached"):
            readback(self.candidate, self.verification)

    def test_changed_toolchain_with_recomputed_checksums_is_rejected(self):
        self.manifest["actualToolchain"] = {
            **self.manifest["actualToolchain"],
            "managedRuntimeVersion": "10.0.14",
        }
        self.write_manifest()
        with self.assertRaisesRegex(ValueError, "detached"):
            readback(self.candidate, self.verification)

    def test_changed_staging_identity_with_matching_manifest_is_rejected(self):
        staged = json.loads((self.candidate / "candidate-staging.json").read_text())
        staged["identity"]["sourceHead"] = "0" * 40
        # Amend both staging and its manifest receipt, but not the independent
        # verification report that attested to the original staging bytes.
        modified = (json.dumps(staged, sort_keys=True) + "\n").encode()
        (self.candidate / "candidate-staging.json").write_bytes(modified)
        self.manifest["identity"] = staged["identity"]
        self.manifest["stagingReceiptSha256"] = sha(modified)
        self.write_manifest()
        with self.assertRaisesRegex(ValueError, "detached"):
            readback(self.candidate, self.verification)

    def test_extra_untracked_payload_is_rejected(self):
        (self.candidate / "evil.ttf").write_bytes(b"font")
        with self.assertRaisesRegex(ValueError, "Extra or missing"):
            readback(self.candidate, self.verification)


if __name__ == "__main__":
    unittest.main()
