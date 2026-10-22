"""Run new rules against delivered DLLs, never real Android saves."""
import shutil
import subprocess
from build import ROOT, CECIL, run


def verify(test, tools, env, output):
    tests=ROOT/'offline/tests'
    import json
    report=json.loads((output/'build-report.json').read_text())
    from pathlib import Path
    original=Path(report['work_directory'])/'original/assets/bin/Data/Managed/Assembly-CSharp.dll'
    shutil.copy2(original,test/'original-Assembly-CSharp.dll')
    run('mcs','-out:'+str(tools/'VerifyRulesHooks.exe'),'-r:'+CECIL,tests/'VerifyRulesHooks.cs')
    log=subprocess.check_output(['mono',str(tools/'VerifyRulesHooks.exe'),str(test/'Assembly-CSharp.dll'),str(test/'original-Assembly-CSharp.dll')],text=True)
    refs=['-r:'+str(test/n) for n in ['Assembly-CSharp.dll','DivineGateOffline.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll','EngineSupport.dll']]
    run('mcs','-out:'+str(test/'VerifyAccountRules.exe'),*refs,tests/'VerifyAccountRules.cs')
    account_env=dict(env,DG_TEST_PREFS=str(test/'rules-prefs.txt'))
    for mode in [[],['reload']]:
        log+=subprocess.check_output(['mono',str(test/'VerifyAccountRules.exe'),*mode],env=account_env,text=True,timeout=60)
    run('mcs','-out:'+str(test/'VerifyAcquisitionAndSupports.exe'),*refs,tests/'VerifyAcquisitionAndSupports.cs')
    acquisition_log=''
    acquisition_env=dict(env,DG_TEST_PREFS=str(test/'acquisition-prefs.txt'))
    for mode in [[],['reload']]:
        acquisition_log+=subprocess.check_output(['mono',str(test/'VerifyAcquisitionAndSupports.exe'),*mode],env=acquisition_env,text=True,timeout=60)
    (output/'acquisition-support-tests.txt').write_text(acquisition_log)
    log+=acquisition_log
    odds=test/'rules-odds';odds.mkdir()
    for p in test.glob('*.dll'):shutil.copy2(p,odds/p.name)
    run('mcs','-out:'+str(tools/'PrepareRulesOdds.exe'),'-r:'+CECIL,tests/'PrepareRulesOdds.cs')
    log+=subprocess.check_output(['mono',str(tools/'PrepareRulesOdds.exe'),str(test/'Assembly-CSharp.dll'),str(odds/'Assembly-CSharp.dll')],text=True)
    refs=['-r:'+str(odds/n) for n in ['Assembly-CSharp.dll','DivineGateOffline.dll','Assembly-CSharp-firstpass.dll','UnityEngine.dll']]
    run('mcs','-out:'+str(odds/'VerifyRulesOdds.exe'),*refs,tests/'VerifyRulesOdds.cs')
    log+=subprocess.check_output(['mono',str(odds/'VerifyRulesOdds.exe')],env=dict(env,DG_TEST_PREFS=str(odds/'prefs.txt')),text=True,timeout=60)
    from verify_warehouse_prefab import verify as verify_prefab
    import json
    report=json.loads((output/'build-report.json').read_text())
    log+=verify_prefab(report['output'],output)
    (output/'account-rules-tests.txt').write_text(log)
    print(log)
    return log.strip()
