import importlib
from pathlib import Path
import tempfile
import unittest


class InspectionIntegrityTests(unittest.TestCase):
    def setUp(self):
        self.i=importlib.import_module('acceptance_inspect')
        self.case={'id':'B-test','input_sha256':'a'*64,'requirements':[],'records':[],'checks':{'text_blocks':['A']}}
        self.environment={'environment_id':'b'*64,'fonts':[]}

    def test_standalone_inspector_rejects_missing_entrypoints_and_unknown_cases(self):
        from unittest.mock import patch
        from acceptance_corpus import generate_corpus, write_json, load_manifest
        with tempfile.TemporaryDirectory() as t:
            root=Path(t); corpus=root/'corpus'; generate_corpus(corpus)
            case=load_manifest(corpus/'manifest.json')['cases'][0]
            runs=root/'runs'; runs.mkdir()
            write_json(runs/'environment.json',{'source_sha':'a'*40,'environment_id':'b'*64,'fonts':[]})
            write_json(runs/'runs.json',{'runs':[{'case_id':case['id'],'entrypoint':'cli',
                'status':'candidate','pdf_path':'not-used.pdf','pdf_sha256':'c'*64,'pdf_bytes':10}]})
            argv=['acceptance_inspect.py','capture','--manifest',str(corpus/'manifest.json'),
                  '--runs',str(runs),'--output',str(root/'out')]
            with patch('sys.argv',argv), patch.object(self.i,'inspect_pdf',return_value={'checks_passed':True}):
                with self.assertRaises(ValueError):self.i.main()

    def test_chrome_uses_existing_working_browser_selection_and_startup_flags(self):
        from unittest.mock import patch
        with tempfile.TemporaryDirectory() as t:
            source=Path(t)/'input.html';source.write_text('<html><head></head><body>A</body></html>')
            commands=[]
            def run(argv, out):
                commands.append(argv);return b''
            with patch.object(self.i.shutil,'which',side_effect=lambda name:'/tools/'+name), \
                 patch.object(self.i,'tool',side_effect=run), \
                 patch.object(self.i,'inspect_pdf',return_value={}):
                self.i.chrome_reference(source,self.case,[],self.environment,Path(t)/'chrome')
            self.assertEqual(commands[0][0],'/tools/google-chrome')
            for flag in ('--headless=new','--disable-gpu','--disable-extensions','--no-first-run','--no-default-browser-check'):
                self.assertIn(flag,commands[0])
            text=(Path(t)/'chrome/chrome-input.html').read_text()
            self.assertIn('print-color-adjust:exact',text)
            self.assertIn('font-kerning:none',text)


if __name__=='__main__':unittest.main()
