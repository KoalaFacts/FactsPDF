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
from verify_package import current_source, expected_contract, local_package, validate_package, validate_assembly_source


def digest(data): return hashlib.sha256(data).hexdigest()
def tool(name): return os.environ.get('FACTSPDF_'+name.upper(),name)


def stage_consumer(source,target):
    target.mkdir()
    for name in ('FirstUse.csproj','Program.cs','NuGet.Config','input.html','unsupported.html','expected-pages.json'):
        shutil.copyfile(source/name,target/name)


def configure_consumer(project,version,target):
    # Only the isolated copy is specialized. Expected values come from the
    # clean repository declaration or explicit consumer target, never the package.
    tree=ET.parse(project)
    frameworks=tree.findall('PropertyGroup/TargetFramework')
    references=tree.findall('.//PackageReference')
    assert len(frameworks)==1 and not tree.findall('.//TargetFrameworks'),'sample single target'
    assert len(references)==1 and references[0].get('Include')=='FactsPDF','sample package reference'
    assert not tree.findall('.//ProjectReference'),'sample source project reference'
    frameworks[0].text=target
    references[0].set('Version','['+version+']')
    tree.write(project,encoding='unicode')


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


def validate_assets(assets,work,version,target):
    expected=work.resolve();restore=assets['project']['restore']
    paths=lambda values:{Path(v).resolve() for v in values}
    assert paths(assets['packageFolders'])=={expected/'packages'},'package folders'
    assert Path(restore['packagesPath']).resolve()==expected/'packages','package cache'
    assert paths(restore['sources'])=={expected/'feed'},'restore sources'
    assert paths(restore['configFilePaths'])=={expected/'consumer/NuGet.Config'},'restore config'
    assert Path(restore['projectPath']).resolve()==expected/'consumer/FirstUse.csproj','restore project'
    assert restore['projectStyle']=='PackageReference','project style'
    assert set(restore['frameworks'])=={target},'consumer target framework'
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


def consumer_tfm(value):
    if not isinstance(value,str) or not re.fullmatch(r'net[1-9][0-9]*\.0',value):
        raise ValueError('Expected an explicit consumer TFM such as net10.0 or net11.0')
    return value


def validate_consumer_execution(runtime,target,config):
    """Bind the generated host request and live identity to the selected consumer."""
    consumer_tfm(target)
    options=config.get('runtimeOptions',{})
    assert options.get('tfm')==target,'runtimeconfig consumer target'
    assert 'frameworks' not in options and 'includedFrameworks' not in options,'runtimeconfig framework-dependent consumer'
    framework=options.get('framework',{})
    assert framework.get('name')=='Microsoft.NETCore.App','runtimeconfig framework name'
    requested=framework.get('version','')
    pattern=re.escape(target[3:])+r'\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?'
    assert isinstance(requested,str) and re.fullmatch(pattern,requested),'runtimeconfig framework version'
    validate_runtime(runtime,target)
    # Environment.Version may omit RC identity; FrameworkDescription must still
    # name the same runtime version, with any RC/build suffix retained in evidence.
    assert re.fullmatch(r'\.NET '+re.escape(runtime['version'])+r'(?:[-+][0-9A-Za-z.-]+)?',runtime['framework']),'actual runtime framework description'
    numeric=lambda value:tuple(map(int,re.split(r'[-+]',value)[0].split('.')))
    assert numeric(runtime['version'])>=numeric(requested),'actual runtime below runtimeconfig request'


def stable_version(value):
    if not isinstance(value,str) or not re.fullmatch(r'(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)',value):
        raise ValueError('Expected an explicit stable version such as major.minor.patch; no suffixes or floating values')
    return value


def validate_release_toolchain(sdk,runtime,expected_sdk,expected_runtime):
    stable_version(expected_sdk);stable_version(expected_runtime)
    assert sdk==expected_sdk,'Release SDK differs from the exact expected version'
    assert runtime.get('version')==expected_runtime,'Release runtime differs from the exact expected version'
    # Environment.Version is numeric and can omit RC/preview identity. Require
    # the independently reported full FrameworkDescription to be stable too.
    assert runtime.get('framework')=='.NET '+expected_runtime,'Release runtime framework differs from the exact stable version'


def probe_version(value):
    if not isinstance(value,str) or not re.fullmatch(r'(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?',value):
        raise ValueError('Expected an exact SDK/runtime version, optionally prerelease; no floating or build metadata')
    return value


