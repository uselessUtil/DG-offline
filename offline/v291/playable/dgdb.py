"""Deterministic DGDB projection into the native 2.9.1 schema.

The old cache controls identities and presentation, never combat balance.
Missing layout tables are the one explicit gameplay fallback.
"""
import collections
import copy
import hashlib
import json
import re
from pathlib import Path

ROOT = Path('/workspace')
SQL = ROOT/'external/dgdb/database/dump_20170921.sql'
SQL_SHA = 'a507e0d519a70bab7617c7e37bd9f32b2a75894eb8bdbbffffa0c5b4b259d25f'
QUEST_MAP = {383:1883,389:1884,390:1885,391:1886,392:1887,393:1888}
TOKEN = re.compile(r"\s*(?:'((?:\\.|''|[^'\\])*)'|(NULL|-?[0-9]+(?:\.[0-9]+)?))\s*(?:,|$)")
ESC = {'0':'\0','b':'\b','n':'\n','r':'\r','t':'\t','Z':'\x1a'}
def values(text):
    result=[]; pos=0
    while pos<len(text):
        m=TOKEN.match(text,pos); assert m,(pos,text[pos:pos+30])
        if m[1] is not None: v=re.sub(r'\\(.)',lambda x:ESC.get(x[1],x[1]),m[1]).replace("''", "'")
        elif m[2]=='NULL':v=None
        else:v=float(m[2]) if '.' in m[2] else int(m[2])
        result.append(v);pos=m.end()
    return result

def sql_tables(path):
    tables={};columns={};current=None
    for line in path.open():
        m=re.match(r'INSERT INTO \x60([^\x60]+)\x60 \((.*?)\) VALUES',line)
        if m:
            current=m[1];columns[current]=re.findall(r'\x60([^\x60]+)\x60',m[2]);tables.setdefault(current,[]);continue
        if current and line.startswith('('):
            text=line.rstrip();assert text.endswith(('),',');'))
            row=values(text[1:-2]);assert len(row)==len(columns[current]),current
            tables[current].append(dict(zip(columns[current],row)))
            if text.endswith(';'):current=None
    return tables,columns
LAST_AUDIT = None
def read_sql(path=SQL):
    assert hashlib.sha256(path.read_bytes()).hexdigest()==SQL_SHA, 'DGDB source changed'
    return sql_tables(path)[0]

def index(rows):
    result={}
    for r in rows:
        if not r or "fix_id" not in r:continue
        assert r['fix_id'] not in result, ('duplicate ID',r['fix_id'])
        result[r['fix_id']]=r
    return result

def encode(rows):return json.dumps(rows,ensure_ascii=True,separators=(',',':')).encode()

def native_guard_fields(skill):
    """DGDB type 5 stores remaining damage %, not reduction %, in value_01."""
    if skill['skill_type'] != 5:
        return {}
    element, rate = skill['skill_value_00'], skill['skill_value_01']
    assert isinstance(element, int) and 0 <= element <= 7, ('Unsupported guard element', skill['fix_id'])
    assert isinstance(rate, int) and 0 <= rate <= 100, ('Unsupported guard rate', skill['fix_id'])
    assert all(skill['skill_value_%02d' % i] == 0 for i in range(2,16)), ('Unsupported guard parameters', skill['fix_id'])
    fields = {'skill_decline_dmg_active':2, 'skill_decline_dmg_element':element, 'skill_decline_dmg_rate':rate}
    prior = {k:skill[k] for k in fields}
    assert all(v == 0 for v in prior.values()) or prior == fields, ('Conflicting native guard', skill['fix_id'])
    return fields

