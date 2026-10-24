"""Offline structural regression checks, not an Android gameplay test."""
import argparse,base64,io,json,math,re,struct,subprocess
from pathlib import Path
import UnityPy

def verify_maps(work):
    original=json.loads((work/'build/map-geometry.json').read_text())
    report=json.loads((work/'build/map-restore.json').read_text())
    payloads=dict(re.findall(r'case "([^"]+)": return Convert.FromBase64String\("([^"]+)"\);',(work/'build/MapData400.cs').read_text()))
    assert len(payloads)==report['resources']==50
    tested_vertices=tested_curves=tested_events=0
    for name,encoded in payloads.items():
        source=original[report['missing_geometry_aliases'].get(name,name)]
        stream=io.BytesIO(base64.b64decode(encoded))
        def integer():return struct.unpack('<i',stream.read(4))[0]
        def number():return struct.unpack('<f',stream.read(4))[0]
        def string():return stream.read(integer()).decode()
        def same(expected):
            actual=number()
            assert actual==struct.unpack('<f',struct.pack('<f',expected))[0],(name,actual,expected)
        assert integer()==4001 and integer()==len(source['nodes'])
        paths={n['path'] for n in source['nodes']}
        for node in source['nodes']:
            assert string()==node['name'] and integer()==node['parent'] and integer()==node['layer'] and integer()==int(node['active'])
            for field,axes in [('m_LocalPosition','xyz'),('m_LocalRotation','xyzw'),('m_LocalScale','xyz')]:
                for axis in axes:same(node['transform'][field][axis])
            count=integer();mesh=node.get('mesh');assert count==(len(mesh['vertices']) if mesh else 0)
            if not count:continue
            tested_vertices+=count
            for i,v in enumerate(mesh['vertices']):
                for value in [*v,*mesh['uv'][i],*(mesh['colors'][i] if mesh['colors'] else [1,1,1,1])]:same(value)
            assert integer()==len(mesh['triangles'])
            assert [integer() for _ in mesh['triangles']]==mesh['triangles']
            assert integer()==int(node['enabled'])
        clips=source['clips'];assert integer()==len(clips)==(4 if name.endswith('_door') else 0)
        for clip in clips:
            assert string()==clip['m_Name'] and integer()==clip['m_WrapMode'];same(clip['m_SampleRate'])
            expected=[]
            for field,prop,axes in [('m_PositionCurves','localPosition','xyz'),('m_RotationCurves','localRotation','xyzw'),('m_ScaleCurves','localScale','xyz')]:
                for curve in clip[field]:
                    for axis in axes:expected.append((curve,prop+'.'+axis,axis))
            assert integer()==len(expected)
            for curve,prop,axis in expected:
                assert string()==curve['path'] and curve['path'] in paths and string()==prop
                assert integer()==8 and integer()==8 # ClampForever, never reinterpret infinity=2 as Loop.
                keys=curve['curve']['m_Curve'];assert integer()==len(keys)
                for key in keys:
                    same(key['time'])
                    for field in ['value','inSlope','outSlope']:same(key[field][axis])
                tested_curves+=1
            assert integer()==len(clip['m_Events'])
            for event in clip['m_Events']:
                same(event['time']);assert string()==event['functionName'];tested_events+=1
        assert stream.tell()==len(base64.b64decode(encoded))
    # Tutorial resources must have a real two-leaf door and nontrivial UVs.
    door=original['map_0001_door'];assert {n['name'] for n in door['nodes']} >= {'l_door','r_door'}
    assert sum(len(n['mesh']['vertices']) for n in door['nodes'] if 'mesh' in n)>8
    assert door['clips'][1]['m_WrapMode']==2 # Boss closed animation loops.
    assert door['clips'][0]['m_WrapMode']==0 # Normal opening does not loop.
    return {'resources':len(payloads),'vertices_compared':tested_vertices,'curves_compared':tested_curves,'events_compared':tested_events,'geometry_aliases':report['missing_geometry_aliases']}

