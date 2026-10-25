"""Audit literal code keys and serialized UI keys against resident + fallback text."""
import json,re,subprocess
from pathlib import Path
import UnityPy
def verify(original,assembly,tools,fallback):
    raw=[];i=0
    while (original/('sharedassets1.assets.split'+str(i))).exists():
        raw.append((original/('sharedassets1.assets.split'+str(i))).read_bytes());i+=1
    env=UnityPy.load(b''.join(raw))
    asset=next(o.read() for o in env.objects if o.type.name=='TextAsset' and o.read().m_Name=='TextDatabase')
    text=asset.m_Script
    if isinstance(text,bytes):text=text.decode('utf8')
    items=text.split(',');resident=set(items[1::2]);assert len(resident)==int(items[0])
    supplement={r['text_key'] for r in fallback}
    assert not resident&supplement,'Fallback must not replace resident Japanese text'
    exe=tools/'TextKeys.exe'
    subprocess.run(['mcs','-r:/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll','-out:'+str(exe),str(Path(__file__).with_name('TextKeys.cs'))],check=True)
    found=set()
    for line in subprocess.check_output(['mono',str(exe),str(assembly)],text=True).splitlines():
        row=line.split('\t')
        if len(row)==3 and re.fullmatch('[A-Z][A-Z0-9_]+',row[2]):found.add(row[2])
    prefixes={k.split('_')[0] for k in resident if '_' in k}
    for p in original.iterdir():
        if not p.is_file():continue
        for match in re.findall(rb'(?<![A-Z0-9_])[A-Z][A-Z0-9]*(?:_[A-Z0-9]+){1,12}(?![A-Z0-9_])',p.read_bytes()):
            key=match.decode()
            if key.split('_')[0] in prefixes:found.add(key)
    # MAX is a literal display value nearby GetText; MM_U straddles an asset split.
    missing=found-resident-supplement-{'MAX','MM_U'}
    assert not missing,sorted(missing)
    return 'PASS '+str(len(resident))+' resident text keys preserved; '+str(len(supplement))+' supplemental keys; no missing candidates from code call sites and serialized UI references (two documented non-key exclusions).\n'
