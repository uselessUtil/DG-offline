import pathlib,subprocess,os
from prepare import sync_sources
tables=os.environ.get("DG_TEST_TABLES");assert tables and pathlib.Path(tables).is_dir(),"Set DG_TEST_TABLES to the verified r17 tables directory"
sync_sources()
w=pathlib.Path('/tmp/dg-godot290');p=w/'quest-tests';p.mkdir(exist_ok=True)
refs=''.join('<Reference Include="'+x.stem+'"><HintPath>'+str(x)+'</HintPath></Reference>' for x in (w/'project/Libraries').glob('*.dll'))
files=['GameCore','QuestRun','QuestSkills','QuestEnemyActions']
sources=''.join('<Compile Include="/workspace/godot290/project/'+n+'.cs"/>' for n in files)
sources+=''.join('<Compile Include="'+str(w/'project/Generated'/n)+'"/>' for n in ('LegacyEnemyAttack.cs','LegacySkillStatus.cs'))
(p/'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><Nullable>enable</Nullable></PropertyGroup><ItemGroup>'+refs+sources+'<Compile Include="/workspace/godot290/tests/QuestTests.cs"/><Compile Include="/workspace/godot290/tests/SkillTests.cs"/></ItemGroup></Project>')
with (w/'quest-tests.log').open('w') as f:r=subprocess.run(['/opt/dg-dotnet9/dotnet','run','--project',str(p/'Test.csproj')],stdout=f,stderr=subprocess.STDOUT)
print((w/'quest-tests.log').read_text()[-4500:]);r.check_returncode()
