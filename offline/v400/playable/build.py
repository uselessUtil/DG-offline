"""Build the isolated 4.0.0 offline gameplay candidate; Docker only."""
import argparse
import hashlib
import json
import re
import shutil
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path
import UnityPy
from UnityPy.files.SerializedFile import SerializedFile
ROOT=Path('/workspace')
CODE=ROOT/'offline/v400/playable'
sys.path.insert(0,str(ROOT/'offline'))
SHA='b3f2a366d553d0ab67f2c6fee76bde79dceb3a372f7ed64c3de4849fc1d9600e'
SOURCE=ROOT/'ディバゲ_4.0.0(紀念)-v20220207073837.apk'
CECIL='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
from manifest import patch as patch_manifest
from text_sources import supplemental_text

from assets import create as image_create
SHA106='ba3ccd42c28e4d9362cb6bda2b4d21483b98085441f9513ca1b825bc71be81c2'
DONOR_TABLES={}
MANAGED='assets/bin/Data/Managed/'
PACKAGE='jp.gungho.dg.offline400'
def digest(raw):return hashlib.sha256(raw).hexdigest()
def run(*args,**kwargs):
    print('+',' '.join(map(str,args)),flush=True)
    return subprocess.run(list(map(str,args)),check=True,**kwargs)
def checked_replace(text,old,new):
    if text.count(old)!=1:raise ValueError('Shared runtime contract changed: '+old[:50])
    return text.replace(old,new)
