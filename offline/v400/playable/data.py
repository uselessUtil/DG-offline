"""Version-specific backup projection. Preserve all gameplay rows and IDs."""
import json,hashlib

def convert(source,inventory):
    schemas={}
    for p in (line.split('\t') for line in inventory.splitlines()):
        if p[0]=='column':schemas.setdefault(p[1],set()).add(p[2])
    result=dict(source);changes=[]
    # 4.0.0 local backend takes EventBase, client takes converted Event.
    result['MasterDataEventBase']=result.pop('MasterDataEvent')
    for name,typ in [('MasterDataArea','MasterDataArea'),('MasterDataEventBase','MasterDataEventBase')]:
        raw=result[name];rows=json.loads(raw,strict=False)
        extra=set().union(*(r.keys() for r in rows))-schemas[typ]
        allowed={'bgm_normal','bgm_boss'} if name=='MasterDataArea' else set()
        if extra-allowed:raise ValueError('Unexpected source columns '+name+str(extra))
        if name=='MasterDataArea':
            # 4.0.0 lists categories before areas. Legacy data has no category
            # table/foreign key; leaving zero silently produces an empty menu.
            assert {'area_cate_id','questlist_sort'} <= schemas[typ]
            assert 'MasterDataAreaCategory' not in result
            categories=[
                (1,1,1,'ストーリー'), (2,2,2,'イベント'),
                (3,3,4,'カジノ'), (6,6,6,'進化クエスト'),
                (9,9,3,'コラボ')]
            # Explicit IDs from the locked 1.0.6 source, not ranges inferred
            # from later master data. Tutorial areas stay out of menu listings.
            casino={200100,200200,200300,200400,200500,200600,200700}
            collabo={210000,210010,210020,210030,210031,210040}
            for order,r in enumerate(rows):
                for k in extra:del r[k]
                # No downloaded audio bundle exists; empty requests use APK-resident audio.
                r['packname_bgm']='';r['packname_se']=''
                assert r['type'] in {0,1,2,3,4}
                category={0:0,1:1,2:2,3:6,4:0}[r['type']]
                if r['fix_id'] in casino:category=3
                if r['fix_id'] in collabo:category=9
                r['area_cate_id']=category
                r['questlist_sort']=order
            category_rows=[dict(fix_id=i,area_cate_type=t,quest_tab_type=tab,
                area_cate_name=label,questlist_sort=order,area_cate_detail='')
                for order,(i,t,tab,label) in enumerate(categories)]
            assert set(category_rows[0])==schemas['MasterDataAreaCategory']
            category_raw=json.dumps(category_rows,ensure_ascii=True,separators=(',',':')).encode()
            result['MasterDataAreaCategory']=category_raw
            changes.append({'table':'MasterDataAreaCategory','rows':len(category_rows),
                'mapping':'Authored 4.0.0 menu categories for original 1.0.6 areas: story, event, casino/training, evolution, collaboration. Tutorial/sentinel have category 0. Area/quest IDs, pre_area and event_id are unchanged; area order follows source.',
                'categories':category_rows,'output_sha256':hashlib.sha256(category_raw).hexdigest()})
        result[name]=json.dumps(rows,ensure_ascii=True,separators=(',',':')).encode()
        changes.append({'table':name,'rows':len(rows),'removed':sorted(extra),'source_sha256':hashlib.sha256(raw).hexdigest(),'output_sha256':hashlib.sha256(result[name]).hexdigest()})
    # 1.0.9 generated one KEY unconditionally. 4.0.0 reads its count from cate5.
    # cate4 is still HEAL in both versions and must remain untouched.
    name='MasterDataCategoryPattern';raw=result[name];rows=json.loads(raw,strict=False)
    if 'cate4' not in schemas[name] or 'cate5' not in schemas[name]:raise ValueError('Unexpected 4.0.0 category schema')
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
            for r in rows:r.update(tab_num=r['type'],tab_name=r['name'],url_img='',url_web='',change_title='')
        result[name]=json.dumps(rows,ensure_ascii=True,separators=(',',':')).encode()
        changes.append({'table':name,'rows':len(rows),'mapping':'seven original pools; local native tabs; no URLs','source_sha256':hashlib.sha256(raw).hexdigest(),'output_sha256':hashlib.sha256(result[name]).hexdigest()})
    for name in ['MasterDataAssetBundlePath','MasterDataInformation']:
        if name in result:
            changes.append({'table':name,'mapping':'Disabled obsolete online download/notice data; artwork resolved by local adapter','source_sha256':hashlib.sha256(result[name]).hexdigest()})
            result[name]=b'[]'
    return result,changes
