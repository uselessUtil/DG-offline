"""Build the isolated 2.9.1 offline gameplay candidate; Docker only."""
import argparse
import hashlib
import json
import shutil
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path
import UnityPy
from UnityPy.files.SerializedFile import SerializedFile
ROOT=Path('/workspace')
CODE=ROOT/'offline/v291/playable'
sys.path.insert(0,str(ROOT/'offline'))
SHA='3832bed7dee0530ff821c141622bc518d335dcab9ee664caa321425490968136'
SOURCE=ROOT/'jp.gungho.dg-291.apk'
CECIL='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
from manifest import patch as patch_manifest
from native import create as native_create
from assets import create as image_create
from save_transfer_android import create as create_save_android
from save_backup import create as create_save_sources
from donor import read as read_donor, SHA106, TABLES as DONOR_TABLES
MANAGED='assets/bin/Data/Managed/'
PACKAGE='jp.gungho.dg.offline291'
def digest(raw):return hashlib.sha256(raw).hexdigest()
def run(*args,**kwargs):
    print('+',' '.join(map(str,args)),flush=True)
    return subprocess.run(list(map(str,args)),check=True,**kwargs)
def checked_replace(text,old,new):
    if text.count(old)!=1:raise ValueError('Shared runtime contract changed: '+old[:50])
    return text.replace(old,new)