def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,default=ROOT/'artifacts/4.0.0-offline-playable1')
    parser.add_argument('--resume-work',type=Path,help='Resume an unfinished build before its APK exists')
    parser.add_argument('--assets-from',type=Path,help='Reuse immutable verified artwork from a previous build-report.json')
    args=parser.parse_args();assert digest(SOURCE.read_bytes())==SHA
    output=args.output.resolve();output.mkdir(parents=True,exist_ok=bool(args.resume_work))
    assert not (output/'ディバゲ_4.0.0.apk').exists(),'Never overwrite an APK'
    work=args.resume_work.resolve() if args.resume_work else Path(tempfile.mkdtemp(prefix='dg400-playable-',dir='/tmp'))
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
    recovered=json.loads(subprocess.check_output(['/opt/unity-tools/bin/python',str(CODE/'recover.py')],text=True))
    for name in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll']:
        shutil.copyfile(Path(recovered['output_dir'])/'managed'/name,managed/name)

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
    donor_apk=ROOT/'ディバゲ_1.0.6.apk'
    assert digest(donor_apk.read_bytes())==SHA106
    donor=work/'donor'
    with zipfile.ZipFile(donor_apk) as z:
        for name in z.namelist():
            if name.startswith('assets/bin/Data/') and not name.endswith('/'):
                z.extract(name,donor)
    source_tables={}
    for o in UnityPy.load(str(donor/'assets/bin/Data')).objects:
        if o.type.name=='TextAsset':
            value=o.read()
            if value.m_Name.startswith('MasterData'):
                raw=value.m_Script.encode('utf8','surrogateescape') if isinstance(value.m_Script,str) else value.m_Script
                source_tables[value.m_Name]=raw
    assert len(json.loads(source_tables['MasterDataParamChara'],strict=False))==523
    assert len(json.loads(source_tables['MasterDataQuest'],strict=False))==371
    from data import convert
    source_tables,conversions=convert(source_tables,inventory)
    # New 4.0.0 downloadable text keys have no counterpart in the old backup.
    # Keep resident TextDatabase; supplement missing keys and documented stale
    # format contracts (PARTY_NUM_RATE has two slots but 4.0.0 passes one value).
    text_fallback=supplemental_text(CODE/'text-fallback.json', 'v400', SOURCE)
    repairs=json.loads((CODE/'text-repairs.json').read_text())
    assert not (text_fallback.keys() & repairs.keys()),'Duplicate supplemental text key'
    text_fallback.update(repairs)
    complete=json.loads((CODE/'text-complete.json').read_text())
    assert not (text_fallback.keys() & complete.keys()),'Duplicate completed text key'
    text_fallback.update(complete)
    assert 'MasterDataTextDefinition' not in source_tables
    text_rows=[{'fix_id':i+1,'text_key':key,'text':value} for i,(key,value) in enumerate(text_fallback.items())]
    text_raw=json.dumps(text_rows,ensure_ascii=True,separators=(',',':')).encode()
    source_tables['MasterDataTextDefinition']=text_raw
    conversions.append({'table':'MasterDataTextDefinition','rows':len(text_rows),
        'mapping':'Authored fallback/format corrections plus three resident labels resolved from the source APK; see offline/SOURCE_PROVENANCE.md.',
        'resident_source_keys':[k for k,v in json.loads((CODE/'text-fallback.json').read_text()).items() if v is None],
        'keys':list(text_fallback),'output_sha256':digest(text_raw)})
    (output/'data-conversions.json').write_text(json.dumps(conversions,ensure_ascii=False,indent=2)+'\n')
    payloads=[]
    lines=['using LitJson;','internal static class DG400Data {',
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
    assert imported['MasterDataParamChara']['rows']==523 and imported['MasterDataQuest']['rows']==371
    assert imported['MasterDataGacha']['rows']==7
    characters={r['fix_id']:r for r in json.loads((tables/'MasterDataParamChara.json').read_text(),strict=False)}
    for row in json.loads((tables/'MasterDataGachaAssign.json').read_text(),strict=False):
        assert row['assign_chara_id'] in characters, 'Missing gacha character '+str(row)
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
        else:lines.append('master.'+field+'=new '+typ+'[0];')
    lines+=['}','}'];(build/'DG400Data.cs').write_text('\n'.join(lines))
    offsets=[];offset=0
    with (build/'master-data.bin').open('wb') as stream:
        for i,raw in enumerate(payloads):
            stream.write(raw);offsets.append(str(i)+'\t'+str(offset)+'\t'+str(len(raw)));offset+=len(raw)
    (build/'master-data-offsets.tsv').write_text('\n'.join(offsets)+'\n')
    data_report={'source':'original 1.0.6 embedded master tables; backup artwork only','imported':imported,'disabled_missing_tables':disabled,'backup_files':baseline,
        'donor':{'source':str(donor_apk),'sha256':SHA106,'tables':DONOR_TABLES,'normalization':'Seven source pools retained; projected onto the 4.0.0 schema'}}
    (output/'master-data.json').write_text(json.dumps(data_report,ensure_ascii=False,indent=2)+'\n')
    text=(ROOT/'offline/Runtime.cs').read_text()
    start=text.index('    public static void Ready(');end=text.index('    private static void OpenLocalEvents',start)
    text=text[:start]+text[end:]
    shared_build=re.search(r'public const string Build = "(1\.0\.6 offline prototype (22r\d+))";',text)
    assert shared_build,'Shared runtime build-label contract changed'
    text=checked_replace(text,shared_build.group(1),'4.0.0 offline gameplay candidate r16')
    text=text.replace('        patch.m_PatchUpdateRequestText = false;\n','').replace('        patch.m_PatchUpdateRequestBanner = false;\n','')
    text=text.replace('1.0.6','4.0.0').replace('offline106','offline400').replace('prototype '+shared_build.group(2),'4.0.0 candidate r16')
    text=text.replace('private static bool showNotice = true;', 'private static bool showNotice = false;')
    text=text.replace('store.m_StoreRestoreOK = true;', '').replace('TitleMenuSeqLoadAssetBundle','TitleMenuSeqLoadMasterAndAssetBundle')
    (build/'Runtime.cs').write_text(text)
    # Clearly labelled procedural fallback remains available for absent resources.
    from generate_images import create as placeholders
    placeholders(build/'Images.cs',tables)
    if args.assets_from:
        previous=json.loads(args.assets_from.read_text());assert previous['source_sha256']==SHA and previous['characters']==522
        cached=Path(previous['work_directory'])/'build'
        for name in ['RecoveredImages.cs','RecoveredImages.bin','image-offsets.tsv','images.json']:shutil.copyfile(cached/name,build/name)
    images=json.loads((build/'images.json').read_text()) if (build/'images.json').exists() else image_create(backup,tables,build)
    (build/'NativeAssetIndex.cs').write_text('internal static class NativeAssetIndex { public const bool Enabled=false; public static string Find(string kind,string name){return null;} }')
    from map_assets import create as restore_maps
    maps=restore_maps(backup,tables,build)
    shutil.copyfile(build/'map-restore.json',output/'map-restore.json')
    native={'characters':0,'areas':0,'bundles':[],'status':'No native CAB loading. Map meshes and animation curves rebuilt through Unity 5 APIs; character geometry remains simplified.'}
    shutil.copyfile(build/'images.json',output/'images.json')
    (output/'native-bundle-manifest.json').write_text(json.dumps(native,indent=2))
    runtime=build/'Runtime.cs'
    rt=runtime.read_text().replace('.renderer','.GetComponent<Renderer>()').replace('.animation','.GetComponent<Animation>()')
    rt=rt.replace('atlasTexture = new Texture2D(2, 2);\n        if (!atlasTexture.LoadImage(Convert.FromBase64String(OfflineImages.Atlas)))\n            throw new InvalidOperationException("Cannot decode offline atlas");','atlasTexture = DecodePng400(Convert.FromBase64String(OfflineImages.Atlas));')
    rt=rt.replace('if (!texture.LoadImage(Convert.FromBase64String(OfflineImages.Portraits[Number(name) % 1024])))\n            throw new InvalidOperationException("Cannot decode offline portrait");','texture = DecodePng400(Convert.FromBase64String(OfflineImages.Portraits[Number(name) % 1024]));')
    rt=rt.replace('if (!texture.LoadImage(data)) throw new InvalidOperationException("Cannot decode recovered " + key);','texture = DecodePng400(data);')
    rt=checked_replace(rt,'else asset = Map(bundle, name);','else asset = Map400(bundle, name);')
    runtime.write_text(rt)
    nt=(ROOT/'offline/NativeAssets.cs').read_text().replace('.Load(ticket.Name.ToLowerInvariant(),type)','.LoadAsset(ticket.Name.ToLowerInvariant(),type)')
    (build/'NativeAssets.cs').write_text(nt)
    api=build/'compile-api';api.mkdir(exist_ok=True)
    run('mcs','-r:'+CECIL,'-out:'+str(build/'CompileApi.exe'),CODE/'CompileApi.cs')
    run('mono',build/'CompileApi.exe',managed/'Assembly-CSharp.dll',api/'Assembly-CSharp.dll')
    refs=['-r:'+str(managed/n) for n in ['mscorlib.dll','System.dll','System.Core.dll','UnityEngine.dll','Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll']]
    refs=[r.replace(str(managed/'Assembly-CSharp.dll'),str(api/'Assembly-CSharp.dll')) for r in refs]
    adapter=build/'DivineGateOffline.dll'
    run('mcs','-nostdlib','-target:library','-out:'+str(adapter),*refs,build/'Runtime.cs',CODE/'Runtime400.cs',CODE/'Scratch400.cs',CODE/'Skills400.cs',CODE/'Png400.cs',CODE/'Maps400.cs',build/'MapData400.cs',build/'NativeAssets.cs',build/'DG400Data.cs',build/'Images.cs',build/'RecoveredImages.cs',build/'NativeAssetIndex.cs')
    run('mcs','-r:'+CECIL,'-out:'+str(build/'Embed.exe'),ROOT/'offline/EmbedImages.cs')
    run('mono',build/'Embed.exe',adapter,managed/'mscorlib.dll',build/'RecoveredImages.bin',build/'image-offsets.tsv',build/'WithImages.dll')
    shutil.move(build/'WithImages.dll',adapter)
    run('mono',build/'Embed.exe',adapter,managed/'mscorlib.dll',build/'master-data.bin',build/'master-data-offsets.tsv',build/'WithData.dll','DG400Data')
    shutil.move(build/'WithData.dll',adapter)
    run('mcs','-r:'+CECIL,'-out:'+str(build/'Patch.exe'),CODE/'Patch.cs')
    run('mono',build/'Patch.exe',managed/'Assembly-CSharp.dll',adapter,build/'Assembly-CSharp.dll',output/'adapter-hooks.txt')
    run(sys.executable,CODE/'verify_repairs.py',work,'--output',output/'repair-checks.json')
    run(sys.executable,CODE/'verify_quest_build.py',work,'--output',output/'quest-build-checks.txt')
    run(sys.executable,CODE/'verify_gacha.py',work,'--output',output/'gacha-menu-checks.txt')
    run(sys.executable,CODE/'verify_support.py',work,'--output',output/'support-setup-checks.txt')
    run(sys.executable,CODE/'verify_badges.py',work,'--output',output/'footer-badge-checks.txt')
    run(sys.executable,CODE/'verify_router.py',work,'--output',output/'startup-router-checks.txt')
    run(sys.executable,CODE/'verify_policy.py',work,'--output',output/'policy-gate-checks.txt')
    text_report=build/'text-build-report.json'
    text_report.write_text(json.dumps({'work_directory':str(work),'version':'4.0.0 offline106 r16'}))
    with (build/'text-audit.log').open('w') as audit_log:
        run(sys.executable,CODE/'audit_text.py','--report',text_report,'--output',output/'text-audit.json','--require-complete',stdout=audit_log)
    main=UnityPy.load(str(original/'assets/bin/Data/globalgamemanagers'))
    manager=next(o for o in main.objects if o.type.name=='MonoManager')
    before={o.path_id:o.get_raw_data() for o in manager.assets_file.objects.values()}
    tree=manager.read_typetree();assert 'DivineGateOffline.dll' not in tree['m_AssemblyNames']
    tree['m_AssemblyNames'].append('DivineGateOffline.dll');manager.save_typetree(tree);main_raw=manager.assets_file.save()
    after={o.path_id:o.get_raw_data() for o in UnityPy.load(main_raw).objects}
    assert set(before)==set(after) and all(after[k]==v for k,v in before.items() if k!=manager.path_id)
    new_manifest,removed=patch_manifest(manifest,package=PACKAGE,label='Divine Gate Offline 4.0.0',
        extra_permissions=['android.permission.WRITE_EXTERNAL_STORAGE','android.permission.READ_EXTERNAL_STORAGE','android.permission.READ_PHONE_STATE'],
        extra_components=['it.partytrack.sdk.ReferrerReceiver','com.CFM.ELAN.ELANAlarmCreator','com.CFM.ELAN.TimedAlarm','com.google.games.bridge.NativeBridgeActivity'])
    replacements={'AndroidManifest.xml':new_manifest,'assets/bin/Data/globalgamemanagers':main_raw,MANAGED+'Assembly-CSharp-firstpass.dll':(managed/'Assembly-CSharp-firstpass.dll').read_bytes(),MANAGED+'Assembly-CSharp.dll':(build/'Assembly-CSharp.dll').read_bytes(),MANAGED+'DivineGateOffline.dll':adapter.read_bytes()}
    from layout_assets import create as repair_layout
    replacements.update(repair_layout(original/'assets/bin/Data',build))
    shutil.copyfile(build/'layout-repairs.json',output/'layout-repairs.json')
    for row in native['bundles']:replacements['assets/dg-native/'+row['file']]=(build/'native-bundles'/row['file']).read_bytes()
    from loader import create as create_loader
    replacements.update(create_loader(SOURCE,build,CODE))
    shutil.copyfile(build/'advertising-id-checks.json',output/'advertising-id-checks.json')
    expected=set(replacements)
    with zipfile.ZipFile(SOURCE) as src,zipfile.ZipFile(build/'unsigned.apk','w') as dst:
        for info in src.infolist():
            if info.filename.upper().startswith('META-INF/'):continue
            dst.writestr(info,replacements.pop(info.filename,src.read(info.filename)))
        for name,data in replacements.items():dst.writestr(name,data,compress_type=zipfile.ZIP_DEFLATED)
    run('zipalign','4',build/'unsigned.apk',build/'aligned.apk')
    key=Path('/var/lib/divine-gate-offline/playable400.p12')
    if not key.exists():
        key.parent.mkdir(parents=True,exist_ok=True)
        run('keytool','-genkeypair','-keystore',key,'-storetype','PKCS12','-storepass','android','-keypass','android','-alias','offline400','-keyalg','RSA','-keysize','2048','-validity','10000','-dname','CN=Divine Gate Local Offline 400')
        key.chmod(0o600)
    target=output/'ディバゲ_4.0.0.apk'
    run('apksigner','sign','--ks',key,'--ks-key-alias','offline400','--ks-pass','pass:android','--min-sdk-version','9','--v1-signing-enabled','true','--v2-signing-enabled','true','--v4-signing-enabled','false','--out',target,build/'aligned.apk')
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
    report={'version':'4.0.0 offline / original 1.0.6 content / candidate r4','status':'Build complete; validation pending','source':str(SOURCE),'source_sha256':SHA,'donor_sha256':SHA106,'output':str(target),'sha256':digest(target.read_bytes()),'size':target.stat().st_size,'package':PACKAGE,'work_directory':str(work),'characters':522,'quest_rows':371,'native_characters':native['characters'],'native_areas':native['areas'],'missing_tables_disabled':disabled,'removed_manifest_items':removed,'original_modified_entries':sorted(changed),'added_entries':sorted(added),'android_tested':False}
    report.update(version='4.0.0 offline / original 1.0.6 content / candidate r16', status='Build and artifact checks complete; gameplay testing delegated to user', restored_map_resources=maps['resources'], map_geometry_aliases=maps['missing_geometry_aliases'], layout_repairs='layout-repairs.json', gacha_menu_checks='gacha-menu-checks.txt', support_setup_checks='support-setup-checks.txt', footer_badge_checks='footer-badge-checks.txt', startup_router_checks='startup-router-checks.txt', policy_gate_checks='policy-gate-checks.txt', advertising_id_checks='advertising-id-checks.json')
    (output/'build-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    (output/'SHA256SUMS').write_text(report['sha256']+'  '+target.name+'\n')
    print(json.dumps(report,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
