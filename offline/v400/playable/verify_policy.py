"""Exercise both real policy gates with disposable UI and preference boundaries."""
import argparse
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('work', type=Path)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--expect-legacy-failure', action='store_true')
    args = parser.parse_args()
    work = args.work.resolve(); code = Path(__file__).resolve().parent
    probe = work / 'policy-probe'; probe.mkdir(exist_ok=True)
    managed = work / 'original/assets/bin/Data/Managed'
    cecil = '/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
    def run(*command):
        subprocess.run(list(map(str, command)), check=True, cwd=probe)
    run('mcs', '-r:' + cecil, '-out:' + str(probe / 'Prepare.exe'), code / 'PrepareQuestProbe400.cs')
    names = ['UnityEngine.dll', 'Assembly-CSharp-firstpass.dll', 'Assembly-CSharp.dll', 'DivineGateOffline.dll']
    for name in names:
        source = (work / 'build' if name in names[2:] else managed) / name
        run('mono', probe / 'Prepare.exe', source, probe / name)
    run('mcs', '-target:library', '-out:' + str(probe / 'BadgeProbeIO400.dll'), code / 'BadgeProbeIO400.cs')
    run('mcs', '-target:library', '-r:' + str(probe / 'Assembly-CSharp.dll'), '-r:' + str(probe / 'UnityEngine.dll'),
        '-out:' + str(probe / 'PolicyProbeIO400.dll'), code / 'PolicyProbeIO400.cs')
    for name in ['BadgeProbePrep400', 'PolicyProbePrep400']:
        run('mcs', '-r:' + cecil, '-out:' + str(probe / (name + '.exe')), code / (name + '.cs'))
    run('mono', probe / 'BadgeProbePrep400.exe', 'io', probe / 'Assembly-CSharp.dll', probe / 'WithIO.dll', probe / 'BadgeProbeIO400.dll')
    run('mono', probe / 'PolicyProbePrep400.exe', 'prepare', probe / 'WithIO.dll', probe / 'Prepared.dll', probe / 'PolicyProbeIO400.dll')
    shutil.move(probe / 'Prepared.dll', probe / 'Assembly-CSharp.dll')
    run('mcs', *['-r:' + str(probe / name) for name in names[:3] + ['BadgeProbeIO400.dll', 'PolicyProbeIO400.dll']],
        '-out:' + str(probe / 'PolicyProbe.exe'), code / 'PolicyGateProbe400.cs')
    command = ['mono', str(probe / 'PolicyProbe.exe')]
    if args.expect_legacy_failure:
        command.append('legacy')
    result = subprocess.run(command, cwd=probe, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    log = result.stdout
    if not args.expect_legacy_failure and result.returncode == 0:
        log += subprocess.check_output(['mono', str(probe / 'PolicyProbePrep400.exe'), 'check',
                                       str(managed / 'Assembly-CSharp.dll'), str(work / 'build/Assembly-CSharp.dll')], text=True)
    if args.output:
        args.output.write_text(log)
    print(log); result.check_returncode()


if __name__ == '__main__':
    main()
