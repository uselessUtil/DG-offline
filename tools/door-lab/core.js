export const STATES=["普通開門 · slot 0","敲門／晃動 · slot 1","關門／復位 · slot 2","Boss 開門 · slot 3"];
export const TYPES={original:"原始曲線",swing:"鉸鏈旋開",slideX:"水平滑開",slideZ:"垂直滑開",shrink:"水平收合"};
export function curve(keys,time){
 if(!keys.length)throw Error("空曲線");if(time<=keys[0].time)return keys[0].value.slice();if(time>=keys.at(-1).time)return keys.at(-1).value.slice();
 let j=keys.findIndex(k=>k.time>time),a=keys[j-1],b=keys[j],dt=b.time-a.time,u=(time-a.time)/dt;
 return a.value.map((v,i)=>{if(a.out[i]===null||b.in[i]===null)return v;return (2*u*u*u-3*u*u+1)*v+(u*u*u-2*u*u+u)*dt*a.out[i]+(-2*u*u*u+3*u*u)*b.value[i]+(u*u*u-u*u)*dt*b.in[i];});
}
export function quat(q){const n=Math.hypot(...q);return n?q.map(x=>x/n):[0,0,0,1];}
export function rotate(q,p){let[x,y,z,w]=quat(q),[a,b,c]=p;return[(1-2*y*y-2*z*z)*a+(2*x*y-2*z*w)*b+(2*x*z+2*y*w)*c,(2*x*y+2*z*w)*a+(1-2*x*x-2*z*z)*b+(2*y*z-2*x*w)*c,(2*x*z-2*y*w)*a+(2*y*z+2*x*w)*b+(1-2*x*x-2*y*y)*c];}
export function multiply(a,b){return[a[3]*b[0]+a[0]*b[3]+a[1]*b[2]-a[2]*b[1],a[3]*b[1]-a[0]*b[2]+a[1]*b[3]+a[2]*b[0],a[3]*b[2]+a[0]*b[1]-a[1]*b[0]+a[2]*b[3],a[3]*b[3]-a[0]*b[0]-a[1]*b[1]-a[2]*b[2]];}
export function easing(t,name){return name==="linear"?t:name==="overshoot"?1+2.70158*Math.pow(t-1,3)+1.70158*Math.pow(t-1,2):t*t*(3-2*t);}
export function defaultProfile(area){return{family:area.nativeRig?area.doorFamily:"fallback",duration:0,nodes:{},curves:{},note:"",completed:false};}
export function phaseFor(slot,phase){return slot===0||slot===3?phase:slot===2?1-phase:0;}
export function sourceDuration(family,profile,slot){return (profile.curves[slot]?Math.max(...profile.curves[slot].flatMap(t=>t.keys.map(k=>k.time))):0)||family.clips[slot].duration;}
export function transforms(family,profile,slot,phase){
 const clip=family.clips[slot],tracks=profile.curves[slot]||clip.tracks,time=phase*sourceDuration(family,profile,slot);
 const result=family.nodes.map(n=>({position:n.position.slice(),rotation:n.rotation.slice(),scale:n.scale.slice(),uvScale:(n.uvScale||[1,1]).slice(),uvOffset:(n.uvOffset||[0,0]).slice(),visible:n.active}));
 for(const track of tracks){const index=family.nodes.findIndex(n=>n.path===track.path);if(index<0)throw Error("曲線節點不存在");const target=result[index],v=curve(track.keys,time);
  if(track.kind!=="float")target[track.kind]=track.kind==="rotation"?quat(v):v;
  else {const attr=track.attribute;
   if(attr.includes("_MainTex")&&attr.endsWith(".x"))target.uvOffset[0]=v[0];
   else if(attr.includes("_MainTex")&&attr.endsWith(".y"))target.uvOffset[1]=v[0];
   else if(attr==="m_Enabled"||attr==="m_IsActive")target.visible=v[0]>.5;
   else throw Error("尚未支援的原始動畫屬性："+attr);
  }
 }
 for(let i=0;i<family.nodes.length;i++){
  const node=family.nodes[i],p=profile.nodes[node.path];if(!p)continue;
  const t=result[i];t.uvScale=t.uvScale.map((v,k)=>v*(k?p.uvScaleY:p.uvScaleX));t.uvOffset=t.uvOffset.map((v,k)=>v+(k?p.uvOffsetY:p.uvOffsetX));if(p.mode==="original"||slot===1)continue;
  Object.assign(t,{position:node.position.slice(),rotation:node.rotation.slice(),scale:node.scale.slice()});
  const f=easing(phaseFor(slot,phase),p.ease),travel=p.travel*f,angle=p.angle*f*Math.PI/180;
  if(p.mode==="slideX")t.position[0]+=travel;
  if(p.mode==="slideZ")t.position[2]+=travel;
  if(p.mode==="shrink")t.scale[0]*=1-f;
  if(p.mode==="swing"){
   const q=[0,0,Math.sin(angle/2),Math.cos(angle/2)],pivot=[p.pivotX*node.scale[0],0,p.pivotZ*node.scale[2]],a=rotate(node.rotation,pivot),b=rotate(multiply(node.rotation,q),pivot);
   t.position=t.position.map((v,k)=>v+a[k]-b[k]);t.rotation=multiply(node.rotation,q);
  }
 }
 return result;
}
export function worldPoint(family,trans,nodeIndex,p){let i=nodeIndex,v=p.slice();while(i>=0){const t=trans[i];v=rotate(t.rotation,v.map((x,k)=>x*t.scale[k])).map((x,k)=>x+t.position[k]);i=family.nodes[i].parent;}return v;}
export function newNode(){return{mode:"original",travel:1,angle:100,pivotX:0,pivotZ:0,ease:"smooth",uvScaleX:1,uvScaleY:1,uvOffsetX:0,uvOffsetY:0};}
export function exportFile(profiles,data){return{format:"dg-door-animation",schema:1,source_apk_sha256:data.sourceApkSha256,data_version:data.dataVersion,created_at:new Date().toISOString(),areas:Object.entries(profiles).map(([id,p])=>({id:+id,...structuredClone(p)}))};}
export function validateFile(file,data){
 if(file?.format!=="dg-door-animation"||file.schema!==1||file.source_apk_sha256!==data.sourceApkSha256||file.data_version!==data.dataVersion)throw Error("不是相同 r18 來源的門動畫調整檔。");
 if(!Array.isArray(file.areas)||file.areas.length>data.areas.length)throw Error("區域清單不正確。");let result={};
 for(const row of file.areas){
  const area=data.areas.find(a=>a.id===row.id);if(!area||result[row.id])throw Error("未知或重複區域 ID。");
  if(Object.keys(row).some(k=>!["id","family","duration","nodes","curves","note","completed"].includes(k)))throw Error("不允許非動畫欄位。");
  const family=data.families[row.family];if(!family)throw Error("門動畫樣板不存在。");
  if(!Number.isFinite(row.duration)||row.duration<0||row.duration>10)throw Error("播放時長超出範圍。");
  if(typeof row.note!=="string"||row.note.length>1000||typeof row.completed!=="boolean")throw Error("備註或完成狀態不正確。");
  if(!row.nodes||Array.isArray(row.nodes)||!row.curves||Array.isArray(row.curves))throw Error("節點／曲線格式不正確。");
  for(const[path,p]of Object.entries(row.nodes)){
   if(!family.nodes.some(n=>n.path===path&&n.mesh)||!Object.hasOwn(TYPES,p.mode)||!["smooth","linear","overshoot"].includes(p.ease))throw Error("動畫節點或模式不正確。");
   if(Object.keys(p).some(k=>!Object.hasOwn(newNode(),k)))throw Error("未知節點欄位。");
   for(const[k,min,max]of[["travel",-5,5],["angle",-360,360],["pivotX",-3,3],["pivotZ",-3,3],["uvScaleX",.01,4],["uvScaleY",.01,4],["uvOffsetX",-2,2],["uvOffsetY",-2,2]])if(!Number.isFinite(p[k])||p[k]<min||p[k]>max)throw Error("節點參數超出範圍："+k);
  }
  for(const[slot,tracks]of Object.entries(row.curves)){
   if(!["0","1","2","3"].includes(slot)||!Array.isArray(tracks)||tracks.length!==family.clips[slot].tracks.length)throw Error("曲線組數不符原始綁定。");
   tracks.forEach((t,i)=>{
    const base=family.clips[slot].tracks[i];for(const k of["path","kind","attribute","classID"])if(t[k]!==base[k])throw Error("曲線綁定不可改成其他元件。");
    if(Object.keys(t).some(k=>!["path","kind","attribute","classID","keys"].includes(k))||!Array.isArray(t.keys)||!t.keys.length||t.keys.length>300)throw Error("曲線格式不正確。");
    let prev=-1;for(const k of t.keys){if(!Number.isFinite(k.time)||k.time<=prev||k.time<0||k.time>10)throw Error("關鍵影格時間必須遞增。");prev=k.time;
     for(const kind of["value","in","out"]){if(!Array.isArray(k[kind])||k[kind].length!==base.keys[0].value.length)throw Error("關鍵影格維度不正確。");for(const v of k[kind])if(!(v===null&&kind!=="value")&&(!Number.isFinite(v)||Math.abs(v)>10000))throw Error("曲線值超出範圍。");}
    }
   });
  }
  result[row.id]={family:row.family,duration:row.duration,nodes:structuredClone(row.nodes),curves:structuredClone(row.curves),note:row.note,completed:row.completed};
 }return result;
}
