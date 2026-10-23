"""Offline structural regression checks, not an Android gameplay test."""
import argparse,base64,io,json,math,re,struct
from pathlib import Path

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
    return {'supplemental_total':len(values),'new_repair_keys':len(repairs),'required_keys_and_format_contracts':len(required)}

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('work',type=Path);p.add_argument('--output',type=Path,required=True);args=p.parse_args()
    report={'status':'PASS','maps':verify_maps(args.work),'text':verify_text(args.work),'android_tested':False}
    args.output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps(report,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
