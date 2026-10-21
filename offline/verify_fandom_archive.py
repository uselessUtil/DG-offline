"""Verify the saved Fandom archive independently, without network access."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import xml.etree.ElementTree as ET

from PIL import Image


def verify(root):
    def load(name):
        return json.loads((root/name).read_text(encoding='utf-8'))
    inventory = load('metadata/images.json')
    manifest = load('metadata/image-manifest.json')
    assert len(manifest) == len(inventory)
    expected = {r['name']:r for r in inventory}
    assert len({r['name'] for r in manifest}) == len(manifest)
    assert set(expected) == {r['name'] for r in manifest}
    content = {}
    original = variants = thumbnails = 0
    for row in manifest:
        assert row['status'] in ('verified','cdn_variant'), row['name']
        source = expected[row['name']]
        for key in ('size','sha1','url','descriptionurl','mime','width','height'):
            assert row[key] == source[key], (row['name'],key)
        path = (root/row['path']).resolve()
        assert path.is_relative_to((root/'images').resolve()), path
        if row['path'] not in content:
            raw = path.read_bytes()
            record = {'size':len(raw),'sha1':hashlib.sha1(raw).hexdigest(),
                'sha256':hashlib.sha256(raw).hexdigest()}
            if row.get('decoded_format'):
                with Image.open(io.BytesIO(raw)) as image:
                    record['dimensions'] = image.size
                    record['format'] = image.format
                    image.verify()
            content[row['path']] = record
        actual = content[row['path']]
        assert actual['size'] == row['downloaded_size'], row['name']
        assert actual['sha1'] == row['downloaded_sha1'], row['name']
        assert actual['sha256'] == row['sha256'], row['name']
        assert path.stem == actual['sha1'], row['name']
        matches = actual['size'] == row['size'] and actual['sha1'] == row['sha1']
        assert row['original_bytes_match'] == matches
        if row['status'] == 'verified':
            assert matches, row['name']
            original += 1
        else:
            assert not matches and row['variant_note'], row['name']
            assert actual['format'] == {'image/png':'PNG','image/jpeg':'JPEG',
                'image/gif':'GIF','image/webp':'WEBP','video/youtube':'JPEG'}[row['mime']]
            variants += 1
        if row.get('decoded_format'):
            assert actual['dimensions'] == (row['width'],row['height']), row['name']
            assert actual['format'] == row['decoded_format']
        if row['mime'] == 'video/youtube':
            assert row['media_kind'] == 'external_video_thumbnail' and actual['format'] == 'JPEG'
            assert row['downloaded_mime'] == 'image/jpeg'
            thumbnails += 1
    pages = load('metadata/pages.json')
    exports = load('metadata/page-exports.json')
    assert not exports['failures']
    pageids = []
    for batch in exports['batches']:
        path = (root/batch['path']).resolve()
        assert path.is_relative_to((root/'pages').resolve())
        raw = path.read_bytes()
        assert hashlib.sha256(raw).hexdigest() == batch['sha256']
        assert len(raw) == batch['size']
        tree = ET.fromstring(raw)
        rows = tree.findall('{*}page')
        ids = [int(p.find('{*}id').text) for p in rows]
        assert len(rows) == batch['pages'] and sorted(ids) == batch['pageids']
        assert all(len(p.findall('{*}revision')) == 1 for p in rows)
        pageids.extend(ids)
    assert len(pageids) == len(set(pageids)) == len(pages) == exports['exported_pages']
    assert set(pageids) == {p['pageid'] for p in pages}
    titles = {p['title'].replace('_',' ') for p in pages}
    missing_descriptions = [r['name'] for r in manifest
        if r.get('canonicaltitle',r['title']).replace('_',' ') not in titles]
    report = load('archive-report.json')
    assert not report['media_failed'] and not report['page_failures']
    assert report['media_listed'] == report['media_saved'] == len(manifest)
    assert report['media_original_verified'] == original and report['cdn_variants'] == variants
    assert report['unique_media_files'] == len(content)
    assert report['pages_exported'] == report['pages_listed'] == len(pages)
    assert report['youtube_thumbnail_references'] == thumbnails
    assert not report['youtube_video_streams_archived']
    result = {'status':'PASS','media_records':len(manifest),'unique_files':len(content),
        'original_hash_matches':original,'clearly_labeled_cdn_variants':variants,
        'youtube_thumbnails_not_video_streams':thumbnails,
        'unique_file_bytes':sum(r['size'] for r in content.values()),
        'current_pages':len(pages),'xml_batches':len(exports['batches']),
        'file_description_pages_missing':missing_descriptions,
        'checks':'API metadata unchanged; each actual size/SHA-1/SHA-256 and content-addressed filename; raster format/dimensions/verification; variant labels; XML hashes/page IDs/single revision; counts agree with report'}
    (root/'validation-report.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False,indent=2))
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('path',type=Path,nargs='?',default=Path('/workspace/external/divine-gate-wiki'))
    verify(parser.parse_args().path)
