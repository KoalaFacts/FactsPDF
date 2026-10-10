import copy
import importlib
from pathlib import Path
import tempfile
import unittest

class InspectionTests(unittest.TestCase):
    def setUp(self):
        try: self.i = importlib.import_module('acceptance_inspect')
        except ModuleNotFoundError: self.fail('Task 3 acceptance_inspect is not implemented')
        self.case = {'id':'B-test','kind':'baseline','input_sha256':'a'*64, 'requirements':['records'],
                     'records':['R0001','R0002'], 'checks': {'text_blocks':['Report','R0001 A B 中文。','R0002 Done.']}}
        self.environment = {'environment_id':'b'*64, 'fonts':[]}

    def test_missing_or_reordered_record_fails(self):
        good = 'Report\nR0001 A B 中文。\nR0002 Done.\n'
        self.i.check_text(good,self.case)
        for bad in [good.replace('中文','中'), good.replace('A B','AB'), good.replace('R0002','R0001'),
                    'Report\nR0002 Done.\nR0001 A B 中文。']:
            with self.assertRaises(ValueError): self.i.check_text(bad,self.case)

    def test_outside_page_box_is_rejected(self):
        good='<html><body><doc><page width="595.28" height="841.89"><word xMin="40" yMin="40" xMax="70" yMax="52">A</word></page></doc></body></html>'
        self.i.check_boxes(good.encode(),36)
        for bad in [good.replace('xMin="40"','xMin="2"'),good.replace('xMax="70"','xMax="nan"')]:
            with self.assertRaises(ValueError): self.i.check_boxes(bad.encode(),36)

    def test_missing_review_fails_closed(self):
        with self.assertRaises(ValueError): self.i.validate_reference({},self.case,self.environment)

    def test_environment_or_font_drift_requires_new_review(self):
        review = self.review()
        self.i.validate_reference(review,self.case,self.environment)
        for key in ('environment_id','input_sha256'):
            wrong=copy.deepcopy(review); wrong[key]='c'*64
            with self.assertRaises(ValueError): self.i.validate_reference(wrong,self.case,self.environment)

    def review(self):
        return {'case_id':'B-test','approval':'approved','reviewer_kind':'human','reviewer':'test-fixture-human',
                'review_url':'https://example.invalid/review','reviewed_at':'2026-10-11',
                'input_sha256':'a'*64,'environment_id':'b'*64,'font_inputs':[], 'source_sha':'c'*40,
                'pdf_sha256':'d'*64,'text_sha256':'e'*64,'page_image_sha256':['f'*64],
                'pages':1,'artifact_location':'docs/acceptance/reference/B-test.pdf','requirements':['records']}

    def test_capture_cannot_approve_itself(self):
        r=self.review(); r['reviewer_kind']='agent';
        with self.assertRaises(ValueError): self.i.validate_reference(r,self.case,self.environment)

    def test_reference_rejects_expiring_only_artifact(self):
        r=self.review(); r['artifact_location']='https://example.invalid/artifact?sig=secret&expires=tomorrow'
        with self.assertRaises(ValueError): self.i.validate_reference(r,self.case,self.environment)

    def test_invalid_xref_or_tool_failure_is_not_pass(self):
        with tempfile.TemporaryDirectory() as t:
            fake=Path(t)/'bad.pdf'; fake.write_bytes(b'%PDF-not-valid')
            with self.assertRaises((ValueError,RuntimeError,OSError)):
                self.i.inspect_pdf(fake,self.case,self.environment,Path(t)/'inspect')

    def test_pdf_content_and_pixels_both_required(self):
        ref=self.review()
        candidate={'pdf_sha256':ref['pdf_sha256'],'text_sha256':ref['text_sha256'],
                   'page_image_sha256':ref['page_image_sha256'],'pages':1, 'checks_passed':True}
        self.assertTrue(self.i.compare_evidence(ref,candidate,self.case)['matched'])
        candidate['page_image_sha256']=['0'*64]
        with self.assertRaises(ValueError): self.i.compare_evidence(ref,candidate,self.case)

if __name__=='__main__': unittest.main()
