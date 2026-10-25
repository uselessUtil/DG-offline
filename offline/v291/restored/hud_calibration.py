"""Five separately fitted HUD layouts; grouped held-out validation, no player data."""
from statistics import median
import math

def solve(a,b):
    n=len(b);m=[list(a[i])+[b[i]] for i in range(n)]
    for j in range(n):
        k=max(range(j,n),key=lambda k:abs(m[k][j]))
        if abs(m[k][j])<1e-12:raise ValueError('Singular HUD calibration')
        m[j],m[k]=m[k],m[j];v=m[j][j];m[j]=[x/v for x in m[j]]
        for i in range(n):
            if i==j:continue
            v=m[i][j];m[i]=[x-v*y for x,y in zip(m[i],m[j])]
    return [r[-1] for r in m]

def features(match):
    x,y,w,h=match['match_rect_256']
    return [1,(x+w/2)/256,1-(y+h/2)/256,h/256]

def original(c,slot):
    k=str(4-slot);s=c['img_'+k+'_tiling']/1000
    return [c['img_'+k+'_offsetX']/1000,c['img_'+k+'_offsetY']/1000,s,s]

def target(c,slot):
    x,y,s,_=original(c,slot);return [x+s/2,y+s/2,s]

def regression(xs,ys):
    weights=[1.]*len(xs);n=len(xs[0]);coef=None
    for _ in range(10):
        a=[[sum(w*x[i]*x[j] for w,x in zip(weights,xs))+(1e-6 if i==j else 0) for j in range(n)] for i in range(n)]
        b=[sum(w*x[i]*y for w,x,y in zip(weights,xs,ys)) for i in range(n)]
        coef=solve(a,b);res=[abs(y-sum(v*z for v,z in zip(coef,x))) for x,y in zip(xs,ys)]
        cutoff=max(.004,1.5*median(res));weights=[min(1,cutoff/max(v,1e-10)) for v in res]
    return coef

def fit(chars,matches):
    xs=[features(matches[c['fix_id']]) for c in chars]
    return [[regression(xs,[target(c,slot)[axis] for c in chars]) for axis in range(3)] for slot in range(5)]

def predict(model,match,slot):
    f=features(match);cx,cy,size=[sum(a*b for a,b in zip(coeff,f)) for coeff in model[slot]]
    size=max(.04,min(1,size))
    return [max(0,min(1-size,cx-size/2)),max(0,min(1-size,cy-size/2)),size,size]

def overlap(a,b):
    x,y,w,h=a;u,v,s,t=b
    area=max(0,min(x+w,u+s)-max(x,u))*max(0,min(y+h,v+t)-max(y,v))
    return area/(w*h+s*t-area) if w*h+s*t>area else 0

def calibration(characters,matches,evolutions):
    accepted=[c for c in characters if matches[c['fix_id']]['normalized_error']<=.055]
    # Union entire evolution families and repeated draw IDs before fold assignment.
    parent={c['fix_id']:c['fix_id'] for c in characters}
    def find(x):
        while parent[x]!=x:parent[x]=parent[parent[x]];x=parent[x]
        return x
    def union(a,b):
        if a in parent and b in parent:parent[find(a)]=find(b)
    draws={}
    for c in characters:
        d=c['draw_id'];union(c['fix_id'],draws.setdefault(d,c['fix_id']))
    for e in evolutions:
        if e:union(e['unit_id_pre'],e['unit_id_after'])
    groups={}
    for c in accepted:groups.setdefault(find(c['fix_id']),[]).append(c)
    folds=[[] for _ in range(5)]
    for _,cs in sorted(groups.items(),key=lambda kv:(-len(kv[1]),kv[0])):
        folds[min(range(5),key=lambda i:len(folds[i]))]+=cs
    held=[];metrics=[[] for _ in range(5)];errors=[[] for _ in range(5)]
    for fold,validation in enumerate(folds):
        ids={c['fix_id'] for c in validation};training=[c for c in accepted if c['fix_id']not in ids]
        model=fit(training,matches)
        for c in validation:
            predicted=[predict(model,matches[c['fix_id']],slot) for slot in range(5)]
            actual=[original(c,slot) for slot in range(5)]
            ious=[overlap(a,b) for a,b in zip(predicted,actual)]
            center=[math.hypot((a[0]+a[2]/2)-(b[0]+b[2]/2),(a[1]+a[3]/2)-(b[1]+b[3]/2))/b[2] for a,b in zip(predicted,actual)]
            for slot in range(5):metrics[slot].append(ious[slot]);errors[slot].append(center[slot])
            held.append({'id':c['fix_id'],'name':c['name'],'fold':fold,'group':find(c['fix_id']),'predicted':predicted,'official':actual,'iou':ious,'center_error_in_official_side':center})
    result={'method':'Per-slot Huber robust affine regression: native crop centerX,centerY,size from matched face centerX,centerY,height. Separate rules for five native positions.',
        'native_reference_count':len(characters),'match_error_threshold':.055,'accepted_reference_count':len(accepted),
        'folds':[len(v) for v in folds],'grouping':'Entire evolution families and repeated draw IDs kept in one fold.',
        'slot_to_img_field':[4,3,2,1,0],'median_iou':[median(v) for v in metrics],
        'median_center_error_in_official_side':[median(v) for v in errors],
        'held_out_predictions':sorted(held,key=lambda x:x['id']),
        'review_native_examples':[r for r in held if min(r['iou'])<.45],
        'limitations':'Coordinates inferred, not official recovered metadata. Good icon match does not certify composition; material/giant/low-match cases require separate review. Original595 crops remain unmodified.'}
    result['final_model']=fit(accepted,matches)
    return result
