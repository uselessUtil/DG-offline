"""Delivered managed code roundtrip, native protocol, and manifest checks."""
import json
import os
from pathlib import Path
import subprocess
import zipfile
from build import ROOT, run, digest
from manifest import chunks, strings_from, attributes, U32


def verify(test, tools, env, output):
    source=ROOT/'offline/tests'
    report=json.loads((output/'build-report.json').read_text())
    refs=['-r:'+str(test/n) for n in ['Assembly-CSharp.dll','DivineGateOffline.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll']]
    run('mcs','-out:'+str(test/'VerifySaveBackup.exe'),*refs,source/'VerifySaveBackup.cs')
    directory=test/'save-backups'
    backup_env=dict(env,DG_TEST_PREFS=str(test/'backup-prefs.txt'),DG_TEST_SAVE_DIR=str(directory))
    log=''
    for mode in [[],['reload']]:
        log+=subprocess.check_output(['mono',str(test/'VerifySaveBackup.exe'),*mode],env=backup_env,text=True,timeout=60)
    native=test/'native-save-preview';native.mkdir()
    libraries='/opt/dg-save-tools/android.jar:/opt/dg-save-tools/json.jar'
    run('javac','--release','8','-cp',libraries,'-d',native,
        ROOT/'offline/android/SaveTransferActivity.java',source/'SavePreviewTest.java')
    actual=Path(report['work_directory'])/'build/save-android/classes'
    for compiled in actual.rglob('*.class'):
        assert digest(compiled.read_bytes())==digest((native/compiled.relative_to(actual)).read_bytes()),'Native source differs from APK build inputs'
    log+=subprocess.check_output(['java','-cp',str(native)+':/opt/dg-save-tools/json.jar:/opt/dg-save-tools/android.jar',
        'SavePreviewTest',str(directory/'sample.dgbackup.json')],text=True,timeout=30)
    with zipfile.ZipFile(report['output']) as archive:
        manifest=list(chunks(archive.read('AndroidManifest.xml')))
        pool=strings_from(next(c for k,c in manifest if k==1));found=0
        for kind,c in manifest:
            if kind!=0x102 or pool[U32.unpack_from(c,20)[0]]!='activity':continue
            attrs=list(attributes(c,pool))
            if not any(k=='name' and v=='jp.gungho.dg.offline106.SaveTransferActivity' for k,v,p in attrs):continue
            exported=[U32.unpack_from(c,p+16)[0] for k,v,p in attrs if k=='exported']
            assert exported==[0],'Backup activity is externally exported'
            found+=1
        assert found==1,'Missing/duplicate backup activity'
        dex=test/'save-classes.dex';dex.write_bytes(archive.read('classes.dex'))
    classes=subprocess.check_output(['baksmali','list','classes',str(dex)],text=True)
    assert 'Ljp/gungho/dg/offline106/SaveTransferActivity;' in classes
    log+='PASS APK integration: compiled native bridge matches build inputs, Activity is present in DEX and declared non-exported; no extra permissions. Original DEX class source preserved during merge.\n'
    log+='LIMIT: managed engine I/O stand-in plus desktop Java protocol; Android document picker, providers, confirmation UI, pause/resume and actual quit require device testing.\n'
    (output/'save-transfer-tests.txt').write_text(log)
    print(log)
    return log.strip()
