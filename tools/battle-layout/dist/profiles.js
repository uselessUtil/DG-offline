export const FORMAT="dg-battle-presentation";
export const SLOT_LABELS=["隊長 · 右側大格","隊員 1 · 右上","隊員 2 · 右下","隊員 3 · 左上","好友 · 左下"];
export const ENEMY_FIELDS=["pos_absolute","posx_offset","posy_offset","posz_value","hp_gauge_type","hp_posx_offset","hp_posy_offset"];
export const SIZE_FIELDS=["size_width","size_height","pivot","side_offset"];
export const VISUAL_FIELDS=[...Array.from({length:5},(_,i)=>["tiling","offsetX","offsetY"].map(k=>"img_"+i+"_"+k)).flat(),"img_cutin_tiling","img_cutin_offsetX","img_cutin_offsetY","size_width","size_height","pivot","side_offset"];
export function limits(key){if(key.includes("tiling"))return[1,4000];if(key.startsWith("img_"))return[-4000,4000];if(key==="size_width"||key==="size_height")return[16,1280];if(key==="pivot")return[1,2];if(key==="side_offset")return[0,640];if(key==="pos_absolute")return[0,2];if(key==="hp_gauge_type")return[0,4];return[-2000,2000];}
export function makeProfile(unit){return{fields:{...unit.fields},enemyOverrides:{},completed:false,note:""};}
export function needsEnemySize(unit){return !!unit.missingSize&&unit.enemies.length>0;}
export function validateFile(value,catalog){
 if(!value||value.format!==FORMAT||value.schema!==1)throw Error("不是本工具支援的調整檔（schema 1）。");
 if(value.data_version!==catalog.dataVersion||value.source_apk_sha256!==catalog.sourceApkSha256)throw Error("資料版本或來源 APK 不相同，未匯入任何資料。");
 if(!Array.isArray(value.characters)||value.characters.length>catalog.units.length)throw Error("角色清單不合法。");
 const byId=new Map(catalog.units.map(c=>[c.id,c])),out={},seen=new Set();
 const check=(key,n)=>{const[a,b]=limits(key);if(!Number.isInteger(n)||n<a||n>b)throw Error(key+" 必須是 "+a+"～"+b+" 的整數。");};
 for(const row of value.characters){
  if(!Number.isInteger(row.id)||!byId.has(row.id)||seen.has(row.id))throw Error("角色 ID 不存在或重複。");seen.add(row.id);
  const unit=byId.get(row.id),profile=makeProfile(unit);
  if(!row.fields||typeof row.fields!=="object"||Array.isArray(row.fields))throw Error("顯示欄位格式不正確。");
  for(const[key,n]of Object.entries(row.fields)){if(!VISUAL_FIELDS.includes(key))throw Error("不允許的欄位："+key);check(key,n);profile.fields[key]=n;}
  if(row.enemy_overrides!=null){
   if(typeof row.enemy_overrides!=="object"||Array.isArray(row.enemy_overrides))throw Error("敵人設定格式不正確。");
   for(const[eid,fields]of Object.entries(row.enemy_overrides)){
    if(!unit.enemies.some(e=>String(e.fix_id)===eid))throw Error("敵人設定不屬於這名角色。");
    if(!fields||typeof fields!=="object"||Array.isArray(fields))throw Error("敵人顯示欄位格式不正確。");
    const values={};for(const[key,n]of Object.entries(fields)){if(!ENEMY_FIELDS.includes(key))throw Error("不允許改寫戰鬥數值："+key);check(key,n);values[key]=n;}profile.enemyOverrides[eid]=values;
   }
  }
  if(typeof row.note!=="undefined"&&(typeof row.note!=="string"||row.note.length>1000))throw Error("備註超過 1000 字或格式錯誤。");
  if(row.completed!=null&&typeof row.completed!=="boolean")throw Error("完成狀態格式不正確。");
  // Earlier full-field drafts accidentally recorded the inherited large template
  // even when only a crop/note was edited. Replace only that unchanged quartet.
  if(value.visual_revision!==2&&!unit.native&&unit.sizeAdapter&&SIZE_FIELDS.every(k=>profile.fields[k]===unit.masterFields[k]))for(const k of SIZE_FIELDS)profile.fields[k]=unit.fields[k];
  profile.note=row.note||"";profile.completed=!!row.completed;out[row.id]=profile;
 }return out;
}
export function createExport(profiles,catalog){
 return{format:FORMAT,schema:1,visual_revision:2,data_version:catalog.dataVersion,source_apk_sha256:catalog.sourceApkSha256,created_at:new Date().toISOString(),description:"Native visual fields only. DGDB size category mapped to native dimensions/pivot/spacing. img_4=leader/right, img_0=friend/left. Party slot0..4 uses img4..0. Cutin scaleY=scaleX*0.15. Apply HUD values to r17 runtime override as well as native fields.",characters:Object.entries(profiles).map(([id,p])=>({id:+id,fields:p.fields,enemy_overrides:p.enemyOverrides,completed:p.completed,note:p.note,party_slot_rects:Array.from({length:5},(_,slot)=>{let n=4-slot;return[p.fields["img_"+n+"_offsetX"]/1000,p.fields["img_"+n+"_offsetY"]/1000,p.fields["img_"+n+"_tiling"]/1000,p.fields["img_"+n+"_tiling"]/1000];})}))};
}
