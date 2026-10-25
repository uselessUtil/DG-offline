import pathlib,subprocess,shutil,os
from prepare import ROOT,PROJECT,WORK
from build import GODOT,ENV,command
def main():
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
    shutil.copytree(ROOT/'godot290/project',PROJECT,dirs_exist_ok=True)
    command('android-export',[GODOT,'--headless','--path',str(PROJECT),'--export-debug','Android ARM64',str(WORK/'DivineGate-Godot-arm64-dev.apk')])
if __name__=='__main__':main()
