"""Run the stable signed-APK regressions with explicit expanded-profile expectations.
Only test copies in /tmp are adapted; production DLLs are extracted from the APK.
"""
import argparse, hashlib, json, subprocess, tempfile
from pathlib import Path
ROOT=Path('/workspace');BASE=ROOT/'offline/v291/playable'
def replace(text,old,new):
    assert text.count(old)==1,(old,text.count(old))
    return text.replace(old,new)
def main():
    p=argparse.ArgumentParser();p.add_argument('report',type=Path);a=p.parse_args()
    r=json.loads(a.report.read_text());assert r['data_version']=='dgdb-2-restored'
    assert (r['characters'],r['active_quests'],r['quest_rows'])==(1748,921,928)
    audit=json.loads((a.report.parent/'dgdb-projection.json').read_text())
    assert len(audit['characters'])==1748 and len(audit['quests'])==921
    assert len(audit['native_guard_conversions'])==25
    view=Path(tempfile.mkdtemp(prefix='dg291-restored-test-view-',dir='/tmp'))
    # Same regression bodies, only immutable roster/schema/package expectations differ.
    edits={
      'PlayableTests.cs':[
       ('Length == 596 && server.m_MasterDataQuest.Length == 399','Length == 1749 && server.m_MasterDataQuest.Length == 928'),
       ('Length==596 && all.result.master_array_quest.Length==399','Length==1749 && all.result.master_array_quest.Length==928'),
       ('characters==595,"All 595','characters==1748,"All 1748')],
      'NativeDgdbTests.cs':[('all==253 && damage>100','all==674 && damage>100')],
      'LeaderGuardTests291.cs':[
       ('expected.Count==23','expected.Count==25'),('PASS 76 owners / 23 LS','PASS 109 owners / 25 LS'),('owners==76 && seen.Count==23','owners==109 && seen.Count==25')],
      'DropLevelTests291.cs':[('cases==2144 && normalized==9 && levels.Count==10','cases==4206 && normalized==2068 && levels.Count==10')],
      'Migration290Tests291.cs':[
       ('envelope.schema==2 && envelope.data_version=="dgdb-1"','envelope.schema==3 && envelope.data_version=="dgdb-2-restored"'),
       ('schema2/data revision','schema3/data revision')],
      'verify_dgdb291.py':[
       ("==262","==717"),("==763","==1366"),
       ('exactly595 characters/392 active quests, 262 dependency-complete evolutions, 763 floor rows',
        'exactly1748 characters/921 active quests, 717 dependency-complete evolutions, 1366 floor rows'),
       ('original roster','restored roster'),('len(conversions)==23','len(conversions)==25')],
      'verify_save_backup291.py':[
       ("'jp.gungho.dg.offline291'","'jp.gungho.dg.offline291.restored'"),
       ("'jp.gungho.dg.offline291.SaveTransferActivity'","'jp.gungho.dg.offline291.restored.SaveTransferActivity'"),
       ("'Ljp/gungho/dg/offline291/SaveTransferActivity;'","'Ljp/gungho/dg/offline291/restored/SaveTransferActivity;'")],
      'UpdateLoopTests.cs':[('=="offline291-dgdb-1"','=="offline291-dgdb-2-restored"')],
      'verify_legacy106.py':[
       ('import jp.gungho.dg.offline291.SaveTransferActivity;','import jp.gungho.dg.offline291.restored.SaveTransferActivity;')]
    }
    for f in BASE.iterdir():
        if not f.is_file():continue
        if f.suffix not in ('.py','.cs'):
            (view/f.name).symlink_to(f);continue
        text=f.read_text()
        for old,new in edits.get(f.name,[]):
            if f.name=='UpdateLoopTests.cs':
                assert text.count(old)==2;text=text.replace(old,new)
            else:text=replace(text,old,new)
        # Test modules explicitly use their copied C# fixtures, not baseline constants.
        text=text.replace("CODE=ROOT/'offline/v291/playable'","CODE=Path("+repr(str(view))+")")
        text=text.replace("CODE=Path('/workspace/offline/v291/playable')","CODE=Path("+repr(str(view))+")")
        (view/f.name).write_text(text)
    (a.report.parent/'test-profile.json').write_text(json.dumps({
        'view':str(view),'profile':'dgdb-2-restored','expected_counts':{
        'units':1748,'active_quests':921,'evolutions':717,'floors':1366,
        'active_skills':674,'guard_skills':25,'guard_owners':109,
        'enemy_drop_cases':4206,'normalized_drop_levels':2068},
        'adapted_test_literals':edits},ensure_ascii=False,indent=2))
    subprocess.run(['/opt/unity-tools/bin/python',str(view/'test.py'),str(a.report.resolve())],check=True)
    for entry in ['test_keys.py','test_layout.py','test_update_loop.py']:
        subprocess.run(['/opt/unity-tools/bin/python',str(view/entry),str(a.report.resolve())],check=True)
if __name__=='__main__':main()