def project(source, inventory):
    global LAST_AUDIT
    db=read_sql();di={n:index(rs) for n,rs in db.items()}
    schemas={}
    for line in inventory.splitlines():
        p=line.split('\t')
        if p[0]=='column':schemas.setdefault(p[1],{})[p[2]]=p[3]
    base={n:json.loads(raw,strict=False) for n,raw in source.items()}
    # Serialized target table names differ from old cache asset names.
    aliases={'MasterDataParamEnemy':'MasterDataEnemyChara','MasterDataStatusAilmentParam':'MasterDataStatusAilment'}
    old={n:index(base.get(aliases.get(n,n),[])) for n in schemas}
    chars={i:r for i,r in old['MasterDataParamChara'].items() if i}
    quests={i:r for i,r in old['MasterDataQuest'].items() if i and r['active']==2}
    assert len(chars)==595 and len(quests)==392
    reverse={QUEST_MAP.get(i,i):i for i in quests}
    audit={'data_version':'dgdb-1','dgdb_sha256':SQL_SHA,'dgdb_commit':'1a15adf733bb02467d51303a110f361535a7b4bc',
           'characters':sorted(chars),'quests':sorted(quests),'quest_mapping':QUEST_MAP,
           'fallbacks':[],'excluded_evolutions':[],'tables':{},'skill_changes':{}}
    result=dict(source)
    runtime={'timing_public','value_list','value_turn_min','value_turn_max','param_list'}
    def emit(native, rows):
        result[aliases.get(native,native)]=encode(rows)
        audit['tables'][native]={'rows':len(rows),'sha256':hashlib.sha256(result[aliases.get(native,native)]).hexdigest()}
    def row(native, sqlrow, fallback=None, overrides=None):
        fallback=fallback or {}
        out={}
        for key,typ in schemas[native].items():
            if key in runtime:continue
            if key in sqlrow:
                out[key]=sqlrow[key]
                if out[key] is None and typ!='System.String':raise ValueError(('NULL numeric',native,key))
            elif key in fallback:out[key]=fallback[key]
            elif typ=='System.String':out[key]=''
            elif typ=='System.Boolean':out[key]=False
            elif typ.endswith('[]'):continue
            else:out[key]=0
        out.update(overrides or {})
        return out
    def require(table, ident):
        assert ident in di[table],('Missing DGDB dependency',table,ident)
        return di[table][ident]
    # Keep zero/sentinel character row, then exactly the cache roster.
    units=[old['MasterDataParamChara'][0]]
    for i,c in chars.items():
        d=require('unit',i);assert d['draw_id']==c['draw_id'] and d['res_chara_id_str']==c['res_chara_id_str']
        units.append(row('MasterDataParamChara',d,c))
    emit('MasterDataParamChara',units)
    audit['fallbacks'].append({'table':'MasterDataParamChara','fields':sorted(set(schemas['MasterDataParamChara'])-set(db['unit'][0])-runtime),'reason':'old portrait/model placement; unchanged resource identity'})
    # Preserve menu membership, order and IDs while adopting DGDB quest balance.
    qr=[]
    for r in base['MasterDataQuest']:
        if not r or r['fix_id'] not in quests:qr.append(r);continue
        d=require('quest',QUEST_MAP.get(r['fix_id'],r['fix_id']))
        assert d['clear_unit']==0 or d['clear_unit'] in chars
        qr.append(row('MasterDataQuest',d,r,{'fix_id':r['fix_id'],'area_id':r['area_id'],'active':2}))
    emit('MasterDataQuest',qr)
    # Old area topology and asset routes are essential to the fixed old quest list.
    area_map={}
    for i,q in quests.items():area_map[q['area_id']]=require('quest',QUEST_MAP.get(i,i))['area_id']
    areas=[]
    for r in base['MasterDataArea']:
        if not r:areas.append(r);continue
        target=area_map.get(r['fix_id'],r['fix_id']);d=di['area'].get(target)
        out=copy.deepcopy(r)
        if d:
            for k in ['area_name','area_name_eng','area_detail','area_element']+['cost'+str(j) for j in range(7)]:
                if k in schemas['MasterDataArea']:out[k]=d[k]
        areas.append(out)
    emit('MasterDataArea',areas)
    audit['fallbacks'].append({'table':'MasterDataArea','reason':'cache menu topology, resource routes, music and activity scheduling retained; DGDB costs and element adopted','dgdb_area_mapping':area_map})
    # Floor IDs are internal. Preserve cache floor identity where a semantic
    # pair exists; maintain DGDB table order within each floor kind.
    fs=[];oldfloors=base['MasterDataQuestFloor'];used_ids=set()
    new_id=max(r['fix_id'] for r in oldfloors if r)+1
    for qi in quests:
        of=[r for r in oldfloors if r and r['quest_id']==qi and r['active']==2]
        kinds=collections.defaultdict(list)
        for f in of:kinds[f['under']].append(f)
        counts=collections.Counter()
        for d in db['quest_floor']:
            if d['quest_id']!=QUEST_MAP.get(qi,qi):continue
            n=counts[d['under']];counts[d['under']]+=1
            prior=kinds[d['under']][n] if n<len(kinds[d['under']]) else None
            assert prior is not None or qi==1,('No old generation pattern',qi,d['under'],n)
            prior=prior or next(r for r in oldfloors if r and r['quest_id']==1)
            fid=prior['fix_id']
            if fid in used_ids:fid=new_id;new_id+=1
            used_ids.add(fid)
            fs.append(row('MasterDataQuestFloor',d,prior,{'fix_id':fid,'quest_id':qi,'active':2,
               'pattern_expect':prior['pattern_expect'],'pattern_category':prior['pattern_category']}))
    assert len(fs)==763
    emit('MasterDataQuestFloor',fs)
    audit['fallbacks'].append({'table':'MasterDataQuestFloor','fields':['pattern_expect','pattern_category'],'reason':'DGDB has no CategoryPattern/ExpectPattern; paired old floor generation and random 24+1 key logic retained'})
    # Only evolution routes fully contained in the fixed content set.
    ev=[]
    for d in db['evo']:
        if d['unit_id_pre'] not in chars:continue
        invalid={k:d[k] for k in ['unit_id_after','unit_id_parts1','unit_id_parts2','unit_id_parts3','unit_id_parts4'] if d[k] and d[k] not in chars}
        if d['quest_id'] and d['quest_id'] not in reverse:invalid['quest_id']=d['quest_id']
        if invalid:audit['excluded_evolutions'].append({'row':d,'outside':invalid});continue
        ev.append(row('MasterDataParamCharaEvol',d,overrides={'active':2,'quest_id':reverse.get(d['quest_id'],0)}))
    assert len(ev)==262 and len(audit['excluded_evolutions'])==92
    emit('MasterDataParamCharaEvol',ev)
    # Keep the old schedule and eligible encounter set, but use DGDB groups/stats.
    invasion=base['MasterDataGuerrillaBoss']
    groupids={f[k] for f in fs for k in ['boss_group_id']+['enemy_group_id_'+str(j) for j in range(1,8)] if f[k]}
    groupids.update(r['enemy_group_id'] for r in invasion if r and r.get('active')==2 and r.get('quest_id') in quests and r.get('enemy_group_id'))
    todo=list(groupids)
    while todo:
        g=todo.pop();chain=require('enemy_group',g)['chain_id']
        if chain and chain not in groupids:groupids.add(chain);todo.append(chain)
    emit('MasterDataEnemyGroup',[row('MasterDataEnemyGroup',require('enemy_group',i)) for i in sorted(groupids)])
    enemyids={di['enemy_group'][g]['enemy_id_'+str(j)] for g in groupids for j in range(1,8) if di['enemy_group'][g]['enemy_id_'+str(j)]}
    enemies=[]
    for i in sorted(enemyids):
        d=require('enemy',i)
        assert d['chara_id'] in chars and (not d['drop_unit_id'] or d['drop_unit_id'] in chars),('Enemy outside roster',i)
        fallback=old['MasterDataParamEnemy'].get(i)
        if fallback is None:
            candidates=[r for r in old['MasterDataParamEnemy'].values() if r['chara_id']==d['chara_id']]
            assert candidates,('Missing enemy presentation',i,d['chara_id'])
            fallback=min(candidates,key=lambda r:r['fix_id'])
        enemies.append(row('MasterDataParamEnemy',d,fallback))
    emit('MasterDataParamEnemy',enemies)
    audit['fallbacks'].append({'table':'MasterDataParamEnemy','fields':['posz_value','pos_absolute','posx_offset','posy_offset','hp_gauge_type','hp_posx_offset','hp_posy_offset'],'reason':'old enemy presentation; new enemy IDs inherit matching-character placement'})
    # Actions and abilities form their own recursive closure.
    atids={r['act_table'+str(j)] for r in enemies for j in range(1,9) if r['act_table'+str(j)]}
    apids={r[k] for r in enemies for k in ['act_first','act_dead'] if r[k]}
    abids={r['ability'+str(j)] for r in enemies for j in range(1,5) if r['ability'+str(j)]}
    for i in sorted(atids):
        d=require('enemy_action_table',i)
        apids.update(d[k] for k in d if k.startswith('action_param_id') and d[k])
    emit('MasterDataEnemyActionTable',[row('MasterDataEnemyActionTable',require('enemy_action_table',i)) for i in sorted(atids)])
    actions=[]
    for i in sorted(apids):
        d=require('enemy_action_param',i)
        assert all(not d.get('skill_param'+str(j),0) for j in range(9,17)),('Unsupported action parameters',i)
        prior=old['MasterDataEnemyActionParam'].get(i)
        if prior is None:
            matches=[r for r in old['MasterDataEnemyActionParam'].values() if r['skill_type']==d['skill_type']]
            assert matches,('No action presentation',i,d['skill_type'])
            prior=min(matches,key=lambda r:r['fix_id'])
        actions.append(row('MasterDataEnemyActionParam',d,prior))
    emit('MasterDataEnemyActionParam',actions)
    abilities=[row('MasterDataEnemyAbility',require('enemy_ability',i)) for i in sorted(abids)]
    emit('MasterDataEnemyAbility',abilities)
    # Trap/item/heal groups are selected by the actual DGDB floor rows.
    pgids={f[k] for f in fs for k in [p+'_group_id_'+str(j) for p in ['trap','item','heal'] for j in range(1,8)] if f[k]}
    missing_pg=sorted(pgids-set(di['panel_group']))
    assert not missing_pg,('DGDB panel group missing',missing_pg)
    pids={di['panel_group'][g]['panel_id_'+str(j)] for g in pgids for j in range(1,8) if di['panel_group'][g]['panel_id_'+str(j)]}
    emit('MasterDataParamPanelGroup',[row('MasterDataParamPanelGroup',require('panel_group',i)) for i in sorted(pgids)])
    panels=[]
    for i in sorted(pids):
        d=require('panel',i);prior=old['MasterDataParamPanel'].get(i)
        if prior is None:
            candidates=[r for r in old['MasterDataParamPanel'].values() if r['effective_type']==d['effective_type'] and r['trap_type']==d['trap_type'] and r['res_panel']==d['res_panel']]
            assert candidates,('Missing panel visual effect mapping',i)
            prior=min(candidates,key=lambda r:(abs(r['effective_value']-d['effective_value']),r['fix_id']))
            audit['fallbacks'].append({'table':'MasterDataParamPanel','id':i,'fields':['effect_type'],'visual_from':prior['fix_id']})
        panels.append(row('MasterDataParamPanel',d,prior))
    emit('MasterDataParamPanel',panels)
    reqids={r['quest_requirement_id'] for r in qr if r and r.get('active')==2 and r.get('quest_requirement_id')}
    reqs=[]
    for i in sorted(reqids):
        d=require('quest_requirement',i)
        extras={k:v for k,v in d.items() if k not in schemas['MasterDataQuestRequirement'] and k not in ['created_at','updated_at'] and v not in [0,None,'']}
        assert not extras,('Unsupported quest restriction',i,extras)
        reqs.append(row('MasterDataQuestRequirement',d))
    emit('MasterDataQuestRequirement',reqs)
    # Full native skill closure. Include all 39 old buffs in the audit even when unused.
    asids={c['skill_limitbreak'] for c in units if c and c.get('skill_limitbreak')}
    asids.update(i for i,r in old['MasterDataSkillLimitBreak'].items() if i and r['skill_cate'] in [17,18,43])
    lsids={c['skill_leader'] for c in units if c and c.get('skill_leader')}
    nsids={c[k] for c in units if c for k in ['skill_active0','skill_active1'] if c.get(k)}
    nsids.update(i for i,r in old['MasterDataSkillActive'].items() if i and r.get('always')==2)
    psids={c['skill_passive'] for c in units if c and c.get('skill_passive')}
    todo=list(lsids)
    while todo:
        i=todo.pop();n=require('l_skill',i)['add_fix_id']
        if n and n not in lsids:lsids.add(n);todo.append(n)
    active=[]
    for i in sorted(nsids):
        d=require('n_skill',i)
        assert not d.get('ability'),('Unsupported normal skill ability',i)
        prior=old['MasterDataSkillActive'].get(i)
        if prior is None:
            candidates=[r for r in old['MasterDataSkillActive'].values() if r['skill_type']==d['skill_type'] and r['skill_element']==d['skill_element'] and r['fix_id']]
            assert candidates,('No normal-skill visual mapping',i)
            prior=min(candidates,key=lambda r:sum(r.get('cost'+str(j),0)!=d['cost'+str(j)] for j in range(1,6)))
            audit['fallbacks'].append({'table':'MasterDataSkillActive','id':i,'visual_from':prior['fix_id'],'fields':['effect','skill_active']})
        active.append(row('MasterDataSkillActive',d,prior))
    emit('MasterDataSkillActive',active)
    skills=[]
    for i in sorted(asids):
        d=require('a_skill',i)
        assert not any(d['value'+str(j)] for j in range(5,16)),('Unsupported extended AS',i)
        skills.append(row('MasterDataSkillLimitBreak',d))
    emit('MasterDataSkillLimitBreak',[row('MasterDataSkillLimitBreak',require('a_skill',0),old['MasterDataSkillLimitBreak'].get(0))]+skills)
    leaders=[];guard_conversions=[]
    for i in sorted(lsids):
        d=require('l_skill',i);guard=native_guard_fields(d)
        leaders.append(row('MasterDataSkillLeader',d,overrides=guard))
        if guard:
            guard_conversions.append({'id':i,'name':d['name'],'detail':d['detail'],
                'element':d['skill_value_00'],'remaining_damage_percent':d['skill_value_01'],
                'native_fields':guard,'source_fields':{k:d[k] for k in guard},
                'add_fix_id':d['add_fix_id'],
                'users':[{'id':c['fix_id'],'draw_id':c['draw_id'],'name':c['name']}
                         for c in units if c and c.get('skill_leader')==i]})
    assert len(guard_conversions)==23 and sum(len(r['users']) for r in guard_conversions)==76
    audit['native_guard_conversions']=guard_conversions
    audit['fallbacks'].append({'table':'MasterDataSkillLeader',
        'fields':['skill_decline_dmg_active','skill_decline_dmg_element','skill_decline_dmg_rate'],
        'ids':[r['id'] for r in guard_conversions],
        'reason':'DGDB type5 element/remaining-damage percent mapped to native 2.9.1 guard flags; no balance changes'})
    passive=[row('MasterDataSkillPassive',require('p_skill',i)) for i in sorted(psids)]
    emit('MasterDataSkillLeader',leaders);emit('MasterDataSkillPassive',passive)
    boosts={r['skill_boost_id'] for r in active if r['skill_boost_id']}
    br=[row('MasterDataSkillBoost',require('skill_boost',i)) for i in sorted(boosts)]
    emit('MasterDataSkillBoost',br)
    sids={r['status_ailment'+str(j)] for r in skills+br+actions for j in range(1,5) if r['status_ailment'+str(j)]}
    sr=[]
    for i in sorted(sids):
        d=require('status_ailment',i)
        # Native ClearStatusAilment requires clear_type=2 for explicit dispel.
        # All imported finite statuses use that native flag; duration/update
        # flags remain exact DGDB values.
        sr.append(row('MasterDataStatusAilmentParam',d,overrides={'clear_type':2}))
    emit('MasterDataStatusAilmentParam',sr)
    audit['fallbacks'].append({'table':'MasterDataStatusAilmentParam','fields':['clear_type'],'value':2,'reason':'native dispellable status enum; DGDB omits the field'})
    # Diff complete definitions, including every character using each skill.
    for native,rows,field in [('MasterDataSkillLimitBreak',skills,'skill_limitbreak'),('MasterDataSkillLeader',leaders,'skill_leader'),('MasterDataSkillPassive',passive,'skill_passive'),('MasterDataSkillActive',active,None)]:
        diffs=[]
        for r in rows:
            prior=old[native].get(r['fix_id'],{})
            changes={k:{'old':prior.get(k),'dgdb':v} for k,v in r.items() if k not in runtime and prior.get(k)!=v}
            if changes:
                users=[{'id':c['fix_id'],'name':c['name']} for c in units if c and (c.get(field)==r['fix_id'] if field else r['fix_id'] in [c.get('skill_active0'),c.get('skill_active1')])]
                diffs.append({'id':r['fix_id'],'name':r.get('name'),'users':users,'changes':changes})
        audit['skill_changes'][native]=diffs
    audit['fallbacks'].append({'table':'MasterDataGuerrillaBoss/EventBase/AreaAmend','reason':'preserve existing local activity eligibility, permanent evolution temples and old encounter schedule; encounter groups and enemy data resolve to DGDB'})
    audit['fallbacks'].extend([
      {'table':'MasterDataSkillActive','fields':['effect','skill_active'],'reason':'DGDB omits both; existing native effect/enable metadata retained, new IDs use same type/element/cost presentation mapping'},
      {'table':'MasterDataParamPanel','fields':['effect_type'],'reason':'DGDB omits display effect; same-ID visual metadata or recorded same-kind visual mapping'},
      {'table':'MasterDataEnemyActionParam','fields':['effect_id','shake_screen'],'reason':'DGDB omits both presentation fields; existing original action visuals retained'},
      {'table':'MasterDataQuest','fields':['active','limit_exp','limit_money'],'reason':'fixed old availability and native reward-limit metadata absent from DGDB'},
      {'table':'MasterDataQuestFloor','fields':['active','ticket_effective','ticket_rate'],'reason':'fixed old availability and unsupported offline ticket metadata absent from DGDB'},
      {'table':'MasterDataParamCharaEvol','fields':['active'],'value':2,'reason':'explicitly enable only the dependency-complete whitelist routes'}
    ])
    table_map={'MasterDataParamChara':'unit','MasterDataQuest':'quest','MasterDataQuestFloor':'quest_floor',
      'MasterDataParamCharaEvol':'evo','MasterDataEnemyGroup':'enemy_group','MasterDataParamEnemy':'enemy',
      'MasterDataEnemyActionTable':'enemy_action_table','MasterDataEnemyActionParam':'enemy_action_param',
      'MasterDataEnemyAbility':'enemy_ability','MasterDataParamPanel':'panel','MasterDataParamPanelGroup':'panel_group',
      'MasterDataQuestRequirement':'quest_requirement','MasterDataSkillActive':'n_skill','MasterDataSkillLimitBreak':'a_skill',
      'MasterDataSkillLeader':'l_skill','MasterDataSkillPassive':'p_skill','MasterDataSkillBoost':'skill_boost',
      'MasterDataStatusAilmentParam':'status_ailment'}
    for native,table in table_map.items():
        fields=set(db[table][0])
        audit['tables'][native]['dgdb_columns']=sorted(set(schemas[native])&fields-runtime)
        audit['tables'][native]['missing_dgdb_columns']=sorted(set(schemas[native])-fields-runtime)
    LAST_AUDIT=audit
    return result, audit
