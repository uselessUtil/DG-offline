"""Reproduce/check the actual managed gacha menu lookup; Docker only."""
import argparse
import json
import subprocess
from pathlib import Path
import UnityPy


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('work',type=Path)
    parser.add_argument('--output',type=Path)
    parser.add_argument('--expect-legacy-failure',action='store_true')
    args=parser.parse_args()
    work=args.work.resolve();code=Path(__file__).resolve().parent
    probe=work/'gacha-probe';probe.mkdir(exist_ok=True)
    managed=work/'original/assets/bin/Data/Managed'
    cecil='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
    def run(*command):subprocess.run(list(map(str,command)),check=True,cwd=probe)
    run('mcs','-r:'+cecil,'-out:'+str(probe/'Prepare.exe'),code/'PrepareQuestProbe400.cs')
    for name in ['UnityEngine.dll','Assembly-CSharp-firstpass.dll','Assembly-CSharp.dll','DivineGateOffline.dll']:
        source=(work/'build' if name in ['Assembly-CSharp.dll','DivineGateOffline.dll'] else managed)/name
        run('mono',probe/'Prepare.exe',source,probe/name)
    run('mcs',*['-r:'+str(probe/name) for name in ['UnityEngine.dll','Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll']],
        '-out:'+str(probe/'GachaProbe.exe'),code/'GachaDateProbe400.cs')
    command=['mono',str(probe/'GachaProbe.exe')]
    if args.expect_legacy_failure:command.append('legacy')
    result=subprocess.run(command,cwd=probe,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
    log=result.stdout
    if result.returncode:
        if args.output:args.output.write_text(log)
        print(log)
    result.check_returncode()
    if not args.expect_legacy_failure:
        # Source-field comparison protects prices, event IDs and every weight.
        source={}
        for obj in UnityPy.load(str(work/'donor/assets/bin/Data')).objects:
            if obj.type.name!='TextAsset':continue
            asset=obj.read()
            if asset.m_Name in ['MasterDataGacha','MasterDataGachaAssign']:
                source[asset.m_Name]=json.loads(asset.m_Script,strict=False)
        ids={10,100100,200100,300100,300200,300300,300400}
        for name,key in [('MasterDataGacha','fix_id'),('MasterDataGachaAssign','gacha_id')]:
            original=[r for r in source[name] if r[key] in ids]
            actual=json.loads((work/'tables'/(name+'.json')).read_bytes(),strict=False)
            assert len(original)==len(actual)
            for old,new in zip(original,actual):
                assert all(new[k]==v for k,v in old.items()),(name,old['fix_id'])
        log+='PASS: all original pool fields and assignment rows match the locked 1.0.6 donor.\n'
    if args.output:args.output.write_text(log)
    print(log)


if __name__=='__main__':main()
