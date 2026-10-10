import importlib
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

class ReviewRoundTwoTests(unittest.TestCase):
    def test_standalone_rejects_prior_checkout_even_when_records_and_review_agree(self):
        from acceptance_corpus import generate_corpus,load_manifest,write_json
        inspector=importlib.import_module('acceptance_inspect')
        with tempfile.TemporaryDirectory() as t:
            root=Path(t);generate_corpus(root/'corpus')
            manifest=load_manifest(root/'corpus/manifest.json');manifest['cases']=manifest['cases'][:1]
            write_json(root/'corpus/manifest.json',manifest);case=manifest['cases'][0]
            env={'source_sha':'a'*40,'environment_id':'b'*64,'fonts':[]}
            runs=root/'runs';runs.mkdir();write_json(runs/'environment.json',env)
            rows=[{'case_id':case['id'],'entrypoint':ep,'status':'candidate','pdf_path':'unused.pdf',
                   'pdf_sha256':'c'*64,'pdf_bytes':100,'source_sha':env['source_sha'],
                   'environment_id':env['environment_id'],'input_sha256':case['input_sha256'],'font_inputs':[]}
                  for ep in ('cli','api')]
            write_json(runs/'runs.json',{'runs':rows})
            argv=['inspect','capture','--manifest',str(root/'corpus/manifest.json'),'--runs',str(runs),'--output',str(root/'out')]
            with patch('sys.argv',argv),patch.object(inspector,'inspect_pdf',return_value={'checks_passed':True}):
                with self.assertRaisesRegex(ValueError,'source|checkout'):
                    inspector.main()

    def test_other_paper_size_cannot_pass_physical_bounds(self):
        inspector=importlib.import_module('acceptance_inspect')
        xml=b'<html><page width="612" height="792"><word xMin="40" yMin="40" xMax="70" yMax="52">A</word></page></html>'
        for reference in (False,True):
            with self.assertRaisesRegex(ValueError,'dimension|size|paper'):
                inspector.classify_page_bounds(xml,36,reference)

    def test_inspected_file_hash_and_length_must_match_run_record(self):
        inspector=importlib.import_module('acceptance_inspect')
        row={'pdf_sha256':'a'*64,'pdf_bytes':100}
        with self.assertRaises(ValueError):inspector.validate_run_output(row,{'pdf_sha256':'b'*64,'pdf_bytes':100})
        with self.assertRaises(ValueError):inspector.validate_run_output(row,{'pdf_sha256':'a'*64,'pdf_bytes':101})
        inspector.validate_run_output(row,dict(row))

    def test_generated_or_merely_staged_reference_is_not_durable(self):
        from acceptance_corpus import digest
        pipeline=importlib.import_module('acceptance_pipeline')
        with tempfile.TemporaryDirectory() as t:
            root=Path(t);subprocess.run(['git','init','-q',str(root)],check=True)
            folder=root/'docs/acceptance/references/B00';folder.mkdir(parents=True)
            for name,data in [('native.pdf',b'%PDF-test'),('text.txt',b'A'),('page-1.png',b'png-test')]:
                (folder/name).write_bytes(data)
            review={'artifact_location':folder.relative_to(root).as_posix(),
                    'pdf_sha256':digest(b'%PDF-test'),'text_sha256':digest(b'A'),
                    'page_image_sha256':[digest(b'png-test')]}
            with self.assertRaisesRegex(ValueError,'committed|tracked|HEAD'):
                pipeline._stored_reference(review,root)
            subprocess.run(['git','-C',str(root),'add','.'],check=True)
            with self.assertRaisesRegex(ValueError,'committed|tracked|HEAD'):
                pipeline._stored_reference(review,root)
            subprocess.run(['git','-C',str(root),'-c','user.name=Test','-c','user.email=test@example.invalid',
                            'commit','-qm','test references'],check=True)
            pipeline._stored_reference(review,root)

if __name__=='__main__':unittest.main()
