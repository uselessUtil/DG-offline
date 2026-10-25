"""Run only inside the project development container. Pin tool versions for repeatable builds."""
import hashlib,json,pathlib,subprocess,tarfile,urllib.request,zipfile,sys
W=pathlib.Path('/tmp/dg-godot290');W.mkdir(exist_ok=True)
def fetch(url,target):
    if not target.exists():urllib.request.urlretrieve(url,target)
    return target
def main():
    subprocess.run(['apt-get','update'],check=True,stdout=subprocess.DEVNULL)
    subprocess.run(['apt-get','install','-y','libicu76','libfontconfig1','xvfb','xauth','libgl1-mesa-dri','libglx-mesa0'],check=True,stdout=subprocess.DEVNULL)
    subprocess.run(['/opt/unity-tools/bin/pip','install','TypeTreeGeneratorAPI==0.0.10'],check=True)
    base='https://github.com/godotengine/godot/releases/download/4.6.3-stable/'
    name='Godot_v4.6.3-stable_mono_linux_x86_64.zip';archive=fetch(base+name,W/name)
    sums=urllib.request.urlopen(base+'SHA512-SUMS.txt').read().decode()
    assert hashlib.sha512(archive.read_bytes()).hexdigest()==next(s.split()[0] for s in sums.splitlines() if s.endswith(name))
    target=pathlib.Path('/opt/godot/4.6.3');target.mkdir(parents=True,exist_ok=True)
    with zipfile.ZipFile(archive) as z:z.extractall(target)
    (target/name.removesuffix('.zip')/'Godot_v4.6.3-stable_mono_linux.x86_64').chmod(0o755)
    if not pathlib.Path('/opt/dg-dotnet9/dotnet').exists():
        manifest=json.load(urllib.request.urlopen('https://builds.dotnet.microsoft.com/dotnet/release-metadata/9.0/releases.json'))
        sdks=[sdk for release in manifest['releases'] for sdk in release.get('sdks',[])]
        sdk=next(s for s in sdks if s['version']=='9.0.319')
        item=next(f for f in sdk['files'] if f['rid']=='linux-x64' and f['url'].endswith('.tar.gz'))
        archive=fetch(item['url'],W/'dotnet-sdk-9.0.319-linux-x64.tar.gz')
        assert hashlib.sha512(archive.read_bytes()).hexdigest()==item['hash'].lower()
        target=pathlib.Path('/opt/dg-dotnet9');target.mkdir(exist_ok=True)
        with tarfile.open(archive) as t:t.extractall(target,filter='data')
    if '--android' in sys.argv:
        from install_export_tools import main as android
        android()
if __name__=='__main__':main()
