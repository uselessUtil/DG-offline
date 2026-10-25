using UnityEngine;
public static partial class DivineGateOffline {
 public static Rect HudCrop291(uint id,int slot) {
  float[] r=RestoredHudLayouts.Get(id);
  if(r==null || slot<0 || slot>4)return new Rect(0,0,0,0);
  int start=slot*4;
  return new Rect(r[start],r[start+1],r[start+2],r[start+3]);
 }
 public static void FixBattleHudPortraits291(InGameManager game) {
  var scene=Singleton<SceneObjReferGameMain>.Instance;
  if(scene==null || game==null || game.m_PlayerPartyChara==null)return;
  var mats=new[]{scene.m_MaterialInstancePartyChara0,scene.m_MaterialInstancePartyChara1,
                 scene.m_MaterialInstancePartyChara2,scene.m_MaterialInstancePartyChara3,scene.m_MaterialInstancePartyChara4};
  for(int i=0;i<5 && i<game.m_PlayerPartyChara.Length;i++){
   var chara=game.m_PlayerPartyChara[i];
   if(chara==null || chara.m_CharaMasterDataParam==null || mats[i]==null)continue;
   Rect crop=HudCrop291(chara.m_CharaMasterDataParam.fix_id,i);if(crop.width<=0)continue;
   mats[i].SetTextureOffset("_MainTex",new Vector2(crop.x,crop.y));
   mats[i].SetTextureScale("_MainTex",new Vector2(crop.width,crop.height));
  }
 }
}
