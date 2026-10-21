"""Read-only audit of the supplied 1.1.0 APK; run only inside project Docker."""
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import zipfile
from datetime import datetime, timezone

import UnityPy

ROOT = Path('/workspace')
SOURCE = ROOT/'sczm/184044_sczm.apk'
SHA = '0d3e13a9e44f02c79f984e7f83bb187f6c1362a8b9002c1709b76278869f35fd'
OUT = ROOT/'artifacts/1.1.0-security-audit'
WORK = Path('/tmp/dg110-audit')
CECIL = '/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'


def run(*args):
    return subprocess.check_output(list(map(str, args)), text=True,
        env={**os.environ, 'LANG':'C.UTF-8', 'LC_ALL':'C.UTF-8'})


def main():
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == SHA
    OUT.mkdir(parents=True, exist_ok=True)
    WORK.mkdir(exist_ok=True)
    (OUT/'preflight.json').unlink(missing_ok=True)
    update = subprocess.run(['freshclam','--stdout'], capture_output=True, text=True)
    (OUT/'freshclam.txt').write_text(update.stdout + update.stderr)
    if update.returncode:
        raise RuntimeError('Virus database update failed; review freshclam.txt')
    signatures = {}
    for source in [SOURCE, ROOT/'ディバゲ_1.0.6.apk', ROOT/'ディバゲ_2.1.0.apk']:
        signatures[source.name] = run('apksigner','verify','--verbose','--print-certs',source)
    (OUT/'certificates.json').write_text(json.dumps(signatures, ensure_ascii=False, indent=2))
    cert = '9623d6804d17d3abe7741709ff7ce2bcb98b1fb0d0782c10dfc7d68569d00bbc'
    assert all('certificate SHA-256 digest: '+cert in v for v in signatures.values())
    for filename, args in [('manifest.txt',['aapt','dump','xmltree',SOURCE,'AndroidManifest.xml']),
                           ('badging.txt',['aapt','dump','badging',SOURCE])]:
        (OUT/filename).write_text(run(*args))
    with zipfile.ZipFile(SOURCE) as archive:
        names = archive.namelist()
        assert archive.testzip() is None
        assert len(names) == len(set(names))
        assert not any(n.startswith('/') or '\\' in n or '..' in Path(n).parts for n in names)
        archive.extractall(WORK/'original')
        (OUT/'file-inventory.json').write_text(json.dumps([
            {'name':i.filename,'size':i.file_size,'sha256':hashlib.sha256(archive.read(i)).hexdigest()}
            for i in archive.infolist()], indent=2))
    scan = subprocess.run(['clamscan','--infected','--recursive','--max-filesize=200M',
        '--max-scansize=1000M','--alert-exceeds-max=yes',str(SOURCE),str(WORK/'original')],
        capture_output=True,text=True)
    (OUT/'clamav.txt').write_text(scan.stdout+scan.stderr)
    if scan.returncode or 'Infected files: 0' not in scan.stdout or scan.stderr.strip():
        raise RuntimeError('Antivirus scan did not finish cleanly; inspect clamav.txt')
    env = UnityPy.load(str(WORK/'original/assets/bin/Data'))
    counts, versions, tables, texts = {}, set(), {}, []
    (WORK/'tables').mkdir(exist_ok=True)
    for obj in env.objects:
        versions.add(str(obj.assets_file.unity_version))
        counts[obj.type.name] = counts.get(obj.type.name,0)+1
        if obj.type.name != 'TextAsset': continue
        value = obj.read()
        texts.append({'name':value.m_Name,'file':obj.assets_file.name,'path_id':obj.path_id})
        if not value.m_Name.startswith('MasterData'): continue
        raw = value.m_Script
        if isinstance(raw,str): raw = raw.encode('utf8','surrogateescape')
        (WORK/'tables'/(value.m_Name+'.json')).write_bytes(raw)
        tables[value.m_Name] = len(json.loads(raw,strict=False))
    managed = WORK/'original/assets/bin/Data/Managed/Assembly-CSharp.dll'
    run('mcs','-out:'+str(WORK/'Inspect.exe'),'-r:'+CECIL,ROOT/'offline/v110/Inspect.cs')
    inventory = run('mono',WORK/'Inspect.exe',managed)
    (OUT/'backend-inventory.tsv').write_text(inventory)
    required = [s.split('\t')[1] for s in inventory.splitlines() if s.startswith('resource\t')]
    report = {'source':str(SOURCE),'source_sha256':SHA,'source_size':SOURCE.stat().st_size,
        'unity_versions':sorted(versions),'object_counts':counts,'embedded_tables':tables,
        'text_assets':texts,'required_resources':required,
        'missing_resources':[r for r in required if r.split('/')[-1] not in tables]}
    (OUT/'inventory.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    # Smali is diagnostic text only: never execute untrusted app code during audit.
    run('java','-jar','/usr/share/java/baksmali.jar','d',WORK/'original/classes.dex','-o',WORK/'smali')
    terms = ['DexClassLoader','PathClassLoader','Ljava/lang/Runtime;->exec',
        'Ljava/lang/ProcessBuilder;','/system/bin/su','/system/xbin/su','AccessibilityService',
        'DeviceAdminReceiver','sendTextMessage','getDeviceId','getSubscriberId','getLine1Number',
        'Landroid/provider/Contacts','Landroid/provider/Telephony','loadLibrary']
    matches, urls = {}, set()
    for f in sorted((WORK/'smali').rglob('*.smali')):
        for number, line in enumerate(f.read_text().splitlines(),1):
            for term in terms:
                if term in line:
                    matches.setdefault(term,[]).append({'file':str(f.relative_to(WORK/'smali')),
                        'line':number,'text':line.strip()})
            urls.update(re.findall(r'https?://[^\s"<>]+',line))
    (OUT/'dex-indicators.json').write_text(json.dumps(matches,indent=2))
    (OUT/'dex-urls.json').write_text(json.dumps(sorted(urls),indent=2))
    run('mcs','-out:'+str(WORK/'SecurityInspect.exe'),'-r:'+CECIL,
        ROOT/'offline/v110/SecurityInspect.cs')
    (OUT/'managed-sensitive.tsv').write_text(run('mono',WORK/'SecurityInspect.exe',
        managed,managed.with_name('Assembly-CSharp-firstpass.dll')))
    (WORK/'decompiled').mkdir(exist_ok=True)
    typedefs = run('monodis','--typedef',managed)
    for name in ['ServerForgery','ServerDataManager','LocalSaveManager','ResidentManager',
        'PatchManager','PatchUtil','MasterDataManager','TitleMenuManager','StoreManager',
        'AssetBundleManager','TitleMenuSeqInformationFox','TitleMenuSeqLoadAssetBundle',
        'ServerForgerySaveUser','ServerDataUtilRecv','PlayGameServiceUtil']:
        if not re.search(r'\b'+name+r'\b',typedefs): continue
        (WORK/'decompiled'/(name+'.cs')).write_text(run('/opt/dotnet/dotnet',
            '/opt/ilspy/tools/net8.0/any/ilspycmd.dll','-t',name,managed))
    preflight = {'source_sha256':SHA,'checked_at_utc':datetime.now(timezone.utc).isoformat(),
        'certificate_sha256':cert,'matches_archived_signers':True,
        'scanner_version':run('clamscan','--version').strip(),'scan_returncode':scan.returncode,
        'scan_report_sha256':hashlib.sha256((OUT/'clamav.txt').read_bytes()).hexdigest(),
        'status':'Automated preflight passed; manual sensitive API review required before build',
        'external_upload':False,'android_execution':False}
    (OUT/'preflight.json').write_text(json.dumps(preflight,indent=2)+'\n')
    print(json.dumps({k:v for k,v in report.items() if k != 'text_assets'},indent=2),flush=True)


if __name__ == '__main__': main()
