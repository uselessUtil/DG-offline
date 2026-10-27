import pathlib,subprocess,shutil,os,argparse
from prepare import ROOT,PROJECT,WORK,sync_sources
from build import GODOT,ENV,command
def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--x86-64',action='store_true',help='Native MuMu build; preserve the ARM64 phone APK')
    args=parser.parse_args()
    config=pathlib.Path.home()/'.config/godot';config.mkdir(parents=True,exist_ok=True)
    settings=config/'editor_settings-4.6.tres'
    key=WORK/'debug.keystore'
    if not key.exists():subprocess.run(['keytool','-genkeypair','-keystore',str(key),'-storepass','android','-keypass','android','-alias','androiddebugkey','-dname','CN=Android Debug,O=Android,C=US','-keyalg','RSA','-keysize','2048','-validity','10000'],check=True)
    text='[gd_resource type="EditorSettings" format=3]\n\n[resource]\n'
    values={'export/android/android_sdk_path':'/opt/dg-android-sdk','export/android/java_sdk_path':'/usr/lib/jvm/java-21-openjdk-amd64','export/android/debug_keystore':str(key),'export/android/debug_keystore_user':'androiddebugkey','export/android/debug_keystore_pass':'android'}
    import json
    if settings.exists():
        text=settings.read_text()
        text='\n'.join(line for line in text.splitlines() if not any(line.startswith(k+' =') for k in values))+'\n'
    settings.write_text(text+'\n'.join(k+' = '+json.dumps(v) for k,v in values.items())+'\n')
    sync_sources()
    presets=PROJECT/'export_presets.cfg'
    original=presets.read_text()
    if args.x86_64:
        presets.write_text(original.replace('architectures/arm64-v8a=true','architectures/arm64-v8a=false').replace('architectures/x86_64=false','architectures/x86_64=true'))
    try:
        arch='x86_64' if args.x86_64 else 'arm64'
        command('android-export-'+arch,[GODOT,'--headless','--path',str(PROJECT),'--export-debug','Android ARM64',str(WORK/f'DivineGate-Godot-{arch}-dev.apk')])
    finally:
        presets.write_text(original)
if __name__=='__main__':main()
