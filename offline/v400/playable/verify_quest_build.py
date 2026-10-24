"""Execute the packaged quest builder in a disposable Mono engine-I/O probe."""
import argparse,shutil,subprocess
from pathlib import Path

def main():
    p=argparse.ArgumentParser();p.add_argument('work',type=Path);p.add_argument('--output',type=Path);args=p.parse_args()
    code=Path(__file__).resolve().parent;work=args.work.resolve();probe=work/'quest-probe';probe.mkdir(exist_ok=True)
    managed=work/'original/assets/bin/Data/Managed'
    for name in ['Assembly-CSharp-firstpass.dll','UnityEngine.dll']:shutil.copyfile(managed/name,probe/name)
    for name in ['Assembly-CSharp.dll','DivineGateOffline.dll']:shutil.copyfile(work/'build'/name,probe/name)
    cecil='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
    def run(*a):subprocess.run(list(map(str,a)),check=True,cwd=probe)
    run('mcs','-r:'+cecil,'-out:'+str(probe/'Prepare.exe'),code/'PrepareQuestProbe400.cs')
    run('mono',probe/'Prepare.exe',managed/'UnityEngine.dll',probe/'UnityEngine.dll')
    for name in ['Assembly-CSharp.dll','DivineGateOffline.dll','Assembly-CSharp-firstpass.dll']:
        run('mono',probe/'Prepare.exe',probe/name,probe/(name+'.probe'))
        shutil.move(probe/(name+'.probe'),probe/name)
    run('mcs','-r:'+str(probe/'Assembly-CSharp.dll'),'-r:'+str(probe/'Assembly-CSharp-firstpass.dll'),'-r:'+str(probe/'UnityEngine.dll'),'-out:'+str(probe/'QuestProbe.exe'),code/'QuestBuildProbe400.cs')
    result=subprocess.run(['mono',str(probe/'QuestProbe.exe'),str(work/'tables')],cwd=probe,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    if args.output:args.output.write_text(result.stdout)
    print(result.stdout)
    result.check_returncode()
if __name__=='__main__':main()
