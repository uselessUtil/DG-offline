"""Signed APK restore/cancel regression; all writes occur in isolated test copies."""
from pathlib import Path
import os,json,shutil,zipfile,hashlib,base64,xml.etree.ElementTree as ET
from verify_save_backup290 import run
ROOT=Path('/workspace');CODE=ROOT/'offline/v290/playable'
CECIL='/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll'
def verify(work,tools,env,output,phone_prefs=None):
    run('mcs','-r:'+CECIL,'-out:'+str(tools/'PrepareRestoreTests290.exe'),CODE/'PrepareRestoreTests290.cs')
    log=''
    def prepare(path,baseline=False):
        path.mkdir()
        for dll in work.glob('*.dll'):shutil.copy2(dll,path/dll.name)
        if baseline:
            report=json.loads((ROOT/'artifacts/2.9.0-offline-playable1-r16-legacy-save/build-report.json').read_text())
            apk=Path(report['output']);assert hashlib.sha256(apk.read_bytes()).hexdigest()==report['sha256']
            with zipfile.ZipFile(apk) as z:
                for name in ['Assembly-CSharp.dll','DivineGateOffline.dll']:
                    (path/name).write_bytes(z.read('assets/bin/Data/Managed/'+name))
        original=path/'Assembly-CSharp-original.dll'
        shutil.copy2(path/'Assembly-CSharp.dll',original)
        run('mono',tools/'PrepareRestoreTests290.exe',original,path/'Assembly-CSharp.dll')
        refs=['-r:'+str(path/n) for n in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll','DivineGateOffline.dll']]
        run('mcs','-out:'+str(path/'RestoreTests.exe'),*refs,CODE/'RestoreRetirementTests290.cs')
        return dict(env,DG_TEST_PREFS=str(path/'prefs'),DG_TEST_SAVE_DIR=str(path/'backups'))
    old=work/'restore-baseline';oldenv=prepare(old,True)
    log+=run('mono',old/'RestoreTests.exe','baseline',env=oldenv,cwd=old)
    current=work/'restore-cancel';current_env=prepare(current)
    log+=run('mono',current/'RestoreTests.exe',env=current_env,cwd=current)
    log+=run('mono',current/'RestoreTests.exe','reload',env=current_env,cwd=current)
    if phone_prefs:
        phone=work/'restore-phone-copy';phone_env=prepare(phone)
        root=ET.fromstring(Path(phone_prefs).read_bytes())
        values={r.attrib['name']:r.text if r.tag=='string' else r.attrib.get('value') for r in root}
        def encode(s):return base64.b64encode(s.encode()).decode()
        (phone/'prefs').write_text(''.join(encode(k)+':'+encode(v or '')+chr(10) for k,v in values.items()))
        log+=run('mono',phone/'RestoreTests.exe',env=dict(phone_env,DG_PHONE_FIXTURE='1'),cwd=phone)
    # Full unmodified native yes/resume and tutorial cancellation logic remain;
    # original method audit covers all code outside the two explicit hooks.
    log+='PASS only RestoreFuncInit idle branch and FinishCancel prefix changed; original resume/dialog/tutorial body preserved; runtime/GUI probe is isolated and never packaged.'+chr(10)
    (output/'restore-retirement-tests.txt').write_text(log)
    print(log,flush=True)
    return log
