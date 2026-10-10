import copy
import importlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
from acceptance_corpus import generate_corpus, load_manifest, write_json


class ReviewRegressionTests(unittest.TestCase):
    def review(self, case, env):
        return {'case_id':case['id'], 'approval':'approved','reviewer_kind':'human',
                'reviewer':'test-fixture-human','review_url':'https://example.invalid/review',
                'reviewed_at':'2026-10-11','input_sha256':case['input_sha256'],
                'environment_id':env['environment_id'],'font_inputs':env['fonts'],
                'source_sha':env['source_sha'],'requirements':case['requirements'],
                'pdf_sha256':'d'*64,'text_sha256':'e'*64,'page_image_sha256':['f'*64],
                'pages':1,'artifact_location':'does-not-exist/reference'}

    def test_byte_identical_output_cannot_reuse_an_approval_for_another_commit(self):
        inspector = importlib.import_module('acceptance_inspect')
        case = {'id':'B00','input_sha256':'a'*64,'requirements':['text']}
        env={'source_sha':'b'*40,'environment_id':'c'*64,'fonts':[]}
        reviewed=self.review(case,env)
        inspector.validate_reference(reviewed,case,env)
        reviewed['source_sha']='0'*40
        with self.assertRaisesRegex(ValueError,'source|commit|checkout'):
            inspector.validate_reference(reviewed,case,env)

    def test_unexpected_success_preserves_probe_prefix_but_is_not_infrastructure_failure(self):
        runner=importlib.import_module('acceptance_run')
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); generate_corpus(root/'corpus')
            case=next(c for c in load_manifest(root/'corpus/manifest.json')['cases'] if c['id']=='N-pages')
            case['_root']=root/'corpus'
            fake=root/'host'
            fake.write_text('#!'+sys.executable+'\nimport json,sys\nfrom pathlib import Path\n'
                'Path(sys.argv[3]).write_bytes(b"keep%PDF-1.7\\n")\n'
                'print(json.dumps({"status":"success","pages":1,"pdf_bytes":13}))\n')
            fake.chmod(0o755)
            row=runner.run_case(case,'api',fake,[],root/'out',2)
            self.assertEqual(row['status'],'unexpected-success')
            self.assertFalse(row['output_unchanged'])

    def test_standalone_verify_requires_actual_durable_reference_files(self):
        inspector=importlib.import_module('acceptance_inspect')
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); generate_corpus(root/'corpus')
            m=load_manifest(root/'corpus/manifest.json')
            m['cases']=[next(c for c in m['cases'] if c['id']=='B00')]
            write_json(root/'corpus/manifest.json',m); case=m['cases'][0]
            env={'source_sha':'b'*40,'environment_id':'c'*64,'fonts':[]}
            reviewed=self.review(case,env)
            runs=root/'runs';runs.mkdir()
            write_json(runs/'environment.json',env)
            rows=[{'case_id':case['id'],'entrypoint':ep,'status':'candidate','pdf_path':'unused.pdf',
                   'pdf_sha256':'d'*64,'pdf_bytes':100,'source_sha':env['source_sha'],
                   'environment_id':env['environment_id'],'input_sha256':case['input_sha256'],
                   'font_inputs':[]} for ep in ('cli','api')]
            write_json(runs/'runs.json',{'runs':rows})
            write_json(root/'reviews.json',{'schema_version':1,'reviews':[reviewed]})
            argv=['acceptance_inspect.py','verify','--manifest',str(root/'corpus/manifest.json'),
                  '--runs',str(runs),'--reviews',str(root/'reviews.json'),'--output',str(root/'out')]
            candidate={**reviewed,'checks_passed':True}
            with patch('sys.argv',argv),patch.object(inspector,'inspect_pdf',return_value=candidate):
                with self.assertRaisesRegex(ValueError,'reference|missing'):
                    inspector.main()

class ReferenceObservationTests(unittest.TestCase):
    def test_reference_margin_difference_is_reported_without_approving_it(self):
        inspector=importlib.import_module('acceptance_inspect')
        xml=b'<html><page width="595.28" height="841.89"><word xMin="40" yMin="34" xMax="70" yMax="46">A</word></page></html>'
        with self.assertRaises(ValueError): inspector.classify_page_bounds(xml,36,False)
        pages,observation=inspector.classify_page_bounds(xml,36,True)
        self.assertEqual(len(pages),1)
        self.assertIn('34',observation)
        outside=xml.replace(b'yMin="34"',b'yMin="-1"')
        with self.assertRaises(ValueError):inspector.classify_page_bounds(outside,36,True)

    def test_one_pixel_pdf_rounding_keeps_original_origin_and_dimensions(self):
        from PIL import Image
        pipeline=importlib.import_module('acceptance_pipeline')
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); a=root/'a';b=root/'b';a.mkdir();b.mkdir()
            Image.new('RGB',(10,10),'white').save(a/'page-1.png')
            im=Image.new('RGB',(9,10),'white');im.putpixel((0,0),(0,0,0));im.save(b/'page-1.png')
            result=pipeline.compare_images(a,b,root/'public','B00')
            self.assertTrue((root/'public/B00/overlay-page-1.png').exists())
            self.assertEqual(result['pages'][0]['chrome'],[9,10])
            self.assertEqual(result['pages'][0]['native'],[10,10])
            self.assertEqual(result['pages'][0]['padding_policy'],'white-right-bottom-only-no-registration')

if __name__=='__main__':unittest.main()
