"""Read-only text coverage inventory; run inside the project container.

Candidate keys are not proof of reachable UI: raw serialized strings and nearby
IL literals can include non-key constants. Dynamic keys require runtime evidence.
"""
import argparse
import json
import re
import subprocess
import struct
from pathlib import Path
import UnityPy

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    report=json.loads(args.report.read_text())
    work=Path(report['work_directory'])
    original=work/'original/assets/bin/Data'
    env=UnityPy.load(str(original))
    databases=[]
    for obj in env.objects:
        if obj.type.name!='TextAsset':continue
        asset=obj.read()
        if asset.m_Name!='TextDatabase':continue
        raw=asset.m_Script
        if isinstance(raw,bytes):raw=raw.decode('utf8')
        items=raw.split('\t')
        count=int(items[0]);assert len(items)>=count*2+1
        databases.append(dict(zip(items[1:count*2+1:2],items[2:count*2+1:2])))
    assert len(databases)==1
    resident=databases[0]
    supplemental={row['text_key']:row['text'] for row in json.loads((work/'tables/MasterDataTextDefinition.json').read_text())}
    cecil='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
    exe=work/'TextKeys.exe'
    subprocess.run(['mcs','-r:'+cecil,'-out:'+str(exe),'/workspace/offline/v290/playable/TextKeys.cs'],check=True)
    lines=subprocess.check_output(['mono',str(exe),str(work/'build/Assembly-CSharp.dll')],text=True).splitlines()
    references={}
    for line in lines:
        row=line.split('\t')
        if len(row)==3 and re.fullmatch('[A-Z][A-Z0-9_]+',row[2]):
            references.setdefault(row[2],set()).add('IL:'+row[0]+':'+row[1])
    prefixes={k.split('_')[0] for k in resident if '_' in k}
    for path in original.iterdir():
        if not path.is_file():continue
        for value in re.findall(rb'(?<![A-Z0-9_])[A-Z][A-Z0-9]*(?:_[A-Z0-9]+){1,12}(?![A-Z0-9_])',path.read_bytes()):
            key=value.decode()
            if key.split('_')[0] in prefixes:
                references.setdefault(key,set()).add('serialized:'+path.name)
    missing={k:sorted(v) for k,v in sorted(references.items()) if k not in resident and k not in supplemental}
    skill_labels=[]
    for obj in env.objects:
        if obj.type.name!='MonoBehaviour' or Path(obj.assets_file.name).name!='level9':continue
        raw=obj.get_raw_data()
        keys=[k for k in ('WORD_SKILL','WORD_LINK') if k.encode() in raw]
        if not keys:continue
        try:
            # Unity 5.3 MonoBehaviour prefix: local GameObject PPtr (i32+i64).
            # Custom fields have no typetree; do not ask UnityPy to decode them.
            file_id,go_id=struct.unpack_from('<iq',raw)
            assert file_id==0
            go=obj.assets_file.objects[go_id].read()
            names=[go.m_Name]
            for component in go.m_Component:
                pointer=component[1] if isinstance(component,tuple) else component.component
                if pointer.type.name=='Transform':
                    transform=pointer.read()
                    while transform.m_Father.path_id:
                        transform=transform.m_Father.read()
                        names.insert(0,transform.m_GameObject.read().m_Name)
                    break
            skill_labels.append({'keys':keys,'object_path':'/'.join(names),'path_id':obj.path_id})
        except Exception as error:skill_labels.append({'keys':keys,'path_id':obj.path_id,'read_error':str(error)})
    result={'build':report['version'],'resident_count':len(resident),'supplemental_count':len(supplemental),
        'candidate_count':len(references),'missing_candidate_count':len(missing),'missing_candidates':missing,
        'overlap':sorted(resident.keys()&supplemental.keys()),'skill_labels':skill_labels,
        'skill_window_keys':{k:{'resident':resident.get(k),'supplemental':supplemental.get(k)} for k in sorted(set(references)|set(resident)) if 'SKILLWINDOW' in k},
        'scope':'Static candidate inventory, not a claim of complete runtime coverage. No emulator operations.'}
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    summary={k:v for k,v in result.items() if k!='missing_candidates'}
    summary['gameplay_missing_candidates']={k:v for k,v in missing.items() if k.startswith(('SKILL','INGAME','BATTLE','TUTORIAL','UNIT_DETAIL'))}
    print(json.dumps(summary,ensure_ascii=False,indent=2))

if __name__=='__main__':main()
