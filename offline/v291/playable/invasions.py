"""Audit permanent invasion collisions; never change original master rows."""
import collections,json
from pathlib import Path
def audit(tables,output):
    table=lambda name:json.loads((tables/(name+'.json')).read_bytes(),strict=False)
    quests={r['fix_id']:r for r in table('MasterDataQuest') if r['active']==2}
    groups={r['fix_id']:r for r in table('MasterDataEnemyGroup')}
    enemies={r['fix_id']:r for r in table('MasterDataEnemyChara')}
    chars={r['fix_id']:r for r in table('MasterDataParamChara')}
    categories={r['fix_id']:r for r in table('MasterDataCategoryPattern')}
    expects={r['fix_id']:r for r in table('MasterDataExpectPattern')}
    floors=table('MasterDataQuestFloor')
    byquest=collections.defaultdict(dict);source=table('MasterDataGuerrillaBoss');active=0
    for r in source:
        if r['active']!=2 or r['quest_id'] not in quests:continue
        active+=1
        byquest[r['quest_id']][r['enemy_group_id']]=r
    rows=[]
    for q,choices in sorted(byquest.items()):
        rates={r['enemy_rate'] for r in choices.values()}
        assert len(rates)==1,('Explicit invasion rate policy required',q,rates)
        entries=[]
        for g,r in choices.items():
            units=[];seen=set();chain=g
            while chain:
                assert chain not in seen;seen.add(chain);group=groups[chain]
                for i in range(1,8):
                    eid=group['enemy_id_'+str(i)]
                    if eid:
                        e=enemies[eid];c=chars[e['chara_id']];assert not e['drop_unit_id'] or e['drop_unit_id'] in chars
                        units.append(dict(enemy_id=eid,character_id=c['fix_id'],name=c['name'],drop_unit_id=e['drop_unit_id']))
                chain=group['chain_id']
            entries.append(dict(group_id=g,enemies=units,rate=r['enemy_rate'],conditional_probability=1/len(choices)))
        possible=[f['fix_id'] for f in floors if f['quest_id']==q and f['active']==2 and expects[f['pattern_expect']]['level7']>0 and categories[f['pattern_category']]['cate1']>0]
        rows.append(dict(quest_id=q,name=quests[q]['quest_name'],total_rate=next(iter(rates)),groups=entries,seven_star_enemy_possible_floors=possible))
    result=dict(source_rows=len(source),eligible_schedule_rows=active,quests=len(rows),distinct_choices=sum(len(r['groups']) for r in rows),
      conflicting_quests=sum(len(r['groups'])>1 for r in rows),policy='Ignore invasion dates only; keep quest/active/user-group/prerequisite rules. Deduplicate repeated schedules. Keep original total chance, then choose a distinct group uniformly per floor.',
      no_seven_star_enemy_floors=[r['quest_id'] for r in rows if not r['seven_star_enemy_possible_floors']],entries=rows)
    (output/'permanent-invasions.json').write_text(json.dumps(result,ensure_ascii=False,indent=2))
    return result
