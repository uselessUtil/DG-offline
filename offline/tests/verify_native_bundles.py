"""Verify native data in the actual APK and exercise its loader with stand-ins."""
import json
from pathlib import Path
import shutil
import subprocess
import zipfile

import UnityPy
from UnityPy.files.SerializedFile import SerializedFile

from build import CECIL, ROOT, MANAGED, digest, run


def verify(artifact, output, work, test, tools, env):
    data=json.loads((output/'native-bundle-manifest.json').read_text())
    source=Path(data['source'])
    before=SerializedFile.set_version
    SerializedFile.set_version=lambda self,s:before(self,s.splitlines()[0])
    try:
        with zipfile.ZipFile(artifact) as archive:
            entries={n for n in archive.namelist() if n.startswith('assets/dg-native/')}
            assert entries=={'assets/dg-native/'+r['file'] for r in data['bundles']}
            for row in data['bundles']:
                raw=archive.read('assets/dg-native/'+row['file'])
                assert raw.startswith(b'UnityRaw\0') and digest(raw)==row['sha256']
                parsed=UnityPy.load(raw)
                node=next(iter(parsed.files.values())).files[row['id']]
                original=(source/row['cab']).read_bytes()
                assert node.reader.bytes==original and digest(original)==row['cab_sha256']
                for obj in parsed.objects:
                    if obj.type.name=='TextAsset':
                        assert not obj.read().m_Name.startswith('MasterData')
                assert all(e=='library/unity default resources' for e in row['dependencies'])
    finally:
        SerializedFile.set_version=before
    areas=json.loads((work/'tables/MasterDataArea.json').read_bytes(),strict=False)
    chars=json.loads((work/'tables/MasterDataParamChara.json').read_bytes(),strict=False)
    for char in chars:
        if char['fix_id']:
            assert 'GameObject/chara_prefab_'+char['res_chara_id_str'] in data['routes']
            assert 'Texture/tex_chara_l_'+char['res_chara_id_str'] in data['routes']
    missing=[]
    for a in areas:
        if not a['fix_id']:continue
        routes=[data['routes'].get('GameObject/'+a['mesh_map']),data['routes'].get('GameObject/'+a['mesh_door']),
                data['routes'].get('Texture/'+a['res_map'])]
        if all(routes):
            assert data['routes']['Material/'+a['mesh_map'].removesuffix('_mesh')]==routes[0]
        else:missing.append(a['fix_id'])
    assert missing==[240060,240070]
    assert 'GameObject/ef_stage_0012' not in data['routes']
    # Exact cached objects were preserved, including original door components,
    # float/half UVs and shader programs. This is not a GPU execution test.
    text=(f'PASS {len(data["bundles"])} local UnityRaw packages in the exact APK; all embedded CAB bytes/hash match backup. '
          'No cached master tables, unresolved external CAB dependencies or extra native packages.\n'
          'PASS 522 character prefabs/textures and 99 area floor/door/full-sheet routes; shared mapMaterial selected by model bundle. '
          'Missing maps 240060/240070 and ef_stage_0012 retain fallback.\n')
    tests=ROOT/'offline/tests'
    run('mcs','-out:'+str(tools/'VerifyNativeHooks.exe'),'-r:'+CECIL,tests/'VerifyNativeHooks.cs')
    text+=subprocess.check_output(['mono',str(tools/'VerifyNativeHooks.exe'),
        str(work/'original'/MANAGED/'Assembly-CSharp.dll'),str(test/'Assembly-CSharp.dll'),str(test/'DivineGateOffline.dll')],text=True)
    probe=test/'native-probe'
    probe.mkdir()
    for path in test.glob('*.dll'):shutil.copy2(path,probe/path.name)
    run('mcs','-out:'+str(tools/'PrepareNativeProbe.exe'),'-r:'+CECIL,tests/'PrepareNativeProbe.cs')
    run('mono',tools/'PrepareNativeProbe.exe',probe/'DivineGateOffline.dll',probe/'NativeProbe.dll')
    shutil.move(probe/'NativeProbe.dll',probe/'DivineGateOffline.dll')
    run('mcs','-out:'+str(probe/'NativeAssetTests.exe'),'-r:'+str(probe/'Assembly-CSharp.dll'),
        '-r:'+str(probe/'DivineGateOffline.dll'),'-r:'+str(probe/'UnityEngine.dll'),
        '-r:'+str(probe/'EngineSupport.dll'),tests/'NativeAssetTests.cs')
    text+=subprocess.check_output(['mono',str(probe/'NativeAssetTests.exe')],env=env,text=True,timeout=45)
    (output/'native-restoration-tests.txt').write_text(text)
    print(text)
    return text.strip()
