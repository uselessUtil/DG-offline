"""Map DGDB size categories to verified native visual profiles, not battle stats."""
from collections import Counter,defaultdict
import hashlib,json

FIELDS=('size_width','size_height','pivot','side_offset')
def key(c):return tuple(c[k] for k in FIELDS)
def profile(values):return dict(zip(FIELDS,values))
def canonical(value):return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':')).encode()).hexdigest()

def references(native,units):
    groups=defaultdict(list)
    for ident,c in sorted(native.items()):
        d=units[ident];size=d['size']
        assert size in (1,2,3),('Unknown native source size',ident,size)
        groups[size].append((ident,d['kind'],d['rare'],key(c)))
    assert set(groups)=={1,2,3}
    expected={1:{(640,640,1,320)},2:{(216,216,2,78),(284,284,2,105)},3:{(106,106,2,48),(140,140,2,65)}}
    assert {size:{r[3] for r in rows} for size,rows in groups.items()}==expected,'Native profile contract changed'
    return groups

def select(d,groups):
    rows=groups.get(d['size'])
    if not rows:raise ValueError(('Unmapped DGDB size',d['fix_id'],d['size']))
    # Finer native variants are inferred only from unambiguous reference sets.
    for reason,selected in [
        ('native-size-category-consensus',rows),
        ('native-size-kind-rarity-consensus',[r for r in rows if r[1]==d['kind'] and r[2]==d['rare']]),
        ('native-size-kind-consensus',[r for r in rows if r[1]==d['kind']])]:
        options={r[3]for r in selected}
        if len(options)==1:return next(iter(options)),reason,[r[0]for r in selected]
    counts=Counter(r[3]for r in rows)
    ordered=sorted(counts,key=lambda p:(-counts[p],p))
    assert len(ordered)==1 or counts[ordered[0]]>counts[ordered[1]],'Ambiguous modal visual profile'
    chosen=ordered[0]
    return chosen,'native-size-category-mode-fallback',[r[0]for r in rows if r[3]==chosen]

def apply(added,native,units):
    groups=references(native,units);changes=[];methods=Counter()
    for c in added:
        d=units[c['fix_id']];before=profile(key(c));chosen,reason,ids=select(d,groups)
        after=profile(chosen);c.update(after);methods[reason]+=1
        if before!=after:
            changes.append({'id':c['fix_id'],'name':c['name'],'dgdb_size':d['size'],
                'before':before,'after':after,'method':reason,'reference_ids':ids,
                'exact_official_per_character_layout':False})
    return {'revision':1,'source_field':'DGDB unit.size','changed_fields':list(FIELDS),
        'native_references':len(native),'native_profile_sha256':canonical({i:profile(key(c)) for i,c in sorted(native.items())}),
        'profiles':{str(size):[dict(count=n,**profile(p))for p,n in sorted(Counter(r[3]for r in rows).items())]for size,rows in groups.items()},
        'policy':'Keep595 original profiles. New size1 uses native large consensus. For size2/3 use a native size/kind/rarity or size/kind consensus; otherwise native size modal profile. Exact historical per-character sub-size is unavailable; inference is explicit.',
        'selection_methods':dict(methods),'new_size_counts':dict(Counter(units[c['fix_id']]['size']for c in added)),
        'changes':changes,'changed_count':len(changes),'value_sha256':canonical(changes)}

def verify_delta(before,after,meta,original):
    a={c['fix_id']:c for c in before if c};b={c['fix_id']:c for c in after if c}
    assert a.keys()==b.keys()
    changed={i for i in a if a[i]!=b[i]}
    assert changed=={r['id']for r in meta['changes']} and len(changed)==meta['changed_count']==304
    assert not changed & set(original)
    assert meta['value_sha256']==canonical(meta['changes'])
    for row in meta['changes']:
        i=row['id'];assert row['before']==profile(key(a[i])) and row['after']==profile(key(b[i]))
        assert all(a[i][k]==b[i][k] for k in a[i]if k not in FIELDS)
        assert row['dgdb_size']in (2,3) and b[i]['pivot']==2 and b[i]['size_width']<300 and b[i]['side_offset']<=105
    return len(changed)