def verify_text(work):
    rows=json.loads((work/'tables/MasterDataTextDefinition.json').read_text())
    values={r['text_key']:r['text'] for r in rows};assert len(rows)==len(values)
    required={
      'PARTY_NUM_RATE':{0},'BTN_ITEM_USE_STATE':set(),
      'FORMAT_HP_TOTAL':set(),'FORMAT_COST_TOTAL':set(),
      'SWITCH_PAGE_BACK_BTN':set(),'SWITCH_PAGE_NEXT_BTN':set(),
      'POINTSHOP_LIMITOVER_CHARM':set(),
      'SKILLWINDOW_BTN_LBS_ACTIVATE':set(),'SKILLWINDOW_BTN_LBS_TURNCOUNT':{0},
      'WORD_SKILL':set(),'WORD_LINK':set(),'INGAME_DROP_LIST_EMPTY':set(),
      'INGAME_ITEM_EFFECT_TITLE':set(),'INGAME_ITEM_EFFECT_NONE':set(),
      'HEADER_EXP_LABEL_FORMAT':{0},'HEADER_STAMINA_LABEL_FORMAT':{0,1,2},'HEADER_STAMINA_MAX_LABEL_FORMAT':{0,1},
      'LINK_UNIT_STATUS_FORMAT':{0,1},'UNIT_STATUS_FORMAT':{0,1},'UNIT_DETAIL_LINK_ODDS_RATE':{0},'UNIT_DETAIL_LINK_POINT_RATE':{0}}
    for key,args in required.items():
        text=values[key];assert text.strip() and '?' not in text
        actual={int(x) for x in re.findall(r'\{(\d+)(?::[^}]*)?\}',text)}
        assert actual==args,(key,text)
        text.format(*[3]*(max(args)+1 if args else 0))
    repairs=json.loads(Path(__file__).with_name('text-repairs.json').read_text())
    assert all(values.get(k)==v for k,v in repairs.items())
    complete=json.loads(Path(__file__).with_name('text-complete.json').read_text())
    assert all(values.get(k)==v for k,v in complete.items())
    resident=[]
    for obj in UnityPy.load(str(work/'original/assets/bin/Data')).objects:
        if obj.type.name!='TextAsset':continue
        asset=obj.read()
        if asset.m_Name=='TextDatabase':
            parts=asset.m_Script.split('\t')
            resident.append(dict(zip(parts[1::2],parts[2::2])))
    assert len(resident)==1
    original=resident[0]['PARTY_NUM_RATE']
    cecil='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
    exe=work/'build/PartyFormatProbe.exe'
    subprocess.run(['mcs','-r:'+cecil,'-out:'+str(exe),str(Path(__file__).with_name('PartyFormatProbe.cs'))],check=True)
    probe=subprocess.check_output(['mono',str(exe),str(work/'build/Assembly-CSharp.dll'),
        base64.b64encode(original.encode()).decode(),base64.b64encode(values['PARTY_NUM_RATE'].encode()).decode()],text=True).strip()
    return {'supplemental_total':len(values),'new_repair_keys':len(repairs),'completion_keys':len(complete),'required_keys_and_format_contracts':len(required),
        'party_format_regression':probe,'resident_party_format':original,'override_party_format':values['PARTY_NUM_RATE']}

def verify_categories(work):
    tables=work/'tables'
    areas=json.loads((tables/'MasterDataArea.json').read_text())
    categories=json.loads((tables/'MasterDataAreaCategory.json').read_text())
    # Legacy JSON contains literal CRLF inside story strings. read_text would
    # normalize them and incorrectly report a source-data change.
    quests=json.loads((tables/'MasterDataQuest.json').read_bytes(),strict=False)
    source={}
    for obj in UnityPy.load(str(work/'donor/assets/bin/Data')).objects:
        if obj.type.name=='TextAsset':
            asset=obj.read()
            if asset.m_Name in ('MasterDataArea','MasterDataQuest'):
                source[asset.m_Name]=json.loads(asset.m_Script,strict=False)
    assert quests==source['MasterDataQuest'],'Source quest gameplay rows changed'
    original={r['fix_id']:r for r in source['MasterDataArea']}
    assert {r['fix_id'] for r in areas}==set(original)
    category_by_id={c['fix_id']:c for c in categories}
    assert len(categories)==len(category_by_id)==5
    grouped={c['fix_id']:[] for c in categories}
    for area in areas:
        before=original[area['fix_id']]
        for key,value in before.items():
            if key not in ('bgm_normal','bgm_boss'):assert area[key]==value,(area['fix_id'],key)
        if area['type'] in (0,4):
            assert area['area_cate_id']==0 # Tutorial stages must not become repeatable menu entries.
        else:
            assert area['area_cate_id'] in category_by_id
            grouped[area['area_cate_id']].append(area)
    assert all(grouped.values())
    assert len(grouped[1])==42 and len(grouped[2])==36 and len(grouped[3])==7 and len(grouped[6])==6 and len(grouped[9])==6
    assert {c['quest_tab_type'] for c in categories}=={1,2,3,4,6}
    # The normal list filters locked areas using all prior-area quest bits.
    # Before any story clears, the first town must be reachable; the next must
    # stay locked. Completing all first-town quests must unlock the next town.
    def available(cleared):
        return [a['fix_id'] for a in grouped[1] if a['event_id']==0 and
            (a['pre_area']==0 or all(q['fix_id'] in cleared for q in quests if q['area_id']==a['pre_area']))]
    assert available(set())==[100100]
    first={q['fix_id'] for q in quests if q['area_id']==100100}
    assert first and available(first)==[100100,100101]
    assert available(first-{next(iter(first))})==[100100]
    return {'category_rows':len(categories),'mapped_menu_areas':sum(map(len,grouped.values())),
        'unchanged_quest_rows':len(quests),'unchanged_area_gameplay_rows':len(areas),
        'new_account_story_areas':available(set()),'after_first_town_clear':available(first),
        'tutorial_area_ids_excluded':[a['fix_id'] for a in areas if a['type']==4]}

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('work',type=Path);p.add_argument('--output',type=Path,required=True);args=p.parse_args()
    report={'status':'PASS','maps':verify_maps(args.work),'text':verify_text(args.work),'categories':verify_categories(args.work),'android_tested':False}
    args.output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps(report,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
