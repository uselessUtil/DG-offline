"""Conservative 2.9.1-era expansion; no replacement of recovered native assets.

The date audit is evidence, not an official historical snapshot. This module
never invents skill semantics: unsupported normal-skill abilities are excluded.
Every generated layout/presentation substitute is recorded.
"""
import copy, json, hashlib
from pathlib import Path
from map_layout import canonical_map,separate_panels
ROOT=Path('/workspace')
AUDIT=ROOT/'artifacts/2.9.1-wiki-dgdb-coverage-2026-10-25'
def expand(base,schemas,db,quest_map):
    di={k:{r['fix_id']:r for r in rs if 'fix_id' in r} for k,rs in db.items()}
    for name,sha in [('units.json','dd998f5d136a7f646bc3eb515b4c253eedd9532d7639457452f8c3f8c8a3ce19'),('quests.json','c9eb581ad689d42f7a8a9c459ff573da0c664bec220d2366975ca74d86540346')]:
        assert hashlib.sha256((AUDIT/name).read_bytes()).hexdigest()==sha,'Wiki evidence selection changed'
    ur=json.loads((AUDIT/'units.json').read_text())['period_end']
    qr=json.loads((AUDIT/'quests.json').read_text())['period_end']
    excluded=[r for r in ur if any(i[0]=='NS_ability' for i in r['issues'])]
    target={r['id'] for r in ur}-{r['id'] for r in excluded}
    assert len(target)==1748
    original_chars={r['fix_id']:r for r in base['MasterDataParamChara'] if r['fix_id']}
    original_quests={r['fix_id']:r for r in base['MasterDataQuest'] if r['fix_id'] and r['active']==2}
    assert len(original_chars)==595 and len(original_quests)==392 and set(original_chars)<=target
    fullmap=dict(quest_map);reverse={fullmap.get(i,i):i for i in original_quests}
    selected=[]
    rejected=[]
    for q in qr:
        if q['dependencies']['issues']:
            rejected.append(q);continue
        enemies=[di['enemy'][i] for i in q['dependencies']['enemy_ids']]
        if any(e['chara_id'] not in target or e['drop_unit_id'] and e['drop_unit_id'] not in target for e in enemies):
            rejected.append(dict(q,restoration_reason='unsupported character dependency'));continue
        d=di['quest'][q['id']]
        if d.get('clear_unit') and d['clear_unit'] not in target:
            rejected.append(dict(q,restoration_reason='clear reward outside dated character set'));continue
        requirement=di['quest_requirement'].get(d.get('quest_requirement_id'))
        if requirement:
            extras={k:v for k,v in requirement.items() if k not in schemas['MasterDataQuestRequirement'] and k not in ['created_at','updated_at'] and v not in [None,0,'']}
            outside={k:v for k,v in requirement.items() if k.startswith('fix_unit_') and k.endswith('_id') and '_link_' not in k and v and v not in target}
            if extras or outside:
                rejected.append(dict(q,restoration_reason='later/unsupported quest restrictions or fixed-team references',unsupported=extras,outside_characters=outside));continue
        selected.append(q)
    assert len(selected)<=924
    assert set(reverse)<={r['id'] for r in selected}
    meta={'date_cutoff':'2015-12-09','historical_snapshot':False,
          'original_characters':sorted(original_chars),'original_quests':sorted(original_quests),
          'characters':sorted(target),'excluded_characters':excluded,
          'excluded_quest_candidates':rejected,'fallbacks':[],'quest_mapping':fullmap}
    def native(typ,d,prior=None):
        prior=prior or {}
        return {k:copy.deepcopy(d[k] if k in d else prior[k] if k in prior else '' if t=='System.String' else False if t=='System.Boolean' else 0)
                for k,t in schemas[typ].items() if k not in {'timing_public','value_list','value_turn_min','value_turn_max','param_list'} and not t.endswith('[]')}
    char_template=original_chars[10]
    for i in sorted(target-set(original_chars)):
        d=di['unit'][i]
        r=native('MasterDataParamChara',d,char_template)
        base['MasterDataParamChara'].append(r)
    from size_layout import apply as apply_visual_sizes
    added=[r for r in base['MasterDataParamChara'] if r['fix_id'] and r['fix_id']not in original_chars]
    meta['visual_size_adapter']=apply_visual_sizes(added,original_chars,di['unit'])
    assert meta['visual_size_adapter']['changed_count']==304
    meta['fallbacks'].append({'table':'MasterDataParamChara','ids':sorted(target-set(original_chars)),
        'reason':'No recovered per-character prefab; HUD/cutin metadata initially from character10; visual dimensions/pivot/spacing mapped explicitly from DGDB size and native595 profiles. Exact DGDB large/icon images by draw_id. Native original595 prefab routes remain preferred.'})
    used={r['fix_id'] for r in base['MasterDataQuest'] if r}
    for q in selected:
        i=q['id']
        if i in reverse:continue
        targetid=i
        if targetid in used:
            targetid=next(n for n in range(1917,2048) if n not in used and n not in {x['id'] for x in selected})
        used.add(targetid);reverse[i]=targetid
        if i!=targetid:fullmap[targetid]=i
    oldareas={r['fix_id']:r for r in base['MasterDataArea'] if r['fix_id']}
    oldfloors=[r for r in base['MasterDataQuestFloor'] if r and r['active']==2]
    area_reverse={}
    for oldid,q in original_quests.items():
        source=di['quest'][quest_map.get(oldid,oldid)]['area_id']
        area_reverse.setdefault(source,q['area_id'])
    # Native resource identities only exist in the cache. New textures are
    # optional fallback assets, never overrides for any existing prefab.
    bytexture={r['res_map']:r for r in oldareas.values() if r['mesh_map'] and r['mesh_door']}
    normal_template=bytexture['map_0001_tex']
    split_template=bytexture['map_0012_tex']
    newareas={}
    for q in selected:
        d=di['quest'][q['id']];aid=d['area_id']
        if aid in area_reverse:continue
        if aid in oldareas:
            area_reverse[aid]=aid;continue
        a=di['area'][aid]
        template=bytexture.get(a['res_map'])
        # Exact native texture/model family wins; otherwise record a shared
        # atlas-compatible geometry substitute for an absent original model.
        if template is None:
            from PIL import Image
            path=ROOT/'external/dgdata/map'/(a['res_map']+'.png')
            assert path.exists(),('Missing original atlas',path)
            with Image.open(path) as im:
                split=separate_panels(canonical_map(im))
            template=split_template if split else normal_template
        r=native('MasterDataArea',a,template)
        r.update(fix_id=aid,event_id=0,packname_bgm='',packname_se='',area_effect='')
        newareas[aid]=r;area_reverse[aid]=aid
        meta['fallbacks'].append({'table':'MasterDataArea','id':aid,'source_res_map':a['res_map'],
            'mesh_template':template['fix_id'],'native_source_exists':a['res_map'] in bytexture,
            'reason':'Original family model when available; otherwise reusable original geometry/door animation with new atlas because original model is missing. Old models/animations are never replaced. Archived event dates removed for local availability; no new audio source invented.'})
    allareas=set(oldareas)|set(newareas)
    for aid,r in newareas.items():
        parent=di['area'][aid]['pre_area']
        if parent in area_reverse:r['pre_area']=area_reverse[parent]
        elif parent in allareas:r['pre_area']=parent
        elif parent:
            meta['fallbacks'].append({'table':'MasterDataArea','id':aid,'field':'pre_area','source':parent,'effective':0,
                                     'reason':'Historical prerequisite area has no recoverable selected stages; avoid a permanently inaccessible restored menu.'})
            r['pre_area']=0
    base['MasterDataArea']+=list(newareas.values())
    categories={r['area_cate_id'] for r in newareas.values() if r.get('area_cate_id')}
    base['MasterDataAreaCategory']=[native('MasterDataAreaCategory',di['area_category'][i]) for i in sorted(categories)]
    # Only new quests use DGDB menu topology; existing save IDs and order stay.
    quest_template=next(r for r in original_quests.values() if r['fix_id']>5)
    for q in selected:
        i=q['id'];targetid=reverse[i]
        if targetid in original_quests:continue
        d=di['quest'][i];r=native('MasterDataQuest',d,quest_template)
        r.update(fix_id=targetid,area_id=area_reverse[d['area_id']],active=2)
        base['MasterDataQuest'].append(r)
    cats={r['fix_id']:r for r in base['MasterDataCategoryPattern']}
    expects={r['fix_id']:r for r in base['MasterDataExpectPattern']}
    def valid(f,c,e):
        stars={j for j in range(1,8) if e['level'+str(j)]>0}
        if not stars:return False
        for ci,prefix in [(1,'enemy'),(2,'trap'),(3,'item'),(4,'heal')]:
            if c['cate'+str(ci)] and any(not f[prefix+'_group_id_'+str(j)] for j in stars):return False
        return True
    last=max(r['fix_id'] for r in base['MasterDataQuestFloor'] if r)
    pairs={(f['pattern_category'],f['pattern_expect']) for f in oldfloors}
    for q in selected:
        qi=reverse[q['id']]
        if qi in original_quests:continue
        for d in db['quest_floor']:
            if d['quest_id']!=q['id']:continue
            candidate=(d['pattern_category'],d['pattern_expect'])
            if candidate[0] in cats and candidate[1] in expects and valid(d,cats[candidate[0]],expects[candidate[1]]):
                pair=candidate
            else:
                eligible=[p for p in pairs if valid(d,cats[p[0]],expects[p[1]])]
                if not eligible:
                    # A key + empty floor is the only safe no-group pattern.
                    c=dict(next(iter(cats.values())));c.update(fix_id=max(cats)+1,cate0=24,cate1=0,cate2=0,cate3=0,cate4=0,cate5=1)
                    cats[c['fix_id']]=c;base['MasterDataCategoryPattern'].append(c)
                    e=dict(next(iter(expects.values())));e.update(fix_id=max(expects)+1,**{'level'+str(j):25 if j==1 else 0 for j in range(1,8)})
                    expects[e['fix_id']]=e;base['MasterDataExpectPattern'].append(e);eligible=[(c['fix_id'],e['fix_id'])]
                pair=min(eligible,key=lambda p:(abs(cats[p[0]]['cate1']-14)+abs(cats[p[0]]['cate2']-2)+abs(cats[p[0]]['cate3']-2)+abs(cats[p[0]]['cate4']-2),p))
            prior=oldfloors[0];last+=1;r=native('MasterDataQuestFloor',d,prior)
            r.update(fix_id=last,quest_id=qi,active=2,pattern_category=pair[0],pattern_expect=pair[1])
            base['MasterDataQuestFloor'].append(r)
            meta['fallbacks'].append({'table':'MasterDataQuestFloor','id':last,'quest_id':qi,'dgdb_floor_id':d['fix_id'],
                'source_patterns':candidate,'effective_patterns':pair,
                'reason':'DGDB lacks generation pattern tables. Use a compatible recovered 24+1-key pattern with only supported star/group combinations; original106 random algorithm retained, historical exact layout distribution is not claimed.'})
    # Missing enemy presentation is deliberately seeded, not treated as balance.
    enemies=base['MasterDataEnemyChara'];have={r['chara_id'] for r in enemies}
    template=enemies[0]
    for i in sorted(target-have):
        r=copy.deepcopy(template);r.update(fix_id=900000000+i,chara_id=i);enemies.append(r)
    actions=base['MasterDataEnemyActionParam'];have={r['skill_type'] for r in actions}
    for typ in range(1,10):
        if typ not in have:
            r=copy.deepcopy(actions[0]);r.update(fix_id=900000000+typ,skill_type=typ,effect_id=0,shake_screen=0)
            actions.append(r)
            meta['fallbacks'].append({'table':'MasterDataEnemyActionParam','skill_type':typ,'reason':'No recovered original action effect; no visual effect/shake, exact DGDB damage/status/action parameters retained.'})
    meta['expected_characters']=len(target);meta['expected_quests']=len(original_quests)+sum(reverse[q['id']] not in original_quests for q in selected)
    meta['expected_floor_rows']=sum(1 for f in db['quest_floor'] if f['quest_id'] in reverse)
    assert 392<meta['expected_quests']<=924
    meta['new_character_ids']=sorted(target-set(original_chars))
    meta['new_quest_ids']=sorted(set(reverse.values())-set(original_quests))
    return base,meta
