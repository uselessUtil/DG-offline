"""Recover local-only rule adapters from the fixed r17 APK; never ship their bodies."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import tempfile
import zipfile

ILSPY = '/opt/ilspy/tools/net8.0/any/ilspycmd.dll'
DOTNET = '/opt/dotnet/dotnet'
ASSEMBLY_SHA = '7c4c7bb7c19e9d1cbf897a681ffad221be029df8556eb534b3cebd0937a63d6a'
CONTRACTS = {
    'LegacyEnemyAttack.cs': ('BattleManager', 'EnemyAttackAllDamage',
        'e181c30a0303d89f3a101604a84f1bc3df804a24d5938e48e949f8390f51be42',
        '47af5ded516cc7923a0aaaa5d97dc1d2ff268c4675d8ee7d758cc70bef145e2e'),
    'LegacySkillStatus.cs': ('BattleUtil', 'SkillUpdate_StatusAilment',
        '4f18e79d1dfe28fa8f7daeb956a341050b6911e8bf8b184dc55dd7536c402237',
        '14e440b13862780e44863b81fa14b59ce502a04feb4bbd0695d1ac4b0cd97e0d'),
}
def sha(raw):
    return hashlib.sha256(raw).hexdigest()

def normalized(text):
    return '\n'.join(line.strip() for line in text.splitlines()
                     if line.strip() and not line.lstrip().startswith('//'))

def replace(text, old, new, count=1):
    if text.count(old) != count:
        raise ValueError('Source adapter contract changed: ' + old)
    return text.replace(old, new)

def adapt(method, enemy):
    if enemy:
        method = replace(method, '\tprivate int ', 'int ')
        for old, new in [('m_EnemyParam', 'Enemies'),
                         ('m_BattleHandCardParam', 'battle.m_BattleHandCardParam'),
                         ('m_EnemySkillHandCardRemake', 'battle.m_EnemySkillHandCardRemake')]:
            method, count = re.subn(r'\b' + old + r'\b', new, method)
            if not count:
                raise ValueError('Missing source field: ' + old)
        method = replace(method, 'UnityUtil.ChkObjectEnabled(m_EnemyObjectCharaPos[i])', 'true')
        for call in ['ChangeCardDesign', 'BattleUtil.PlayHandCardEffect']:
            method, count = re.subn(r'(?m)^\s*' + re.escape(call) + r'\([^\n]+\);\n', '', method)
            if count != 1:
                raise ValueError('Unexpected visual call count: ' + call)
        prefix = 'using System;\nnamespace DivineGateGodot;\npublic sealed partial class QuestRun\n{\n'
    else:
        method = replace(method, '\tprivate static void SkillUpdate_StatusAilment(', 'void ApplyAilments(')
        method = replace(method, 'Debug.LogError(', 'UnityEngine.Debug.LogError(')
        # The r17 entry hook belongs to the Unity patch, not the Godot adapter.
        method = replace(method, 'DivineGateOffline.LegacyAilment290(activity, nAtk, nHPMax) || ', '')
        method, count = re.subn(r'(?m)^\s*instance\.m_EnemyTurnCount\[[^\n]+\]\.text = [^\n]+;\n', '', method)
        if count != 3:
            raise ValueError('Unexpected Unity label write count')
        prefix = 'namespace DivineGateGodot;\npublic sealed partial class QuestRun {\n'
    return '// Generated locally from the supplied r17 APK; third-party content, not project MIT.\n' + prefix + method + '\n}\n'

def recover(source, expected, project):
    source, project = Path(source), Path(project)
    if not source.is_file():
        raise FileNotFoundError(f'Provide the fixed r17 reference APK: {source}; expected SHA-256 {expected}')
    if sha(source.read_bytes()) != expected:
        raise ValueError('Unsupported r17 APK: SHA-256 mismatch')
    version = subprocess.check_output([DOTNET, ILSPY, '--version'], text=True)
    if 'ilspycmd: 9.1.0.7988' not in version:
        raise ValueError('Use the Dockerfile ILSpy version 9.1.0.7988')
    generated = {}
    with tempfile.TemporaryDirectory(prefix='dg-rules-') as temp:
        temp = Path(temp)
        with zipfile.ZipFile(source) as archive:
            for name in archive.namelist():
                if name.startswith('assets/bin/Data/Managed/') and name.endswith('.dll'):
                    (temp / Path(name).name).write_bytes(archive.read(name))
        assembly = temp / 'Assembly-CSharp.dll'
        if sha(assembly.read_bytes()) != ASSEMBLY_SHA:
            raise ValueError('Reference game assembly changed')
        for filename, (typ, name, method_sha, output_sha) in CONTRACTS.items():
            code = subprocess.check_output([DOTNET, ILSPY, '-t', typ, str(assembly)], text=True)
            matches = re.findall(r'(?ms)^\tprivate (?:static )?(?:int|void) ' + re.escape(name)
                                 + r'\([^\n]+\)\n\t\{.*?^\t\}', code)
            if len(matches) != 1 or sha(matches[0].encode()) != method_sha:
                raise ValueError('Decompiled method differs from reference: ' + name)
            text = adapt(matches[0], typ == 'BattleManager')
            if sha(normalized(text).encode()) != output_sha:
                raise ValueError('Generated rule differs from validated adapter: ' + filename)
            generated[filename] = text
    # Validate all inputs/results before replacing any generated source.
    stale_files = [project / name for name in generated if (project / name).exists()]
    for stale in stale_files:
        if sha(normalized(stale.read_text()).encode()) != CONTRACTS[stale.name][3]:
            raise ValueError('Refusing to remove modified legacy adapter: ' + str(stale))
    out = project / 'Generated'
    out.mkdir(parents=True, exist_ok=True)
    for filename, text in generated.items():
        (out / filename).write_text(text, encoding='utf-8')
    # Previous builds placed these files directly in the generated project.
    for stale in stale_files:
        stale.unlink()
    (out / 'provenance.json').write_text(json.dumps({
        'source_apk_sha256': expected, 'assembly_sha256': ASSEMBLY_SHA,
        'decompiler': 'ilspycmd 9.1.0.7988',
        'files': {name: {'sha256': sha(text.encode()), 'normalized_sha256': CONTRACTS[name][3]}
                  for name, text in generated.items()},
        'distribution': 'Local recovered game code; not included in the public source checkout.',
    }, indent=2) + '\n')
    return out

if __name__ == '__main__':
    from prepare import SOURCE, EXPECTED, PROJECT
    print(recover(SOURCE, EXPECTED, PROJECT))
