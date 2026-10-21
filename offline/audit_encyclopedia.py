"""Inventory already extracted Encyclopedia bundles in Docker; never execute APK code.

All extraction/cache input lives in /tmp. Output contains metadata and selected PNGs.
The master table is the original 1.0.6 MasterDataParamChara TextAsset JSON.
"""
import argparse
import collections
import csv
import gc
import json
from pathlib import Path

import UnityPy
from PIL import Image, ImageDraw


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work-dir", required=True, type=Path)
    parser.add_argument("--master-table", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    previews = args.output / "previews"
    previews.mkdir(exist_ok=True)
    inventory = []
    bundles = []
    # Include an early, later and nonsequential old character ID for visual checks.
    samples = {1, 4, 7, 28, 267, 610}
    for source in sorted(args.work_dir.glob("*.bin")):
        env = UnityPy.load(str(source))
        counts = collections.Counter(obj.type.name for obj in env.objects)
        versions = set()
        for obj in env.objects:
            versions.add(obj.assets_file.unity_version)
            if obj.type.name not in ("Texture2D", "Sprite", "AudioClip"):
                continue
            data = obj.read(check_read=False)
            row = dict(bundle=source.name, type=obj.type.name,
                       name=data.m_Name, path_id=obj.path_id)
            if obj.type.name == "Texture2D":
                row.update(width=data.m_Width, height=data.m_Height,
                           format=data.m_TextureFormat)
                prefix = "unit_" if data.m_Name.startswith("unit_") else "chara_icon_"
                suffix = data.m_Name.removeprefix(prefix)
                if suffix.isdigit() and int(suffix) in samples:
                    path = previews / (data.m_Name + ".png")
                    data.image.save(path)
                    row["decoded_preview"] = str(path.relative_to(args.output))
            if obj.type.name == "AudioClip":
                row.update(channels=data.m_Channels, frequency=data.m_Frequency,
                           length_seconds=data.m_Length)
            inventory.append(row)
        bundles.append(dict(name=source.name, unity_versions=sorted(versions),
                            objects=dict(counts)))
        print(source.name, dict(counts), flush=True)
        del env
        gc.collect()
    (args.output / "inventory.json").write_text(
        json.dumps(dict(bundles=bundles, assets=inventory), ensure_ascii=False, indent=2),
        encoding="utf-8")
    units = {int(row["name"][5:]) for row in inventory
             if row["type"] == "Texture2D" and row["name"].startswith("unit_")
             and row["name"][5:].isdigit()}
    icons = {int(row["name"][11:]) for row in inventory
             if row["type"] == "Texture2D" and row["name"].startswith("chara_icon_")
             and row["name"][11:].isdigit()}
    master = json.loads(args.master_table.read_text(encoding="utf-8"), strict=False)
    coverage = []
    for row in master:
        if not row["fix_id"]:
            continue
        resource_id = int(row["res_chara_id_str"])
        coverage.append(dict(fix_id=row["fix_id"], name=row["name"],
                             draw_id=row["draw_id"], resource_id=resource_id,
                             portrait=resource_id in units, icon=resource_id in icons))
    with (args.output / "coverage-106.csv").open("w", encoding="utf-8-sig", newline="") as out:
        writer = csv.DictWriter(out, fieldnames=list(coverage[0]))
        writer.writeheader()
        writer.writerows(coverage)
    summary = dict(master_rows=len(master), characters=len(coverage),
                   matching_portraits=sum(r["portrait"] for r in coverage),
                   matching_icons=sum(r["icon"] for r in coverage),
                   matching_both=sum(r["portrait"] and r["icon"] for r in coverage),
                   missing=[r for r in coverage if not (r["portrait"] and r["icon"])],
                   total_unit_textures=len(units), total_icon_textures=len(icons),
                   note="ID coverage; selected images decoded. Not Android rendering verification.")
    (args.output / "coverage-summary.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    sheet = Image.new("RGB", (900, 620), (225, 225, 225))
    draw = ImageDraw.Draw(sheet)
    for idx, resource_id in enumerate(sorted(samples)):
        x, y = (idx % 3) * 300, (idx // 3) * 310
        path = previews / ("unit_%04d.png" % resource_id)
        if path.exists():
            image = Image.open(path).convert("RGBA")
            image.thumbnail((290, 275))
            sheet.paste(image, (x + (300-image.width)//2, y + 20), image)
        icon = previews / ("chara_icon_%04d.png" % resource_id)
        if icon.exists():
            image = Image.open(icon).convert("RGBA")
            image.thumbnail((64, 64))
            sheet.paste(image, (x + 230, y + 240), image)
        draw.text((x+10, y+5), "Resource ID %04d" % resource_id, fill="black")
    sheet.save(args.output / "preview-sheet.jpg", quality=92)
    print({k:v for k,v in summary.items() if k != "missing"}, flush=True)


if __name__ == "__main__":
    main()
