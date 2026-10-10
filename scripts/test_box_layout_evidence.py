import copy
import hashlib
import unittest

from box_layout_evidence import BASELINE_SHA, EXPECTED_PAGES, build_report, workloads


class BoxLayoutEvidenceTests(unittest.TestCase):
    def sample(self, name):
        return {
            'mode': 'full', 'runtime': '.NET 10.0', 'os': 'Linux test', 'architecture': 'X64',
            'dynamic_code_supported': False, 'output_bytes': 1000, 'pages': EXPECTED_PAGES[name],
            'process_lifetime_peak_working_set_bytes': 10000,
            'input_sha256': hashlib.sha256(workloads()[name].encode('utf-8')).hexdigest(),
            'output_sha256': 'c' * 64,
            'reused_font_conversion': {
                'samples': 20, 'median_ms': 1.0, 'median_managed_allocated_bytes': 200,
                'raw': [{'ms': 1.0, 'managed_allocated_bytes': 200} for _ in range(20)]},
        }

    def pair(self):
        baseline = {name: self.sample(name) for name in workloads()}
        return baseline, copy.deepcopy(baseline)

    def build(self, baseline, candidate, source='a' * 40, baseline_sha=BASELINE_SHA, harness='b' * 64):
        return build_report(baseline, candidate, source, baseline_sha, harness)

    def test_workloads_are_deterministic_ascii_and_distinct(self):
        self.assertEqual(set(workloads()), {'plain', 'nested', 'fragmented'})
        self.assertEqual(workloads(), workloads())
        self.assertEqual(len(set(workloads().values())), 3)
        self.assertTrue(all(value.isascii() for value in workloads().values()))

    def test_valid_same_bytes_report_retains_raw_measurements(self):
        report = self.build(*self.pair())
        self.assertEqual(report['source_sha'], 'a' * 40)
        self.assertEqual(report['baseline_sha'], BASELINE_SHA)
        self.assertEqual(report['harness_sha256'], 'b' * 64)
        self.assertEqual(report['errors'], [])
        self.assertEqual(len(report['cases']), 3)
        self.assertEqual(len(report['cases']['plain']['candidate']['reused_font_conversion']['raw']), 20)

    def test_missing_or_extra_case_is_rejected(self):
        for extra in (False, True):
            baseline, candidate = self.pair()
            if extra:
                candidate['unexpected'] = self.sample('plain')
            else:
                candidate.pop('nested')
            with self.assertRaises(ValueError):
                self.build(baseline, candidate)

    def test_input_output_and_page_mismatches_are_rejected(self):
        for field, value in [('input_sha256', 'd' * 64), ('output_sha256', 'd' * 64),
                             ('pages', 9), ('output_bytes', 2000)]:
            baseline, candidate = self.pair()
            candidate['plain'][field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.build(baseline, candidate)

    def test_jit_or_missing_provenance_is_rejected(self):
        for key, value in [('dynamic_code_supported', True), ('dynamic_code_supported', 0),
                           ('runtime', ''), ('architecture', 'ARM64')]:
            baseline, candidate = self.pair()
            candidate['plain'][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                self.build(baseline, candidate)
        for source, base, harness in [('', BASELINE_SHA, 'b' * 64),
                                      ('a' * 40, 'e' * 40, 'b' * 64),
                                      ('a' * 40, BASELINE_SHA, 'bad')]:
            with self.assertRaises(ValueError):
                self.build(*self.pair(), source, base, harness)

    def test_invalid_measurements_are_rejected(self):
        for bad in [float('nan'), float('inf'), -1, True, '1']:
            baseline, candidate = self.pair()
            candidate['plain']['reused_font_conversion']['raw'][0]['ms'] = bad
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                self.build(baseline, candidate)

    def test_raw_sample_count_and_summary_are_checked(self):
        for change in ['count', 'median', 'allocation']:
            baseline, candidate = self.pair()
            samples = candidate['plain']['reused_font_conversion']
            if change == 'count':
                samples['raw'].pop()
            elif change == 'median':
                samples['median_ms'] = 4
            else:
                samples['median_managed_allocated_bytes'] = 201
            with self.subTest(change=change), self.assertRaises(ValueError):
                self.build(baseline, candidate)

    def test_missing_measurement_field_is_rejected(self):
        baseline, candidate = self.pair()
        candidate['plain'].pop('output_sha256')
        with self.assertRaises(ValueError):
            self.build(baseline, candidate)

    def test_slower_measurement_is_not_a_correctness_failure(self):
        baseline, candidate = self.pair()
        candidate['plain']['reused_font_conversion']['median_ms'] = 100
        for row in candidate['plain']['reused_font_conversion']['raw']:
            row['ms'] = 100
        self.assertEqual(self.build(baseline, candidate)['errors'], [])


if __name__ == '__main__':
    unittest.main()
