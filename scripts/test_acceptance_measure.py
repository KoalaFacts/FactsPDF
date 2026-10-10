import copy
import importlib
from pathlib import Path
import tempfile
import unittest


class MeasureTests(unittest.TestCase):
    def setUp(self):
        try: self.m = importlib.import_module('acceptance_measure')
        except ModuleNotFoundError: self.fail('Task 4 measurement implementation is missing')

    def data(self):
        samples = [{'sample_index': i, 'elapsed_ms': x, 'managed_allocated_bytes': n,
                    'pdf_bytes': 100, 'pdf_sha256': 'b'*64, 'pages': 1,
                    'measurement_scope': self.m.WARM_SCOPE} for i,(x,n) in enumerate([(1.,10),(3.,30)])]
        return {'schema_version': 1, 'engine_baseline_sha': self.m.BASELINE_SHA, 'source_sha':'a'*40,
                'environment_id':'c'*64, 'input_sha256':'d'*64, 'font_inputs':[],
                'case_id':'B00', 'batch_index':0, 'expected_cold_samples':2, 'expected_warm_samples':2,
                'status':'provisional', 'cold':[{'sample_index':i, 'pid':i+100,
                    'elapsed_ms':x, 'peak_rss_bytes':None, 'pdf_bytes':100, 'pdf_sha256':'b'*64,
                    'pages':1, 'measurement_scope':self.m.COLD_SCOPE} for i,x in enumerate([2.,4.])],
                'warm':samples, 'warm_process_peak_bytes':None,
                'font_load_elapsed_ms':0., 'font_load_managed_allocated_bytes':0}

    def test_sample_counts_and_medians_are_recomputed(self):
        d = self.data(); d['claimed_median'] = 999
        out = self.m.summarize_measurements(d)
        self.assertEqual(out['warm_elapsed_median_ms'], 2.)
        self.assertEqual(out['cold_elapsed_median_ms'], 3.)
        self.assertEqual(out['warm_managed_allocated_median_bytes'], 20.)

    def test_cold_runs_are_distinct_processes(self):
        d = self.data(); d['cold'][1]['pid'] = d['cold'][0]['pid']
        with self.assertRaises(ValueError): self.m.validate_measurements(d)

    def test_unknown_peak_is_null_not_zero(self):
        result = self.m.summarize_measurements(self.data())
        self.assertIsNone(result['cold_peak_rss_median_bytes'])
        self.assertIsNone(result['warm_process_peak_bytes'])
        d = self.data(); d['cold'][0]['peak_rss_bytes'] = 0
        with self.assertRaises(ValueError): self.m.validate_measurements(d)

    def test_hash_mismatch_fails_measurement(self):
        for section in ('cold','warm'):
            d = self.data(); d[section][1]['pdf_sha256'] = 'e'*64
            with self.assertRaises(ValueError): self.m.validate_measurements(d)

    def test_m8_legacy_and_m12_acceptance_baselines_cannot_be_relabelled(self):
        d = self.data(); d['engine_baseline_sha'] = '9b9ad5b04c3cc89a18f620b346589381b693695b'
        with self.assertRaises(ValueError): self.m.validate_measurements(d)

    def test_invalid_or_missing_samples_are_not_imputed(self):
        for action in ('missing','negative_time','nan','repeat_index','wrong_scope'):
            d = self.data()
            if action == 'missing': d['warm'].pop()
            elif action == 'negative_time': d['cold'][0]['elapsed_ms'] = -1
            elif action == 'nan': d['warm'][0]['elapsed_ms'] = float('nan')
            elif action == 'repeat_index': d['warm'][1]['sample_index'] = 0
            else: d['warm'][0]['measurement_scope'] = 'total-system-memory'
            with self.assertRaises(ValueError): self.m.validate_measurements(d)

    def test_missing_negative_or_noninteger_allocation_fails(self):
        for n in (None, -1, 1.5, True):
            d = self.data(); d['warm'][0]['managed_allocated_bytes'] = n
            with self.assertRaises(ValueError): self.m.validate_measurements(d)
        d = self.data(); del d['warm'][0]['managed_allocated_bytes']
        with self.assertRaises(ValueError): self.m.validate_measurements(d)

    def test_allocation_summary_is_recomputed_from_raw_samples(self):
        d = self.data(); d['warm'][1]['managed_allocated_bytes'] = 100
        self.assertEqual(self.m.summarize_measurements(d)['warm_managed_allocated_median_bytes'], 55.)
        self.assertEqual(d['warm'][0]['managed_allocated_bytes'], 10)

    def test_parallel_outputs_are_isolated(self):
        with tempfile.TemporaryDirectory() as t:
            paths = self.m.parallel_output_paths(Path(t), 2)
            self.assertEqual(len(set(paths)), 2)
            self.assertTrue(all(p.parent == Path(t) for p in paths))
            with self.assertRaises(ValueError): self.m.parallel_output_paths(Path(t), 0)

    def test_source_environment_and_input_are_required(self):
        for field in ('source_sha','environment_id','input_sha256'):
            d = self.data(); d[field] = 'missing'
            with self.assertRaises(ValueError): self.m.validate_measurements(d)


if __name__ == '__main__': unittest.main()
