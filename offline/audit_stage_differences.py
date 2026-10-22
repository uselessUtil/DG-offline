"""Read-only, version-aware stage comparison. Run only in the project Docker.

Raw field differences and effective encounter differences are deliberately separate:
fixed enemy groups ignore num_min/max, and newly added zero action fields do not
constitute changed enemy behavior. This does not import or execute backup scripts.
"""
import argparse
from collections import Counter, defaultdict
import csv
import hashlib
import json
from math import gcd
from pathlib import Path
import re
import tempfile
import zipfile

import UnityPy
from UnityPy.files.SerializedFile import SerializedFile


def digest(data):
    return hashlib.sha256(data).hexdigest()


def write(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')


def raw_text(value):
    return value.m_Script.encode('utf8', 'surrogateescape') if isinstance(value.m_Script, str) else value.m_Script


def clean(text):
    return re.sub(r'\[(?:[0-9a-fA-F]{6}|-)\]', '', text or '').replace('|', '／').replace('\n', ' ')


def fields_diff(a, b):
    return {k: {'apk': (a or {}).get(k), 'backup': (b or {}).get(k)}
            for k in sorted(set(a or {}) | set(b or {})) if (a or {}).get(k) != (b or {}).get(k)}


def differences(a, b, prefix=''):
    """Small path/value list for semantic snapshots, retaining actual values."""
    if a == b:
        return []
    if isinstance(a, dict) and isinstance(b, dict):
        result = []
        for k in sorted(set(a) | set(b)):
            result += differences(a.get(k), b.get(k), prefix + '/' + str(k))
        return result
    if isinstance(a, list) and isinstance(b, list) and len(a) == len(b):
        result = []
        for k, (x, y) in enumerate(zip(a, b)):
            result += differences(x, y, prefix + '/' + str(k))
        return result
    return [{'path': prefix, 'apk': a, 'backup': b}]


class Dataset:
    def __init__(self, tables):
        self.tables = tables
        self.index = {}
        self.duplicates = {}
        for name, rows in tables.items():
            if isinstance(rows, list) and all(isinstance(r, dict) and 'fix_id' in r for r in rows):
                self.index[name] = {}
                duplicate = []
                for row in rows:
                    if row['fix_id'] in self.index[name]:
                        duplicate.append(row['fix_id'])
                    else:  # MasterDataUtil returns the first matching ID.
                        self.index[name][row['fix_id']] = row
                if duplicate:
                    self.duplicates[name] = duplicate
        self.missing = set()

    def get(self, table, key):
        row = self.index.get(table, {}).get(key)
        if row is None:
            self.missing.add((table, key))
        return row

    def char(self, key):
        row = self.get('MasterDataParamChara', key)
        return {k: row.get(k) for k in ('name', 'draw_id', 'res_chara_id_str', 'element', 'kind',
                'size_width', 'size_height', 'pivot', 'side_offset')} if row else {'missing_character': key}

    def enemy(self, key):
        row = self.get('MasterDataEnemyChara', key)
        if row is None:
            return {'missing_enemy': key}
        return {k: v for k, v in row.items() if k != 'fix_id' and not k.startswith('act_')}

    def actions(self, key):
        row = self.get('MasterDataEnemyChara', key)
        return {k: v for k, v in (row or {}).items() if k.startswith('act_') and v}

    def chain(self, key):
        result, seen = [], set()
        while key:
            if key in seen:
                result.append({'cycle': key})
                break
            seen.add(key)
            g = self.get('MasterDataEnemyGroup', key)
            if g is None:
                result.append({'missing_group': key})
                break
            slots = [g.get('enemy_id_' + str(i), 0) for i in range(1, 8)]
            if g['fix'] == 2:
                enemies = [v for v in slots if v]
                selection = {'mode': 'fixed', 'count': len(enemies)}
            else:
                enemies = slots[:slots.index(0)] if 0 in slots else slots
                selection = {'mode': 'random', 'min': g['num_min'], 'max_exclusive_unless_equal': g['num_max']}
            result.append({'group_id': key, 'selection': selection, 'enemy_ids': enemies,
                           'chain_id': g['chain_id'], 'chain_turn_offset': g['chain_turn_offset'],
                           'enemies': [self.enemy(v) for v in enemies],
                           'characters': [self.char(self.enemy(v).get('chara_id', 0)) for v in enemies],
                           'actions': [self.actions(v) for v in enemies]})
            key = g['chain_id']
        return result

    def floors(self, quest_id):
        # FieldManager uses the first contiguous run in array order, not sorting
        # by fix_id/under and not filtering active flags here.
        rows = self.tables['MasterDataQuestFloor']
        start = next((i for i, r in enumerate(rows) if r['quest_id'] == quest_id), None)
        if start is None:
            return []
        result = []
        for r in rows[start:]:
            if r['quest_id'] != quest_id:
                break
            result.append(r)
        return result

    def panels(self, group_id):
        if not group_id:
            return []
        g = self.get('MasterDataParamPanelGroup', group_id)
        if g is None:
            return [{'missing_panel_group': group_id}]
        result = []
        for i in range(1, 8):
            pid = g['panel_id_' + str(i)]
            if not pid:
                break
            panel = self.get('MasterDataParamPanel', pid)
            result.append({k: v for k, v in panel.items() if k != 'fix_id'} if panel else {'missing_panel': pid})
        return result

    def snapshot(self, quest_id):
        quest = self.index['MasterDataQuest'].get(quest_id)
        if not quest:
            return None
        area = self.get('MasterDataArea', quest['area_id']) or {}
        floors = self.floors(quest_id)
        bosses, normal, boards, panels = [], [], [], []
        for f in floors:
            bosses.append(self.chain(f['boss_group_id']))
            normal.append({str(i): self.chain(f['enemy_group_id_' + str(i)]) for i in range(1, 8)})
            boards.append({'category_id': f['pattern_category'],
                           'category': self.get('MasterDataCategoryPattern', f['pattern_category']),
                           'stars_id': f['pattern_expect'],
                           'stars': self.get('MasterDataExpectPattern', f['pattern_expect']),
                           'question_ct': f['question_ct']})
            panels.append({kind: {str(i): self.panels(f[kind + '_group_id_' + str(i)]) for i in range(1, 8)}
                           for kind in ['trap', 'item']})
        guerrilla = [r for r in self.tables.get('MasterDataGuerrillaBoss', [])
                     if r.get('quest_id') == quest_id and r.get('active') == 2]
        return {'quest': quest, 'area': area, 'floors': floors, 'bosses': bosses,
                'normal': normal, 'boards': boards, 'panels': panels,
                'guerrilla': [{'rule': {k: v for k, v in r.items() if k != 'fix_id'},
                              'encounter': self.chain(r['enemy_group_id'])} for r in guerrilla],
                'area_amend': [{k: v for k, v in r.items() if k != 'fix_id'} for r in self.tables.get('MasterDataAreaAmend', [])
                               if r.get('area_id') == quest['area_id'] and r.get('active') == 2]}


def distribution(values):
    counts = Counter(json.dumps(v, ensure_ascii=False, sort_keys=True) for v in values)
    divisor = 0
    for n in counts.values():
        divisor = gcd(divisor, n)
    return [{'value': json.loads(v), 'weight': n // divisor} for v, n in sorted(counts.items())]


def flow(chains):
    def one(chain):
        return [{'selection': n.get('selection'), 'characters':
                 (distribution([e.get('chara_id') for e in n.get('enemies', [])])
                  if n.get('selection', {}).get('mode') == 'random' else [e.get('chara_id') for e in n.get('enemies', [])]),
                 'turn_offset_next': n.get('chain_turn_offset') if i + 1 < len(chain) else None,
                 **{k: n[k] for k in ('cycle', 'missing_group') if k in n}}
                for i, n in enumerate(chain)]
    return [one(c) for c in chains]


def contents(chains, key):
    return [[distribution(n.get(key, [])) if n.get('selection', {}).get('mode') == 'random'
             else n.get(key) for n in chain] for chain in chains]


def action_references(chains):
    # Empty actions on an extra phase/candidate do not mean new-version AI.
    return sorted({json.dumps({'enemy_id': eid, 'actions': action}, sort_keys=True)
                   for chain in chains for n in chain
                   for eid, action in zip(n.get('enemy_ids', []), n.get('actions', [])) if action})


def compare_common_when_roster_changes(a, b, ca, cb, prefix):
    chains_a = a['bosses'] if prefix == 'boss' else [v for f in a['normal'] for v in f.values()]
    chains_b = b['bosses'] if prefix == 'boss' else [v for f in b['normal'] for v in f.values()]
    category = 'boss_phases' if prefix == 'boss' else 'normal_groups'
    if ca[category] == cb[category]:
        return
    # Removed/added phases are already reported as composition changes. Only
    # separately claim stat/display changes for records shared by both rosters.
    for key, source_key, id_key in [('enemies', 'enemies', 'enemy_ids'), ('display', 'characters', 'enemy_ids')]:
        aa = {eid: value for c in chains_a for n in c for eid, value in zip(n.get(id_key, []), n.get(source_key, []))}
        bb = {eid: value for c in chains_b for n in c for eid, value in zip(n.get(id_key, []), n.get(source_key, []))}
        common = set(aa) & set(bb)
        ca[prefix + '_' + key] = {str(i): aa[i] for i in sorted(common)}
        cb[prefix + '_' + key] = {str(i): bb[i] for i in sorted(common)}


def components(s):
    if s is None:
        return {}
    normal = [v for floor in s['normal'] for v in floor.values()]
    q, a = s['quest'], s['area']
    return {
        'boss_phases': flow(s['bosses']),
        'boss_enemies': contents(s['bosses'], 'enemies'),
        'boss_display': contents(s['bosses'], 'characters'),
        'boss_new_actions': action_references(s['bosses']),
        'floor_structure': [{'under': f['under']} for f in s['floors']],
        'floor_flags': [f['active'] for f in s['floors']],
        'board_patterns': s['boards'],
        'normal_groups': flow(normal),
        'normal_enemies': contents(normal, 'enemies'),
        'normal_display': contents(normal, 'characters'),
        'normal_new_actions': action_references(normal),
        'traps_items': s['panels'],
        'quest_rules': {k: v for k, v in q.items() if k not in ('fix_id', 'quest_name', 'story', 'clear_unit_msg', 'active')},
        'quest_flags': q.get('active'),
        'text': {k: q.get(k) for k in ('quest_name', 'story', 'clear_unit_msg')},
        'area_visuals': {k: v for k, v in a.items() if k.startswith(('res_', 'mesh_', 'bgm_')) or k == 'area_effect'},
        'area_rules': {k: v for k, v in a.items() if k.startswith('cost') or k in ('area_element', 'type', 'pre_area', 'event_id')},
        'guerrilla_schedule': [{**r, 'rule': {k: v for k, v in r['rule'].items() if k != 'quest_id'}}
                              for r in s['guerrilla']],
        'area_amend_schedule': s['area_amend'],
    }


LABELS = {'boss_phases': 'Boss 階段／組成', 'boss_enemies': 'Boss 能力／掉落', 'boss_display': 'Boss 角色／外觀',
          'boss_new_actions': 'Boss 新版行動', 'floor_structure': '樓層數／地下層', 'floor_flags': '樓層 active 欄位',
          'board_patterns': '地板種類／星數', 'normal_groups': '一般怪物組成／串接', 'normal_enemies': '一般怪物能力／掉落',
          'normal_display': '一般怪物角色／外觀', 'normal_new_actions': '一般怪物新版行動', 'traps_items': '陷阱／寶箱',
          'quest_rules': '體力／獎勵／連戰等規則', 'quest_flags': '關卡啟用', 'text': '名稱／文字',
          'area_visuals': '地圖／門／背景／音樂引用', 'area_rules': '移動 SP／屬性／開放條件',
          'guerrilla_schedule': '亂入排程／內容', 'area_amend_schedule': '區域加成排程'}


def boss_label(snapshot):
    if not snapshot:
        return '—'
    floors = []
    for chain in snapshot['bosses']:
        if not chain:
            continue
        phases = []
        for node in chain:
            names = [clean(c.get('name', '?')) for c in node.get('characters', [])]
            select = node.get('selection', {})
            if select.get('mode') == 'random':
                lo, hi = select['min'], select['max_exclusive_unless_equal']
                count = str(lo) if hi <= lo else str(lo) + '~' + str(hi - 1)
                phases.append('抽選{' + '、'.join(dict.fromkeys(names)) + '}×' + count)
            else:
                phases.append('＋'.join(names) or '?')
        floors.append(' → '.join(phases))
    return ' / '.join(floors) or '無 Boss'


def table_differences(original, backup):
    result = {}
    for name in sorted(set(original.tables) | set(backup.tables)):
        a, b = original.tables.get(name), backup.tables.get(name)
        ai, bi = original.index.get(name), backup.index.get(name)
        if ai is not None and bi is not None:
            shared = set(ai) & set(bi)
            changed = {str(i): fields_diff(ai[i], bi[i]) for i in sorted(shared) if ai[i] != bi[i]}
            result[name] = {'apk_rows': len(a), 'backup_rows': len(b), 'added_ids': sorted(set(bi) - set(ai)),
                            'removed_ids': sorted(set(ai) - set(bi)), 'changed': changed,
                            'shared_fields': sorted(set().union(*(r.keys() for r in a)) & set().union(*(r.keys() for r in b))),
                            'added_fields': sorted(set().union(*(r.keys() for r in b)) - set().union(*(r.keys() for r in a)))}
        else:
            result[name] = {'apk_rows': len(a) if a is not None else None,
                            'backup_rows': len(b) if b is not None else None,
                            'indexed': False, 'equal': a == b,
                            'note': 'Non-fix_id legacy data or table present on only one side; not interpreted as stage configuration.'}
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-apk', type=Path, required=True)
    parser.add_argument('--current-apk', type=Path, required=True)
    parser.add_argument('--backup', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    out = args.output
    out.mkdir(parents=True, exist_ok=True)
    if (out / 'summary.json').exists():
        raise FileExistsError('Use a new audit output directory')
    (out / 'quests').mkdir(exist_ok=True)
    apk_hashes = {str(p): digest(p.read_bytes()) for p in (args.original_apk, args.current_apk)}
    original_tables, backup_tables, proof = {}, {}, {}
    with tempfile.TemporaryDirectory(prefix='dg-stage-audit-', dir='/tmp') as temp:
        temp = Path(temp)
        with zipfile.ZipFile(args.original_apk) as source, zipfile.ZipFile(args.current_apk) as current:
            for name in source.namelist():
                if name.startswith('assets/bin/Data/') and not name.startswith('assets/bin/Data/Managed/') and not name.endswith('/'):
                    source.extract(name, temp)
            env = UnityPy.load(str(temp / 'assets/bin/Data'))
            for obj in env.objects:
                if obj.type.name != 'TextAsset':
                    continue
                value = obj.read()
                if not value.m_Name.startswith('MasterData'):
                    continue
                name = value.m_Name
                raw = raw_text(value)
                original_tables[name] = json.loads(raw, strict=False)
                asset = 'assets/bin/Data/' + Path(obj.assets_file.name).name
                same = source.read(asset) == current.read(asset)
                if not same:
                    raise ValueError('Current master asset changed: ' + asset)
                proof[name] = {'asset': asset, 'asset_sha256': digest(source.read(asset)),
                               'current_asset_bytes_equal_original': same, 'table_sha256': digest(raw)}
    cabs = []
    old = SerializedFile.set_version
    SerializedFile.set_version = lambda self, value: old(self, value.splitlines()[0])
    try:
        inventory = json.loads((Path(__file__).resolve().parents[1] / 'artifacts/cache-backup-audit-2026-10-22/objects.json').read_text())
        paths = sorted({r['cab'] for r in inventory if r['type'] == 'TextAsset' and r.get('name', '').startswith('MasterData')})
        for relative in paths:
            path = args.backup / relative
            cabs.append({'path': str(path), 'sha256': digest(path.read_bytes())})
            for obj in UnityPy.load(path.read_bytes()).objects:
                if obj.type.name == 'TextAsset':
                    value = obj.read()
                    if value.m_Name.startswith('MasterData'):
                        if value.m_Name in backup_tables:
                            raise ValueError('Ambiguous cached master table ' + value.m_Name)
                        backup_tables[value.m_Name] = json.loads(raw_text(value), strict=False)
    finally:
        SerializedFile.set_version = old
    overlay = None
    build_report = args.current_apk.parent/'build-report.json'
    if build_report.exists():
        metadata = json.loads(build_report.read_text())
        if metadata.get('backup_stage_import'):
            if metadata['sha256'] != apk_hashes[str(args.current_apk)]:
                raise ValueError('Current APK does not match overlay build report')
            with zipfile.ZipFile(args.current_apk) as archive:
                adapter = archive.read('assets/bin/Data/Managed/DivineGateOffline.dll')
            overlay = {}
            for name,row in metadata['backup_stage_import']['tables'].items():
                path = args.current_apk.parent/'stage-tables'/(name+'.json')
                data = path.read_bytes()
                if digest(data)!=row['sha256'] or data.decode().encode('utf-16le') not in adapter:
                    raise ValueError('Stage override JSON/hash not present in actual adapter: '+name)
                original_tables[name] = json.loads(data,strict=False)
                overlay[name] = row
    original, backup = Dataset(original_tables), Dataset(backup_tables)
    raw = table_differences(original, backup)
    write(out / 'table-differences.json', raw)
    write(out / 'backup-only-tables.json', {k: v for k, v in backup.tables.items() if k not in original.tables})
    write(out / 'source-proof.json', {'apk_sha256': apk_hashes, 'cached_master_cabs': cabs, 'original_tables': proof,
                                     'current_compiled_stage_overrides':overlay})
    quests_a, quests_b = original.index['MasterDataQuest'], backup.index['MasterDataQuest']
    # Renumbered quests are not automatically missing/new gameplay. Only pair
    # unique exact (area_id, quest_name) matches among otherwise unmatched IDs.
    only_a, only_b = set(quests_a) - set(quests_b), set(quests_b) - set(quests_a)
    remaps = {}
    for qid in sorted(only_a):
        q = quests_a[qid]
        aa = [i for i in only_a if (quests_a[i]['area_id'], quests_a[i]['quest_name']) == (q['area_id'], q['quest_name'])]
        bb = [i for i in only_b if (quests_b[i]['area_id'], quests_b[i]['quest_name']) == (q['area_id'], q['quest_name'])]
        if len(aa) == len(bb) == 1:
            remaps[qid] = bb[0]
    pairs = [(i, i if i in quests_b else remaps.get(i)) for i in sorted(quests_a)]
    pairs += [(None, i) for i in sorted(only_b - set(remaps.values()))]
    rows, category_counts = [], Counter()
    area_rows = defaultdict(list)
    for apk_qid, backup_qid in pairs:
        qid = apk_qid if apk_qid is not None else backup_qid
        # ID 0 is a disabled integrity placeholder, not a selectable quest.
        if qid == 0:
            continue
        a = original.snapshot(apk_qid) if apk_qid is not None else None
        b = backup.snapshot(backup_qid) if backup_qid is not None else None
        ca, cb = components(a), components(b)
        if a and b:
            compare_common_when_roster_changes(a, b, ca, cb, 'boss')
            compare_common_when_roster_changes(a, b, ca, cb, 'normal')
        changes = [k for k in LABELS if ca.get(k) != cb.get(k)] if a and b else []
        state = ('shared' if apk_qid == backup_qid else 'renumbered') if a and b else 'backup_only' if b else 'apk_only'
        s = a or b
        area_id = s['quest']['area_id']
        area_quests = [q for q in (quests_a if a else quests_b).values() if q['area_id'] == area_id and q['fix_id'] != 0]
        qno = next(i + 1 for i, q in enumerate(area_quests) if q['fix_id'] == qid)
        boss_diff = a and b and any(k in changes for k in ['boss_phases', 'boss_enemies', 'boss_display', 'boss_new_actions'])
        record = {'quest_id': qid, 'apk_quest_id': apk_qid, 'backup_quest_id': backup_qid,
                  'area_id': area_id, 'area': clean(s['area'].get('area_name', '')),
                  'area_type': s['area'].get('type'), 'q_label': 'Q' + str(qno),
                  'quest': clean(s['quest']['quest_name']), 'state': state,
                  'apk_active': a['quest']['active'] if a else None, 'backup_active': b['quest']['active'] if b else None,
                  'apk_floor_count': len(a['floors']) if a else None, 'backup_floor_count': len(b['floors']) if b else None,
                  'categories': changes, 'boss_difference': bool(boss_diff),
                  'apk_boss': boss_label(a), 'backup_boss': boss_label(b),
                  'apk_boss_max_phases': max((len(c) for c in a['bosses']), default=0) if a else None,
                  'backup_boss_max_phases': max((len(c) for c in b['bosses']), default=0) if b else None}
        rows.append(record)
        area_rows[area_id].append(record)
        if a and b:
            category_counts.update(changes)
        if changes or state != 'shared':
            write(out / 'quests' / (str(qid) + '.json'), {'summary': record,
                  'component_differences': {k: differences(ca.get(k), cb.get(k)) for k in changes},
                  'apk': a, 'backup': b})
    # Regression against the previously reproduced actual APK phase routing.
    q1 = next(r for r in rows if r['quest_id'] == 86)
    assert q1['apk_boss_max_phases'] == (1 if overlay else 3) and q1['backup_boss_max_phases'] == 1
    assert original.chain(100270)[0]['selection']['count'] == backup.chain(100270)[0]['selection']['count'] == 1
    shared = [r for r in rows if r['state'] in ('shared', 'renumbered')]
    phase_fixtures = []
    for r in shared:
        if 'boss_phases' not in r['categories']:
            continue
        for fi, f in enumerate(original.floors(r['apk_quest_id'])):
            aa = original.chain(f['boss_group_id'])
            bf = backup.floors(r['backup_quest_id'])[fi]
            bb = backup.chain(bf['boss_group_id'])
            if flow([aa]) != flow([bb]):
                phase_fixtures.append({'quest_id': r['quest_id'], 'floor_index': fi,
                                       'apk_groups': [n['group_id'] for n in aa],
                                       'backup_groups': [n['group_id'] for n in bb]})
    write(out / 'boss-phase-fixtures.json', phase_fixtures)
    shared_enemy_changes = {i: {k: v for k, v in d.items() if not k.startswith('act_')}
                            for i, d in raw['MasterDataEnemyChara']['changed'].items()
                            if any(not k.startswith('act_') for k in d)}
    nonzero_actions = []
    for eid, e in backup.index['MasterDataEnemyChara'].items():
        actions = backup.actions(eid)
        if actions:
            nonzero_actions.append({'enemy_id': eid, 'character': backup.char(e['chara_id']), 'actions': actions,
                                    'present_in_apk': eid in original.index['MasterDataEnemyChara']})
    write(out / 'enemy-stat-and-action-changes.json', {'shared_enemy_changes': shared_enemy_changes,
                                                     'backup_nonzero_action_references': nonzero_actions})
    summary = {'original_quest_rows': len(original_tables['MasterDataQuest']),
               'backup_quest_rows': len(backup_tables['MasterDataQuest']),
               'shared_same_id_quests': sum(r['state'] == 'shared' for r in rows),
               'renumbered_quests': [{'apk_id': a, 'backup_id': b} for a, b in remaps.items()],
               'matched_existing_quests': len(shared), 'shared_changed_any_category': sum(bool(r['categories']) for r in shared),
               'shared_unchanged_all_categories': sum(not r['categories'] for r in shared),
               'backup_only_quests': sum(r['state'] == 'backup_only' for r in rows),
               'apk_only_quests': sum(r['state'] == 'apk_only' for r in rows),
               'shared_active_on_both': sum(r['apk_active'] == r['backup_active'] == 2 for r in shared),
               'affected_quests_by_category': dict(category_counts),
               'boss_different_quests': sum(r['boss_difference'] for r in shared),
               'phase_count_changed_quests': sum(r['apk_boss_max_phases'] != r['backup_boss_max_phases'] for r in shared),
               'shared_enemy_rows_changed_existing_fields': len(shared_enemy_changes),
               'backup_nonzero_action_enemy_rows': len(nonzero_actions),
               'duplicates': {'apk': original.duplicates, 'backup': backup.duplicates},
               'referenced_missing_records': {'apk': sorted(original.missing), 'backup': sorted(backup.missing)},
               'apk_modified': False, 'android_tested': False}
    write(out / 'quest-index.json', rows)
    write(out / 'summary.json', summary)
    fields = ['quest_id', 'apk_quest_id', 'backup_quest_id', 'area_id', 'area', 'area_type', 'q_label', 'quest', 'state', 'apk_active', 'backup_active',
              'apk_floor_count', 'backup_floor_count', 'categories', 'apk_boss', 'backup_boss']
    with (out / 'quest-index.csv').open('w', encoding='utf-8-sig', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=fields, extrasaction='ignore')
        writer.writeheader()
        for r in rows:
            writer.writerow({**r, 'categories': '、'.join(LABELS[k] for k in r['categories'])})
    bosses = ['# Boss 差異清單', '', '比較 APK 內建設定與備份；差異不等於全部都是程式錯誤。固定群組忽略 num_min/max。', '',
              '| 區域／關卡 | quest ID | APK Boss 流程 | 備份 Boss 流程 | 差異種類 |', '|---|---:|---|---|---|']
    for r in shared:
        if r['boss_difference']:
            bosses.append('| ' + ' | '.join([r['area'] + ' ' + r['q_label'] + ' ' + r['quest'], str(r['quest_id']),
                r['apk_boss'], r['backup_boss'], '、'.join(LABELS[k] for k in r['categories'] if k.startswith('boss_'))]) + ' |')
    (out / 'boss-differences.md').write_text('\n'.join(bosses) + '\n')
    stages = ['# 全關卡差異索引', '', 'Q 編號依各來源主資料順序（包含 inactive 項目），以 quest ID 作為精確定位。', '',
              '每關完整原值、備份值與欄位路徑存於 quests/<quest ID>.json；CSV 可篩選分類。', '']
    for aid, rr in sorted(area_rows.items()):
        stages += ['## ' + rr[0]['area'] + '（' + str(aid) + '）', '',
                   '| 關卡 | ID | 狀態 | 樓層 APK→備份 | 差異 |', '|---|---:|---|---|---|']
        for r in rr:
            stages.append('| ' + ' | '.join([r['q_label'] + ' ' + r['quest'], str(r['quest_id']), r['state'],
                str(r['apk_floor_count']) + '→' + str(r['backup_floor_count']),
                '、'.join(LABELS[k] for k in r['categories']) or ('無差異' if r['state'] in ('shared', 'renumbered') else '單側資料')]) + ' |')
        stages.append('')
    (out / 'all-stages.md').write_text('\n'.join(stages) + '\n')
    # Export exact groups for an independent probe of fixed-group semantics.
    for name, dataset in [('apk', original), ('backup', backup)]:
        write(out / (name + '-enemy-groups.json'), list(dataset.index['MasterDataEnemyGroup'].values()))
    for path, before in apk_hashes.items():
        assert digest(Path(path).read_bytes()) == before
    for cab in cabs:
        assert digest(Path(cab['path']).read_bytes()) == cab['sha256']
    print(json.dumps(summary, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
