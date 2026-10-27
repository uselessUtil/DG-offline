#!/usr/bin/env python3
"""Prepare an independent Godot project from the immutable r17 reference APK."""
import hashlib, json, pathlib, shutil, subprocess, zipfile

ROOT=pathlib.Path('/workspace')
SOURCE=ROOT/'artifacts/2.9.0-offline-playable1-r17-restore-retire/ディバゲ_2.9.0.apk'
EXPECTED='b8caed0f1f5c6b34e1285db7dffcf3584436a2980393aa88324e18faa72b6e8f'
WORK=pathlib.Path('/tmp/dg-godot290')
PROJECT=WORK/'project'
def run(*args): subprocess.run([str(x) for x in args],check=True)
def main():
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==EXPECTED, 'Reference APK changed'
    managed=WORK/'reference/Managed';managed.mkdir(parents=True,exist_ok=True)
    libs=PROJECT/'Libraries';libs.mkdir(parents=True,exist_ok=True)
    with zipfile.ZipFile(SOURCE) as z:
        (PROJECT/'icon.png').write_bytes(z.read('res/drawable-xxxhdpi-v4/app_icon.png'))
        for name in z.namelist():
            if name.startswith('assets/bin/Data/Managed/') and name.endswith('.dll'):
                target=managed/pathlib.Path(name).name;target.write_bytes(z.read(name))
    for name in ['Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll','DivineGateOffline.dll','P31RestKit.dll','UnityEngine.UI.dll']:
        shutil.copy2(managed/name,libs/name)
    run('mcs','-target:library','-r:'+str(managed/'UnityEngine.dll'),'-out:'+str(libs/'PortableEngine.dll'),ROOT/'godot290/bridge/PortableEngine.cs')
    run('mcs','-r:/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll','-out:'+str(WORK/'RewriteEngine.exe'),ROOT/'godot290/bridge/RewriteEngine.cs')
    run('mono',WORK/'RewriteEngine.exe',managed/'UnityEngine.dll',libs/'PortableEngine.dll',libs/'UnityEngine.dll')
    run('mcs','-out:'+str(libs/'ExportRecovered.exe'),ROOT/'godot290/bridge/ExportRecovered.cs')
    run('mono',libs/'ExportRecovered.exe',libs/'DivineGateOffline.dll',PROJECT/'Reference/recovered')
    (libs/'ExportRecovered.exe').unlink()
    run('mcs','-r:/usr/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756/Mono.Cecil.dll','-out:'+str(WORK/'RewriteEmbeddedData.exe'),ROOT/'godot290/bridge/RewriteEmbeddedData.cs')
    run('mono',WORK/'RewriteEmbeddedData.exe',managed/'DivineGateOffline.dll',libs/'PortableEngine.dll',libs/'DivineGateOffline.dll')
    shutil.copytree(ROOT/'godot290/project',PROJECT,dirs_exist_ok=True)
    (PROJECT/'reference.json').write_text(json.dumps({'apk':str(SOURCE.relative_to(ROOT)),'sha256':EXPECTED,'version':'2.9.0 r17','engine':'Godot 4.6.3 .NET','status':'port in progress'},indent=2))
    print(PROJECT)
if __name__=='__main__':main()
