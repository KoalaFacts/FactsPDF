import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from acceptance_corpus import digest


class BuildProvenanceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.executables = {role: self.root/role/name for role, name in
                            [('cli', 'FactsPDF.Cli'), ('api', 'FactsPDF.Acceptance')]}
        for role, file in self.executables.items():
            file.parent.mkdir(); file.write_bytes((role+' native binary').encode())
        self.source = 'a'*40
        self.manifest = {'schema_version': 1, 'source_sha': self.source,
                         'build_mode': 'linux-x64-native-aot',
                         'executables': {role: {'name': file.name, 'sha256': digest(file.read_bytes())}
                                         for role, file in self.executables.items()}}
        self.write()

    def write(self):
        for file in self.executables.values():
            (file.parent/'build-provenance.json').write_text(json.dumps(self.manifest))

    def validate(self):
        from acceptance_build import validate_build_provenance
        return validate_build_provenance(self.executables, self.source)

    def test_matching_source_and_both_binaries_are_required(self):
        self.assertEqual(self.validate()['source_sha'], self.source)

    def test_stale_cli_or_host_binary_is_rejected(self):
        for role, file in self.executables.items():
            with self.subTest(role=role):
                original = file.read_bytes(); file.write_bytes(b'old binary')
                with self.assertRaises(ValueError): self.validate()
                file.write_bytes(original)

    def test_current_environment_cannot_relabel_old_build(self):
        self.manifest['source_sha'] = 'b'*40; self.write()
        with self.assertRaises(ValueError): self.validate()

    def test_missing_build_record_is_rejected(self):
        (self.executables['api'].parent/'build-provenance.json').unlink()
        with self.assertRaises(ValueError): self.validate()

    def test_mixed_build_records_are_rejected(self):
        file = self.executables['api'].parent/'build-provenance.json'
        other = dict(self.manifest, source_sha='b'*40)
        file.write_text(json.dumps(other))
        with self.assertRaises(ValueError): self.validate()

    def test_wrong_build_mode_is_rejected(self):
        self.manifest['build_mode'] = 'managed'; self.write()
        with self.assertRaises(ValueError): self.validate()

    def test_capture_rejects_stale_bytes_before_any_tool_probe(self):
        from acceptance_run import capture_environment
        self.executables['cli'].write_bytes(b'old binary')
        with patch.dict(os.environ, {'FACTSPDF_SOURCE_SHA': self.source}):
            with self.assertRaisesRegex(ValueError, 'Executable bytes'):
                capture_environment(self.executables, [])


if __name__ == '__main__': unittest.main()
