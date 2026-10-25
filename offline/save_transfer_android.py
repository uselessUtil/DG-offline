"""Build the isolated document Activity and merge its classes without rewriting old smali."""
from pathlib import Path
import hashlib
import shutil
import subprocess
import zipfile

ROOT=Path('/workspace')
TOOLS=Path('/opt/dg-save-tools')


def source_code(package, game_version):
    code=(ROOT/'offline/android/SaveTransferActivity.java').read_text()
    code=code.replace('jp.gungho.dg.offline106',package).replace('1.0.6',game_version)
    if game_version=='2.9.0':
        old='!"2.9.0".equals(root.getString("game_version"))'
        new='(!"2.9.0".equals(root.getString("game_version")) && !"1.0.6".equals(root.getString("game_version")))'
        assert old in code
        code=code.replace(old,new)
        needle='            String payload=root.getString("payload");'
        code=code.replace(needle,'''            boolean legacy="1.0.6".equals(root.getString("game_version"));
            if(legacy && !"1.0.6 offline prototype 22r7".equals(root.getString("prototype")))
                throw new IOException("Unsupported 1.0.6 backup revision");
'''+needle)
        code=code.replace('return "Name: "', 'return (legacy ? "Convert 1.0.6 to 2.9.0. Quest IDs will be mapped; new features start empty. " : "")+"Name: "')
    if game_version=='2.9.1':
        start=code.index('            if(!"dg-offline-save".equals')
        end=code.index('            String payload=',start)
        code=code[:start]+'''            String version=root.getString("game_version");
            boolean nativeBackup="2.9.1".equals(version);
            boolean legacy106="1.0.6".equals(version);
            boolean oldBackup=legacy106 || "2.9.0".equals(version);
            if(!"dg-offline-save".equals(root.getString("format")) ||
                !((nativeBackup && root.getInt("schema")==2 && "dgdb-1".equals(root.optString("data_version",""))) ||
                  (oldBackup && root.getInt("schema")==1)))throw new IOException("Not a compatible DGDB backup");
            if(legacy106 && !"1.0.6 offline prototype 22r7".equals(root.getString("prototype")))
                throw new IOException("Unsupported 1.0.6 backup revision");
'''+code[end:]
        code=code.replace('return "Name: "', 'return (oldBackup ? "Convert to 2.9.1 DGDB. Skill levels above new limits will be capped. " : "")+"Name: "')
    return code


def create(source_dex, work, package="jp.gungho.dg.offline106", game_version="1.0.6"):
    work=work/'save-android';work.mkdir()
    classes=work/'classes';classes.mkdir()
    assert package in {'jp.gungho.dg.offline106','jp.gungho.dg.offline290','jp.gungho.dg.offline291'}
    assert game_version in {'1.0.6','2.9.0','2.9.1'}
    source=work/'SaveTransferActivity.java'
    source.write_text(source_code(package,game_version))
    subprocess.run(['javac','--release','8','-cp',str(TOOLS/'android.jar')+':'+str(TOOLS/'json.jar'),'-d',str(classes),
                    str(source)],check=True)
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
        assert str(relative).startswith(package.replace('.','/')+'/SaveTransferActivity')
        destination=original/relative
        assert not destination.exists(),'New activity collides with archived DEX class'
        destination.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,destination)
    for name,value in old_hashes.items():assert hashlib.sha256((original/name).read_bytes()).hexdigest()==value
    merged=work/'classes.dex'
    subprocess.run(['smali','a',str(original),'-o',str(merged)],check=True)
    return merged.read_bytes(),{'activity':package+'.SaveTransferActivity','permissions_added':[],
        'minimum_android_for_picker':19,'original_classes_unchanged_smali':len(old_hashes),
        'source':'offline/android/SaveTransferActivity.java', 'game_version':game_version,
        'generated_source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
        'tool_sha256':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in TOOLS.glob('*.jar')}}