def configure_probe_toolchain(work,sdk,runtime):
    probe_version(sdk);probe_version(runtime)
    project=work/'consumer/FirstUse.csproj';tree=ET.parse(project)
    target=consumer_tfm(tree.findtext('.//TargetFramework'))
    assert runtime.startswith(target[3:]+'.'),'Probe runtime must match the consumer target family'
    assert not tree.findall('.//RuntimeFrameworkVersion') and not tree.findall('.//RollForward'),'sample runtime overrides'
    group=ET.SubElement(tree.getroot(),'PropertyGroup')
    ET.SubElement(group,'RuntimeFrameworkVersion').text=runtime
    ET.SubElement(group,'RollForward').text='Disable'
    tree.write(project,encoding='unicode')
    (work/'global.json').write_text(json.dumps(dict(sdk=dict(version=sdk,rollForward='disable',allowPrerelease='-' in sdk)),indent=2)+'\n')


def validate_probe_toolchain(sdk,runtime,expected_sdk,expected_runtime):
    probe_version(expected_sdk);probe_version(expected_runtime)
    assert sdk==expected_sdk,'Probe SDK differs from the exact expected version'
    assert runtime.get('version')==expected_runtime.split('-',1)[0],'Probe numeric runtime differs from the exact expected version'
    assert runtime.get('framework')=='.NET '+expected_runtime,'Probe runtime framework differs from the exact expected version'


def validate_probe_runtimeconfig(config,expected_runtime):
    options=config.get('runtimeOptions',{})
    assert options.get('framework',{}).get('version')==expected_runtime,'Probe runtimeconfig version'
    assert options.get('rollForward')=='Disable','Probe runtimeconfig roll-forward'


def verify_package_bytes(work,package,version,expected_dll,target,consumer_target=None):
    cache=work/'packages/factspdf'/version
    assert (cache/f'factspdf.{version}.nupkg').read_bytes()==package.read_bytes(),'cached package bytes'
    checksum=base64.b64encode(hashlib.sha512(package.read_bytes()).digest()).decode()
    assert (cache/f'factspdf.{version}.nupkg.sha512').read_text().strip()==checksum,'package SHA512'
    assert (cache/'lib'/target/'FactsPDF.dll').read_bytes()==expected_dll,'cached assembly bytes'
    assert (work/'consumer/bin/Release'/(consumer_target or target)/'FactsPDF.dll').read_bytes()==expected_dll,'consumer assembly bytes'


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


