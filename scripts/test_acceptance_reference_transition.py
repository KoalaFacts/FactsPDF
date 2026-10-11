import importlib
from pathlib import Path
import subprocess
import tempfile
import unittest

class SourceTransitionTests(unittest.TestCase):
    def setUp(self):
        try: self.r=importlib.import_module('acceptance_reference_transition')
        except ModuleNotFoundError: self.fail('Reference-only source transition is not implemented')
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name)
        self.git('init','-q');self.git('config','user.name','Test');self.git('config','user.email','test@example.invalid')
        self.write('src/engine.txt','unchanged engine')
        self.old=self.commit()

    def git(self,*args):
        return subprocess.check_output(['git','-C',str(self.root),*args],stderr=subprocess.STDOUT).decode().strip()
    def write(self,path,text):
        target=self.root/path;target.parent.mkdir(parents=True,exist_ok=True);target.write_text(text)
    def commit(self):
        self.git('add','.');self.git('commit','-qm','fixture');return self.git('rev-parse','HEAD')
    def review(self,policy=True):
        value={'source_sha':self.old}
        if policy:value['source_transition_policy']='reference-files-only-v1'
        return value

    def test_exact_source_keeps_legacy_behavior(self):
        self.r.validate_source_transition({'source_sha':self.old},self.old,self.root)

    def test_mismatch_remains_rejected_without_explicit_transition_policy(self):
        self.write('docs/acceptance/reference-reviews.json','{}');new=self.commit()
        with self.assertRaises(ValueError): self.r.validate_source_transition(self.review(False),new,self.root)

    def test_reference_metadata_and_artifacts_only_transition_is_allowed(self):
        self.write('docs/acceptance/reference-reviews.json','{}')
        self.write('docs/acceptance/references/B01/native.pdf','%PDF-test')
        self.write('docs/acceptance/references/B01/text.txt','hello')
        self.write('docs/acceptance/references/B01/page-1.png','png-test')
        new=self.commit()
        self.r.validate_source_transition(self.review(),new,self.root)
        self.assertEqual(self.review()['source_sha'],self.old)

    def test_changed_engine_tests_workflow_or_unrelated_docs_are_rejected(self):
        for path in ('src/engine.txt','scripts/acceptance_corpus.py','.github/workflows/x.yml','README.md'):
            self.git('reset','--hard',self.old)
            self.git('clean','-fd')
            self.write(path,'change');new=self.commit()
            with self.subTest(path=path),self.assertRaises(ValueError):
                self.r.validate_source_transition(self.review(),new,self.root)

    def test_reference_folder_cannot_smuggle_executables_or_symlinks(self):
        self.write('docs/acceptance/references/B00/hook.py','print(1)');new=self.commit()
        with self.assertRaises(ValueError):self.r.validate_source_transition(self.review(),new,self.root)
        self.git('reset','--hard',self.old);self.git('clean','-fd')
        link=self.root/'docs/acceptance/references/B00/native.pdf';link.parent.mkdir(parents=True)
        link.symlink_to('../../../../src/engine.txt');new=self.commit()
        with self.assertRaises(ValueError):self.r.validate_source_transition(self.review(),new,self.root)

    def test_forged_candidate_sha_and_uncommitted_changes_are_rejected(self):
        self.write('docs/acceptance/reference-reviews.json','{}');new=self.commit()
        with self.assertRaises(ValueError):self.r.validate_source_transition(self.review(),self.old,self.root)
        self.write('src/engine.txt','dirty')
        with self.assertRaises(ValueError):self.r.validate_source_transition(self.review(),new,self.root)

    def test_unrelated_history_cannot_reuse_approval(self):
        self.git('checkout','--orphan','unrelated');self.git('rm','-rf','.')
        self.write('docs/acceptance/reference-reviews.json','{}');new=self.commit()
        with self.assertRaises(ValueError):self.r.validate_source_transition(self.review(),new,self.root)

if __name__=='__main__':unittest.main()
