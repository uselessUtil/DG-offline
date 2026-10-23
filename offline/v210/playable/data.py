"""Version-specific backup projection. Preserve all gameplay rows and IDs."""
import json,hashlib

def convert(source,inventory):
    schemas={}
    for p in (line.split('\t') for line in inventory.splitlines()):
        if p[0]=='column':schemas.setdefault(p[1],set()).add(p[2])
    result=dict(source);changes=[]
    # 2.1.0 local backend takes EventBase, client takes converted Event.
    result['MasterDataEventBase']=result.pop('MasterDataEvent')
    for name,typ in [('MasterDataArea','MasterDataArea'),('MasterDataEventBase','MasterDataEventBase')]:
        raw=result[name];rows=json.loads(raw,strict=False)
        extra=set().union(*(r.keys() for r in rows))-schemas[typ]
        allowed={'bgm_normal','bgm_boss'} if name=='MasterDataArea' else set()
        if extra-allowed:raise ValueError('Unexpected source columns '+name+str(extra))
        if name=='MasterDataArea':
            for r in rows:
                for k in extra:del r[k]
                # No downloaded audio bundle exists; empty requests use APK-resident audio.
                r['packname_bgm']='';r['packname_se']=''
        result[name]=json.dumps(rows,ensure_ascii=True,separators=(',',':')).encode()
        changes.append({'table':name,'rows':len(rows),'removed':sorted(extra),'source_sha256':hashlib.sha256(raw).hexdigest(),'output_sha256':hashlib.sha256(result[name]).hexdigest()})
    return result,changes
