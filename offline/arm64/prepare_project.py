"""Prepare an independent project in isolated tooling; never edit the APK."""
import argparse
import hashlib
import json
import re
from pathlib import Path
import shutil
from migrate_api import migrate

ROOT = Path('/workspace')
SOURCE_HASH = 'd98b73348e2f17a576f430153e00eec43caecb08422ed53a005435af023bf029'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--recovered', type=Path, required=True)
    parser.add_argument('--firstpass', type=Path, required=True)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    source_report = json.loads((ROOT/'artifacts/1.0.6-offline-prototype17-area-map-crops-wiki-preserved/build-report.json').read_text())
    source = Path(source_report['output'])
    if hashlib.file_digest(source.open('rb'), 'sha256').hexdigest() != SOURCE_HASH:
        raise ValueError('Unexpected prototype 17 source')
    if args.project.exists():
        raise FileExistsError('Use a new project directory')
    if args.project.resolve().is_relative_to(ROOT):
        raise ValueError('Generated/extracted project must remain in container temporary storage')
    shutil.copytree(args.recovered, args.project)
    scripts = args.project/'Assets/Scripts'
    if not (args.project/'Assets/Plugins/Assembly-CSharp-firstpass').exists():
        shared = scripts/'RecoveredFirstpass'
        for src in args.firstpass.rglob('*.cs'):
            if 'Properties' in src.parts:
                continue
            dest = shared/src.relative_to(args.firstpass)
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dest)
    # Recovered plug-in sources refer to each other; compile them together,
    # while retaining each script .meta GUID used by the recovered prefabs.
    plugins = args.project/'Assets/Plugins'
    if plugins.exists():
        dest = scripts/'RecoveredPlugins'
        dest.mkdir()
        for folder in list(plugins.iterdir()):
            if folder.is_dir() and any(folder.rglob('*.cs')):
                shutil.move(str(folder), str(dest/folder.name))
                meta = folder.with_name(folder.name+'.meta')
                if meta.exists():
                    shutil.move(str(meta), str(dest/meta.name))
    assembly_info = args.project.parent/'recovered-assembly-info'
    for src in list(scripts.rglob('AssemblyInfo.cs')):
        dest = assembly_info/src.relative_to(scripts)
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.move(str(src), str(dest))
    adapter = scripts/'OfflineArm64'
    adapter.mkdir()
    for name in ['Runtime.cs', 'FreeChipShop.cs', 'CollabGacha.cs']:
        text = (ROOT/'offline'/name).read_text()
        if name == 'Runtime.cs':
            text, count = re.subn(r'public const string Build = "1\.0\.6 offline prototype \d+";',
                'public const string Build = "1.0.6 ARM64 migration 1 (unverified)";', text)
            if count != 1:
                raise ValueError('Unexpected offline adapter build label')
            text, notice_count = re.subn(r'"Offline prototype \d+"',
                '"ARM64 migration 1 (unverified)"', text)
            if notice_count != 1:
                raise ValueError('Unexpected offline adapter notice label')
        if name == 'FreeChipShop.cs':
            old = 'throw new InvalidOperationException("Missing local shop save accessor");'
            if text.count(old) != 1:
                raise ValueError('Unexpected shop accessor')
            text = text.replace(old, 'return server.DGOfflineShopPlayer();')
        (adapter/name).write_text(text)
    old_build = Path(source_report['work_directory'])/'build'
    shutil.copy2(old_build/'Images.cs', adapter/'Images.cs')
    text = (old_build/'RecoveredImages.cs').read_text()
    old = 'public static byte[] Payload(int index) { return null; }'
    if text.count(old) != 1:
        raise ValueError('Unexpected recovered image reader')
    text = text.replace(old, '''public static byte[] Payload(int index) {
 var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>("OfflineImages/" + index);
 if (asset == null) throw new System.IO.FileNotFoundException("Offline image " + index);
 byte[] bytes = asset.bytes;
 UnityEngine.Resources.UnloadAsset(asset);
 return bytes;
}''')
    (adapter/'RecoveredImages.cs').write_text(text)
    images = args.project/'Assets/Resources/OfflineImages'
    images.mkdir(parents=True)
    image_manifest = json.loads((source.parent/'recovered-images.json').read_text())
    image_rows = []
    by_range = {(row['offset'], row['size']): row['sha256'] for row in image_manifest['payloads'].values()}
    with (old_build/'RecoveredImages.bin').open('rb') as stream:
        for line in (old_build/'image-offsets.tsv').read_text().splitlines():
            index, offset, size = map(int, line.split('\t'))
            stream.seek(offset)
            raw = stream.read(size)
            digest = hashlib.sha256(raw).hexdigest()
            if digest != by_range[(offset, size)]:
                raise ValueError('Image differs from prototype 17')
            (images/(str(index)+'.bytes')).write_bytes(raw)
            image_rows.append({'index': index, 'sha256': digest, 'size': size})
    # Preserve original 4.1 serialized assets for Unity's importer to upgrade.
    (args.project/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 2019.4.40f1\nm_EditorVersionWithRevision: 2019.4.40f1 (ffc62b691db5)\n')
    editor = args.project/'Assets/Editor'
    editor.mkdir(exist_ok=True)
    shutil.copy2(ROOT/'offline/arm64/BuildArm64.cs', editor/'BuildArm64.cs')
    (args.project/'Assets/link.xml').write_text('<linker>\n <assembly fullname="Assembly-CSharp" preserve="all"/>\n</linker>\n')
    changed = migrate(args.project)
    report = {
        'status': 'source migration only; no ARM64 APK built',
        'source': str(source), 'source_sha256': SOURCE_HASH,
        'project': str(args.project), 'unity': '2019.4.40f1',
        'android_package': 'jp.gungho.dg.offline106.arm64',
        'original_scenes': len(list(args.recovered.rglob('*.unity'))),
        'original_prefabs': len(list(args.recovered.rglob('*.prefab'))),
        'original_animation_clips': len(list(args.recovered.rglob('*.anim'))),
        'original_assets_dropped': 0,
        'api_migration_files': sorted(set(changed)),
        'image_count': len(image_rows), 'images': image_rows,
        'limitations': ['Editor activation required', 'C# API migration in progress',
                        'Unity asset import, IL2CPP, shaders, Android launch and gameplay unverified']
    }
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps({k: v for k, v in report.items() if k != 'images'}, indent=2))


if __name__ == '__main__':
    main()
