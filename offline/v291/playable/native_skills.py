"""Translate legacy attack buffs to the 2.9 native skill/status tables.

Keep all source fields (including the five old values) byte-for-value. Only
add native execution fields. No later DGDB balance values are imported.
"""
import copy
import hashlib
import json

BUFF_CATEGORIES = {17, 18, 43}
ELEMENT_STATUS = {1: 19, 2: 13, 3: 14, 4: 15, 5: 16, 6: 17, 7: 18}
ELEMENT_ICON = {1: 'atkUpNone', 2: 'atkUpFire', 3: 'atkUpWater',
                4: 'atkUpLight', 5: 'atkUpDark', 6: 'atkUpWind', 7: ''}
KIND_ICON = {1: 'atkUpHuman', 2: 'atkUpDragon', 3: 'atkUpGod',
             4: 'atkUpDemon', 5: 'atkUpFairy', 6: 'atkUpBeast',
             7: 'atkUpMachine', 8: ''}

def encode(rows):
    return json.dumps(rows, ensure_ascii=True, separators=(',', ':')).encode()

def convert(source, schemas):
    name = 'MasterDataSkillLimitBreak'
    original = source[name]
    skills = json.loads(original, strict=False)
    status_original = source['MasterDataStatusAilment']
    statuses = json.loads(status_original, strict=False)
    status_ids = {row['fix_id'] for row in statuses}
    characters = json.loads(source['MasterDataParamChara'], strict=False)
    changed = []
    for row in skills:
        category = row['skill_cate']
        if category not in BUFF_CATEGORIES:
            continue
        before = copy.deepcopy(row)
        assert row['skill_type'] == 7 and row['value4'] == 0, row
        low, high = row['value0'], row['value1']
        assert 1 <= low <= high <= 99, row
        if category == 17:
            status_category, rate, icon = 12, row['value2'], 'atkUp'
        elif category == 18:
            status_category = ELEMENT_STATUS[row['value2']]
            rate, icon = row['value3'], ELEMENT_ICON[row['value2']]
        else:
            assert 1 <= row['value2'] <= 8, row
            status_category = 33 + row['value2']
            rate, icon = row['value3'], KIND_ICON[row['value2']]
        # One fixed-duration row for every original random duration. The native
        # activity initializer still makes the original duration roll; the
        # activity hook only selects its corresponding native status ID.
        ids = []
        for duration in range(low, high if high > low else low + 1):
            status_id = row['fix_id'] * 100 + duration
            assert status_id not in status_ids, status_id
            status_ids.add(status_id); ids.append(status_id)
            state = dict(fix_id=status_id, good_or_bad=2, clear_type=2,
                         category=status_category, duration=duration, icon=icon,
                         name=row['name'], detail=row['detail'],
                         update_move=2, update_battle=2)
            state.update({'param%02d' % i: rate if i == 1 else 0 for i in range(1, 13)})
            assert not (set(state) - schemas['MasterDataStatusAilmentParam'])
            statuses.append(state)
        native = dict(skill_damage_enable=1, skill_power=0, skill_power_fix=0,
                      skill_power_hp_rate=0, skill_absorb=0, skill_kickback=0,
                      skill_kickback_fix=0, skill_chk_atk_affinity=2,
                      skill_chk_atk_leader=2, skill_chk_atk_passive=2,
                      skill_chk_atk_ailment=2, skill_chk_def_defence=2,
                      skill_chk_def_ailment=2, status_ailment_target=1,
                      status_ailment1=ids[0], status_ailment2=0,
                      status_ailment3=0, status_ailment4=0)
        assert not (set(native) - schemas[name])
        # Stop rather than silently overwrite an already-native source.
        assert not (set(native) & set(row)), row['fix_id']
        row.update(native)
        assert all(row[key] == value for key, value in before.items())
        changed.append(dict(skill_id=row['fix_id'], name=row['name'],
            category=category, original_values=[row['value'+str(i)] for i in range(5)],
            multiplier_percent=rate, duration_min=low, duration_max=high,
            native_status_category=status_category, native_status_ids=ids,
            duration_upper_bound='exclusive when min < max; original Rand.GetRand contract',
            native_execution_fields=native,
            users=[dict(id=c['fix_id'], number=c['draw_id'], name=c['name'])
                   for c in characters if c.get('skill_limitbreak') == row['fix_id']]))
    assert len(changed) == 39
    assert sum(x['category'] == 17 for x in changed) == 3
    assert sum(x['category'] == 18 for x in changed) == 26
    assert sum(x['category'] == 43 for x in changed) == 10
    source[name] = encode(skills)
    source['MasterDataStatusAilment'] = encode(statuses)
    return [
        dict(table=name, mapping='39 old attack buffs to native 2.9 execution fields; all original fields unchanged',
             source_sha256=hashlib.sha256(original).hexdigest(),
             output_sha256=hashlib.sha256(source[name]).hexdigest(), skills=changed),
        dict(table='MasterDataStatusAilment', mapping='Append native attack-buff rows; original 12 status rows unchanged',
             source_sha256=hashlib.sha256(status_original).hexdigest(),
             output_sha256=hashlib.sha256(source['MasterDataStatusAilment']).hexdigest(),
             original_rows=len(statuses)-sum(len(x['native_status_ids']) for x in changed),
             added_rows=sum(len(x['native_status_ids']) for x in changed))]
