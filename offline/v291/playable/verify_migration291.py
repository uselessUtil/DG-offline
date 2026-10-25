"""Actual r17 export and signed 2.9.1 import, isolated cross-process fixtures."""
import os,json,shutil,zipfile,hashlib
from pathlib import Path
from verify_save_backup291 import run
ROOT=Path('/workspace');CODE=ROOT/'offline/v291/playable'
def verify(test,tools,env,output):
    report=json.loads((output/'build-report.json').read_text())
    source_report=json.loads((ROOT/'artifacts/2.9.0-offline-playable1-r17-restore-retire/build-report.json').read_text())
    source_apk=Path(source_report['output']);assert hashlib.sha256(source_apk.read_bytes()).hexdigest()==source_report['sha256']
    source=test/'source290';source.mkdir()
    with zipfile.ZipFile(source_apk) as z:
        for n in z.namelist():
            if n.startswith('assets/bin/Data/Managed/') and n.endswith('.dll'):(source/Path(n).name).write_bytes(z.read(n))
    shutil.copy2(source/'UnityEngine.dll',source/'original-engine.dll')
    run('mcs','-target:library','-out:'+str(source/'EngineSupport.dll'),'-r:'+str(source/'UnityEngine.dll'),ROOT/'offline/tests/EngineSupport.cs')
    run('mono',tools/'TestEngine.exe',source/'original-engine.dll',source/'EngineSupport.dll',source/'UnityEngine.dll')
    # Compile the legal all-unit fixture against the actual old assemblies.
    fixture_source=(CODE/'ExportLegacy106Fixture.cs').read_text().replace('Singleton<MasterDataManager>.Instance=new MasterDataManager();','Singleton<TimeManager>.Instance=new TimeManager();Singleton<MasterDataManager>.Instance=new MasterDataManager();')
    (source/'Export290.cs').write_text(fixture_source)
    refs=['-r:'+str(source/n) for n in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll','DivineGateOffline.dll']]
    run('mcs','-out:'+str(source/'Export290.exe'),*refs,source/'Export290.cs')
    source_env=dict(env,DG_TEST_PREFS=str(source/'prefs'),DG_TEST_TABLES=source_report['work_directory']+'/tables')
    log=run('mono',source/'Export290.exe',source/'fixtures',env=source_env,cwd=source)
    incoming=source/'fixtures/legacy106.dgbackup.json'
    old_envelope=json.loads(incoming.read_text());assert old_envelope['game_version']=='2.9.0'
    directory=test/'migration290';target_env=dict(env,DG_TEST_PREFS=str(test/'migration290.prefs'))
    refs=['-r:'+str(test/n) for n in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll','DivineGateOffline.dll']]
    run('mcs','-out:'+str(test/'Migration290.exe'),*refs,CODE/'Migration290Tests291.cs')
    for mode in [[],['reload']]:log+=run('mono',test/'Migration290.exe',incoming,directory,*mode,cwd=test,env=target_env)
    new_envelope=json.loads((directory/'migrated.json').read_text());new_payload=json.loads(new_envelope['payload'])
    old=json.loads(json.loads(old_envelope['payload'])['server_save']);new=json.loads(new_payload['server_save'])
    assert len(old['unit_list'])==595
    for key,value in old.items():
        if key=='unit_list':
            for a,b in zip(value,new[key]):
                for f,v in a.items():assert b[f]==(5 if a['id']==148 and f=='limitbreak_lv' else v),(a['id'],f,v,b[f])
        else:assert value==new[key],key
    migration=json.loads(new_payload['migration_report'])
    assert migration['source_checksum']==old_envelope['sha256'] and migration['source_version']=='2.9.0'
    assert len(migration['skill_adjustments'])==1
    assert migration['skill_adjustments'][0]['character_id']==148 and migration['skill_adjustments'][0]['before']==15 and migration['skill_adjustments'][0]['after']==5
    log+='PASS field-by-field 595-unit 2.9.0 comparison: ONLY Shadow skill15->5 changes; identity/EXP/pluses/times/order/parties/currencies/gifts/capacity/rank/stamina/all quest and tutorial flags preserved. Source checksum and exact skill adjustment recorded.'+chr(10)
    refs=['-r:'+str(source/n) for n in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll','DivineGateOffline.dll']]
    run('mcs','-out:'+str(source/'RejectNewBackup.exe'),*refs,CODE/'RejectNewBackup.cs')
    log+=run('mono',source/'RejectNewBackup.exe',directory/'migrated.json',env=dict(source_env,DG_TEST_PREFS=str(source/'reject.prefs')),cwd=source)
    (output/'migration290-tests.txt').write_text(log);print(log,flush=True)
    return log
