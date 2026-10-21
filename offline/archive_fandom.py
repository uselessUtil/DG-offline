"""Archive public current pages and original media via MediaWiki API."""
import argparse
import concurrent.futures
import hashlib
import http.client
import io
import json
import mimetypes
from pathlib import Path
import re
import threading
import time
import urllib.error
import urllib.parse
import xml.etree.ElementTree as ET
from datetime import datetime, timezone

from PIL import Image

API = 'https://divine-gate.fandom.com/api.php'
USER_AGENT = 'DivineGateOfflineAssetArchive/1.0 (personal game preservation; read-only)'
HEADERS = {'User-Agent': USER_AGENT, 'Accept': '*/*'}
CONNECTIONS = threading.local()


def read_https(url, redirects=0):
    parts = urllib.parse.urlsplit(url)
    assert parts.scheme == 'https' and redirects <= 5
    if not hasattr(CONNECTIONS, 'pool'):
        CONNECTIONS.pool = {}
    key = (parts.hostname, parts.port or 443)
    connection = CONNECTIONS.pool.get(key)
    if connection is None:
        connection = http.client.HTTPSConnection(*key, timeout=60)
        CONNECTIONS.pool[key] = connection
    target = parts.path or '/'
    if parts.query:
        target += '?'+parts.query
    try:
        connection.request('GET', target, headers=HEADERS)
        response = connection.getresponse()
        raw, headers = response.read(), dict(response.headers)
    except (OSError, http.client.HTTPException):
        CONNECTIONS.pool.pop(key, None)
        connection.close()
        raise
    if response.status in (301,302,303,307,308):
        return read_https(urllib.parse.urljoin(url,response.headers['Location']),redirects+1)
    if response.status >= 400:
        raise urllib.error.HTTPError(url,response.status,response.reason,response.headers,None)
    return raw, headers, url


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name+'.part-'+str(threading.get_ident()))
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    temporary.replace(path)


def fetch(url, attempts=4):
    for attempt in range(attempts):
        try:
            return read_https(url)
        except urllib.error.HTTPError as exc:
            if exc.code in (401, 403, 404) or attempt == attempts-1:
                raise
            wait = exc.headers.get('Retry-After', '')
            time.sleep(int(wait) if wait.isdigit() else min(30, 2**attempt))
        except (OSError, TimeoutError, http.client.HTTPException):
            if attempt == attempts-1:
                raise
            time.sleep(min(30, 2**attempt))


