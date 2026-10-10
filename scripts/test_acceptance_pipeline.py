import copy
import importlib
from pathlib import Path
import tempfile
import unittest
from acceptance_corpus import generate_corpus, load_manifest, summarize_results


class PipelineTests(unittest.TestCase):
    def setUp(self):
        try: self.p = importlib.import_module('acceptance_pipeline')
        except ModuleNotFoundError: self.fail('Task 5 pipeline is not implemented')
        self.tmp=tempfile.TemporaryDirectory(); self.addCleanup(self.tmp.cleanup)
        self.root=Path(self.tmp.name); generate_corpus(self.root)
        self.manifest=load_manifest(self.root/'manifest.json')
        self.case=next(c for c in self.manifest['cases'] if c['id']=='B00')

    def rows(self):
        return [{'case_id':'B00','entrypoint':ep,'status':'candidate','pdf_sha256':'b'*64,
                 'pdf_bytes':100} for ep in ('cli','api')]

    def test_summary_separates_regressions_errors_and_targets(self):
        rows=[]
        for c in self.manifest['cases']:
            state={'baseline':'pending-review','negative':'expected-error','target':'unsupported'}[c['kind']]
            if c['input_path'] is None:state='blocked-design'
            rows.append({'case_id':c['id'],'status':state})
        s=summarize_results(self.manifest,rows)
        self.assertEqual(s['baseline_regression_passed'],0); self.assertEqual(s['negative_expected_errors'],12)
        self.assertEqual(s['target_total'],9); self.assertEqual(s['target_verified_count'],0)
        self.assertFalse(s['preview_ready'])

    def test_strict_targets_fails_when_any_target_is_unverified(self):
        with self.assertRaises(ValueError):self.p.require_verified_targets({'target_total':9,'target_verified_count':8})
        self.p.require_verified_targets({'target_total':9,'target_verified_count':9})

    def test_missing_review_fails_baseline_verify(self):
        with self.assertRaises(ValueError):self.p.review_map({'schema_version':1,'reviews':[]},self.manifest,required=True)

    def test_missing_or_duplicate_applicable_entrypoint_fails(self):
        rows=self.rows()
        for bad in (rows[:1],rows+[rows[0]],rows+[{'case_id':'B00','entrypoint':'wasm'}]):
            with self.assertRaises(ValueError):self.p.check_entrypoints(self.case,bad)

    def test_cross_entrypoint_bytes_must_match(self):
        rows=self.rows();self.p.check_entrypoints(self.case,rows)
        rows[1]['pdf_sha256']='c'*64
        with self.assertRaises(ValueError):self.p.check_entrypoints(self.case,rows)

    def test_artifact_allowlist_excludes_fonts_binaries_and_secrets(self):
        for name in ('font.ttf','cli.exe','access-token.txt','.env','native.bin','nested/font.otf'):
            with self.assertRaises(ValueError):self.p.public_copy(self.root/name,self.root/'public',name)
        f=self.root/'ok.pdf';f.write_bytes(b'%PDF-test')
        target=self.p.public_copy(f,self.root/'public','B00/native.pdf')
        self.assertEqual(target.read_bytes(),b'%PDF-test')
        with self.assertRaises(ValueError):self.p.public_copy(f,self.root/'public','../native.pdf')

    def test_report_contains_exact_source_and_next_gap(self):
        text=self.p.report_markdown({'source_sha':'a'*40,'baseline_total':6,'baseline_regression_passed':0,
            'negative_expected_errors':12,'negative_total':12,'target_total':9,'target_verified_count':0,
            'baseline_independently_checked':6},[])
        self.assertIn('a'*40,text);self.assertIn('pending',text.lower());self.assertIn('9',text)
        self.assertIn('not',text.lower())

    def test_bad_negative_does_not_make_evidence_complete(self):
        rows=[]
        for c in self.manifest['cases']:
            state={'baseline':'passed','negative':'wrong-error','target':'unsupported'}[c['kind']]
            rows.append({'case_id':c['id'],'status':state,'verified_requirements':c['requirements'],
                         'review_status':'approved','inspection_passed':True,'evidence_sha256':'b'*64})
        self.assertFalse(summarize_results(self.manifest,rows)['evidence_complete'])

    def test_reviews_cannot_duplicate_a_case(self):
        with self.assertRaises(ValueError):
            self.p.review_map({'schema_version':1,'reviews':[{'case_id':'B00'},{'case_id':'B00'}]},self.manifest)


if __name__=='__main__':unittest.main()
