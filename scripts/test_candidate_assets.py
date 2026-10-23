"""Synthetic metadata fixtures test policy, not real RC2 package acceptance."""
import json
import tempfile
import unittest
from pathlib import Path
from zipfile import ZipFile
import verify_package as v

SHA='a'*40
VERSION='0.1.0-dev'
TARGETS=('net10.0','net11.0')
class CandidateAssetsTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup)
        self.root=Path(self.tmp.name)
        for n in ('LICENSE.md','README.md'):(self.root/n).write_bytes(n.encode())
    def package(self, targets=TARGETS, mutate=None, extra=None):
        assets={f'lib/{t}/FactsPDF.dll':json.dumps(dict(name='FactsPDF',assemblyVersion='0.1.0.0',culture='',publicKey='',runtimeReferences=[t[3:]+'.0.0'],targetFramework='.NETCoreApp,Version=v'+t[3:],informationalVersion=VERSION+'+'+SHA)).encode() for t in targets}
        if mutate:mutate(assets)
        if extra:assets.update(extra)
        xml=f'<package><metadata><id>FactsPDF</id><version>{VERSION}</version><license type="file">LICENSE.md</license><readme>README.md</readme><repository type="git" url="https://github.com/KoalaFacts/FactsPDF" commit="{SHA}"/><dependencies>'+''.join(f'<group targetFramework="{t}"/>' for t in targets)+'</dependencies></metadata></package>'
        p=self.root/'candidate.nupkg'
        with ZipFile(p,'w') as z:
            z.writestr('FactsPDF.nuspec',xml)
            for n in ('LICENSE.md','README.md'):z.writestr(n,n.encode())
            for n,data in assets.items():z.writestr(n,data)
        return p
    def validate(self,p,targets=TARGETS):
        return v.validate_package(p,self.root,VERSION,SHA,targets,assembly_reader=json.loads)
    def test_accepts_exact_dual_assets_and_preserves_single_contract(self):
        r=self.validate(self.package());self.assertEqual(set(r['assemblies']),set(TARGETS))
        r=self.validate(self.package(('net10.0',)),'net10.0');self.assertEqual(r['assembly'],r['assemblies']['net10.0'])
    def test_rejects_missing_unexpected_and_copied_old_asset(self):
        changes=[lambda a:a.pop('lib/net11.0/FactsPDF.dll'),lambda a:a.update({'lib/net11.0/FactsPDF.dll':a['lib/net10.0/FactsPDF.dll']})]
        for change in changes:
            with self.subTest(change=change),self.assertRaises(AssertionError):self.validate(self.package(mutate=change))
        with self.assertRaises(AssertionError):self.validate(self.package(extra={'lib/net12.0/FactsPDF.dll':b'extra'}))
    def test_rejects_wrong_binary_identity_framework_version_and_source(self):
        for key,value in [('name','Other'),('assemblyVersion','9.0.0.0'),('culture','en-US'),('publicKey','01'),('targetFramework','.NETCoreApp,Version=v10.0'),('informationalVersion',VERSION+'+'+'b'*40)]:
            def change(a):
                n='lib/net11.0/FactsPDF.dll';m=json.loads(a[n]);m[key]=value;a[n]=json.dumps(m).encode()
            with self.subTest(key=key),self.assertRaises(AssertionError):self.validate(self.package(mutate=change))
    def test_rejects_missing_duplicate_or_old_runtime_reference_after_retagging(self):
        for references in ([], ['10.0.0.0'], ['11.0.0.0','11.0.0.0']):
            def change(a):
                n='lib/net11.0/FactsPDF.dll';m=json.loads(a[n]);m['runtimeReferences']=references;a[n]=json.dumps(m).encode()
            with self.subTest(references=references),self.assertRaisesRegex(AssertionError, 'Assembly runtime reference'):
                self.validate(self.package(mutate=change))

    def test_requires_reader_and_rejects_duplicate_target_configuration(self):
        with self.assertRaises(AssertionError):v.validate_package(self.package(),self.root,VERSION,SHA,TARGETS)
        with self.assertRaises(AssertionError):self.validate(self.package(),('net10.0','net10.0'))
    def test_rejects_dependency_groups_outside_declared_dependencies(self):
        for targets in (('net10.0',), TARGETS):
            p=self.package(targets)
            with ZipFile(p) as z:files={n:z.read(n) for n in z.namelist()}
            files['FactsPDF.nuspec']=files['FactsPDF.nuspec'].replace(b'</metadata>',b'<group targetFramework="net10.0"/></metadata>')
            with ZipFile(p,'w') as z:
                for n,data in files.items():z.writestr(n,data)
            with self.subTest(targets=targets),self.assertRaisesRegex(AssertionError, 'Unexpected dependency group'):
                self.validate(p,targets if len(targets)>1 else targets[0])

    def test_rejects_duplicate_zip_entry(self):
        import warnings
        p=self.package()
        with warnings.catch_warnings():
            warnings.simplefilter('ignore')
            with ZipFile(p,'a') as z:z.writestr('lib/net11.0/FactsPDF.dll',b'duplicate')
        with self.assertRaises(AssertionError):self.validate(p)
    def test_consumer_selection_binds_target_and_bytes_not_cli_asset(self):
        r=self.validate(self.package());selected={'compile':['lib/net11.0/FactsPDF.dll'],'runtime':['lib/net11.0/FactsPDF.dll']}
        v.validate_selected_asset(r,'net11.0',selected,r['assemblies']['net11.0'])
        for bad in [dict(compile=['lib/net10.0/FactsPDF.dll'],runtime=selected['runtime']),dict(compile=selected['compile'],runtime=['lib/net10.0/FactsPDF.dll'])]:
            with self.assertRaises(AssertionError):v.validate_selected_asset(r,'net11.0',bad,r['assemblies']['net11.0'])
        with self.assertRaises(AssertionError):v.validate_selected_asset(r,'net11.0',selected,r['assemblies']['net10.0'])
        with self.assertRaises(AssertionError):v.validate_selected_asset(r,'net12.0',selected,r['assemblies']['net11.0'])
if __name__=='__main__':unittest.main()