def main(*,strict_toolchain=False):
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('cli-dll','output'):parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--package',type=Path,help='Defaults to the sole declared-version package in artifacts/packages')
    parser.add_argument('--dotnet',default='dotnet')
    parser.add_argument('--consumer-tfm',type=consumer_tfm,help='Explicit consumer target; defaults to the declared library TFM')
    if strict_toolchain:
        parser.add_argument('--expected-sdk',type=stable_version,required=True)
        parser.add_argument('--expected-runtime',type=stable_version,required=True)
    else:
        parser.add_argument('--probe-sdk',type=probe_version,help='Pin only the temporary consumer SDK; requires --probe-runtime')
        parser.add_argument('--probe-runtime',type=probe_version,help='Pin and verify the actual consumer runtime; requires --probe-sdk')
    args=parser.parse_args()
    exact_probe=not strict_toolchain and args.probe_sdk is not None
    if not strict_toolchain and (args.probe_sdk is None)!=(args.probe_runtime is None):
        parser.error('--probe-sdk and --probe-runtime must be supplied together')
    root=Path(__file__).resolve().parents[1];cli=args.cli_dll.resolve();out=args.output.resolve()
    source_head=current_source(root)
    version,framework=expected_contract(root)
    consumer_target=args.consumer_tfm or framework
    package=(args.package or local_package(root,version)).resolve()
    metadata=validate_package(package,root,version,source_head,framework);assembly=metadata['assembly']
    out.mkdir(parents=True,exist_ok=False)
    assert cli.is_file() and cli.with_suffix('.runtimeconfig.json').is_file() and cli.with_suffix('.deps.json').is_file(),'published CLI files'
    assert (cli.parent/'FactsPDF.dll').read_bytes()==assembly,'published CLI uses the newly packed assembly'
    commands=[]
    with tempfile.TemporaryDirectory(prefix='factspdf-first-use-') as temporary:
        work=Path(temporary).resolve();validate_workspace(work,root)
        for name in ('feed','packages','http-cache','dotnet-home','nuget-scratch','msbuild-user'): (work/name).mkdir()
        stage_consumer(root/'examples/first-use',work/'consumer')
        configure_consumer(work/'consumer/FirstUse.csproj',version,consumer_target)
        if exact_probe:configure_probe_toolchain(work,args.probe_sdk,args.probe_runtime)
        else:shutil.copy2(root/'global.json',work/'global.json')
        sdkconfig=(work/'global.json').read_bytes()
        (out/'consumer.global.json').write_bytes(sdkconfig)
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
        if strict_toolchain:
            assert sdk==args.expected_sdk,'Release SDK differs from the exact expected version'
        if exact_probe:
            assert sdk==args.probe_sdk,'Probe SDK differs from the exact expected version'
        execute(args.dotnet,'restore',project,'--configfile',config,'--packages',work/'packages','--no-http-cache')
        assets=json.loads((work/'consumer/obj/project.assets.json').read_text());validate_assets(assets,work,version,consumer_target)
        evaluation=json.loads(execute(args.dotnet,'msbuild',project,
            '-getProperty:TargetFramework,DirectoryBuildPropsPath,DirectoryBuildTargetsPath,DirectoryPackagesPropsPath',
            '-getItem:ProjectReference').stdout)
        assert evaluation['Properties']['TargetFramework']==consumer_target,'evaluated framework'
        assert not any(evaluation['Properties'][k] for k in ('DirectoryBuildPropsPath','DirectoryBuildTargetsPath','DirectoryPackagesPropsPath')),'inherited build properties'
        assert not evaluation['Items']['ProjectReference'],'evaluated project references'
        preprocessed=out/'consumer.preprocessed.xml'
        execute(args.dotnet,'msbuild',project,'-p:Configuration=Release','-preprocess:'+str(preprocessed))
        imports=validate_imports(preprocessed.read_text(),root,project)
        (out/'imports.json').write_text(json.dumps(imports,indent=2)+'\n')
        execute(args.dotnet,'build',project,'-c','Release','--no-restore')
        verify_package_bytes(work,package,version,assembly,framework,consumer_target)
        for name,data in [('assets.json',assets),('evaluation.json',evaluation)]:
            (out/name).write_text(json.dumps(data,indent=2)+'\n')
        expected=json.loads((work/'consumer/expected-pages.json').read_text())
        input_path=work/'consumer/input.html';bad=work/'consumer/unsupported.html'
        consumer=work/'consumer/bin/Release'/consumer_target/'FirstUse.dll'
        runtimeconfig=consumer.with_suffix('.runtimeconfig.json').read_bytes()
        runtimeconfig_data=json.loads(runtimeconfig)
        (out/'consumer.runtimeconfig.json').write_bytes(runtimeconfig)
        if exact_probe:validate_probe_runtimeconfig(runtimeconfig_data,args.probe_runtime)
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
                validate_consumer_execution(api.get('runtime',{}),consumer_target,runtimeconfig_data)
                if strict_toolchain:
                    validate_release_toolchain(sdk,api['runtime'],args.expected_sdk,args.expected_runtime)
                if exact_probe:
                    validate_probe_toolchain(sdk,api['runtime'],args.probe_sdk,args.probe_runtime)
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
        cached=work/'packages/factspdf'/version/'lib'/framework/'FactsPDF.dll';cached.write_bytes(b'not the packed assembly')
        try:verify_package_bytes(work,package,version,assembly,framework,consumer_target)
        except AssertionError as error:assert str(error)=='cached assembly bytes',str(error)
        else:raise AssertionError('accepted wrong cached assembly')
        finally:cached.write_bytes(assembly)
        verify_package_bytes(work,package,version,assembly,framework,consumer_target)
        assert current_source(root)==source_head,'Source changed during verification'
        report=dict(sourceHead=source_head,dirty=False,sdk=sdk,
            execution=dict(kind='managed-framework-dependent',nativeAot=False,jitTraceCaptured=False),
            consumerTargetFramework=consumer_target,
            consumerSdkConfig=dict(file='consumer.global.json',sha256=digest(sdkconfig),data=json.loads(sdkconfig)),
            consumerRuntimeConfig=dict(file='consumer.runtimeconfig.json',sha256=digest(runtimeconfig),data=runtimeconfig_data),
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
        if strict_toolchain:
            report['toolchainConstraint']=dict(mode='exact-stable',expectedSdk=args.expected_sdk,
                expectedRuntime=args.expected_runtime,verified=True,
                scope='SDK and managed consumer runtime; not publication approval or an official GA designation')
        if exact_probe:
            report['toolchainConstraint']=dict(mode='exact-probe',expectedSdk=args.probe_sdk,
                expectedRuntime=args.probe_runtime,verified=True,
                scope='Actual managed consumer only; not official toolchain provenance, GA approval, Native AOT or WASM')
        (out/'report.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(dict(report=str(out/'report.json'),pages=2,entries=2,isolatedWorkspaceRemoved=True)))


if __name__=='__main__':main()
