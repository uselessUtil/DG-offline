"""Build the isolated document Activity and merge its classes without rewriting old smali."""
from pathlib import Path
import hashlib
import shutil
import subprocess
import zipfile

ROOT=Path('/workspace')
TOOLS=Path('/opt/dg-save-tools')


def create(source_dex, work):
    work=work/'save-android';work.mkdir()
    classes=work/'classes';classes.mkdir()
    subprocess.run(['javac','--release','8','-cp',str(TOOLS/'android.jar')+':'+str(TOOLS/'json.jar'),'-d',str(classes),
                    str(ROOT/'offline/android/SaveTransferActivity.java')],check=True)
    jar=work/'bridge.jar'
    with zipfile.ZipFile(jar,'w') as z:
        for f in classes.rglob('*.class'):z.write(f,f.relative_to(classes))
    dex=work/'dex';dex.mkdir()
    subprocess.run(['java','-cp',str(TOOLS/'r8.jar'),'com.android.tools.r8.D8','--min-api','9',
                    '--lib',str(TOOLS/'android.jar'),'--lib',str(TOOLS/'json.jar'),'--output',str(dex),str(jar)],check=True)
    old=work/'original.dex';old.write_bytes(source_dex)
    original=work/'original-smali';new=work/'bridge-smali'
    subprocess.run(['baksmali','d',str(old),'-o',str(original)],check=True)
    subprocess.run(['baksmali','d',str(dex/'classes.dex'),'-o',str(new)],check=True)
    old_hashes={str(p.relative_to(original)):hashlib.sha256(p.read_bytes()).hexdigest() for p in original.rglob('*.smali')}
    for p in new.rglob('*.smali'):
        relative=p.relative_to(new)
        assert str(relative).startswith('jp/gungho/dg/offline106/SaveTransferActivity')
        destination=original/relative
        assert not destination.exists(),'New activity collides with archived DEX class'
        destination.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,destination)
    for name,value in old_hashes.items():assert hashlib.sha256((original/name).read_bytes()).hexdigest()==value
    merged=work/'classes.dex'
    subprocess.run(['smali','a',str(original),'-o',str(merged)],check=True)
    return merged.read_bytes(),{'activity':'jp.gungho.dg.offline106.SaveTransferActivity','permissions_added':[],
        'minimum_android_for_picker':19,'original_classes_unchanged_smali':len(old_hashes),
        'source':'offline/android/SaveTransferActivity.java',
        'tool_sha256':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in TOOLS.glob('*.jar')}}
