"""Build a standalone, file:// compatible gallery for the local asset archives."""
import json
from pathlib import Path
from urllib.parse import quote

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
EXTERNAL = ROOT / "external"
OUTPUT = EXTERNAL / "image-preview.html"
EXTENSIONS = {".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".ico"}


def local_url(path):
    return "/".join(quote(part, safe="") for part in path.relative_to(EXTERNAL).parts)


def main():
    records = []
    wiki_records = []
    for folder, language in (("divine-gate-wiki", ""), ("divine-gate-wiki-zh", "中文 / ")):
        manifest = EXTERNAL / folder / "metadata/image-manifest.json"
        if not manifest.exists() and language:
            continue
        wiki_records.extend((folder, language, item) for item in json.loads(manifest.read_text(encoding="utf-8")))
    for folder, language, item in wiki_records:
        path = EXTERNAL / folder / item["path"]
        if not item.get("mime", "").startswith("image/") and item.get("media_kind") != "external_video_thumbnail":
            continue
        if not path.is_file():
            raise FileNotFoundError(path)
        records.append(dict(
            name=item["name"], source="Wiki", group=language + (item.get("decoded_format") or path.suffix[1:]).upper(),
            src=local_url(path), path=path.relative_to(ROOT).as_posix(),
            width=item.get("width"), height=item.get("height"),
            size=item["downloaded_size"],
            status=("YouTube 縮圖" if item.get("media_kind") == "external_video_thumbnail"
                    else "CDN 版本" if item["status"] == "cdn_variant" else "原檔雜湊相符"),
            url=item["descriptionurl"],
        ))
    image_root = EXTERNAL / "dgdb/public/img"
    for path in sorted(image_root.rglob("*")):
        if not path.is_file() or path.suffix.lower() not in EXTENSIONS:
            continue
        relative = path.relative_to(image_root)
        with Image.open(path) as img:
            width, height = img.size
        records.append(dict(
            name=path.name, source="DGDB", group=relative.parts[0] if len(relative.parts) > 1 else "根目錄",
            src=local_url(path), path=path.relative_to(ROOT).as_posix(),
            width=width, height=height, size=path.stat().st_size,
            status="Git 倉庫圖片",
            url="https://github.com/kennyfong19931/dgdb/blob/1a15adf733bb02467d51303a110f361535a7b4bc/public/img/" +
                "/".join(quote(part, safe="") for part in relative.parts),
        ))
    records.sort(key=lambda row: (row["source"], row["name"].casefold(), row["path"]))
    # Escape markup delimiters so filenames can never terminate the JSON script.
    payload = json.dumps(records, ensure_ascii=False, separators=(",", ":"))
    payload = payload.replace("&", "\\u0026").replace("<", "\\u003c").replace(">", "\\u003e")
    template = (ROOT / "offline/image_preview.html").read_text(encoding="utf-8")
    assert template.count("__IMAGE_RECORDS__") == 1
    OUTPUT.write_text(template.replace("__IMAGE_RECORDS__", payload), encoding="utf-8")
    print(json.dumps({"output": str(OUTPUT), "counts": {
        source: sum(row["source"] == source for row in records) for source in ("Wiki", "DGDB")
    }, "total": len(records)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
