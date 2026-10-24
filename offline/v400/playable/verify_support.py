"""Probe generated support/character initialization, with disposable engine I/O."""
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
    probe=work/'support-probe';probe.mkdir(exist_ok=True)
    managed=work/'original/assets/bin/Data/Managed'
    cecil='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
    def run(*command):subprocess.run(list(map(str,command)),check=True,cwd=probe)
    run('mcs','-r:'+cecil,'-out:'+str(probe/'Prepare.exe'),code/'PrepareQuestProbe400.cs')
    for name in ['UnityEngine.dll','Assembly-CSharp-firstpass.dll','Assembly-CSharp.dll','DivineGateOffline.dll']:
        source=(work/'build' if name in ['Assembly-CSharp.dll','DivineGateOffline.dll'] else managed)/name
        run('mono',probe/'Prepare.exe',source,probe/name)
    run('mcs','-r:'+cecil,'-out:'+str(probe/'PrepareMath.exe'),code/'SupportProbePrep400.cs')
    run('mono',probe/'PrepareMath.exe',probe/'UnityEngine.dll',probe/'Math.dll')
    shutil.move(probe/'Math.dll',probe/'UnityEngine.dll')
    run('mcs',*['-r:'+str(probe/name) for name in ['UnityEngine.dll','Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll']],
        '-out:'+str(probe/'SupportProbe.exe'),code/'SupportSetupProbe400.cs')
    command=['mono',str(probe/'SupportProbe.exe')]
    if args.expect_legacy_failure:command.append('legacy')
    result=subprocess.run(command,cwd=probe,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    if args.output:args.output.write_text(result.stdout)
    print(result.stdout);result.check_returncode()


if __name__=='__main__':main()
