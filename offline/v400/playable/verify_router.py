"""Exercise the actual startup router with an isolated UI/queue boundary."""
import argparse
import shutil
import subprocess
from pathlib import Path


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('work',type=Path)
    parser.add_argument('--output',type=Path)
    parser.add_argument('--expect-legacy-failure',action='store_true')
    args=parser.parse_args();work=args.work.resolve();code=Path(__file__).resolve().parent
    probe=work/'router-probe';probe.mkdir(exist_ok=True)
    managed=work/'original/assets/bin/Data/Managed'
    cecil='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
    def run(*command):subprocess.run(list(map(str,command)),check=True,cwd=probe)
    run('mcs','-r:'+cecil,'-out:'+str(probe/'Prepare.exe'),code/'PrepareQuestProbe400.cs')
    for name in ['UnityEngine.dll','Assembly-CSharp-firstpass.dll','Assembly-CSharp.dll','DivineGateOffline.dll']:
        source=(work/'build' if name in ['Assembly-CSharp.dll','DivineGateOffline.dll'] else managed)/name
        run('mono',probe/'Prepare.exe',source,probe/name)
    for name in ['BadgeProbeIO400','RouterProbeIO400']:
        run('mcs','-target:library','-out:'+str(probe/(name+'.dll')),code/(name+'.cs'))
    for name in ['BadgeProbePrep400','RouterProbePrep400']:
        run('mcs','-r:'+cecil,'-out:'+str(probe/(name+'.exe')),code/(name+'.cs'))
    run('mono',probe/'BadgeProbePrep400.exe','io',probe/'Assembly-CSharp.dll',probe/'WithIO.dll',probe/'BadgeProbeIO400.dll')
    run('mono',probe/'RouterProbePrep400.exe','prepare',probe/'WithIO.dll',probe/'WithRouter.dll',probe/'RouterProbeIO400.dll')
    shutil.move(probe/'WithRouter.dll',probe/'Assembly-CSharp.dll')
    run('mcs',*['-r:'+str(probe/name) for name in ['UnityEngine.dll','Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','BadgeProbeIO400.dll','RouterProbeIO400.dll']],
        '-out:'+str(probe/'RouterProbe.exe'),code/'RouterMonthlyProbe400.cs')
    command=['mono',str(probe/'RouterProbe.exe')]
    if args.expect_legacy_failure:command.append('legacy')
    result=subprocess.run(command,cwd=probe,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    log=result.stdout
    if not args.expect_legacy_failure and result.returncode==0:
        log+=subprocess.check_output(['mono',str(probe/'RouterProbePrep400.exe'),'check',str(managed/'Assembly-CSharp.dll'),str(work/'build/Assembly-CSharp.dll')],text=True)
    if args.output:args.output.write_text(log)
    print(log);result.check_returncode()


if __name__=='__main__':main()
