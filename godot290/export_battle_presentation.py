"""Export reference battle motion curves and particle prefabs, without modifying APK assets."""
import json,hashlib,math
from inspect_ui import objects,read,deref
from prepare import PROJECT,WORK,SOURCE,EXPECTED
from export_battle_assets import identity,matrix,mul
from UnityPy.helpers.MeshHelper import MeshHandler
EFFECT_FIELDS={1: [0, 'm_EffectNaught_MA_00'], 2: [0, 'm_EffectNaught_MA_01'], 3: [0, 'm_EffectNaught_MM_00'], 4: [0, 'm_EffectNaught_MM_01'], 5: [2, 'm_EffectNaught_SA_00'], 6: [2, 'm_EffectNaught_SA_01'], 7: [2, 'm_EffectNaught_SA_02'], 8: [2, 'm_EffectNaught_SM_00'], 9: [2, 'm_EffectNaught_SM_01'], 10: [2, 'm_EffectNaught_SM_02'], 11: [0, 'm_EffectFire_MA_00'], 12: [0, 'm_EffectFire_MA_01'], 13: [0, 'm_EffectFire_MM_00'], 14: [0, 'm_EffectFire_MM_01'], 15: [2, 'm_EffectFire_SA_00'], 16: [2, 'm_EffectFire_SA_01'], 17: [2, 'm_EffectFire_SA_02'], 18: [2, 'm_EffectFire_SM_00'], 19: [2, 'm_EffectFire_SM_01'], 20: [2, 'm_EffectFire_SM_02'], 21: [0, 'm_EffectWater_MA_00'], 22: [0, 'm_EffectWater_MA_01'], 23: [0, 'm_EffectWater_MM_00'], 24: [0, 'm_EffectWater_MM_01'], 25: [2, 'm_EffectWater_SA_00'], 26: [2, 'm_EffectWater_SA_01'], 27: [2, 'm_EffectWater_SA_02'], 28: [2, 'm_EffectWater_SM_00'], 29: [2, 'm_EffectWater_SM_01'], 30: [2, 'm_EffectWater_SM_02'], 31: [0, 'm_EffectWind_MA_00'], 32: [0, 'm_EffectWind_MA_01'], 33: [0, 'm_EffectWind_MM_00'], 34: [0, 'm_EffectWind_MM_01'], 35: [2, 'm_EffectWind_SA_00'], 36: [2, 'm_EffectWind_SA_01'], 37: [2, 'm_EffectWind_SA_02'], 38: [2, 'm_EffectWind_SM_00'], 39: [2, 'm_EffectWind_SM_01'], 40: [2, 'm_EffectWind_SM_02'], 41: [0, 'm_EffectLight_MA_00'], 42: [0, 'm_EffectLight_MA_01'], 43: [0, 'm_EffectLight_MM_00'], 44: [0, 'm_EffectLight_MM_01'], 45: [2, 'm_EffectLight_SA_00'], 46: [2, 'm_EffectLight_SA_01'], 47: [2, 'm_EffectLight_SA_02'], 48: [2, 'm_EffectLight_SM_00'], 49: [2, 'm_EffectLight_SM_01'], 50: [2, 'm_EffectLight_SM_02'], 51: [0, 'm_EffectDark_MA_00'], 52: [0, 'm_EffectDark_MA_01'], 53: [0, 'm_EffectDark_MM_00'], 54: [0, 'm_EffectDark_MM_01'], 55: [2, 'm_EffectDark_SA_00'], 56: [2, 'm_EffectDark_SA_01'], 57: [2, 'm_EffectDark_SA_02'], 58: [2, 'm_EffectDark_SM_00'], 59: [2, 'm_EffectDark_SM_01'], 60: [2, 'm_EffectDark_SM_02'], 61: [1, 'm_EffectHeal_00'], 62: [1, 'm_EffectHeal_01'], 63: [1, 'm_EffectHeal_02'], 64: [1, 'm_EffectHeal_03'], 65: [1, 'm_EffectHeal_SP'], 68: [2, 'm_EffectBlood'], 69: [0, 'm_EffectPoison'], 70: [1, 'm_EffectBuff'], 71: [0, 'm_EffectDebuff'], 72: [1, 'm_EffectEnemySkillFire00'], 75: [1, 'm_EffectEnemySkillWind00'], 76: [1, 'm_EffectEnemySkillWater00'], 77: [1, 'm_EffectEnemySkillLight00'], 78: [1, 'm_EffectEnemySkillDark00'], 79: [1, 'm_EffectEnemySkillNaught00'], 92: [2, 'm_EffectEnemySkillHeal_S'], 93: [0, 'm_EffectEnemySkillHeal_M'], 73: [0, 'm_EffectEnemySkillBuff'], 74: [0, 'm_EffectEnemySkillDebuff'], 86: [0, 'm_Effect_HM_M_Fire'], 87: [0, 'm_Effect_HM_M_Water'], 88: [0, 'm_Effect_HM_M_Wind'], 89: [0, 'm_Effect_HM_M_Light'], 90: [0, 'm_Effect_HM_M_Dark'], 91: [0, 'm_Effect_HM_M_Naught'], 80: [2, 'm_Effect_HM_S_Fire'], 81: [2, 'm_Effect_HM_S_Water'], 82: [2, 'm_Effect_HM_S_Wind'], 83: [2, 'm_Effect_HM_S_Light'], 84: [2, 'm_Effect_HM_S_Dark'], 85: [2, 'm_Effect_HM_S_Naught'], 94: [1, 'm_EffectPlayerSkillBuff'], 95: [1, 'm_EffectPlayerSkillDebuff']}
OUT=PROJECT/'Reference/battle';OUT.mkdir(exist_ok=True)
textures={};issues=[]
def material(o):
 if o is None:return {}
 d=read(o);result={'name':d['m_Name'],'shader':read(deref(d['m_Shader'],o)).get('m_Name','')}
 for k,v in d.get('m_SavedProperties',{}).get('m_TexEnvs',[]):
  if (k.get('name') if isinstance(k,dict) else k)=='_MainTex' and v['m_Texture']['m_PathID']:
   t=deref(v['m_Texture'],o);key=t.assets_file.name+'_'+str(t.path_id);path='battle/fx_'+key+'.png'
   if key not in textures:t.read().image.save(PROJECT/'Reference'/path);textures[key]=path
   result.update(texture=path,scale=v['m_Scale'],offset=v['m_Offset'])
 return result

