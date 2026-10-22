"""Project cached stage tables onto the 1.0.6 schema; preserve saved quest IDs.

This imports existing stages only. Newer characters/skills/AI are deliberately
outside this stage-data patch. Generated JSON is embedded as CLR string data,
using only LitJson APIs already present in the original client.
"""
from collections import defaultdict
import copy
import hashlib
import json
from pathlib import Path

import UnityPy
from UnityPy.files.SerializedFile import SerializedFile


TABLE_FIELDS = [
    ('MasterDataArea', 'm_MasterDataArea', 'MasterDataArea'),
    ('MasterDataQuest', 'm_MasterDataQuest', 'MasterDataQuest'),
    ('MasterDataQuestFloor', 'm_MasterDataQuestFloor', 'MasterDataQuestFloor'),
    ('MasterDataCategoryPattern', 'm_MasterDataQuestFloorCategory', 'MasterDataCategoryPattern'),
    ('MasterDataExpectPattern', 'm_MasterDataQuestFloorExpect', 'MasterDataExpectPattern'),
    ('MasterDataParamPanel', 'm_MasterDataParamPanel', 'MasterDataParamPanel'),
    ('MasterDataParamPanelGroup', 'm_MasterDataParamPanelGroup', 'MasterDataParamPanelGroup'),
    ('MasterDataEnemyChara', 'm_MasterDataParamEnemy', 'MasterDataParamEnemy'),
    ('MasterDataEnemyGroup', 'm_MasterDataParamEnemyGroup', 'MasterDataEnemyGroup'),
    ('MasterDataAreaAmend', 'm_MasterDataAreaAmend', 'MasterDataAreaAmend'),
    ('MasterDataGuerrillaBoss', 'm_MasterDataGuerrillaBoss', 'MasterDataGuerrillaBoss'),
]


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def disabled(output):
    (output/'BackupStageData.cs').write_text('internal static class BackupStageData { public const bool Enabled=false; public static string Table(int i) { return null; } }')


