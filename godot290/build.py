import pathlib,subprocess,shutil,os,sys
from prepare import ROOT,PROJECT,WORK
GODOT='/opt/godot/4.6.3/Godot_v4.6.3-stable_mono_linux_x86_64/Godot_v4.6.3-stable_mono_linux.x86_64'
ENV=dict(os.environ,DOTNET_ROOT='/opt/dg-dotnet9',DOTNET_CLI_TELEMETRY_OPTOUT='1',PATH='/opt/dg-dotnet9:'+os.environ['PATH'],GODOT_SILENCE_ROOT_WARNING='1')
def command(name,args):
    log=WORK/(name+'.log')
    with log.open('w') as stream:result=subprocess.run(args,env=ENV,stdout=stream,stderr=subprocess.STDOUT)
    print(name,'exit',result.returncode,flush=True)
    if result.returncode:print(log.read_text()[-7000:])
    result.check_returncode()
    errors=[line for line in log.read_text().splitlines() if line.startswith('ERROR:')]
    # Godot 4.6.3's Android platform destructor reads an already-destroyed
    # EditorSettings after a successful command-line export. Preserve its log.
    known='ERROR: EditorSettings not instantiated yet when getting setting "export/android/shutdown_adb_on_exit".'
    if (name=='android-export' and 'Verifying APK' in log.read_text()) or name=='import':errors=[e for e in errors if e!=known]
    if errors:raise RuntimeError('Godot reported an error; inspect '+str(log))
def main():
    shutil.copytree(ROOT/'godot290/project',PROJECT,dirs_exist_ok=True)
    command('build',['/opt/dg-dotnet9/dotnet','build',str(PROJECT/'DivineGateGodot.csproj')])
    if '--import' in sys.argv:command('import',[GODOT,'--headless','--editor','--path',str(PROJECT),'--import'])
    if '--capture' in sys.argv:
        command('capture',['xvfb-run','-a',GODOT,'--path',str(PROJECT),'--rendering-method','gl_compatibility','--audio-driver','Dummy','--','--capture']+([a for a in sys.argv if a in ['--smoke','--quest-smoke']])+[a for a in sys.argv if a.startswith('--page=')])
if __name__=='__main__':main()