def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--revision',type=int,default=1)
    parser.add_argument('--output',type=Path,default=ROOT/'artifacts/2.9.1-offline-playable1')
    parser.add_argument('--resume-work',type=Path,help='Resume an unfinished build before its APK exists')
    parser.add_argument('--assets-from',type=Path,help='Reuse immutable verified artwork from a previous build-report.json')
    args=parser.parse_args();assert args.revision>0;release='2.9.1 offline gameplay dgdb-1 r'+str(args.revision);assert digest(SOURCE.read_bytes())==SHA
    output=args.output.resolve();output.mkdir(parents=True,exist_ok=bool(args.resume_work))
    assert not (output/'ディバゲ_2.9.1.apk').exists(),'Never overwrite an APK'
    work=args.resume_work.resolve() if args.resume_work else Path(tempfile.mkdtemp(prefix='dg291-playable-',dir='/tmp'))
    assert not work.is_relative_to(ROOT)
    if args.resume_work:assert (output/'work.txt').read_text().strip()==str(work)
    (output/'work.txt').write_text(str(work)+'\n')
    original=work/'original';build=work/'build';tables=work/'tables';build.mkdir(exist_ok=bool(args.resume_work));tables.mkdir(exist_ok=bool(args.resume_work))
    with zipfile.ZipFile(SOURCE) as z:
        assert z.testzip() is None
        for n in z.namelist():
            assert not n.startswith('/') and '..' not in Path(n).parts
            if n.startswith('assets/bin/Data/') and not n.endswith('/'):z.extract(n,original)
        manifest=z.read('AndroidManifest.xml')
    managed=original/MANAGED
    run('mcs','-r:'+CECIL,'-out:'+str(work/'Inspect.exe'),CODE/'Inspect.cs')
    inventory=subprocess.check_output(['mono',str(work/'Inspect.exe'),str(managed/'Assembly-CSharp.dll')],text=True)
    (output/'original-schema.tsv').write_text(inventory)
    contracts={}
    for line in inventory.splitlines():
        row=line.split('\t')
        if row[0]=='table':contracts[row[1]]=row[2]
    backup=ROOT/'jp.gungho.dg'
    baseline=json.loads((ROOT/'artifacts/cache-backup-audit-2026-10-22/source-files.json').read_text())
    for row in baseline:
        assert digest((backup/row['path']).read_bytes())==row['sha256'],'Backup changed: '+row['path']
    old=SerializedFile.set_version;SerializedFile.set_version=lambda self,v:old(self,v.splitlines()[0])
    source_tables={}
    try:
        for path in sorted(backup.rglob('CAB-*')):
            for o in UnityPy.load(path.read_bytes()).objects:
                if o.type.name!='TextAsset':continue
                value=o.read()
                if value.m_Name.startswith('MasterData'):
                    raw=value.m_Script.encode('utf8','surrogateescape') if isinstance(value.m_Script,str) else value.m_Script
                    source_tables[value.m_Name]=raw
    finally:SerializedFile.set_version=old
    assert len(source_tables)==31
    donor_apk=ROOT/'ディバゲ_1.0.6.apk'
    for name,raw in read_donor(donor_apk).items():
        assert name not in source_tables
        source_tables[name]=raw
    from data import convert
    legacy_chars=json.loads(source_tables['MasterDataParamChara'],strict=False)
    legacy_as={r['fix_id']:r for r in json.loads(source_tables['MasterDataSkillLimitBreak'],strict=False)}
    original_max={r['fix_id']:legacy_as[r['skill_limitbreak']]['level_max'] if r['skill_limitbreak'] else 0 for r in legacy_chars if r['fix_id']}
    (build/'MigrationCatalog291.cs').write_text('using System.Collections.Generic; public static partial class DivineGateOffline { static readonly Dictionary<uint,uint> OldSkillMaximum291=new Dictionary<uint,uint>{'+','.join('{'+str(i)+'u,'+str(v)+'u}' for i,v in original_max.items())+'}; }')
    source_tables,conversions=convert(source_tables,inventory)
    from dgdb import LAST_AUDIT
    (output/'dgdb-projection.json').write_text(json.dumps(LAST_AUDIT,ensure_ascii=False,indent=2))
    (output/'skill-changes.json').write_text(json.dumps(LAST_AUDIT['skill_changes'],ensure_ascii=False,indent=2))
    (output/'excluded-evolutions.json').write_text(json.dumps(LAST_AUDIT['excluded_evolutions'],ensure_ascii=False,indent=2))
    (output/'fallbacks.json').write_text(json.dumps(LAST_AUDIT['fallbacks'],ensure_ascii=False,indent=2))
    (output/'leader-guard-conversions.json').write_text(json.dumps(LAST_AUDIT['native_guard_conversions'],ensure_ascii=False,indent=2)+chr(10))
    # New 2.9.1 downloadable text keys have no counterpart in the old backup.
    # Keep the APK's resident TextDatabase; supplement only documented missing keys.
    text_fallback=json.loads((CODE/'text-fallback.json').read_text())
    assert 'MasterDataTextDefinition' not in source_tables
    text_rows=[{'fix_id':i+1,'text_key':key,'text':value} for i,(key,value) in enumerate(text_fallback.items())]
    text_raw=json.dumps(text_rows,ensure_ascii=True,separators=(',',':')).encode()
    source_tables['MasterDataTextDefinition']=text_raw
    conversions.append({'table':'MasterDataTextDefinition','rows':len(text_rows),
        'mapping':'Authored Japanese fallback for missing code and serialized-scene text keys; not recovered original text.',
        'keys':list(text_fallback),'output_sha256':digest(text_raw)})
    (output/'data-conversions.json').write_text(json.dumps(conversions,ensure_ascii=False,indent=2)+'\n')
    payloads=[]
    lines=['using LitJson;','internal static class DG291Data {',
        'static byte[] Payload(int index){return null;}',
        'static string Read(int index){var raw=Payload(index);if(raw==null)throw new System.InvalidOperationException("Missing embedded master data");return System.Text.Encoding.UTF8.GetString(raw);}',
        'public static void Apply(ServerForgery server){']
    imported={};disabled=[]
    aliases={'MasterDataParamEnemy':'MasterDataEnemyChara','MasterDataStatusAilmentParam':'MasterDataStatusAilment'}
    for field,typ in contracts.items():
        name=aliases.get(typ,typ)
        if name in source_tables:
            rows=json.loads(source_tables[name],strict=False);assert isinstance(rows,list)
            raw=json.dumps(rows,ensure_ascii=True,separators=(',',':'))
            index=len(payloads);payloads.append(raw.encode('utf8'))
            lines.append('server.'+field+'=JsonMapper.ToObject<'+typ+'[]>(Read('+str(index)+'));')
            (tables/(name+'.json')).write_bytes(source_tables[name])
            imported[name]={'rows':len(rows),'sha256':digest(source_tables[name]),'field':field,'type':typ}
        else:
            lines.append('server.'+field+'=new '+typ+'[0];');disabled.append(name)
    assert len(imported)>=34 and imported['MasterDataParamChara']['rows']==596 and imported['MasterDataQuest']['rows']==399
    assert imported['MasterDataGacha']['rows']==7
    characters={r['fix_id']:r for r in json.loads((tables/'MasterDataParamChara.json').read_text(),strict=False)}
    for row in json.loads((tables/'MasterDataGachaAssign.json').read_text(),strict=False):
        assert row['assign_chara_id'] in characters, 'Missing gacha character '+str(row)
    for id in [335,337,339,341,343,345,418,420,422,424,426,428,610,612,614,616,618,620]:
        assert characters[id]['rare']==4

    lines+=['}','public static void Share(ServerForgery server,MasterDataManager master){']
    env=UnityPy.load(str(original/'assets/bin/Data'))
    script_hashes={o.read().m_ClassName:o.read().m_PropertiesHash for o in env.objects if o.type.name=='MonoScript'}
    master_fields={}
    schemas={}
    for row in (line.split('\t') for line in inventory.splitlines()):
        if row[0]=='masterfield':master_fields[row[1]]=row[2]
    for field,typ in master_fields.items():
        if field in contracts and contracts[field]==typ:
            lines.append('master.'+field+'=server.'+field+';')
        elif field=='m_MasterDataEvent':
            lines.append('master.m_MasterDataEvent=new MasterDataEvent[server.m_MasterDataEvent.Length];')
            lines.append('for(int i=0;i<server.m_MasterDataEvent.Length;i++){var a=server.m_MasterDataEvent[i];master.m_MasterDataEvent[i]=new MasterDataEvent{fix_id=a.fix_id,active=a.active,timing_start=a.timing_start,timing_end=a.timing_end,event_id=a.event_id,user_group=a.user_group};}')
        elif field=='m_MasterDataAchievementConverted':lines.append('master.'+field+'=new MasterDataAchievementConverted[0];')
        elif field=='m_MasterDataNotification':lines.append('master.'+field+'=new MasterDataNotification[0];')
        else:raise ValueError('Unmapped target master field '+field+' / '+typ)
    lines+=['}','}'];(build/'DG291Data.cs').write_text('\n'.join(lines))
    offsets=[];offset=0
    with (build/'master-data.bin').open('wb') as stream:
        for i,raw in enumerate(payloads):
            stream.write(raw);offsets.append(str(i)+'\t'+str(offset)+'\t'+str(len(raw)));offset+=len(raw)
    (build/'master-data-offsets.tsv').write_text('\n'.join(offsets)+'\n')
    data_report={'source':'jp.gungho.dg content whitelist + locked DGDB gameplay + original 1.0.6 scratch tables','imported':imported,'disabled_missing_tables':disabled,'backup_files':baseline,
        'donor':{'source':str(donor_apk),'sha256':SHA106,'tables':DONOR_TABLES,'normalization':'Seven source pools retained; version-specific 22r6 gameplay rules'}}
    (output/'master-data.json').write_text(json.dumps(data_report,ensure_ascii=False,indent=2)+'\n')
    # Record exceptional DGDB level metadata without rewriting master values.
    drop_levels=[]
    for enemy in json.loads((tables/'MasterDataEnemyChara.json').read_text()):
        if not enemy['drop_unit_id']:continue
        unit=characters[enemy['drop_unit_id']]
        raw=enemy['drop_unit_level']
        normalized=max(unit['level_min'],min(unit['level_max'],raw))
        if raw!=normalized:
            drop_levels.append({'enemy_id':enemy['fix_id'],'unit_id':unit['fix_id'],
                'unit_name':unit['name'],'source_level':raw,'effective_level':normalized,
                'unit_level_min':unit['level_min'],'unit_level_max':unit['level_max'],
                'drop_rate_per10000':enemy['drop_unit_rate']})
    (output/'drop-level-normalizations.json').write_text(json.dumps(drop_levels,ensure_ascii=False,indent=2)+chr(10))
    from invasions import audit as audit_invasions
    invasion_report=audit_invasions(tables,output)
    text=(ROOT/'offline/Runtime.cs').read_text()
    start=text.index('    public static void Ready(');end=text.index('    private static void OpenLocalEvents',start)
    text=text[:start]+text[end:]
    text=checked_replace(text,'1.0.6 offline prototype 22r7',release)
    text=text.replace('        patch.m_PatchUpdateRequestText = false;\n','').replace('        patch.m_PatchUpdateRequestBanner = false;\n','')
    text=text.replace('1.0.6','2.9.1').replace('offline106','offline291').replace('prototype 22r7','2.9.1 dgdb-1 r1')
    text=text.replace('private static bool showNotice = true;', 'private static bool showNotice = false;')
    text=text.replace('store.m_StoreRestoreOK = true;', '')
    (build/'Runtime.cs').write_text(text)
    # Clearly labelled procedural fallback remains available for absent resources.
    from generate_images import create as placeholders
    placeholders(build/'Images.cs',tables)
    if args.assets_from:
        cached_report=json.loads(args.assets_from.read_text())
        assert cached_report['source_sha256']==SHA
        cached_data=json.loads((args.assets_from.parent/'master-data.json').read_text())
        # Category counts, text and native attack-buff execution fields do not affect prefab routes.
        # Check every existing skill/status value before permitting asset reuse.
        core=lambda rows:{k:v for k,v in rows.items() if k not in DONOR_TABLES and k not in {'MasterDataCategoryPattern','MasterDataTextDefinition','MasterDataSkillLimitBreak','MasterDataStatusAilment','MasterDataSkillLeader'}}
        assert core(cached_data['imported'])==core(imported) and cached_data['backup_files']==baseline
        cached=Path(cached_report['work_directory'])/'build'
        for table in ['MasterDataSkillLimitBreak','MasterDataStatusAilment']:
            prior=json.loads((cached.parent/'tables'/(table+'.json')).read_text(),strict=False)
            current={row['fix_id']:row for row in json.loads((tables/(table+'.json')).read_text(),strict=False)}
            assert all(row['fix_id'] in current and all(current[row['fix_id']].get(k)==v for k,v in row.items()) for row in prior), 'Changed existing cached skill/status value'
        # Leader guard flags affect combat only. Require every other field,
        # ID, description and chained-skill reference to be identical.
        from dgdb import native_guard_fields
        prior_leaders=json.loads((cached.parent/'tables/MasterDataSkillLeader.json').read_text())
        current_leaders={r['fix_id']:r for r in json.loads((tables/'MasterDataSkillLeader.json').read_text())}
        assert {r['fix_id'] for r in prior_leaders}==set(current_leaders)
        for prior in prior_leaders:
            expected=dict(prior);expected.update(native_guard_fields(prior))
            assert current_leaders[prior['fix_id']]==expected, 'Non-guard cached leader skill changed'
        for name in ['RecoveredImages.cs','RecoveredImages.bin','image-offsets.tsv','images.json','NativeAssetIndex.cs','native-bundle-manifest.json']:
            shutil.copy2(cached/name,build/name)
        if not (args.resume_work and (build/'native-bundles').exists()):
            shutil.copytree(cached/'native-bundles',build/'native-bundles',copy_function=__import__('os').link)
        images=json.loads((build/'images.json').read_text())
        native=json.loads((build/'native-bundle-manifest.json').read_text())
        if not all(b.get('target_engine')=='4.6.5p1' and b.get('conversion_revision')==2 for b in native['bundles']):
            # Rebuild into a separate directory: cached bundles are hard links.
            rebuilt=build/'converted-native'
            native=native_create(backup,rebuilt,tables,expected_characters=595,expected_areas=106,script_hashes=script_hashes)
            for n in ['NativeAssetIndex.cs','native-bundle-manifest.json']:
                shutil.copy2(rebuilt/n,build/n)
            # Replace links, never mutate the previous build's artifacts.
            for old_link in (build/'native-bundles').iterdir():old_link.unlink()
            for new_file in (rebuilt/'native-bundles').iterdir():shutil.copy2(new_file,build/'native-bundles'/new_file.name)
    else:
        images=json.loads((build/'images.json').read_text()) if args.resume_work and (build/'images.json').exists() else image_create(backup,tables,build)
        if args.resume_work and (build/'native-bundle-manifest.json').exists():
            native=json.loads((build/'native-bundle-manifest.json').read_text())
            assert all(b.get('conversion_revision')==2 for b in native['bundles'])
        else:
            native=native_create(backup,build,tables,expected_characters=595,expected_areas=106,script_hashes=script_hashes)
    assert native['characters']==595 and native['areas']==106
    shutil.copy2(build/'images.json',output/'images.json')
    for row in native['bundles']:assert digest((build/'native-bundles'/row['file']).read_bytes())==row['sha256']
    shutil.copy2(build/'native-bundle-manifest.json',output/'native-bundle-manifest.json')
    api=build/'compile-api';api.mkdir(exist_ok=True)
    run('mcs','-r:'+CECIL,'-out:'+str(build/'CompileApi.exe'),CODE/'CompileApi.cs')
    run('mono',build/'CompileApi.exe',managed/'Assembly-CSharp.dll',api/'Assembly-CSharp.dll')
    refs=['-r:'+str(managed/n) for n in ['mscorlib.dll','System.dll','System.Core.dll','UnityEngine.dll','Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll']]
    refs=[r.replace(str(managed/'Assembly-CSharp.dll'),str(api/'Assembly-CSharp.dll')) for r in refs]
    save_feature=create_save_sources(build)
    adapter=build/'DivineGateOffline.dll'
    run('mcs','-nostdlib','-target:library','-out:'+str(adapter),*refs,build/'Runtime.cs',CODE/'Runtime291.cs',CODE/'Skills291.cs',*[CODE/(n+'291.cs') for n in ['AccountRules','AcquisitionOrder','WarehouseSupport','FreeChipShop','Commerce','Gacha','LegacyFloor','TutorialSkip','EvolutionPage','RestoreRetirement','QuestRewards']],ROOT/'offline/NativeAssets.cs',build/'SaveBackup291.cs',build/'SaveTransferUI291.cs',CODE/'LegacySave106.cs',CODE/'Legacy106Account.cs',build/'Legacy106Catalog.cs',CODE/'SaveMigration291.cs',build/'MigrationCatalog291.cs',build/'DG291Data.cs',build/'Images.cs',build/'RecoveredImages.cs',build/'NativeAssetIndex.cs')
    run('mcs','-r:'+CECIL,'-out:'+str(build/'Embed.exe'),ROOT/'offline/EmbedImages.cs')
    run('mono',build/'Embed.exe',adapter,managed/'mscorlib.dll',build/'RecoveredImages.bin',build/'image-offsets.tsv',build/'WithImages.dll')
    shutil.move(build/'WithImages.dll',adapter)
    run('mono',build/'Embed.exe',adapter,managed/'mscorlib.dll',build/'master-data.bin',build/'master-data-offsets.tsv',build/'WithData.dll','DG291Data')
    shutil.move(build/'WithData.dll',adapter)
    run('mcs','-r:'+CECIL,'-out:'+str(build/'Patch.exe'),CODE/'Patch.cs',CODE/'PatchRules291.cs',CODE/'PatchCommerce291.cs',CODE/'PatchGacha291.cs',CODE/'PatchQuest291.cs',CODE/'PatchTutorial291.cs',CODE/'PatchEvolution291.cs',CODE/'PatchSave291.cs',CODE/'PatchRestore291.cs',CODE/'PatchRewards291.cs')
    run('mono',build/'Patch.exe',managed/'Assembly-CSharp.dll',adapter,build/'Assembly-CSharp.dll',output/'adapter-hooks.txt')
    main=UnityPy.load(str(original/'assets/bin/Data/mainData'))
    manager=next(o for o in main.objects if o.type.name=='MonoManager')
    before={o.path_id:o.get_raw_data() for o in manager.assets_file.objects.values()}
    tree=manager.read_typetree();assert 'DivineGateOffline.dll' not in tree['m_AssemblyNames']
    tree['m_AssemblyNames'].append('DivineGateOffline.dll');manager.save_typetree(tree);main_raw=manager.assets_file.save()
    after={o.path_id:o.get_raw_data() for o in UnityPy.load(main_raw).objects}
    assert set(before)==set(after) and all(after[k]==v for k,v in before.items() if k!=manager.path_id)
    new_manifest,removed=patch_manifest(manifest,package=PACKAGE,label='Divine Gate Offline 2.9.1',save_transfer=True,save_activity=PACKAGE+'.SaveTransferActivity',
        extra_permissions=['android.permission.WRITE_EXTERNAL_STORAGE','android.permission.READ_EXTERNAL_STORAGE','android.permission.READ_PHONE_STATE'],
        extra_components=['it.partytrack.sdk.ReferrerReceiver','com.CFM.ELAN.ELANAlarmCreator','com.CFM.ELAN.TimedAlarm','com.google.games.bridge.NativeBridgeActivity'])
    with zipfile.ZipFile(SOURCE) as original_apk:
        save_dex,save_android=create_save_android(original_apk.read('classes.dex'),build,package=PACKAGE,game_version='2.9.1')
    save_feature.update(save_android)
    replacements={'classes.dex':save_dex,'AndroidManifest.xml':new_manifest,'assets/bin/Data/mainData':main_raw,MANAGED+'Assembly-CSharp.dll':(build/'Assembly-CSharp.dll').read_bytes(),MANAGED+'DivineGateOffline.dll':adapter.read_bytes()}
    for row in native['bundles']:replacements['assets/dg-native/'+row['file']]=(build/'native-bundles'/row['file']).read_bytes()
    expected=set(replacements)
    with zipfile.ZipFile(SOURCE) as src,zipfile.ZipFile(build/'unsigned.apk','w') as dst:
        for info in src.infolist():
            if info.filename.upper().startswith('META-INF/'):continue
            dst.writestr(info,replacements.pop(info.filename,src.read(info.filename)))
        for name,data in replacements.items():dst.writestr(name,data,compress_type=zipfile.ZIP_DEFLATED)
    run('zipalign','4',build/'unsigned.apk',build/'aligned.apk')
    key=Path('/var/lib/divine-gate-offline/playable290.p12')
    if not key.exists():
        raise RuntimeError('Expected existing offline signing key')
        key.parent.mkdir(parents=True,exist_ok=True)
        run('keytool','-genkeypair','-keystore',key,'-storetype','PKCS12','-storepass','android','-keypass','android','-alias','offline291','-keyalg','RSA','-keysize','2048','-validity','10000','-dname','CN=Divine Gate Local Offline 291')
        key.chmod(0o600)
    target=output/'ディバゲ_2.9.1.apk'
    run('apksigner','sign','--ks',key,'--ks-key-alias','offline290','--ks-pass','pass:android','--min-sdk-version','9','--v1-signing-enabled','true','--v2-signing-enabled','true','--v4-signing-enabled','false','--out',target,build/'aligned.apk')
    with zipfile.ZipFile(SOURCE) as src,zipfile.ZipFile(target) as dst:
        assert dst.testzip() is None
        changed={n for n in src.namelist() if not n.upper().startswith('META-INF/') and src.read(n)!=dst.read(n)}
        added={n for n in set(dst.namelist())-set(src.namelist()) if not n.upper().startswith('META-INF/')}
        assert changed|added==expected
        assert dst.read(MANAGED+'UnityEngine.dll')==src.read(MANAGED+'UnityEngine.dll')
    with (output/'signature.txt').open('w') as log:run('apksigner','verify','--verbose','--print-certs',target,stdout=log)
    run('zipalign','-c','4',target)
    badging=subprocess.check_output(['aapt','dump','badging',str(target)],text=True);(output/'manifest-summary.txt').write_text(badging)
    assert "package: name='"+PACKAGE+"'" in badging
    assert all('android.permission.'+x not in badging for x in ['INTERNET','READ_PHONE_STATE','WRITE_EXTERNAL_STORAGE','READ_EXTERNAL_STORAGE'])
    assert digest(SOURCE.read_bytes())==SHA
    for row in baseline:assert digest((backup/row['path']).read_bytes())==row['sha256']
    assert digest(donor_apk.read_bytes())==SHA106
    report={'version':release,'status':'Build complete; validation pending','source':str(SOURCE),'source_sha256':SHA,'donor_sha256':SHA106,'output':str(target),'sha256':digest(target.read_bytes()),'size':target.stat().st_size,'package':PACKAGE,'work_directory':str(work),'data_version':'dgdb-1','characters':595,'active_quests':392,'quest_rows':399,'native_characters':native['characters'],'native_areas':native['areas'],'missing_tables_disabled':disabled,'removed_manifest_items':removed,'original_modified_entries':sorted(changed),'added_entries':sorted(added),'android_tested':False,'save_transfer':save_feature}
    report['permanent_invasions']={k:v for k,v in invasion_report.items() if k!='entries'}
    report['restore_retirement']={'original_api':'SERVER_API_QUEST_RETIRE','orphan_repair':True,'journal':'DG291_QUEST_RETIRE_JOURNAL'}
    (output/'build-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    (output/'SHA256SUMS').write_text(report['sha256']+'  '+target.name+'\n')
    print(json.dumps(report,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
