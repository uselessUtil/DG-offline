"""Scoped Unity 5 prefab layout repairs for the verified 4.0.0 archive."""
import copy
import hashlib
import json
import math
import struct

import UnityPy
from UnityPy.classes import PPtr

QUEST = '6f0361b31f70ce3408eec0ee838f16b0'
TUTORIAL = 'e07c40e1067df29488743679b30b40d8'


def hierarchy(obj):
    transform = obj.read()
    names = [transform.m_GameObject.read().m_Name]
    while transform.m_Father.path_id:
        transform = transform.m_Father.read()
        names.insert(0, transform.m_GameObject.read().m_Name)
    return '/'.join(names)


def curve_y_bounds(keys):
    """Extrema of the original Hermite animation, including between keys."""
    values = []
    for left, right in zip(keys, keys[1:]):
        dt = right['time'] - left['time']
        p, q = left['value']['y'], right['value']['y']
        u, v = dt * left['outSlope']['y'], dt * right['inSlope']['y']
        a, b, c, d = 2*p-2*q+u+v, -3*p+3*q-2*u-v, u, p
        times = [0, 1]
        if abs(a) < 1e-12:
            if abs(b) > 1e-12:
                times.append(-c/(2*b))
        elif 4*b*b-12*a*c >= 0:
            root = math.sqrt(4*b*b-12*a*c)
            times.extend([(-2*b-root)/(6*a), (-2*b+root)/(6*a)])
        values.extend(((a*t+b)*t+c)*t+d for t in times if 0 <= t <= 1)
    return min(values), max(values)


def create(data_dir, build):
    env = UnityPy.load(str(data_dir))
    files = {obj.assets_file.name: obj.assets_file for obj in env.objects}
    snapshots = {name: {k: o.get_raw_data() for k, o in files[name].objects.items()}
                 for name in (QUEST, TUTORIAL)}
    changes = []

    def transform(name, path_id, suffix, position, scale, expected_position, expected_scale):
        obj = files[name].objects[path_id]
        path = hierarchy(obj)
        assert path.endswith(suffix), (path_id, path)
        tree = obj.read_typetree()
        old = copy.deepcopy(tree)
        assert tree['m_LocalPosition'] == dict(zip('xyz', expected_position))
        assert tree['m_LocalScale'] == dict(zip('xyz', expected_scale))
        tree['m_LocalPosition'] = dict(zip('xyz', position))
        tree['m_LocalScale'] = dict(zip('xyz', scale))
        obj.save_typetree(tree)
        changes.append({'asset': name, 'path_id': path_id, 'path': path,
                        'before': {k: old[k] for k in ('m_LocalPosition', 'm_LocalScale')},
                        'after': {k: tree[k] for k in ('m_LocalPosition', 'm_LocalScale')}})

    # The two long Japanese labels previously grew right from x=185. Right
    # alignment at x=285 leaves 20 base units inside WindowDetail's right edge.
    # Only the label children move: button colliders and event handlers stay put.
    for transform_id, label_id, button in [(113, 246, 'ButtonQuestURL'), (135, 272, 'ButtonQuestItem')]:
        transform(QUEST, transform_id, '/QuestTitleArea/'+button+'/ButtonMsg',
                  (100, 0, 0), (24, 24, 1), (0, 0, 0), (28, 28, 1))
        obj = files[QUEST].objects[label_id]
        raw = obj.get_raw_data()
        script_file, script_id = struct.unpack_from('<iq', raw, 16)
        script = PPtr(m_FileID=script_file, m_PathID=script_id, assetsfile=obj.assets_file).deref().read()
        assert script.m_ClassName == 'UILabel'
        assert struct.unpack_from('<i', raw, 28)[0] == 0  # empty MonoBehaviour name
        # Unity 5.3 UIWidget: header(32), two PPtrs(24), Color(16), Pivot.
        assert struct.unpack_from('<i', raw, 72)[0] == 3  # Pivot.Left
        patched = raw[:72] + struct.pack('<i', 5) + raw[76:]  # Pivot.Right
        obj.set_raw_data(patched)
        changes.append({'asset': QUEST, 'path_id': label_id, 'field': 'UIWidget.mPivot',
                        'before': 'Left', 'after': 'Right'})

    # Both target buttons are centered at y=-275. The compact prompt fits
    # between the detail frame (bottom=-202.5) and the 40-unit button label.
    # Modify the nonanimated parent before CameraAlignUI caches its position;
    # retain bottom anchoring, status-bar adjustment and the original bobbing.
    for path_id, step, old_y in [(3292, '5250', -240), (3298, '5260', -250)]:
        transform(TUTORIAL, path_id, '/TutorialStepNew'+step+'_10/CheckArrowDTap',
                  (0, -215, 0), (0.5, 0.5, 1), (0, old_y, 0), (1, 1, 1))

    # Measure the unchanged child geometry and animation, not a screenshot mock.
    tutorial = files[TUTORIAL]
    fade = tutorial.objects[5246].get_raw_data()
    file_id, path_id = struct.unpack_from('<iq', fade, 56)
    clip = PPtr(m_FileID=file_id, m_PathID=path_id, assetsfile=tutorial).deref().read_typetree()
    assert clip['m_Name'] == 'AnimArrowD' and not clip['m_ScaleCurves']
    curves = clip['m_PositionCurves']
    assert len(curves) == 1 and curves[0]['path'] == ''
    motion_min, motion_max = curve_y_bounds(curves[0]['curve']['m_Curve'])
    arrow = tutorial.objects[3294].read_typetree()
    message = tutorial.objects[3295].read_typetree()
    for sprite in (5244, 5245):
        assert struct.unpack_from('<i', tutorial.objects[sprite].get_raw_data(), 72)[0] == 4
    bottom = -215 + 0.5*(motion_min + arrow['m_LocalPosition']['y'] - arrow['m_LocalScale']['y']/2)
    top = -215 + 0.5*(motion_max + message['m_LocalPosition']['y'] + message['m_LocalScale']['y']/2)
    frame = files[QUEST].objects[158].read_typetree()
    frame_bottom = -85 - frame['m_LocalScale']['y']/2
    button_top = -275 + files[QUEST].objects[168].read_typetree()['m_LocalScale']['y']/2
    assert top < frame_bottom and bottom > button_top, (top, bottom, frame_bottom, button_top)

    replacements = {}
    checks = []
    for name in (QUEST, TUTORIAL):
        raw = files[name].save()
        after = {o.path_id: o.get_raw_data() for o in UnityPy.load(raw).objects}
        before = snapshots[name]
        assert set(before) == set(after)
        changed = {k for k in before if before[k] != after[k]}
        expected = {row['path_id'] for row in changes if row['asset'] == name}
        assert changed == expected, (name, changed, expected)
        # Includes every collider, animation, tutorial trigger, setter and all
        # other prompts in the serialized files; they must be byte-identical.
        checks.append({'asset': name, 'modified_objects': sorted(changed),
                       'unchanged_objects': len(before)-len(changed),
                       'sha256': hashlib.sha256(raw).hexdigest()})
        replacements['assets/bin/Data/'+name] = raw
    report = {'changes': changes, 'serialized_checks': checks,
              'base_640_geometry': {'prompt_top': top, 'prompt_bottom': bottom,
                                   'frame_bottom': frame_bottom, 'button_label_top': button_top,
                                   'frame_gap': frame_bottom-top, 'button_label_gap': bottom-button_top,
                                   'arrow_animation_y_range': [motion_min, motion_max]},
              'android_visual_tested': False}
    (build/'layout-repairs.json').write_text(json.dumps(report, indent=2)+'\n')
    return replacements
