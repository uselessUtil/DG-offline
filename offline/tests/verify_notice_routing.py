"""Reproduce the pictured22r2 helper-point popup and verify quiet receipt routing."""
import subprocess
import shutil
import zipfile
from build import ROOT, CECIL, MANAGED, digest, run


def verify(test, tools, env, output):
    tests=ROOT/'offline/tests'
    run('mcs','-out:'+str(tools/'PrepareNoticeProbe.exe'),'-r:'+CECIL,tests/'PrepareNoticeProbe.cs')
    old=ROOT/'artifacts/1.0.6-offline-prototype22r2-no-auto-news/ディバゲ_1.0.6.apk'
    if old.exists():assert digest(old.read_bytes())=='92f596d7b55f2df7e101e71ebaaa1ed0b9725500dd25200b4e0936a60558da04'
    else:run('mcs','-out:'+str(tools/'PrepareLegacyFixture.exe'),'-r:'+CECIL,tests/'PrepareLegacyFixture.cs')
    log=''
    for name,baseline in [('notice-baseline',True),('notice-fixed',False)]:
        folder=test/name;folder.mkdir()
        for p in test.glob('*.dll'):shutil.copy2(p,folder/p.name)
        if baseline and old.exists():
            with zipfile.ZipFile(old) as archive:
                for n in ['Assembly-CSharp.dll','DivineGateOffline.dll']:(folder/n).write_bytes(archive.read(MANAGED+n))
        elif baseline:
            fixture=folder/'Legacy.dll'
            log+=subprocess.check_output(['mono',str(tools/'PrepareLegacyFixture.exe'),'notice',str(folder/'Assembly-CSharp.dll'),str(fixture)],text=True)
            shutil.move(fixture,folder/'Assembly-CSharp.dll')
        original=folder/'Assembly-CSharp.dll';probe=folder/'Probe.dll'
        log+=subprocess.check_output(['mono',str(tools/'PrepareNoticeProbe.exe'),str(original),str(probe)],text=True)
        shutil.move(probe,original)
        refs=['-r:'+str(folder/n) for n in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll']]
        run('mcs','-out:'+str(folder/'VerifyNoticeRouting.exe'),*refs,tests/'VerifyNoticeRouting.cs')
        log+=subprocess.check_output(['mono',str(folder/'VerifyNoticeRouting.exe'),*(['baseline'] if baseline else [])],
            env=dict(env,DG_TEST_PREFS=str(folder/'prefs.txt')),text=True,timeout=45)
    (output/'notice-routing-tests.txt').write_text(log)
    print(log)
    return log.strip()
