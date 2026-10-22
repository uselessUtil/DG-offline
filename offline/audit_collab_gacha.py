"""Read-only audit of collab pool contents and local weighted draw buckets.

Run in the project Docker environment. The cached backup is scanned afresh, not inferred from
available characters. No APK, cached asset, account or draw setting is changed.
"""
import argparse
from collections import Counter, defaultdict
import csv
from fractions import Fraction
import hashlib
import json
from pathlib import Path
import zipfile

import UnityPy
from UnityPy.files.SerializedFile import SerializedFile


NAMES = {300100: 'FAIRY TAIL', 300200: '進撃の巨人',
         300300: 'ケリ姫スイーツ', 300400: 'とある魔術の禁書目録'}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def raw(value):
    return value.m_Script.encode('utf8', 'surrogateescape') if isinstance(value.m_Script, str) else value.m_Script


def write(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n')


def bucket_counts(rows):
    """Exact finite ticket enumeration of ForgeryGachaPlay's <= 0 branch.

    These are counts conditional on equiprobable tickets in [0,total). They
    deliberately do not claim historical online-server probabilities.
    """
    total = sum(r['assign_rate'] for r in rows)
    counts = [0] * len(rows)
    for ticket in range(total):
        remaining = ticket
        for i, row in enumerate(rows):
            remaining -= row['assign_rate']
            if remaining <= 0:
                counts[i] += 1
                break
        else:
            counts[0] += 1  # Original fallback, normally unreachable.
    assert sum(counts) == total
    return counts


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--original-apk', type=Path, required=True)
    ap.add_argument('--current-apk', type=Path, required=True)
    ap.add_argument('--backup', type=Path, required=True)
    ap.add_argument('--stage-proof', type=Path, required=True)
    ap.add_argument('--output', type=Path, required=True)
    a = ap.parse_args()
    a.output.mkdir(parents=True, exist_ok=True)
    if (a.output / 'pools.json').exists():
        raise FileExistsError('Use a new output directory')
    proof = json.loads(a.stage_proof.read_text())
    source_hashes = {str(p): sha(p.read_bytes()) for p in (a.original_apk, a.current_apk)}
    for path, digest in source_hashes.items():
        assert proof['apk_sha256'][path] == digest
    tables, verified = {}, {}
    with zipfile.ZipFile(a.original_apk) as original, zipfile.ZipFile(a.current_apk) as current:
        for name in ['MasterDataGacha', 'MasterDataGachaAssign', 'MasterDataParamChara']:
            record = proof['original_tables'][name]
            blob = original.read(record['asset'])
            assert blob == current.read(record['asset'])
            assert sha(blob) == record['asset_sha256']
            for obj in UnityPy.load(blob).objects:
                if obj.type.name == 'TextAsset':
                    value = obj.read()
                    if value.m_Name == name:
                        data = raw(value)
                        assert sha(data) == record['table_sha256']
                        tables[name] = json.loads(data, strict=False)
            assert name in tables
            verified[name] = record
    backup_tables, cab_manifest, tokens = {}, [], defaultdict(list)
    before = SerializedFile.set_version
    SerializedFile.set_version = lambda self, value: before(self, value.splitlines()[0])
    try:
        for path in sorted(a.backup.rglob('CAB-*')):
            if not path.is_file():
                continue
            blob = path.read_bytes()
            relative = str(path.relative_to(a.backup))
            cab_manifest.append({'path': relative, 'sha256': sha(blob), 'size': len(blob)})
            for token in ['MasterDataGacha', 'MasterDataGachaAssign', 'assign_rate', 'assign_chara_id']:
                if token.encode() in blob:
                    tokens[token].append(relative)
            for obj in UnityPy.load(blob).objects:
                if obj.type.name == 'TextAsset':
                    value = obj.read()
                    if value.m_Name.startswith('MasterData'):
                        assert value.m_Name not in backup_tables
                        backup_tables[value.m_Name] = json.loads(raw(value), strict=False)
    finally:
        SerializedFile.set_version = before
    chars = {r['fix_id']: r for r in tables['MasterDataParamChara']}
    backup_chars = {r['fix_id']: r for r in backup_tables.get('MasterDataParamChara', [])}
    assignments = tables['MasterDataGachaAssign']
    pools = []
    for pool in tables['MasterDataGacha']:
        if pool['type'] != 3:
            continue
        pid = pool['fix_id']
        indexed = [(i, r) for i, r in enumerate(assignments) if r['gacha_id'] == pid and r['active'] == 2]
        assert indexed
        start, end = indexed[0][0], indexed[-1][0]
        assert all(r['gacha_id'] == pid for r in assignments[start:end+1] if r['active'] == 2)
        rows = [r for _, r in indexed]
        assert all(r['assign_rate'] > 0 for r in rows)
        counts = bucket_counts(rows)
        total = sum(r['assign_rate'] for r in rows)
        grouped = defaultdict(lambda: {'nominal_weight': 0, 'local_buckets': 0, 'variants': []})
        for row, count in zip(rows, counts):
            result = grouped[row['assign_chara_id']]
            result['nominal_weight'] += row['assign_rate']
            result['local_buckets'] += count
            result['variants'].append({'row': row, 'local_buckets': count})
        members = []
        for cid, result in grouped.items():
            c = chars[cid]
            result.update(character_id=cid, encyclopedia_no=c['draw_id'], name=c['name'], stars=c['rare']+1,
                          resource_id=c['res_chara_id_str'],
                          nominal_fraction=str(Fraction(result['nominal_weight'], total)),
                          nominal_percent=result['nominal_weight']/total*100,
                          local_bucket_fraction=str(Fraction(result['local_buckets'], total)),
                          local_bucket_percent=result['local_buckets']/total*100,
                          character_in_backup=cid in backup_chars,
                          backup_name=backup_chars.get(cid, {}).get('name'))
            members.append(result)
        assert sum(m['nominal_weight'] for m in members) == sum(m['local_buckets'] for m in members) == total
        pools.append({'pool_id': pid, 'display_name': NAMES.get(pid, pool['name']), 'metadata': pool,
                      'total_weight': total, 'active_assignment_rows': len(rows),
                      'unique_characters': len(members), 'assignment_array_range': [start, end],
                      'members': members,
                      'five_star_nominal_percent': sum(m['nominal_weight'] for m in members if m['stars']==5)/total*100,
                      'five_star_local_bucket_percent': sum(m['local_buckets'] for m in members if m['stars']==5)/total*100})
    pools.sort(key=lambda p: p['pool_id'])
    summary = {'apk_pool_rows': len(tables['MasterDataGacha']), 'apk_assignment_rows': len(assignments),
               'collab_pool_count': len(pools), 'unique_pool_members_total': sum(p['unique_characters'] for p in pools),
               'backup_cabs_scanned': len(cab_manifest), 'backup_master_tables_found': sorted(backup_tables),
               'backup_has_gacha_table': 'MasterDataGacha' in backup_tables,
               'backup_has_assignments_table': 'MasterDataGachaAssign' in backup_tables,
               'backup_gacha_keyword_hits': dict(tokens),
               'local_weights_customized': False,
               'online_historical_rates_verified': False, 'apk_modified': False}
    write(a.output / 'pools.json', pools)
    write(a.output / 'summary.json', summary)
    write(a.output / 'source-proof.json', {'apk_sha256': source_hashes, 'original_and_current_tables': verified,
                                          'backup_cabs': cab_manifest})
    write(a.output / 'expected-local-counts.json', [{'pool_id': p['pool_id'], 'total': p['total_weight'],
          'members': [{'id': m['character_id'], 'count': m['local_buckets']} for m in p['members']]} for p in pools])
    with (a.output / 'characters.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        fields = ['pool_id', 'pool_name', 'character_id', 'encyclopedia_no', 'name', 'stars', 'nominal_weight',
                  'total_weight', 'nominal_percent', 'local_buckets', 'local_bucket_percent']
        writer = csv.DictWriter(stream, fieldnames=fields, extrasaction='ignore')
        writer.writeheader()
        for p in pools:
            for m in p['members']:
                writer.writerow({**m, 'pool_id': p['pool_id'], 'pool_name': p['display_name'], 'total_weight': p['total_weight']})
    lines = ['# 聯動卡池名單與機率核對', '',
             '以下是 1.0.6 APK 內建卡池與目前離線程式的結果，不能當成已驗證的歷史官方伺服器機率。', '',
             '備份保存角色資料，但沒有找到 MasterDataGacha／MasterDataGachaAssign。原 APK 與目前 APK 的相關主資料資產位元組相同。', '',
             '名目機率＝同角色全部 active 權重加總／該池總權重；同角色的無加值、HP+1、ATK+1 為不同資料列，已合併。', '',
             '目前程式以 [0,總權重) 取票號，逐列扣權重後用 <=0 判斷；因此第一列多一個票號、最後一列少一個。下列「離線票號機率」已計入這個偏差。', '',
             '票號機率以各票號等可能計算，忽略 32 位元 PRNG 取模的極微小偏差；不是用少量實抽估計。', '']
    for p in pools:
        lines += ['## '+p['display_name']+'（'+str(p['pool_id'])+'）', '',
                  '每抽 '+str(p['metadata']['price'])+' 枚代幣；'+str(p['unique_characters'])+' 個角色，'+str(p['active_assignment_rows'])+' 個加值變體資料列；總權重 '+str(p['total_weight'])+'。', '',
                  '| 圖鑑 No. | 角色 | 星數 | 合計權重 | APK 名目機率 | 目前離線票號機率 |',
                  '|---:|---|---:|---:|---:|---:|']
        for m in p['members']:
            lines.append('| '+str(m['encyclopedia_no'])+' | '+m['name']+' | '+str(m['stars'])+' | '+str(m['nominal_weight'])+' | '+format(m['nominal_percent'],'.6f')+'% | '+format(m['local_bucket_percent'],'.6f')+'% |')
        lines += ['', '五星合計：名目 '+format(p['five_star_nominal_percent'],'.6f')+'%；目前離線票號 '+format(p['five_star_local_bucket_percent'],'.6f')+'%。', '']
    lines += ['## 來源與限制', '',
              '- 四個池由原 APK 的 type=3 定義辨識，並非從角色清單猜測。離線版新增的是永久入口與選池介面，沒有重設這些權重。',
              '- 掃描備份全部 '+str(len(cab_manifest))+' 個 CAB；主資料表與關鍵字命中保存在 summary.json。只有角色存在，不能推論該角色曾以何種卡池或機率排出。',
              '- APK 原本的線上抽卡由伺服器回覆；此處檢查的 ServerForgery 是目前離線模式使用的模擬抽選。正式伺服器是否使用同一份權重，無法由現有備份證實。',
              '- 每列為角色 Lv1；加值由該列 plus_hp／plus_pow 決定。完整 78 列資料及分數保存在 pools.json。',
              '- 未修改 APK、機率、角色或玩家存檔。', '']
    (a.output / 'README.md').write_text('\n'.join(lines)+'\n')
    for path, value in source_hashes.items():
        assert sha(Path(path).read_bytes()) == value
    for r in cab_manifest:
        assert sha((a.backup/r['path']).read_bytes()) == r['sha256']
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    for p in pools:
        print(p['display_name'], p['unique_characters'], p['total_weight'],
              [(m['name'], round(m['nominal_percent'], 6), round(m['local_bucket_percent'], 6)) for m in p['members']])


if __name__ == '__main__':
    main()