class Archive:
    def __init__(self, output):
        self.output = output
        self.meta = output/'metadata'
        self.meta.mkdir(parents=True, exist_ok=True)
        self.api_last = 0
        self.api_lock = threading.Lock()
        self.previous = {}
        progress = self.meta/'image-results.jsonl'
        if progress.exists():
            for line in progress.read_text(encoding='utf-8').splitlines():
                try:
                    row = json.loads(line)
                    if row.get('status') in ('verified','cdn_variant'):
                        self.previous[(row['name'],row['sha1'])] = row
                except json.JSONDecodeError:
                    pass  # A terminated write may leave an incomplete last line.

    def api(self, params):
        params = dict(params, format='json', maxlag=5)
        for attempt in range(5):
            with self.api_lock:
                time.sleep(max(0, .35-(time.monotonic()-self.api_last)))
                raw, _, _ = fetch(API+'?'+urllib.parse.urlencode(params))
                self.api_last = time.monotonic()
            if raw.lstrip().startswith(b'{'):
                data = json.loads(raw)
                if data.get('error', {}).get('code') == 'maxlag':
                    time.sleep(5*(attempt+1))
                    continue
                if 'error' in data:
                    raise RuntimeError(str(data['error']))
            return raw
        raise RuntimeError('MediaWiki maxlag: try resuming later')

    def api_json(self, params, path):
        if path.exists():
            return json.loads(path.read_text(encoding='utf-8'))
        value = json.loads(self.api(params))
        write_json(path, value)
        return value

    def inventory(self):
        site = self.api_json({'action':'query', 'meta':'siteinfo',
            'siprop':'general|statistics|namespaces|rightsinfo'}, self.meta/'siteinfo.json')['query']
        assert site['general']['wikiid'] == 'divinegate'
        images, continuation, page = [], {}, 0
        while True:
            data = self.api_json(dict(action='query', list='allimages', ailimit=500,
                aiprop='url|size|sha1|mime|timestamp|user|canonicaltitle', **continuation),
                self.meta/'image-list'/f'{page:04}.json')
            images.extend(data['query']['allimages'])
            page += 1
            if 'continue' not in data:
                break
            continuation = data['continue']
        assert len({row['name'] for row in images}) == len(images)
        write_json(self.meta/'images.json', images)
        print(json.dumps({'phase':'image_inventory', 'files':len(images),
            'original_bytes':sum(row['size'] for row in images)}), flush=True)
        pages = []
        for ns in sorted(int(n) for n in site['namespaces'] if int(n) >= 0):
            continuation, batch = {}, 0
            while True:
                data = self.api_json(dict(action='query', list='allpages', apnamespace=ns,
                    aplimit=500, **continuation), self.meta/'page-list'/f'ns{ns}-{batch:04}.json')
                pages.extend(data['query']['allpages'])
                batch += 1
                if 'continue' not in data:
                    break
                continuation = data['continue']
        assert len({row['pageid'] for row in pages}) == len(pages)
        pages.sort(key=lambda row:row['pageid'])
        write_json(self.meta/'pages.json', pages)
        if not (self.meta/'snapshot.json').exists():
            write_json(self.meta/'snapshot.json', {'source':API, 'created_utc':datetime.now(timezone.utc).isoformat(),
                'scope':'public current pages in all enumerated namespaces; latest uploaded media; no deleted/private data or historical revisions',
                'pages':len(pages), 'media_files':len(images), 'rightsinfo':site.get('rightsinfo')})
        print(json.dumps({'phase':'page_inventory', 'pages':len(pages)}), flush=True)
        return images, pages

    def export_pages(self, pages):
        folder = self.output/'pages'
        folder.mkdir(exist_ok=True)
        batches, failures = [], []
        for offset in range(0, len(pages), 50):
            group = pages[offset:offset+50]
            expected = {row['pageid'] for row in group}
            path = folder/f'current-{offset//50:04}.xml'
            try:
                params = {'action':'query', 'pageids':'|'.join(str(row['pageid']) for row in group), 'export':1, 'exportnowrap':1}
                raw = path.read_bytes() if path.exists() else self.api(params)
                try:
                    ET.fromstring(raw)
                except ET.ParseError:
                    raw = self.api(params)  # Repair an interrupted local XML write.
                tree = ET.fromstring(raw)
                assert tree.tag.endswith('mediawiki'), tree.tag
                exported = tree.findall('{*}page')
                actual = {int(p.find('{*}id').text) for p in exported}
                assert actual == expected, ('XML page coverage mismatch', sorted(expected-actual))
                assert all(len(p.findall('{*}revision')) == 1 for p in exported), 'Expected current revisions only'
                temporary = path.with_suffix('.xml.part')
                temporary.write_bytes(raw)
                temporary.replace(path)
                batches.append({'path':path.relative_to(self.output).as_posix(), 'pages':len(actual),
                    'pageids':sorted(actual), 'sha256':hashlib.sha256(raw).hexdigest(), 'size':len(raw)})
            except Exception as exc:
                failures.append({'pageids':sorted(expected), 'error':str(exc)})
            if offset % 500 == 0 or offset+50 >= len(pages):
                print(json.dumps({'phase':'page_export', 'processed':min(offset+50,len(pages)),
                    'total':len(pages), 'failed_batches':len(failures)}), flush=True)
        result = {'exported_pages':sum(row['pages'] for row in batches), 'batches':batches, 'failures':failures}
        write_json(self.meta/'page-exports.json', result)
        return result

    def cache(self, images, directories):
        wanted = {row['sha1'] for row in images}
        cached = {}
        index = self.meta/'local-cache.json'
        if index.exists():
            for digest,name in json.loads(index.read_text()).items():
                path = Path(name)
                if digest in wanted and path.is_file() and hashlib.sha1(path.read_bytes()).hexdigest() == digest:
                    cached[digest] = path
            print(json.dumps({'phase':'local_cache', 'matching_hashes':len(cached)}), flush=True)
            return cached
        for directory in directories:
            if not directory.exists():
                continue
            for path in directory.rglob('*'):
                if path.is_file() and path.suffix.lower() in ('.png','.jpg','.jpeg','.gif','.webp','.svg'):
                    digest = hashlib.sha1(path.read_bytes()).hexdigest()
                    if digest in wanted:
                        cached.setdefault(digest, path)
        print(json.dumps({'phase':'local_cache', 'matching_hashes':len(cached)}), flush=True)
        write_json(index,{digest:str(path) for digest,path in cached.items()})
        return cached

    def media(self, row, cache):
        digest = row['sha1']
        assert re.fullmatch('[0-9a-f]{40}', digest), ('Unexpected MediaWiki SHA-1', row['name'])
        suffix = { 'image/png':'.png', 'image/jpeg':'.jpg', 'image/gif':'.gif',
            'image/webp':'.webp', 'image/svg+xml':'.svg', 'video/youtube':'.jpg' }.get(row['mime'])
        if suffix is None:
            suffix = mimetypes.guess_extension(row['mime']) or '.bin'
        path = self.output/'images'/digest[:2]/(digest+suffix)
        # Asking for original is essential: the default CDN URL may return a
        # WebP conversion even when the source file has a .png filename.
        parts = urllib.parse.urlsplit(row['url'])
        assert parts.scheme == 'https' and parts.hostname.endswith(('.wikia.nocookie.net','.wikia.com')), row['url']
        query = dict(urllib.parse.parse_qsl(parts.query)); query['format'] = 'original'
        url = urllib.parse.urlunsplit(parts._replace(query=urllib.parse.urlencode(query)))
        source, final_url = 'existing_verified', url
        prior = self.previous.get((row['name'],digest))
        if not path.exists() and prior and prior.get('status') == 'cdn_variant':
            candidate = (self.output/prior['path']).resolve()
            assert candidate.is_relative_to((self.output/'images').resolve())
            if candidate.exists():
                data = candidate.read_bytes()
                if hashlib.sha256(data).hexdigest() == prior['sha256']:
                    path = candidate
        if path.exists():
            raw = path.read_bytes()
        elif digest in cache:
            raw = cache[digest].read_bytes()
            source = 'verified_local_cache'
        else:
            raw, _, final_url = fetch(url)
            source = 'fandom_original_cdn'
        actual_sha1 = hashlib.sha1(raw).hexdigest()
        original_match = len(raw) == row['size'] and actual_sha1 == digest
        decoded = None
        if row['mime'] in ('image/png','image/jpeg','image/gif','image/webp','video/youtube'):
            with Image.open(io.BytesIO(raw)) as image:
                assert image.size == (row['width'],row['height']), ('Dimensions mismatch',row['name'])
                decoded = image.format
                image.verify()
        if not original_match:
            # Some JPEGs are transformed by Fandom even with format=original.
            # Preserve a clearly marked same-format/full-size CDN candidate;
            # never claim its bytes match the uploaded original.
            expected_format = {'image/png':'PNG','image/jpeg':'JPEG','image/gif':'GIF','image/webp':'WEBP','video/youtube':'JPEG'}.get(row['mime'])
            assert decoded is not None and decoded == expected_format, ('Unverified CDN format',row['name'])
            path = self.output/'images'/actual_sha1[:2]/(actual_sha1+suffix)
            source = 'cdn_variant' if source != 'existing_verified' else 'existing_cdn_variant'
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists():
            temporary = path.with_name(path.name+'.part-'+str(threading.get_ident()))
            temporary.write_bytes(raw)
            temporary.replace(path)
        return dict(row, status='verified' if original_match else 'cdn_variant',
            media_kind='external_video_thumbnail' if row['mime']=='video/youtube' else 'wiki_image',
            downloaded_mime='image/jpeg' if row['mime']=='video/youtube' else row['mime'],
            original_bytes_match=original_match, downloaded_size=len(raw), downloaded_sha1=actual_sha1,
            variant_note=None if original_match else 'Same format and dimensions, but CDN bytes differ from the API upload record; not verified as the byte-original.',
            path=path.relative_to(self.output).as_posix(),
            sha256=hashlib.sha256(raw).hexdigest(), retrieval=source, download_url=url,
            final_url=final_url, decoded_format=decoded)

    def download_media(self, images, cache, workers):
        results = []
        with (self.meta/'image-results.jsonl').open('w', encoding='utf-8') as log:
            with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as pool:
                futures = {pool.submit(self.media,row,cache):row for row in images}
                for future in concurrent.futures.as_completed(futures):
                    row = futures[future]
                    try:
                        result = future.result()
                    except Exception as exc:
                        result = dict(row, status='failed', error=str(exc))
                    results.append(result)
                    log.write(json.dumps(result,ensure_ascii=False)+'\n'); log.flush()
                    if len(results)%100 == 0 or len(results)==len(images):
                        print(json.dumps({'phase':'media', 'processed':len(results), 'total':len(images),
                            'failures':sum(r['status']=='failed' for r in results)}), flush=True)
        results.sort(key=lambda r:r['name'])
        write_json(self.meta/'image-manifest.json', results)
        return results

    def catalog(self, results):
        rows = [{'name':r['name'], 'path':r['path'], 'width':r.get('width'), 'height':r.get('height'),
            'mime':r.get('downloaded_mime',r['mime']), 'source':r['descriptionurl'], 'status':r['status'],
            'media_kind':r.get('media_kind','wiki_image')}
            for r in results if r['status'] in ('verified','cdn_variant')]
        payload = json.dumps(rows,ensure_ascii=False).replace('<','\\u003c')
        page = '''<!doctype html><meta charset="utf-8"><title>Divine Gate Wiki 素材庫</title>
<style>body{font:16px sans-serif;background:#202126;color:#eee;margin:24px}input{padding:12px;width:min(80%,600px)}
button{padding:8px;margin:8px}#grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:12px}
article{background:#30313a;padding:12px;overflow-wrap:anywhere}img{width:100%;height:160px;object-fit:contain;background:repeating-conic-gradient(#aaa 0% 25%,#ddd 0% 50%) 0/16px 16px}a{color:#9dd1ff}small{display:block}</style>
<h1>Divine Gate Wiki 素材庫</h1><p>原始檔案；依檔名搜尋。來源、上傳者與雜湊見 metadata/image-manifest.json。</p>
<input id="search" placeholder="例如 town、icon、boss、gate"><button id="prev">上一頁</button><button id="next">下一頁</button><p id="info"></p><div id="grid"></div>
<script>const rows=PAYLOAD;let page=0;const size=100;const q=document.querySelector('#search');
function render(){const found=rows.filter(r=>r.name.toLowerCase().includes(q.value.toLowerCase()));page=Math.max(0,Math.min(page,Math.ceil(found.length/size)-1));
document.querySelector('#info').textContent=found.length+' 個素材，第 '+(page+1)+' 頁';const grid=document.querySelector('#grid');grid.replaceChildren();
for(const row of found.slice(page*size,(page+1)*size)){const card=document.createElement('article');const link=document.createElement('a');link.href=row.path;link.textContent=row.name;
if(['image/png','image/jpeg','image/gif','image/webp'].includes(row.mime)){const img=document.createElement('img');img.loading='lazy';img.src=row.path;img.alt=row.name;card.append(img)}
card.append(link);const dimensions=document.createElement('small');dimensions.textContent=(row.width||'?')+' × '+(row.height||'?');card.append(dimensions);
const state=document.createElement('small');state.textContent=row.status==='verified'?'原檔雜湊相符':'CDN 版本：與上傳原檔雜湊不同';card.append(state);
if(row.media_kind==='external_video_thumbnail'){const note=document.createElement('small');note.textContent='YouTube 縮圖，非影片本體';card.append(note)}
const source=document.createElement('a');source.href=row.source;source.textContent='Wiki 來源頁';card.append(source);grid.append(card)}}
q.oninput=()=>{page=0;render()};document.querySelector('#prev').onclick=()=>{page--;render()};document.querySelector('#next').onclick=()=>{page++;render()};render();</script>'''
        (self.output/'catalog.html').write_text(page.replace('PAYLOAD',payload),encoding='utf-8')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output',type=Path,default=Path('/workspace/external/divine-gate-wiki'))
    parser.add_argument('--workers',type=int,default=4,choices=range(1,5))
    args = parser.parse_args()
    archive = Archive(args.output)
    images,pages = archive.inventory()
    cache = archive.cache(images,[Path('/workspace/external/dgdb/public/img'),
        Path('/workspace/artifacts/fandom-area-search-2026-10-20')])
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        export_future = pool.submit(archive.export_pages,pages)
        results = archive.download_media(images,cache,args.workers)
        exports = export_future.result()
    archive.catalog(results)
    saved = [r for r in results if r['status'] in ('verified','cdn_variant')]
    verified = [r for r in results if r['status']=='verified']
    report = {'source':'https://divine-gate.fandom.com/', 'api':API,
        'completed_utc':datetime.now(timezone.utc).isoformat(),
        'scope':'current public page revisions and current CDN media; byte-original matches and CDN variants labeled separately; YouTube records are thumbnails, not video streams; not full history or a rendered website mirror',
        'media_listed':len(images), 'media_saved':len(saved), 'media_original_verified':len(verified),
        'cdn_variants':len(saved)-len(verified),
        'unique_media_files':len({r['path'] for r in saved}), 'media_failed':[r for r in results if r['status']=='failed'],
        'youtube_thumbnail_references':sum(r['mime']=='video/youtube' for r in saved), 'youtube_video_streams_archived':False,
        'saved_reference_bytes':sum(r['downloaded_size'] for r in saved),
        'pages_listed':len(pages), 'pages_exported':exports['exported_pages'], 'page_failures':exports['failures'],
        'validation':'Originals match API size/SHA-1; CDN variants are separately labeled and match original format/dimensions. All saved media have actual SHA-1/SHA-256 and raster verification. XML page IDs and one current revision per page checked.',
        'apk_modified':False}
    write_json(args.output/'archive-report.json',report)
    print(json.dumps({k:v for k,v in report.items() if k not in ('media_failed','page_failures')},ensure_ascii=False,indent=2),flush=True)
    if report['media_failed'] or report['page_failures']:
        raise SystemExit('Archive has unresolved failures. Resume the same command to retry; verified files are preserved.')


if __name__ == '__main__':
    main()
