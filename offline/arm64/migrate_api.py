"""Apply explicit compatibility edits only to the generated ARM64 project."""
import argparse
import json
import re
import shutil
from pathlib import Path


RULES = Path(__file__).with_name('api-edits.json')


def learn_diagnostics(project, log):
    """Use compiler locations, so valid RaycastHit.collider is never rewritten."""
    edits = json.loads(RULES.read_text()) if RULES.exists() else []
    by_line = {}
    for diagnostic in log.read_text().splitlines():
        match = re.match(r'(.+?)\((\d+),\d+\): error CS0619: (.*)', diagnostic)
        if not match:
            continue
        path, number, message = Path(match[1]), int(match[2]), match[3]
        relative = str(path.relative_to(project))
        lines = path.read_text().splitlines()
        old = lines[number-1]
        key = (relative, old)
        new = by_line.get(key, old)
        prop = re.search(r"'(?:Component|GameObject)\.(\w+)'", message)
        component = re.search(r'Use GetComponent<(\w+)>', message)
        if prop and component:
            new = re.sub(r'\.'+prop[1]+r'\b', '.GetComponent<'+component[1]+'>()', new)
        elif 'GameObject.AddComponent(string)' in message:
            new = new.replace('.AddComponent(', '.AddComponentByName(')
        elif 'Camera.isOrthoGraphic' in message:
            new = new.replace('.isOrthoGraphic', '.orthographic')
        elif 'RuntimePlatform.' in message:
            # Removed web-player branches cannot apply to this Android-only port.
            new = new.replace('RuntimePlatform.WindowsWebPlayer', 'Arm64Compatibility.WindowsWebPlayer')
            new = new.replace('RuntimePlatform.OSXWebPlayer', 'Arm64Compatibility.OSXWebPlayer')
        if new != old:
            by_line[key] = new
    for (path, before), after in by_line.items():
        edits.append({'path': path, 'before': before, 'after': after})
    RULES.write_text(json.dumps(edits, ensure_ascii=False, indent=2)+'\n')


def migrate(project):
    changes = []
    for path in (project/'Assets').rglob('*.cs'):
        if 'Editor' in path.relative_to(project/'Assets').parts:
            continue
        text = path.read_text()
        before = text
        text = text.replace('using Boo.Lang.Runtime;\n', '')
        text = text.replace('RuntimeServices.Coerce(', 'System.Convert.ChangeType(')
        text = text.replace('RuntimeServices.UnboxInt32(', 'System.Convert.ToInt32(')
        text = text.replace('RuntimeServices.UnboxDouble(', 'System.Convert.ToDouble(')
        if text != before:
            path.write_text(text)
            changes.append(str(path.relative_to(project)))
    for edit in json.loads(RULES.read_text()) if RULES.exists() else []:
        path = project/edit['path']
        text = path.read_text()
        # Match complete lines, preserving surrounding code and line numbers.
        lines = text.splitlines(keepends=True)
        updated = ''.join(edit['after']+'\n' if line.rstrip('\r\n') == edit['before'] else line for line in lines)
        if text != updated:
            path.write_text(updated)
            changes.append(edit['path'])
    shutil.copy2(Path(__file__).with_name('Arm64Compatibility.cs'), project/'Assets/Scripts/OfflineArm64/Arm64Compatibility.cs')
    return changes


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('project', type=Path)
    parser.add_argument('--diagnostics', type=Path)
    args = parser.parse_args()
    if args.diagnostics:
        learn_diagnostics(args.project, args.diagnostics)
    print(json.dumps(migrate(args.project), indent=2))