def walk(go,parent):
 d=read(go);cs=[deref(p,go) for _,p in d['m_Component']];tr=next(c for c in cs if c.type.name=='Transform');td=read(tr);world=mul(parent,matrix(td));result=[]
 ps=next((c for c in cs if c.type.name=='ParticleSystem'),None);pr=next((c for c in cs if c.type.name=='ParticleSystemRenderer'),None)
 if ps and pr:
  pd=read(ps);rd=read(pr);m=material(deref(rd['m_Materials'][0],pr)) if rd['m_Materials'] else {}
  if m.get('texture'):
   result.append({'name':d['m_Name'],'matrix':world,'particle':pd,'renderer':rd,'material':m})
   if rd['m_RenderMode']!=0:issues.append({'node':d['m_Name'],'renderMode':rd['m_RenderMode']})
 for p in td['m_Children']:
  child=deref(p,tr);result+=walk(deref(read(child)['m_GameObject'],child),world)
 return result

def clean(v):
 if isinstance(v,float) and not math.isfinite(v):return str(v)
 if isinstance(v,dict):return {k:clean(x) for k,x in v.items()}
 if isinstance(v,list):return [clean(x) for x in v]
 return v

def main():
 assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==EXPECTED
 motions={}
 for file in ['sharedassets0.assets','sharedassets8.assets']:
  for o in objects(file).values():
   if o.type.name!='AnimationClip':continue
   d=read(o);curves={k:d[k] for k in ['m_PositionCurves','m_ScaleCurves','m_RotationCurves','m_FloatCurves']};duration=max([k['time'] for group in curves.values() for c in group for k in c['curve']['m_Curve']]+[0])
   motions[d['m_Name']]={'duration':duration,'curves':curves}
 root=next(o for o in objects('level7').values() if o.type.name=='MonoBehaviour' and read(o).get('_class')=='SceneObjReferGameMain');scene=read(root)
 effects={}
 for id,(anchor,field) in EFFECT_FIELDS.items():
  go=deref(scene[field],root)
  if go:effects[str(id)]={'anchor':anchor,'name':read(go)['m_Name'],'nodes':walk(go,identity())}
 (OUT/'motions.json').write_text(json.dumps(clean(motions),separators=(',',':')))
 (OUT/'effects.json').write_text(json.dumps(clean(effects),separators=(',',':')))
 report={'reference_sha256':EXPECTED,'motion_clips':len(motions),'effects':len(effects),'textures':len(textures),'non_billboard_emitters':issues,'limitations':['2D projection of source particles; mesh/stretched rendering requires separate fidelity verification','Original particle random seeds are not replayable across engines']}
 (WORK/'battle-presentation-conversion.json').write_text(json.dumps(report,indent=2));print({k:v for k,v in report.items() if k!='non_billboard_emitters'},'non-billboard',len(issues))
if __name__=='__main__':main()
