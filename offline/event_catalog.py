"""Inventory original 1.0.6 data without rewriting the archived tables."""
import csv
import json


def create(tables, output, source_label='original 1.0.6 embedded tables'):
    def load(name):
        return json.loads((tables/(name+'.json')).read_bytes(), strict=False)
    areas = load('MasterDataArea')
    quests = load('MasterDataQuest')
    floors = load('MasterDataQuestFloor')
    groups = {x['fix_id']: x for x in load('MasterDataEnemyGroup')}
    enemies = {x['fix_id']: x for x in load('MasterDataEnemyChara')}
    characters = {x['fix_id'] for x in load('MasterDataParamChara')}
    event_areas = [a for a in areas if a['type'] == 2]
    event_ids = {a['fix_id'] for a in event_areas}
    playable = [q for q in quests if q['area_id'] in event_ids and q['active'] == 2]
    reserved = [q for q in quests if q['area_id'] in event_ids and q['active'] != 2]
    checked = set()

    def check_group(gid):
        if not gid or gid in checked:
            return
        checked.add(gid)
        group = groups[gid]
        check_group(group['chain_id'])
        for i in range(1, 8):
            eid = group['enemy_id_'+str(i)]
            if eid:
                enemy = enemies[eid]
                assert enemy['chara_id'] in characters, ('missing enemy character', eid)
                if enemy['drop_unit_id']:
                    assert enemy['drop_unit_id'] in characters, ('missing drop', eid)

    for quest in playable:
        assigned = [f for f in floors if f['quest_id'] == quest['fix_id'] and f['active'] == 2]
        assert any(f['under'] == 0 for f in assigned), ('missing main floor', quest['fix_id'])
        assert any(f['boss_group_id'] for f in assigned), ('missing boss floor', quest['fix_id'])
        for floor in assigned:
            check_group(floor['boss_group_id'])
            for i in range(1, 8):
                check_group(floor['enemy_group_id_'+str(i)])
    assert (len(event_areas), len(playable), len(reserved)) == (49, 132, 7)
    assert all(not any(f['quest_id'] == q['fix_id'] for f in floors) for q in reserved)
    rows = []
    for area in event_areas:
        active = [q for q in playable if q['area_id'] == area['fix_id']]
        rows.append(dict(area_id=area['fix_id'], name_ja=area['area_name'], name_en=area['area_name_eng'],
                         original_event_id=area['event_id'], original_pre_area=area['pre_area'],
                         playable_missions=len(active), quest_ids=' '.join(str(q['fix_id']) for q in active),
                         map_texture=area['res_map'], menu_sprite=area['res_map_icon']))
    with (output/'event-areas.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader(); writer.writerows(rows)
    with (output/'event-missions.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=list(playable[0]))
        writer.writeheader(); writer.writerows(playable)
    text = '# 1.0.6 內建活動關卡\n\n'
    text += '資料來源：'+source_label+'（MasterDataArea／Quest／QuestFloor）。\n\n'
    text += '49 個活動區域，132 個正式任務；prototype 10 開放所有活動區域及正式難度。完成原本教學後，可在「イベントエリア」橫向捲動選擇。部分選關縮圖仍是替代圖。\n\n'
    text += '| 區域 ID | 原名 | 英文名 | 任務數 |\n|---|---|---|---:|\n'
    text += ''.join(f"| {r['area_id']} | {r['name_ja']} | {r['name_en']} | {r['playable_missions']} |\n" for r in rows)
    text += '\n另將六種進化神殿改為常駐，仍從原本進化流程進入：\n\n'
    text += ''.join(f"- {a['area_name']}（{a['area_name_eng']}）\n" for a in areas if a['type'] == 3)
    text += '\n保留停用的 7 筆 reserve：' + '、'.join(str(q['fix_id']) for q in reserved) + '。它們是擴充預留欄位，沒有樓層，不能視為可玩的難度。\n\n'
    text += '解除限制：活動日期／玩家分組、活動區域前置條件、正式活動難度的順序限制。主線進度、體力、獎勵及通關旗標依建置資料表規則；不會替玩家標記已通關。\n\n'
    text += '容器內確認所有正式活動任務具有主樓層及 Boss 樓層，敵人群組（包含連戰）、角色與掉落 ID 可解析。這不代表已完成每關的 Android 遊玩驗證。\n'
    (output/'EVENTS.md').write_text(text, encoding='utf-8')
    return dict(areas=len(rows), playable_missions=len(playable), reserved_missions=len(reserved),
                evolution_temples=sum(a['type'] == 3 for a in areas), checked_enemy_groups=len(checked),
                source=source_label, android_gameplay='pending user verification')
