"""Version-specific backup projection. Preserve all gameplay rows and IDs."""
import json,hashlib

def convert(source,inventory,restored=False):
    schemas={}
    for p in (line.split('\t') for line in inventory.splitlines()):
        if p[0]=='column':schemas.setdefault(p[1],set()).add(p[2])
    result=dict(source);changes=[]
    # 2.9.1 local backend takes EventBase, client takes converted Event.
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
    # 1.0.9 generated one KEY unconditionally. 2.9.1 reads its count from cate5.
    # cate4 is still HEAL in both versions and must remain untouched.
    name='MasterDataCategoryPattern';raw=result[name];rows=json.loads(raw,strict=False)
    if 'cate4' not in schemas[name] or 'cate5' not in schemas[name]:raise ValueError('Unexpected 2.9.1 category schema')
    if any('cate5' in r for r in rows):raise ValueError('Input is not the locked legacy category schema')
    for r in rows:
        total=sum(r['cate'+str(i)] for i in range(5))
        if total!=24:raise ValueError('Legacy category does not reserve exactly one key tile: '+str(r['fix_id']))
        r['cate5']=1
        assert sum(r['cate'+str(i)] for i in range(6))==25
    result[name]=json.dumps(rows,ensure_ascii=True,separators=(',',':')).encode()
    changes.append({'table':name,'rows':len(rows),'mapping':{'cate0..cate4':'unchanged','cate5':'1; original 1.0.9 CreateQuestBuild unconditionally sets type-5 count to 1'},'source_sha256':hashlib.sha256(raw).hexdigest(),'output_sha256':hashlib.sha256(result[name]).hexdigest()})

    # Locked 1.0.6 tutorial, friendship, standard and four collab pools.
    for name,key in [('MasterDataGacha','fix_id'),('MasterDataGachaAssign','gacha_id')]:
        raw=result[name];rows=json.loads(raw,strict=False)
        rows=[r for r in rows if r[key] in {10,100100,200100,300100,300200,300300,300400}]
        if name=='MasterDataGacha':
            for r in rows:r.update(tab_num=0,tab_name='',url_img='',url_web='',change_title='')
        result[name]=json.dumps(rows,ensure_ascii=True,separators=(',',':')).encode()
        changes.append({'table':name,'rows':len(rows),'mapping':'seven original pools; local native tabs; no URLs','source_sha256':hashlib.sha256(raw).hexdigest(),'output_sha256':hashlib.sha256(result[name]).hexdigest()})
    from native_skills import convert as native_buffs
    changes.extend(native_buffs(result,schemas))
    from dgdb import project
    result,audit=project(result,inventory,restored=restored)
    changes=[c for c in changes if c["table"] not in {"MasterDataSkillLimitBreak","MasterDataStatusAilment"}]
    changes.append(dict(table="DGDB",mapping="fixed old content with DGDB native gameplay",audit=audit))
    return result,changes
