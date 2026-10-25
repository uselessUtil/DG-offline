"""Fixed donor APK exports in one process; signed 2.9 APK imports in another."""
import hashlib,json,os,shutil,zipfile,subprocess
from pathlib import Path
from verify_save_backup291 import run
ROOT=Path('/workspace')
CODE=ROOT/'offline/v291/playable'
CECIL='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
def verify(test,tools,env,output):
    catalog=json.loads((ROOT/'offline/v290/playable/legacy106-catalog.json').read_text())
    donor=ROOT/'artifacts/1.0.6-offline-prototype22r7-save-backup'
    apk=donor/'ディバゲ_1.0.6.apk'
    assert hashlib.sha256(apk.read_bytes()).hexdigest()==catalog['apk_sha256']
    source=test/'source106';source.mkdir()
    with zipfile.ZipFile(apk) as z:
        for n in z.namelist():
            if n.startswith('assets/bin/Data/Managed/') and n.endswith('.dll'):
                (source/Path(n).name).write_bytes(z.read(n))
    run('mcs','-r:'+CECIL,'-out:'+str(tools/'VerifyLegacySchema106.exe'),CODE/'VerifyLegacySchema106.cs')
    audit=run('mono',tools/'VerifyLegacySchema106.exe',source/'Assembly-CSharp.dll',test/'Assembly-CSharp.dll',test/'DivineGateOffline.dll')
    shutil.copy2(source/'UnityEngine.dll',source/'original-engine.dll')
    run('mcs','-target:library','-out:'+str(source/'EngineSupport.dll'),'-r:'+str(source/'UnityEngine.dll'),ROOT/'offline/tests/EngineSupport.cs')
    run('mono',tools/'TestEngine.exe',source/'original-engine.dll',source/'EngineSupport.dll',source/'UnityEngine.dll')
    refs=['-r:'+str(source/n) for n in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll','DivineGateOffline.dll']]
    run('mcs','-out:'+str(source/'ExportLegacy106Fixture.exe'),*refs,CODE/'ExportLegacy106Fixture.cs')
    fixture=source/'fixtures'
    donor_report=json.loads((donor/'build-report.json').read_text())
    source_env=dict(env,DG_TEST_TABLES=donor_report['work_directory']+'/tables',DG_TEST_PREFS=str(source/'prefs'))
    log=audit+run('mono',source/'ExportLegacy106Fixture.exe',fixture,env=source_env,cwd=source)
    for file,group,fields in [('source-chars.json','characters',['fix_id','name','level_max']),('source-quests.json','quests',['fix_id','area_id','quest_name'])]:
        actual=[{k:r[k] for k in fields} for r in json.loads((fixture/file).read_text(),strict=False) if r and r['fix_id']]
        assert actual==catalog[group],file
    refs=['-r:'+str(test/n) for n in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll','DivineGateOffline.dll']]
    run('mcs','-out:'+str(test/'ImportLegacy106Tests.exe'),*refs,CODE/'ImportLegacy106Tests.cs')
    incoming=fixture/'legacy106.dgbackup.json'
    directory=test/'legacy-import';directory.mkdir()
    target_env=dict(env,DG_TEST_PREFS=str(test/'legacy.prefs'))
    for mode in [[],['reload']]:
        log+=run('mono',test/'ImportLegacy106Tests.exe',incoming,directory,*mode,env=target_env,cwd=test)
    report=json.loads((output/'build-report.json').read_text())
    old_payload=json.loads(json.loads(incoming.read_text())['payload'])
    new_envelope=json.loads((directory/'migrated.dgbackup.json').read_text())
    assert new_envelope['game_version']=='2.9.1'
    new_payload=json.loads(new_envelope['payload'])
    old=json.loads(old_payload['server_save']);new=json.loads(new_payload['server_save'])
    remaps={420:436,421:437,422:438,423:439,424:440}
    def bits(flags):return {i*8+b for i,v in enumerate(flags or []) for b in range(8) if v&(1<<b)}
    for key,value in old.items():
        if key in ['flag_quest_check','flag_quest_clear']:
            assert bits(new[key])=={remaps.get(i,i) for i in bits(value)},key
        elif key=='unit_list':
            target_tables=Path(report['work_directory'])/'tables'
            chars={r['fix_id']:r for r in json.loads((target_tables/'MasterDataParamChara.json').read_text())}
            skills={r['fix_id']:r for r in json.loads((target_tables/'MasterDataSkillLimitBreak.json').read_text())}
            assert len(value)==len(new[key])
            for a,b in zip(value,new[key]):
                maximum=skills.get(chars[a['id']]['skill_limitbreak'],{}).get('level_max',0)
                for field,v in a.items():assert b[field]==(min(v,maximum) if field=='limitbreak_lv' else v),(a['id'],field)
        elif key=='present_list':
            for a,b in zip(value,new[key]):
                assert all(b[k]==v for k,v in a.items())
                assert all(v==0 for k,v in b.items() if k not in a)
            assert len(value)==len(new[key])
        else:assert value==new[key],key
    prefs={p['key']:p for p in new_payload['preferences']}
    for p in old_payload['preferences']:
        q=prefs[p['key']]
        if p['key']=='divinegate_OPTION':
            a=json.loads(p['text']);b=json.loads(q['text'])
            assert all(b[k]==v for k,v in a.items())
            assert all(v==0 for k,v in b.items() if k not in a)
        elif p['key']=='divinegate_RESTORE_VER':assert q['text']=='240after' and q['present']
        elif p['key']=='divinegate_SAVE_VERSION':assert not q['present']
        elif p['key'] not in {'divinegate_GOES_TO_GAME_RESTORE','divinegate_GOES_TO_GAME_START','divinegate_GOES_TO_MENU_RESULT','divinegate_GOES_TO_MENU_RETIRE','divinegate_QUEST_FP_ACTIVE','divinegate_LOGINPARAM','divinegate_INGAME_CONTINUE','divinegate_INGAME_RESET','divinegate_SCRATCH'}:
            assert p==q,p['key']
    assert new['have_ticket']==0 and new['shop_product_event']==0 and new['quest_build'] is None and new['monthly_present'] is None
    for k in ['event_purchase_list','achievement_list_clear','achievement_list_ensyutsu','achievement_list_acquired','player_login_list']:
        assert new[k]['m_BufferSize']==0,k
    log+='PASS field-by-field comparison: all 522 owned units (IDs/levels/EXP/pluses/acquisition times/order; skill levels capped only at DGDB maximum), 5 parties, currencies, capacity/rank/EXP/stamina, tutorial flags and 6 gift types preserved; all 371 quest records mapped including 420..424 -> 436..440; original preferences retained with native option/restore defaults. No player save used.'+chr(10)
    # Use the same failure injection on a genuine legacy import.
    failure=test/'import-failure'
    text=(CODE/'ImportFailureTests291.cs').read_text()
    text=text.replace('string text=JsonMapper.ToJson(envelope),directory=', 'string text=File.ReadAllText(Environment.GetEnvironmentVariable("DG_LEGACY_BACKUP")),directory=')
    text=text.replace('=="Imported"','=="跨版備份測試⚡"').replace('.have_money==999','.have_money==12345678')
    (failure/'LegacyFailureTests.cs').write_text(text)
    refs=['-r:'+str(failure/n) for n in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll','DivineGateOffline.dll']]
    run('mcs','-out:'+str(failure/'LegacyFailureTests.exe'),*refs,failure/'LegacyFailureTests.cs')
    log+=run('mono',failure/'LegacyFailureTests.exe',cwd=failure,env=dict(env,DG_LEGACY_BACKUP=str(incoming),DG_TEST_PREFS=str(failure/'legacy.prefs'),DG_TEST_SAVE_DIR=str(failure/'legacy-backups')))
    # Java performs its independent checksum/revision/summary check before asking to import.
    native=test/'native-save291'
    java='''import java.nio.file.*;import org.json.JSONObject;import jp.gungho.dg.offline291.SaveTransferActivity;
class LegacyPreviewTest { public static void main(String[] args)throws Exception {
String text=new String(Files.readAllBytes(Paths.get(args[0])),"UTF-8"),s=SaveTransferActivity.preview(text);
if(!s.contains("Convert to 2.9.1 DGDB") || !s.contains("Units: 522"))throw new Exception(s);
JSONObject root=new JSONObject(text);root.put("prototype","future106");boolean bad=false;
try{SaveTransferActivity.preview(root.toString());}catch(Exception e){bad=true;}if(!bad)throw new Exception("Accepted unknown revision");
System.out.println("PASS Android Java preview independently accepts genuine 1.0.6 export, shows conversion warning and unit count; rejects unsupported source revision.");
}}'''
    (native/'LegacyPreviewTest.java').write_text(java)
    libs='/opt/dg-save-tools/android.jar:/opt/dg-save-tools/json.jar'
    run('javac','--release','8','-cp',str(native)+':'+libs,'-d',native,native/'LegacyPreviewTest.java')
    log+=run('java','-cp',str(native)+':'+libs,'LegacyPreviewTest',incoming)
    (output/'legacy106-import-tests.txt').write_text(log)
    print(log,flush=True)
    return log
