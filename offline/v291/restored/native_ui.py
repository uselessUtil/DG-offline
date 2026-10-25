"""Recover only named UI pixels/rectangles from the later DG0 cache.
Never import later master data, scripts, meshes or account state.
"""
import hashlib,json
from pathlib import Path
import UnityPy

ROOT=Path('/workspace')
SOURCES={
 '1e6554ee7d0a9308e563a370ae346952bb660397':'481eee2fdd1ff4a057dcf0e38ad5fe229d19ec8939bab999c2fa85bc2a289e80',
 'af346cd4436aaf8fe038237b5a2c6050ef3de797':'ad67dc9145341fc305574893b4c0215d21e165ee637a66880189e8c639ca4dbe',
}
# Exact source bundles; never search an arbitrary similarly named cache at runtime.
def load():
    root=ROOT/'jp.gungho.dg(DG0)/files/UnityCache/Shared'
    result={}
    # Only these fingerprinted atlas bundles; no master/model substitution.
    for folder,expected in SOURCES.items():
        path=root/folder/'__data';raw=path.read_bytes()
        digest=hashlib.sha256(raw).hexdigest()
        assert digest==expected,('DG0 source changed',str(path))
        env=UnityPy.load(raw)
        texts={o.read().m_Name:json.loads(o.read().m_Script)['frames']
               for o in env.objects if o.type.name=='TextAsset' and o.read().m_Name.startswith(('AtlasAreaIcon','AtlasPanelIcon'))}
        images={o.read().m_Name:(o.read().image.convert('RGBA'),o.path_id)
                for o in env.objects if o.type.name=='Texture2D' and o.read().m_Name.startswith(('AtlasAreaIcon','AtlasPanelIcon'))}
        for obj in env.objects:
            if obj.type.name!='MonoBehaviour':continue
            tree=obj.read_typetree(check_read=False)
            if 'sprites' not in tree:continue
            game=next((o.read() for o in env.objects if o.path_id==tree['m_GameObject']['m_PathID']),None)
            name=getattr(game,'m_Name','')
            if name not in images:continue
            assert tree['mCoordinates']==0 and tree['mPixelSize']==1 and tree['mReplacement']['m_PathID']==0
            image,texture_id=images[name]
            # The original UIAtlas is authoritative. Two TexturePacker labels have
            # an extra ".png"; its rectangle still has to agree with UIAtlas.
            json_rects={tuple(f['frame'][k] for k in ['x','y','w','h']) for f in texts[name].values()}
            for sprite in tree['sprites']:
                assert sprite['outer']==sprite['inner'] and not sprite['rotated']
                assert all(sprite[k]==0 for k in ['paddingLeft','paddingRight','paddingTop','paddingBottom'])
                r=sprite['outer'];rect=tuple(int(r[k]) for k in ['x','y','width','height'])
                assert rect in json_rects,(name,sprite['name'])
                x,y,w,h=rect;assert x>=0 and y>=0 and w>0 and h>0 and x+w<=image.width and y+h<=image.height
                key=sprite['name'];category='area' if name.startswith('AtlasAreaIcon') else 'panel'
                pair=(category,key);assert pair not in result,pair
                result[pair]=(image.crop((x,y,x+w,y+h)),{
                    'source':str(path.relative_to(ROOT)),'source_sha256':digest,
                    'atlas':name,'sprite':key,'source_rect':list(rect),'texture_path_id':texture_id,
                    'atlas_path_id':obj.path_id,'rect_verified':'native UIAtlas and matching TexturePacker rectangle'})
    assert len([k for k in result if k[0]=='area'])==120
    assert len([k for k in result if k[0]=='panel'])==310
    return result
