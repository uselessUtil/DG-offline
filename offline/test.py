"""Test the exact adapter and game assembly packaged in the generated APK.

The engine stand-in covers Resources, PlayerPrefs, math and selected UI flags, not Android,
rendering, native plug-ins, animations, touches or Unity scene execution.
"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import zipfile
from build import ROOT, CECIL, MANAGED, digest, run


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('report', type=Path)
    args = parser.parse_args()
    report = json.loads(args.report.read_text())
    artifact = Path(report['output'])
    if digest(artifact.read_bytes()) != report['sha256']:
        raise ValueError('APK differs from the build report')
    work = Path(report['work_directory'])
    test = Path(tempfile.mkdtemp(prefix='dg106-test-', dir='/tmp'))
    tools = test/'tools'
    tools.mkdir()
    managed = work/'original'/MANAGED
    for source in managed.glob('*.dll'):
        shutil.copy2(source,test/source.name)
    with zipfile.ZipFile(artifact) as archive:
        for name in ['Assembly-CSharp.dll','DivineGateOffline.dll']:
            (test/name).write_bytes(archive.read(MANAGED+name))
    tests = ROOT/'offline/tests'
    run('mcs','-out:'+str(tools/'VerifyReferences.exe'),'-r:'+CECIL,tests/'VerifyReferences.cs')
    run('mono',tools/'VerifyReferences.exe',managed,test)
    run('mcs','-target:library','-out:'+str(test/'EngineSupport.dll'),'-r:'+str(managed/'UnityEngine.dll'),tests/'EngineSupport.cs')
    run('mcs','-out:'+str(tools/'TestEngine.exe'),'-r:'+CECIL,tests/'TestEngine.cs')
    run('mono',tools/'TestEngine.exe',managed/'UnityEngine.dll',test/'EngineSupport.dll',test/'UnityEngine.dll')
    run('mcs','-out:'+str(test/'LocalApiTests.exe'),'-r:'+str(test/'Assembly-CSharp.dll'),
        '-r:'+str(test/'Assembly-CSharp-firstpass.dll'),'-r:'+str(test/'UnityEngine.dll'),tests/'LocalApiTests.cs')
    env = dict(os.environ, DG_TEST_TABLES=str(work/'tables'),DG_TEST_PREFS=str(test/'player-prefs.txt'))
    logs = []
    for args2 in [[],['reload']]:
        log = test/('reload.log' if args2 else 'api.log')
        with log.open('w') as stream:
            result = subprocess.run(['mono',str(test/'LocalApiTests.exe'),*args2],env=env,stdout=stream,stderr=subprocess.STDOUT,timeout=45)
        text = log.read_text(errors='replace')
        print(text[:10000])
        if result.returncode:
            raise RuntimeError('Local API verification failed; full log: '+str(log))
        logs.append(text)
    (args.report.parent/'local-api-tests.txt').write_text(
        'Managed API integration tests with a stubbed engine. Android gameplay remains unverified.\n\n'+''.join(logs))
    report['local_api_tests'] = 'PASS: create, rename, starter party, authentication, tables, login pack, tutorial start/clear/rewards, separate-process reload'
    report['test_engine_limitations'] = 'Does not verify Android installation, Unity rendering, native plug-ins, animation, touch or scene lifecycle.'
    args.report.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')


if __name__ == '__main__':
    main()
