"""Read-only 2.9.0 inventory. Run in the isolated project tool environment; no APK execution."""
import hashlib
import json
import os
from pathlib import Path
import re
import struct
import subprocess
import zipfile
from datetime import datetime, timezone

ROOT = Path('/workspace')
OUT = ROOT / 'artifacts/2.9.0-security-audit'
WORK = Path('/tmp/dg290-audit')
SOURCE = ROOT / 'jp.gungho.dg-v2.9.0.apk'
SHA = '3c6ce05ca09837b07faaf99aa80716cc2754eb22887ca580d17e8c86559f2cc4'


def run(*args):
    return subprocess.check_output(args, text=True, env={**os.environ, 'LANG':'C.UTF-8', 'LC_ALL':'C.UTF-8'})


def main():
    import UnityPy
    OUT.mkdir(exist_ok=True, parents=True)
    (OUT/'security-verdict.json').unlink(missing_ok=True)
    WORK.mkdir(exist_ok=True)
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == SHA
    certificates = {}
    for source in [SOURCE, *ROOT.glob('*1.0.6.apk'), *ROOT.glob('*2.1.0.apk')]:
        cert = run('apksigner', 'verify', '--verbose', '--print-certs', str(source))
        certificates[source.name] = cert
    (OUT/'certificates.json').write_text(json.dumps(certificates, ensure_ascii=False, indent=2))
    expected_cert = '9623d6804d17d3abe7741709ff7ce2bcb98b1fb0d0782c10dfc7d68569d00bbc'
    assert len(certificates) == 3
    assert all('certificate SHA-256 digest: '+expected_cert in c for c in certificates.values())
    (OUT/'manifest.txt').write_text(run('aapt','dump','xmltree',str(SOURCE),'AndroidManifest.xml'))
    with zipfile.ZipFile(SOURCE) as z:
        names = z.namelist()
        assert z.testzip() is None
        assert len(names) == len(set(names)), 'Duplicate ZIP entries'
        assert not any(n.startswith('/') or '..' in Path(n).parts for n in names)
        z.extractall(WORK/'original')
        files = [{'name':i.filename, 'size':i.file_size, 'compressed':i.compress_size,
                  'sha256':hashlib.sha256(z.read(i)).hexdigest()} for i in z.infolist()]
        (OUT/'file-inventory.json').write_text(json.dumps(files, indent=2))
        dex = z.read('classes.dex')
        count, offset = struct.unpack_from('<II', dex, 56)
        strings = []
        for i in range(count):
            pos = struct.unpack_from('<I', dex, offset+4*i)[0]
            while dex[pos] & 128:
                pos += 1
            pos += 1
            strings.append(dex[pos:dex.index(b'\0',pos)].decode('utf8',errors='replace'))
        (OUT/'dex-strings.txt').write_text('\n'.join(strings))
        classes = sorted(s for s in strings if s.startswith('L') and s.endswith(';'))
        (OUT/'dex-types.txt').write_text('\n'.join(classes))
        urls = sorted(set(s for s in strings if re.match(r'https?://', s)))
        (OUT/'dex-urls.json').write_text(json.dumps(urls,indent=2))
        indicators = ['DexClassLoader','PathClassLoader','Ljava/lang/Runtime;', 'Ljava/lang/ProcessBuilder;',
                      '/system/bin/su','/system/xbin/su','AccessibilityService','DeviceAdminReceiver',
                      'SEND_SMS','READ_SMS','READ_CONTACTS','REQUEST_INSTALL_PACKAGES','/proc/']
        matches = {term:[s for s in strings if term in s] for term in indicators}
        (OUT/'dex-indicators.json').write_text(json.dumps(matches,indent=2))
    scan = subprocess.run(['clamscan','--infected','--recursive','--max-filesize=200M',
        '--max-scansize=1000M','--alert-exceeds-max=yes',str(SOURCE),str(WORK/'original')],
        capture_output=True, text=True)
    (OUT/'clamav.txt').write_text(scan.stdout+scan.stderr)
    if scan.returncode != 0 or 'Infected files: 0' not in scan.stdout or scan.stderr.strip():
        raise RuntimeError('ClamAV did not complete cleanly; inspect clamav.txt')
    env = UnityPy.load(str(WORK/'original/assets/bin/Data'))
    tables = WORK/'tables'
    tables.mkdir(exist_ok=True)
    table_counts, errors, text_assets = {}, {}, []
    versions = set()
    for asset in env.files.values():
        version = getattr(asset, 'unity_version', None)
        if version:
            versions.add(str(version))
    object_counts = {}
    for obj in env.objects:
        object_counts[obj.type.name] = object_counts.get(obj.type.name,0)+1
        if obj.type.name == 'TextAsset':
            value = obj.read()
            text_assets.append({'file':obj.assets_file.name,'path_id':obj.path_id,'name':value.m_Name})
            if value.m_Name.startswith('MasterData'):
                raw = value.m_Script
                if isinstance(raw,str): raw = raw.encode('utf8','surrogateescape')
                (tables/(value.m_Name+'.json')).write_bytes(raw)
                try: table_counts[value.m_Name] = len(json.loads(raw,strict=False))
                except Exception as e: errors[value.m_Name] = str(e)
    (OUT/'unity-inventory.json').write_text(json.dumps({'versions':sorted(versions), 'object_counts':object_counts,
        'tables':table_counts,'table_errors':errors,'text_assets':text_assets,
        'serialized_files_loaded':len(env.files)}, indent=2))
    decompiled = WORK/'decompiled'
    decompiled.mkdir(exist_ok=True)
    types = ['ServerForgery','ServerDataManager','LocalSaveManager','PatchManager','PatchUtil',
             'ResidentManager','MasterDataManager','TitleMenuManager','TitleMenuSeqInformationFox',
             'TitleMenuSeqInformationPolicy','TitleMenuSeqLoadMasterAndAssetBundle','TitleMenuSeqLoadMasterData',
             'TitleMenuSeqLoadAssetBundle','TitleMenuSeqLoadStoreProduct','TitleMenuSeqUserAuthentication',
             'AssetBundleManager','StoreManager','ServerForgerySaveUser','ServerDataUtilRecv','TitleMenuSeqWaitInput']
    for name in types:
        text = run('/opt/dotnet/dotnet','/opt/ilspy/tools/net8.0/any/ilspycmd.dll','-t',name,
                   str(WORK/'original/assets/bin/Data/Managed/Assembly-CSharp.dll'))
        (decompiled/(name+'.cs')).write_text(text)
    verdict = {'source_sha256':SHA,'source_size':SOURCE.stat().st_size,
        'checked_at_utc':datetime.now(timezone.utc).isoformat(),
        'scan_report_sha256':hashlib.sha256((OUT/'clamav.txt').read_bytes()).hexdigest(),
        'certificate_sha256':expected_cert,'matches_archived_106_and_210':True,
        'apk_signature':'v1 valid; v2/v3 absent','clamav_returncode':scan.returncode,
        'scanner_version':run('clamscan','--version').strip(),
        'verdict':'No known malware detected or malicious re-signing observed within performed checks; safety not guaranteed.',
        'limitations':['No independently trusted official 2.9.0 file hash or download provenance supplied',
                      'Single antivirus engine; no Android behavioral sandbox',
                      'Native libraries and all managed methods were not exhaustively reverse engineered',
                      'V1 signing does not protect all ZIP metadata; matching cert is not a malware-free guarantee'],
        'external_upload':False}
    (OUT/'security-verdict.json').write_text(json.dumps(verdict,indent=2)+'\n')
    print(json.dumps({'sha256':SHA,'size':SOURCE.stat().st_size,'versions':sorted(versions),
                      'tables':table_counts,'errors':errors,'dex_indicators':matches},indent=2))


if __name__ == '__main__':
    main()