def create(backup, original_tables, output, report_folder):
    original = {p.stem: json.loads(p.read_bytes(), strict=False) for p in original_tables.glob('*.json')}
    inventory = json.loads((Path(__file__).resolve().parents[1]/'artifacts/cache-backup-audit-2026-10-22/objects.json').read_text())
    cabs = sorted({r['cab'] for r in inventory if r['type']=='TextAsset' and r.get('name','').startswith('MasterData')})
    tables, sources = {}, []
    old = SerializedFile.set_version
    SerializedFile.set_version = lambda self,s: old(self,s.splitlines()[0])
    try:
        for relative in cabs:
            path = backup/relative
            raw = path.read_bytes()
            sources.append({'path':str(path), 'sha256':sha(raw)})
            for obj in UnityPy.load(raw).objects:
                if obj.type.name!='TextAsset':continue
                data = obj.read()
                if data.m_Name.startswith('MasterData'):
                    if data.m_Name in tables:raise ValueError('Ambiguous backup table '+data.m_Name)
                    tables[data.m_Name] = json.loads(data.m_Script, strict=False)
    finally:
        SerializedFile.set_version = old
    index = lambda rows: {r['fix_id']:r for r in rows}
    oi = {name:index(original[name]) for name,_,_ in TABLE_FIELDS}
    bi = {name:index(tables[name]) for name,_,_ in TABLE_FIELDS}
    oq, bq = oi['MasterDataQuest'], bi['MasterDataQuest']
    mapping = {}
    for qid,q in oq.items():
        if qid in bq:
            mapping[qid] = qid
        else:
            candidates = [v['fix_id'] for v in bq.values() if v['area_id']==q['area_id'] and v['quest_name']==q['quest_name']]
            if len(candidates)!=1:raise ValueError('Cannot safely match existing quest '+str(qid))
            mapping[qid] = candidates[0]
    reverse = {new:old for old,new in mapping.items()}
    assert len(reverse)==len(mapping)==371
    schemas = {name:set(original[name][0]) for name,_,_ in TABLE_FIELDS}
    def project(name,row):
        if not schemas[name] <= set(row):raise ValueError('Missing 1.0.6 fields in '+name)
        return {k:copy.deepcopy(row[k]) for k in original[name][0]}
    effective, fallbacks = {}, []
    # Fixed-size source tables retain original row order and IDs. Extra backup
    # rows are appended only by the supported guerrilla dependency walker below.
    for name,_,_ in TABLE_FIELDS:
        if name in ('MasterDataQuest','MasterDataQuestFloor','MasterDataGuerrillaBoss','MasterDataAreaAmend'):continue
        effective[name] = []
        for row in original[name]:
            rid = row['fix_id']
            src = bi[name].get(rid)
            if src is None:
                effective[name].append(copy.deepcopy(row))
                fallbacks.append({'table':name,'id':rid,'reason':'Absent from backup; retained for legacy in-progress snapshots.'})
            else:
                if name=='MasterDataEnemyChara' and any(v for k,v in src.items() if k.startswith('act_')):
                    raise ValueError('Existing enemy requires unsupported action engine')
                effective[name].append(project(name,src))
    effective['MasterDataQuest'] = []
    for qid in oq:
        row = project('MasterDataQuest', bq[mapping[qid]])
        row['fix_id'] = qid
        effective['MasterDataQuest'].append(row)
    original_floors, backup_floors = defaultdict(list), defaultdict(list)
    for row in original['MasterDataQuestFloor']:original_floors[row['quest_id']].append(row)
    for row in tables['MasterDataQuestFloor']:backup_floors[row['quest_id']].append(row)
    floor_replacements = {}
    for qid,newid in mapping.items():
        aa,bb = original_floors[qid],backup_floors[newid]
        if len(aa)!=len(bb):raise ValueError('Existing floor count changed; explicit migration needed')
        for a,b in zip(aa,bb):
            row = project('MasterDataQuestFloor',b)
            row['fix_id'],row['quest_id'] = a['fix_id'],qid
            floor_replacements[a['fix_id']] = row
    effective['MasterDataQuestFloor'] = [floor_replacements.get(r['fix_id'],copy.deepcopy(r)) for r in original['MasterDataQuestFloor']]
    area_ids = set(oi['MasterDataArea'])
    effective['MasterDataAreaAmend'] = [project('MasterDataAreaAmend',r) for r in tables['MasterDataAreaAmend'] if r['area_id'] in area_ids]
    chars = set(index(original['MasterDataParamChara']))
    group_rows, enemy_rows = {}, {}
    def supported_group(gid,seen=None):
        if not gid:return
        seen = set() if seen is None else seen
        if gid in seen:raise ValueError('Cyclic group '+str(gid))
        seen.add(gid)
        g = bi['MasterDataEnemyGroup'][gid]
        group_rows[gid] = project('MasterDataEnemyGroup',g)
        for i in range(1,8):
            eid = g['enemy_id_'+str(i)]
            if not eid:continue
            e = bi['MasterDataEnemyChara'][eid]
            if e['chara_id'] not in chars or (e['drop_unit_id'] and e['drop_unit_id'] not in chars):
                raise ValueError('New character/drop required for enemy '+str(eid))
            if any(v for k,v in e.items() if k.startswith('act_')):
                raise ValueError('New AI required for enemy '+str(eid))
            enemy_rows[eid] = project('MasterDataEnemyChara',e)
        supported_group(g['chain_id'],seen)
    # All existing floor encounters must be fully compatible; never silently
    # degrade a Boss or substitute its character to make validation pass.
    for f in floor_replacements.values():
        supported_group(f['boss_group_id'])
        for i in range(1,8):supported_group(f['enemy_group_id_'+str(i)])
    skipped_guerrilla = []
    effective['MasterDataGuerrillaBoss'] = []
    for source in tables['MasterDataGuerrillaBoss']:
        if source['quest_id'] not in reverse:continue
        row = project('MasterDataGuerrillaBoss',source)
        row['quest_id'] = reverse[source['quest_id']]
        if row['active']==2:
            previous_groups,previous_enemies = dict(group_rows),dict(enemy_rows)
            try:
                supported_group(row['enemy_group_id'])
            except (KeyError,ValueError) as ex:
                group_rows,enemy_rows = previous_groups,previous_enemies
                row['active'] = 1
                skipped_guerrilla.append({'id':row['fix_id'],'quest_id':row['quest_id'],'enemy_group_id':row['enemy_group_id'],'reason':str(ex)})
        effective['MasterDataGuerrillaBoss'].append(row)
    for name,extra in [('MasterDataEnemyGroup',group_rows),('MasterDataEnemyChara',enemy_rows)]:
        ids = {r['fix_id'] for r in effective[name]}
        effective[name] += [extra[i] for i in sorted(extra) if i not in ids]
    # Validate exact 1.0.6 JSON schemas, stable quest IDs, contiguous floors and
    # all active encounter/panel references before producing compiled data.
    from audit_stage_differences import Dataset
    check = Dataset({**original,**effective})
    for qid in mapping:
        snap = check.snapshot(qid)
        assert snap is not None
        assert len(check.floors(qid))==len(original_floors[qid])
    assert not check.missing, check.missing
    for name,rows in effective.items():
        assert len({r['fix_id'] for r in rows})==len(rows),name
        # Original inactive floor placeholders omit question_ct (the original
        # class defaults it to zero). Retained rows may keep that omission.
        assert all(set(r)<=schemas[name] for r in rows),name
    assert check.get('MasterDataEnemyGroup',100270)['chain_id']==0
    assert check.get('MasterDataEnemyGroup',100170)['chain_id']==0
    expected_folder = report_folder/'stage-tables'
    expected_folder.mkdir()
    generated = ['internal static class BackupStageData {','public const bool Enabled=true;',
                 'public static string Table(int index) { switch(index) {']
    stats = {}
    for i,(name,_,_) in enumerate(TABLE_FIELDS):
        text = json.dumps(effective[name],ensure_ascii=False,separators=(',',':'))
        (expected_folder/(name+'.json')).write_text(text)
        generated.append('case '+str(i)+': return '+json.dumps(text,ensure_ascii=True)+';')
        stats[name] = {'rows':len(effective[name]),'sha256':sha(text.encode()),
                       'changed_existing_rows':sum(r!=oi[name].get(r['fix_id']) for r in effective[name])}
    generated += ['default: return null; } }','}']
    (output/'BackupStageData.cs').write_text('\n'.join(generated))
    report = {'source':sources,'scope':'All 371 existing quests, backup values projected to original 1.0.6 schema; 28 newer quests deferred.',
              'quests':371,'active_quests':sum(q['active']==2 for q in effective['MasterDataQuest']),
              'quest_id_mapping':[{'local_id':a,'backup_id':b} for a,b in mapping.items() if a!=b],
              'tables':stats,'legacy_snapshot_fallbacks':fallbacks,'disabled_unsupported_guerrilla_rules':skipped_guerrilla,
              'new_quests_not_imported':sorted(set(bq)-set(reverse)),
              'schedules':'Backup historical area bonuses and compatible guerrilla rules retained with their original dates, not forced active. Existing permanent offline event gates retained.',
              'save_policy':'No save fields rewritten; existing quest IDs and clear flags retained. Restart an in-progress quest to receive a fully new encounter snapshot.',
              'schema_policy':'Only original 1.0.6 fields; player characters, skills, gacha, economy and global event tables unchanged.',
              'source_hashes_unchanged':True,'android_gameplay_verified':False}
    for r in sources:assert sha(Path(r['path']).read_bytes())==r['sha256']
    (report_folder/'backup-stage-import.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    return report
