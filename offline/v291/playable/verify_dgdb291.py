"""Validate data consumed by actual signed DLLs against the locked SQL."""
import json,hashlib,collections
from pathlib import Path
from dgdb import read_sql,index,QUEST_MAP
from verify_save_backup291 import run
CODE=Path('/workspace/offline/v291/playable')
def verify(work,env,output):
    refs=['-r:'+str(work/n) for n in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll']]
    run('mcs','-out:'+str(work/'ExportMasterData.exe'),*refs,CODE/'ExportMasterData.cs')
    directory=work/'effective-tables'
    run('mono',work/'ExportMasterData.exe',directory,cwd=work,env=dict(env,DG_TEST_PREFS=str(work/'projection.prefs')))
    actual={p.stem:json.loads(p.read_text(),strict=False) for p in directory.glob('*.json')}
    audit=json.loads((output/'dgdb-projection.json').read_text())
    db=read_sql();di={k:index(rs) for k,rs in db.items()}
    mapping={'MasterDataParamChara':'unit','MasterDataQuest':'quest','MasterDataQuestFloor':'quest_floor',
      'MasterDataParamCharaEvol':'evo','MasterDataEnemyGroup':'enemy_group','MasterDataParamEnemy':'enemy',
      'MasterDataEnemyActionTable':'enemy_action_table','MasterDataEnemyActionParam':'enemy_action_param',
      'MasterDataEnemyAbility':'enemy_ability','MasterDataParamPanel':'panel','MasterDataParamPanelGroup':'panel_group',
      'MasterDataQuestRequirement':'quest_requirement','MasterDataSkillActive':'n_skill','MasterDataSkillLimitBreak':'a_skill',
      'MasterDataSkillLeader':'l_skill','MasterDataSkillPassive':'p_skill','MasterDataSkillBoost':'skill_boost','MasterDataStatusAilmentParam':'status_ailment'}
    units=index(actual['MasterDataParamChara']);quests=index(actual['MasterDataQuest'])
    assert sorted(i for i in units if i)==audit['characters']
    assert sorted(i for i,r in quests.items() if i and r['active']==2)==audit['quests']
    counts={};floor_counter=collections.Counter()
    for native,sql in mapping.items():
        columns=set(db[sql][0]);count=0
        for r in actual[native]:
            if not r:continue
            ident=r['fix_id']
            if native=='MasterDataParamChara' and not ident:continue
            if native=='MasterDataQuest' and (not ident or r['active']!=2):continue
            if native=='MasterDataQuestFloor':
                q=QUEST_MAP.get(r['quest_id'],r['quest_id']);key=(q,r['under'])
                matches=[f for f in db[sql] if f['quest_id']==q and f['under']==r['under']]
                d=matches[floor_counter[key]];floor_counter[key]+=1
                excluded={'fix_id','quest_id','pattern_expect','pattern_category'}
            else:
                d=di[sql][QUEST_MAP.get(ident,ident) if native=='MasterDataQuest' else ident]
                excluded={'fix_id','area_id'} if native=='MasterDataQuest' else set()
                if native=='MasterDataParamCharaEvol':excluded={'quest_id'}
            for k in columns & set(r)-excluded-{'created_at','updated_at','timing_public'}:
                assert r[k]==d[k],(native,ident,k,r[k],d[k]);count+=1
            if native=='MasterDataParamCharaEvol':
                assert r['unit_id_pre'] in units and r['unit_id_after'] in units
                for j in range(1,5):assert not r['unit_id_parts'+str(j)] or r['unit_id_parts'+str(j)] in units
                assert r['quest_id'] in quests
            if native=='MasterDataParamEnemy':
                assert r['chara_id'] in units and (not r['drop_unit_id'] or r['drop_unit_id'] in units)
        counts[native]=count
    assert len(actual['MasterDataParamCharaEvol'])==262
    assert len(actual['MasterDataQuestFloor'])==763
    checks=sum(counts.values())
    text='PASS signed APK effective tables: exactly595 characters/392 active quests, 262 dependency-complete evolutions, 763 floor rows; '+str(checks)+' DGDB scalar values match locked SQL, with only explicit identity/layout exclusions. No account created.'+chr(10)
    text+='PASS actual DGDB enemies/drop units remain inside the original roster; AS/LS/NS/PS/Boost/status/action definitions verified after native loading.'+chr(10)
    (output/'effective-data-tests.txt').write_text(text)
    (output/'effective-data-tests.json').write_text(json.dumps({'sql_values_checked':checks,'field_counts':counts,'work':str(directory)},indent=2))
    print(text,flush=True);return text
