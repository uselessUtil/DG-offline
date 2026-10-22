"""Exercise the delivered backup-stage payload and a real prototype20 save."""
import json
from pathlib import Path
import shutil
import subprocess
import zipfile

from build import ROOT, MANAGED, digest, run


def verify(artifact, output, test, env):
    manifest=json.loads((output/'backup-stage-import.json').read_text())
    for name,row in manifest['tables'].items():
        raw=(output/'stage-tables'/(name+'.json')).read_bytes()
        assert digest(raw)==row['sha256']
    old=ROOT/'artifacts/1.0.6-offline-prototype20-native-models/ディバゲ_1.0.6.apk'
    assert digest(old.read_bytes())=='68a5e639add63b9009a496c4d82612de08af388a67504d3356009c64a27055bc'
    folder=test/'stage-upgrade'
    folder.mkdir()
    for path in test.glob('*.dll'):
        shutil.copy2(path,folder/path.name)
    with zipfile.ZipFile(old) as archive:
        for name in ['Assembly-CSharp.dll','DivineGateOffline.dll']:
            (folder/name).write_bytes(archive.read(MANAGED+name))
    run('mcs','-out:'+str(folder/'VerifyBackupStages.exe'),
        *['-r:'+str(folder/name) for name in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll']],
        ROOT/'offline/tests/VerifyBackupStages.cs')
    stage_env=dict(env,DG_TEST_STAGE_TABLES=str(output/'stage-tables'),
                   DG_TEST_PREFS=str(folder/'upgrade-prefs.txt'),DG_TEST_SAVE_SNAPSHOT=str(folder/'legacy-save.json'))
    log=subprocess.check_output(['mono',str(folder/'VerifyBackupStages.exe'),'seed'],env=stage_env,text=True,timeout=60)
    with zipfile.ZipFile(artifact) as archive:
        for name in ['Assembly-CSharp.dll','DivineGateOffline.dll']:
            (folder/name).write_bytes(archive.read(MANAGED+name))
    log+=subprocess.check_output(['mono',str(folder/'VerifyBackupStages.exe'),'upgrade'],env=stage_env,text=True,timeout=60)
    (output/'backup-stage-tests.txt').write_text(log)
    print(log)
    return log.strip()
