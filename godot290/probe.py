import pathlib,subprocess,os
tables=os.environ.get('DG_TEST_TABLES');assert tables and pathlib.Path(tables).is_dir(),'Set DG_TEST_TABLES to the verified r17 tables directory'
w=pathlib.Path('/tmp/dg-godot290');p=w/'probe';p.mkdir(exist_ok=True)
refs=''.join('<Reference Include="'+x.stem+'"><HintPath>'+str(x)+'</HintPath><Private>true</Private></Reference>' for x in (w/'project/Libraries').glob('*.dll'))
(p/'Probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup>'+refs+'<Compile Include="/workspace/offline/v290/playable/PlayableTests.cs"/><Compile Include="Boot.cs"/></ItemGroup><PropertyGroup><StartupObject>Boot</StartupObject></PropertyGroup></Project>')
(p/'Boot.cs').write_text('class Boot { static void Main(string[] args) { PortableEngine.Configure("/tmp/dg-godot290/probe-save", System.Environment.GetEnvironmentVariable("DG_TEST_TABLES")); Playable290Tests.Main(args); } }')
mode='reload' if 'reload' in __import__('sys').argv else 'fresh'
with (w/('rules-'+mode+'.log')).open('w') as log:
    result=subprocess.run(['/opt/dg-dotnet9/dotnet','run','--project',str(p/'Probe.csproj'),'--']+__import__('sys').argv[1:],stdout=log,stderr=subprocess.STDOUT)
lines=(w/('rules-'+mode+'.log')).read_text().splitlines()
print('\n'.join(lines[-8:]));print('exit',result.returncode,'PASS assertions',sum(s.startswith('PASS ') for s in lines))
result.check_returncode()
