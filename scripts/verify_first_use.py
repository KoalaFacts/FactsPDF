"""Consume a local NuGet package outside the checkout; never publish or use remote feeds."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
from xml.etree import ElementTree as ET
from verify_package import current_source, validate_package, validate_assembly_source


def digest(data): return hashlib.sha256(data).hexdigest()
def tool(name): return os.environ.get('FACTSPDF_'+name.upper(),name)


def stage_consumer(source,target):
    target.mkdir()
    for name in ('FirstUse.csproj','Program.cs','NuGet.Config','input.html','unsupported.html','expected-pages.json'):
        shutil.copyfile(source/name,target/name)


def build_environment(ambient,work):
    # MSBuild treats arbitrary environment variables as properties. A denylist of
    # known hooks cannot establish a clean consumer; retain only OS process basics.
    allowed={'PATH','HOME','SYSTEMROOT','WINDIR','COMSPEC','PATHEXT','TEMP','TMP','TMPDIR','LANG','LC_ALL','TZ'}
    env={key:value for key,value in ambient.items() if key.upper() in allowed}
    settings=dict(NUGET_PACKAGES=str(work/'packages'),NUGET_HTTP_CACHE_PATH=str(work/'http-cache'),
        NUGET_SCRATCH=str(work/'nuget-scratch'),DOTNET_CLI_HOME=str(work/'dotnet-home'),
        DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_NOLOGO='1',DOTNET_GENERATE_ASPNET_CERTIFICATE='false',
        DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false',DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='true',
        MSBuildUserExtensionsPath=str(work/'msbuild-user'))
    for suffix in ('BeforeMicrosoftCommonProps','AfterMicrosoftCommonProps',
                   'BeforeMicrosoftCommonTargets','AfterMicrosoftCommonTargets'):
        settings['ImportUserLocationsByWildcard'+suffix]='false'
    env.update(settings)
    return env,settings


def validate_imports(preprocessed,root,project):
    # -preprocess includes a resolved absolute filename in each expanded import
    # boundary comment. Unlike DirectoryBuild* properties, this sees indirect imports.
    parser=ET.XMLParser(target=ET.TreeBuilder(insert_comments=True))
    document=ET.fromstring(preprocessed,parser=parser)
    paths=[]
    # The initial project boundary precedes the root element, so include it too.
    comments=re.findall(r'<!--(.*?)-->',preprocessed,re.S)
    for comment in comments:
        for candidate in re.findall(r'\n([^\r\n]+)\r?\n={10,}',comment):
            path=Path(candidate.strip())
            if path.is_absolute() and str(path.resolve()) not in paths:paths.append(str(path.resolve()))
    assert document.tag.split('}')[-1]=='Project' and str(project.resolve()) in paths and len(paths)>1,'missing import graph'
    root=root.resolve()
    assert all(Path(p)!=root and root not in Path(p).parents for p in paths),'repository build import'
    return paths


def validate_workspace(work,root):
    work=work.resolve();root=root.resolve()
    if work==root or root in work.parents: raise ValueError('Consumer must be outside the repository')
    for parent in [work,*work.parents]:
        for name in ('Directory.Build.props','Directory.Build.targets','Directory.Packages.props'):
            if (parent/name).exists():raise ValueError('Unexpected inherited build configuration: '+str(parent/name))


def validate_assets(assets,work,version):
    expected=work.resolve();restore=assets['project']['restore']
    paths=lambda values:{Path(v).resolve() for v in values}
    assert paths(assets['packageFolders'])=={expected/'packages'},'package folders'
    assert Path(restore['packagesPath']).resolve()==expected/'packages','package cache'
    assert paths(restore['sources'])=={expected/'feed'},'restore sources'
    assert paths(restore['configFilePaths'])=={expected/'consumer/NuGet.Config'},'restore config'
    assert Path(restore['projectPath']).resolve()==expected/'consumer/FirstUse.csproj','restore project'
    assert restore['projectStyle']=='PackageReference','project style'
    assert set(restore['frameworks'])=={'net10.0'},'consumer target framework'
    assert all(not f.get('projectReferences') for f in restore['frameworks'].values()),'source project reference'
    assert set(assets['libraries'])=={'FactsPDF/'+version},'resolved package identity/version'
    library=assets['libraries']['FactsPDF/'+version]
    assert library['type']=='package' and library['path']=='factspdf/'+version,'resolved package provenance'


def validate_text(actual,expected):
    pages=actual.split('\f')
    if pages and not pages[-1].strip():pages.pop()
    normalize=lambda s:' '.join(s.split())
    assert list(map(normalize,pages))==list(map(normalize,expected)),'authored text/page order'


def validate_runtime(runtime,target):
    framework=re.fullmatch(r'net(\d+)\.(\d+)',target)
    assert framework,'expected consumer target'
    version='.'.join(framework.groups())
    assert all(isinstance(runtime.get(k),str) and runtime[k].strip()
               for k in ('version','framework','os','rid','architecture','targetFramework')),'missing runtime identity'
    assert re.fullmatch(re.escape(version)+r'\.\d+(?:[-+].+)?',runtime['version']),'actual runtime version'
    assert runtime['targetFramework']=='.NETCoreApp,Version=v'+version,'actual consumer target'
    assert runtime.get('dynamicCodeSupported') is True and runtime.get('dynamicCodeCompiled') is True,'managed dynamic-code runtime'


def verify_package_bytes(work,package,version,expected_dll):
    cache=work/'packages/factspdf'/version
    assert (cache/f'factspdf.{version}.nupkg').read_bytes()==package.read_bytes(),'cached package bytes'
    checksum=base64.b64encode(hashlib.sha512(package.read_bytes()).digest()).decode()
    assert (cache/f'factspdf.{version}.nupkg.sha512').read_text().strip()==checksum,'package SHA512'
    assert (cache/'lib/net10.0/FactsPDF.dll').read_bytes()==expected_dll,'cached assembly bytes'
    assert (work/'consumer/bin/Release/net10.0/FactsPDF.dll').read_bytes()==expected_dll,'consumer assembly bytes'


def inspect_pdf(path,expected,execute,raster=True):
    assert path.read_bytes().startswith(b'%PDF-1.7'),'PDF signature'
    execute(tool('qpdf'),'--check',path)
    text=execute(tool('pdftotext'),'-raw',path,'-').stdout.decode()
    validate_text(text,expected)
    bbox=execute(tool('pdftotext'),'-bbox',path,'-').stdout
    document=ET.fromstring(bbox);ns='{http://www.w3.org/1999/xhtml}'
    pages=list(document.iter(ns+'page'));assert len(pages)==len(expected)==2,'authored page count'
    for page in pages:
        assert abs(float(page.attrib['width'])-595.28)<.01 and abs(float(page.attrib['height'])-841.89)<.01,'page dimensions'
        words=list(page.iter(ns+'word'));assert words,'empty page'
        for word in words:
            x0,x1,y0,y1=[float(word.attrib[k]) for k in ('xMin','xMax','yMin','yMax')]
            assert 36-.01<=x0<x1<=559.28+.01 and 36-.01<=y0<y1<=805.89+.01,'content bounds'
    path.with_suffix('.txt').write_text(text);path.with_suffix('.bbox.html').write_bytes(bbox)
    images=[]
    if raster:
        execute(tool('pdftoppm'),'-png','-r','96',path,path.with_suffix(''))
        for i in range(1,3):
            image=path.with_name(path.stem+f'-{i}.png');assert image.is_file(),'rendered page missing'
            images.append(dict(page=i,file=image.name,sha256=digest(image.read_bytes())))
    return dict(pdfSha256=digest(path.read_bytes()),bytes=path.stat().st_size,pages=2,textAndBounds=True,qpdf=True,pageImages=images)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('package','cli-dll','output'):parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--dotnet',default='dotnet');args=parser.parse_args()
    root=Path(__file__).resolve().parents[1];package=args.package.resolve();cli=args.cli_dll.resolve();out=args.output.resolve()
    source_head=current_source(root)
    version=ET.parse(root/'Directory.Build.props').findtext('PropertyGroup/Version')
    metadata=validate_package(package,root,version,source_head);assembly=metadata['assembly']
    out.mkdir(parents=True,exist_ok=False)
    assert cli.is_file() and cli.with_suffix('.runtimeconfig.json').is_file() and cli.with_suffix('.deps.json').is_file(),'published CLI files'
    assert (cli.parent/'FactsPDF.dll').read_bytes()==assembly,'published CLI uses the newly packed assembly'
    commands=[]
    with tempfile.TemporaryDirectory(prefix='factspdf-first-use-') as temporary:
        work=Path(temporary).resolve();validate_workspace(work,root)
        for name in ('feed','packages','http-cache','dotnet-home','nuget-scratch','msbuild-user'): (work/name).mkdir()
        stage_consumer(root/'examples/first-use',work/'consumer')
        shutil.copy2(root/'global.json',work/'global.json')
        shutil.copy2(package,work/'feed'/package.name)
        shutil.copytree(cli.parent,work/'cli')
        env,isolated=build_environment(os.environ,work)
        def execute(*command,expected_exit=0):
            process=subprocess.run([str(c) for c in command],cwd=work/'consumer',env=env,capture_output=True,timeout=180)
            number=len(commands)+1;stem=out/f'command-{number:02}'
            stem.with_suffix('.stdout').write_bytes(process.stdout);stem.with_suffix('.stderr').write_bytes(process.stderr)
            commands.append(dict(command=list(map(str,command)),cwd=str(work/'consumer'),exitCode=process.returncode,
                stdout=stem.with_suffix('.stdout').name,stderr=stem.with_suffix('.stderr').name))
            assert process.returncode==expected_exit,(command,process.returncode,process.stderr.decode(errors='replace'))
            return process
        project=work/'consumer/FirstUse.csproj';config=work/'consumer/NuGet.Config'
        reference=ET.parse(project).find('.//PackageReference')
        assert reference.attrib=={'Include':'FactsPDF','Version':'['+version+']'},'sample package version'
        assert not any((work/'packages').iterdir()),'package cache must start empty'
        sdk=execute(args.dotnet,'--version').stdout.decode().strip()
        execute(args.dotnet,'restore',project,'--configfile',config,'--packages',work/'packages','--no-http-cache')
        assets=json.loads((work/'consumer/obj/project.assets.json').read_text());validate_assets(assets,work,version)
        evaluation=json.loads(execute(args.dotnet,'msbuild',project,
            '-getProperty:TargetFramework,DirectoryBuildPropsPath,DirectoryBuildTargetsPath,DirectoryPackagesPropsPath',
            '-getItem:ProjectReference').stdout)
        assert evaluation['Properties']['TargetFramework']=='net10.0','evaluated framework'
        assert not any(evaluation['Properties'][k] for k in ('DirectoryBuildPropsPath','DirectoryBuildTargetsPath','DirectoryPackagesPropsPath')),'inherited build properties'
        assert not evaluation['Items']['ProjectReference'],'evaluated project references'
        preprocessed=out/'consumer.preprocessed.xml'
        execute(args.dotnet,'msbuild',project,'-p:Configuration=Release','-preprocess:'+str(preprocessed))
        imports=validate_imports(preprocessed.read_text(),root,project)
        (out/'imports.json').write_text(json.dumps(imports,indent=2)+'\n')
        execute(args.dotnet,'build',project,'-c','Release','--no-restore')
        verify_package_bytes(work,package,version,assembly)
        for name,data in [('assets.json',assets),('evaluation.json',evaluation)]:
            (out/name).write_text(json.dumps(data,indent=2)+'\n')
        expected=json.loads((work/'consumer/expected-pages.json').read_text())
        input_path=work/'consumer/input.html';bad=work/'consumer/unsupported.html'
        consumer=work/'consumer/bin/Release/net10.0/FirstUse.dll'
        cli_identity=json.loads(execute(args.dotnet,consumer,'--inspect-cli',work/'cli'/cli.name).stdout)
        assert Path(cli_identity['assembly']).resolve()==work/'cli'/cli.name,'inspected CLI assembly location'
        validate_assembly_source(cli_identity.get('assemblyInformationalVersion'),version,source_head)
        entries={'consumer':[args.dotnet,consumer],'cli':[args.dotnet,work/'cli'/cli.name]}
        results=[];negatives=[];api=None
        for name,command in entries.items():
            pdf=out/(name+'.pdf');response=execute(*command,input_path,pdf)
            if name=='consumer':
                api=json.loads(response.stdout)
                assert Path(api['assembly']).resolve()==consumer.parent/'FactsPDF.dll','runtime assembly location'
                assert api.get('assemblyIdentity','').startswith('FactsPDF, Version='),'runtime assembly identity'
                validate_assembly_source(api.get('assemblyInformationalVersion'),version,source_head)
                validate_runtime(api.get('runtime',{}),evaluation['Properties']['TargetFramework'])
                assert api['pages']==2 and api['bytes']==pdf.stat().st_size,'API conversion metadata'
            else:assert not response.stdout,'CLI PDF went to stdout instead of destination'
            results.append(dict(entry=name,**inspect_pdf(pdf,expected,execute)))
            for target in ('existing','new','stdout') if name=='cli' else ('existing','new'):
                destination=out/f'{name}-{target}.pdf'
                if target=='existing':destination.write_bytes(b'keep original bytes')
                flags=['--overwrite'] if name=='cli' else []
                error=execute(*command,bad,'-' if target=='stdout' else destination,*flags,expected_exit=3)
                assert b'FPDF1102:' in error.stderr and not error.stdout,'unsupported input diagnostic/stdout'
                assert destination.read_bytes()==b'keep original bytes' if target=='existing' else not destination.exists(),'negative destination preservation'
                assert not list(out.glob('.factspdf-*.tmp')),'temporary output leak'
                negatives.append(dict(entry=name,target=target,code='FPDF1102',outputPreserved=True))
                if target=='existing':destination.unlink()
            recovery=out/(name+'-recovery.pdf');execute(*command,input_path,recovery)
            assert recovery.read_bytes()==pdf.read_bytes(),'recovery determinism'
        assert (out/'consumer.pdf').read_bytes()==(out/'cli.pdf').read_bytes(),'consumer/CLI bytes'
        # Reject an actual reordered PDF, not just a mocked conversion result.
        reversed_pdf=out/'reordered.pdf';execute(tool('qpdf'),out/'cli.pdf','--pages','.', '2,1','--',reversed_pdf)
        try:inspect_pdf(reversed_pdf,expected,execute,False)
        except AssertionError as error:assert str(error)=='authored text/page order',str(error)
        else:raise AssertionError('accepted reordered real PDF')
        reversed_pdf.unlink()
        # Cache provenance checks must reject a different real file, then recover.
        cached=work/'packages/factspdf'/version/'lib/net10.0/FactsPDF.dll';cached.write_bytes(b'not the packed assembly')
        try:verify_package_bytes(work,package,version,assembly)
        except AssertionError as error:assert str(error)=='cached assembly bytes',str(error)
        else:raise AssertionError('accepted wrong cached assembly')
        finally:cached.write_bytes(assembly)
        verify_package_bytes(work,package,version,assembly)
        assert current_source(root)==source_head,'Source changed during verification'
        report=dict(sourceHead=source_head,dirty=False,sdk=sdk,
            package=dict(name=package.name,id=metadata['id'],version=version,targetFramework=metadata['targetFramework'],
                repository=metadata['repository'],sha256=digest(package.read_bytes()),assemblySha256=digest(assembly)),
            cliSha256=digest(cli.read_bytes()),cliAssembly=cli_identity,
            inputs={p.name:digest(p.read_bytes()) for p in (work/'consumer').iterdir() if p.is_file()},
            isolation=dict(workspace=str(work),outsideRepository=True,initialPackageCacheEmpty=True,localFeedOnly=True,
                explicitConfigOnly=True,repositoryBuildImportsAbsent=True,packageCacheAndRuntimeAssemblyMatch=True,
                importCount=len(imports),environmentAllowlisted=True,userExtensionsIsolated=True,
                settings=isolated),api=api,cases=results,negativeCases=negatives,
            crossEntryBytesEqual=True,recoveryBytesEqual=True,reorderedPdfRejected=True,wrongCachedAssemblyRejected=True,
            commands=commands,visualReview='pending-engineering-inspection',userReferenceApproval='not requested',published=False)
        (out/'report.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(dict(report=str(out/'report.json'),pages=2,entries=2,isolatedWorkspaceRemoved=True)))


if __name__=='__main__':main()
