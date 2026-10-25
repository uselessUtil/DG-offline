"""Install export tooling inside the project development container only; no host dependencies."""
import hashlib,pathlib,subprocess,urllib.request,zipfile,xml.etree.ElementTree as ET
W=pathlib.Path('/tmp/dg-godot290');W.mkdir(exist_ok=True)
def download(url,path):
    if not path.exists():urllib.request.urlretrieve(url,path)
    return path
def main():
    base='https://github.com/godotengine/godot/releases/download/4.6.3-stable/'
    filename='Godot_v4.6.3-stable_mono_export_templates.tpz'
    archive=download(base+filename,W/filename)
    sums=urllib.request.urlopen(base+'SHA512-SUMS.txt').read().decode()
    expected=next(s.split()[0] for s in sums.splitlines() if s.endswith(filename))
    assert hashlib.sha512(archive.read_bytes()).hexdigest()==expected
    dest=pathlib.Path.home()/'.local/share/godot/export_templates/4.6.3.stable.mono';dest.mkdir(parents=True,exist_ok=True)
    with zipfile.ZipFile(archive) as z:
        for name in z.namelist():
            if name.startswith('templates/') and not name.endswith('/'):
                target=dest/name.removeprefix('templates/');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(z.read(name))
    print('Godot export templates verified and installed',flush=True)
    sdk=pathlib.Path('/opt/dg-android-sdk');sdk.mkdir(exist_ok=True)
    xml=ET.fromstring(urllib.request.urlopen('https://dl.google.com/android/repository/repository2-1.xml').read())
    package=next(e for e in xml.iter() if e.tag.endswith('remotePackage') and e.attrib.get('path')=='cmdline-tools;latest')
    for a in package.find('archives').findall('archive'):
        if a.findtext('host-os')=='linux':item=a.find('complete');break
    path=download('https://dl.google.com/android/repository/'+item.findtext('url'),W/'android-commandline-tools.zip')
    assert hashlib.sha1(path.read_bytes()).hexdigest()==item.findtext('checksum')
    with zipfile.ZipFile(path) as z:
        for name in z.namelist():
            if name.endswith('/'):continue
            target=sdk/'cmdline-tools/latest'/name.removeprefix('cmdline-tools/');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(z.read(name))
    for f in (sdk/'cmdline-tools/latest/bin').iterdir():f.chmod(0o755)
    command=[str(sdk/'cmdline-tools/latest/bin/sdkmanager'),'--sdk_root='+str(sdk)]
    subprocess.run(command+['--licenses'],input=b'y\n'*100,check=True,stdout=subprocess.DEVNULL)
    subprocess.run(command+['platform-tools','build-tools;35.0.1','platforms;android-35'],check=True)
if __name__=='__main__':main()
