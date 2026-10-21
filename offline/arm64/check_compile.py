"""C# compatibility check only. Does not import Unity assets or build an APK."""
import argparse
from pathlib import Path
import subprocess


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('project', type=Path)
    args = parser.parse_args()
    data = Path('/opt/unity/2019.4.40f1/Editor/Data')
    sources = [p for p in (args.project/'Assets').rglob('*.cs') if 'Editor' not in p.relative_to(args.project/'Assets').parts]
    profile = data/'MonoBleedingEdge/lib/mono/4.7.1-api'
    refs = [profile/n for n in ['mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll']]
    refs += list((data/'Managed/UnityEngine').glob('*.dll'))
    response = args.project.parent/'runtime-check.rsp'
    output = args.project.parent/'runtime-check.dll'
    options = ['/noconfig', '/nologo', '/target:library', '/langversion:7.3', '/unsafe', '/nostdlib+',
               '/define:UNITY_ANDROID,UNITY_2019_4,UNITY_2019_4_OR_NEWER', '/out:'+str(output)]
    response.write_text('\n'.join(options + ['/r:"'+str(p)+'"' for p in refs] + ['"'+str(p)+'"' for p in sources]))
    result = subprocess.run(['mono', str(data/'Tools/RoslynNet46/csc.exe'), '@'+str(response)],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
    log = args.project.parent/'compile-check.log'
    log.write_text(result.stdout)
    errors = [line for line in result.stdout.splitlines() if ': error ' in line]
    print('Compiled source files:', len(sources), 'Errors:', len(errors), 'Exit:', result.returncode)
    print('\n'.join(errors[:50]))
    print('Full log:', log)
    raise SystemExit(result.returncode)


if __name__ == '__main__':
    main()
