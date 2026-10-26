import{SLOT_LABELS,ENEMY_FIELDS,limits,makeProfile,needsEnemySize,validateFile,createExport}from"./profiles.js";
import{Renderer}from"./renderer.js";
const el=s=>document.querySelector(s),esc=s=>String(s).replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));
let catalog,layout,byId,renderer,profiles={},selected=810,mode="party",slot=0,enemyIndex=0,enemyCount=1,allSlots=true,showCutin=true,comparison=false,referenceUnlocked=false,limit=80,undo=[],redo=[],saveTimer,toastTimer,storageKey;
const clone=x=>structuredClone(x),getUnit=()=>byId.get(selected),profile=()=>profiles[selected]||makeProfile(getUnit()),fieldValues=()=>comparison?getUnit().fields:profile().fields;
function toast(message,error=false){clearTimeout(toastTimer);const e=el("#toast");e.textContent=message;e.classList.toggle("error",error);e.hidden=false;toastTimer=setTimeout(()=>e.hidden=true,error?7000:3500);}
function download(name,blob){const a=document.createElement("a");a.href=URL.createObjectURL(blob);a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(a.href),1500);}
function remember(){undo.push(clone(profiles));if(undo.length>50)undo.shift();redo=[];}
function changed(){updateLabels();draw();clearTimeout(saveTimer);el("#saveStatus").textContent="儲存中…";saveTimer=setTimeout(persist,200);}
function persist(){try{localStorage.setItem(storageKey,JSON.stringify(createExport(profiles,catalog)));el("#saveStatus").textContent="已儲存在此瀏覽器";}catch(e){el("#saveStatus").textContent="儲存失敗，請先匯出";toast("瀏覽器無法儲存草稿，請先匯出調整檔。",true);}}
function mutate(fn,history=true){if(history)remember();let p=clone(profile());fn(p);profiles[selected]=p;changed();}
function updateLabels(){
 el("#changedCount").textContent=Object.keys(profiles).length;el("#unitCount").textContent=catalog.units.length.toLocaleString();el("#selectedMeta").textContent="NO. "+String(getUnit().drawId).padStart(3,"0")+" / ID "+selected+" · "+getUnit().rare+"★";
 el("#selectedName").textContent=getUnit().name;el("#completeBtn").textContent=profile().completed?"✓ 已標記完成":"○ 標記完成";el("#completeBtn").classList.toggle("done",profile().completed);
 el("#progress").textContent=Object.values(profiles).filter(p=>p.completed).length+" 名已完成 · "+Object.keys(profiles).length+" 名有調整";
 el("#undoBtn").disabled=!undo.length;el("#redoBtn").disabled=!redo.length;
 const native=getUnit().native,edited=!!profiles[selected],badge=el("#sourceBadge");badge.textContent=edited?"玩家調整":native?"原有參數":"r17 推算／待補";badge.className="badge "+(edited?"edited":native?"native":"");
 for(const input of document.querySelectorAll("[data-field]"))if(document.activeElement!==input)input.value=fieldValues()[input.dataset.field];
 updateGuide();
}
function results(){let q=el("#search").value.trim().toLowerCase(),filter=el("#filter").value;return catalog.units.filter(c=>(!q||String(c.id)===q||String(c.drawId)===q||c.name.toLowerCase().includes(q))&&(filter==="all"||filter==="crop"&&c.missingCrop||filter==="size"&&needsEnemySize(c)||filter==="enemy"&&c.enemies.length||filter==="changed"&&profiles[c.id]||filter==="complete"&&profiles[c.id]?.completed));}
function renderList(){
 const rows=results(),list=el("#unitList");el("#resultCount").textContent=rows.length.toLocaleString()+" 名角色";
 list.innerHTML=rows.slice(0,limit).map(c=>'<button class="unit '+(c.id===selected?"active":"")+'" data-unit="'+c.id+'" role="listitem" aria-current="'+(c.id===selected?"true":"false")+'"><img src="'+assetUrl("/asset/icon/"+c.id+".png")+'" loading="lazy" alt=""><span class="unit-info"><strong>'+esc(c.name)+'</strong><small><span class="unit-id">#'+c.drawId+'</span><i class="dot '+(profiles[c.id]?"edited":c.native?"native":"inferred")+'"></i>'+ (c.enemies.length?'<span class="unit-pills">敵</span>':"")+(c.review.length?'<span class="unit-pills">例外</span>':"")+'</small></span>'+(profiles[c.id]?.completed?'<span class="unit-check">✓</span>':"")+'</button>').join("")+(rows.length>limit?'<button id="loadMore" class="unit more">再顯示 80 名（尚有 '+(rows.length-limit)+' 名）</button>':rows.length?"":'<p class="empty-enemy">沒有符合的角色。</p>');
 list.querySelectorAll("[data-unit]").forEach(b=>b.onclick=()=>selectUnit(+b.dataset.unit));const more=el("#loadMore");if(more)more.onclick=()=>{limit+=80;renderList();};
}
function selectUnit(id){selected=id;enemyIndex=0;referenceUnlocked=false;el("#loading").classList.add("hidden");updateLabels();renderList();renderInspector();draw();fit();}
function setMode(next){mode=next;for(const b of document.querySelectorAll("[data-mode]")){b.classList.toggle("active",b.dataset.mode===mode);b.setAttribute("aria-selected",String(b.dataset.mode===mode));}renderInspector();draw();}
function control(key,label,description=""){
 const val=fieldValues()[key],[min,max]=limits(key),disabled=getUnit().native&&!referenceUnlocked?"disabled":"";
 return'<div class="field"><div class="field-head"><label for="n-'+key+'">'+label+'</label><input '+disabled+' type="number" id="n-'+key+'" min="'+min+'" max="'+max+'" step="1" value="'+val+'" data-field="'+key+'"></div><input '+disabled+' type="range" aria-label="'+label+'" min="'+min+'" max="'+max+'" step="1" value="'+val+'" data-field="'+key+'"><div class="field-code"><span>'+key+'</span><span>基準 '+getUnit().fields[key]+'</span></div>'+(description?'<p class="hint">'+description+'</p>':"")+'</div>';
}
function enemyControl(key,label){const e=getEnemy(),[min,max]=limits(key),val=e[key]??0;return'<div class="field"><div class="field-head"><label for="e-'+key+'">'+label+'</label><input type="number" '+(getUnit().native&&!referenceUnlocked?"disabled":"")+' id="e-'+key+'" min="'+min+'" max="'+max+'" step="1" value="'+val+'" data-enemy="'+key+'"></div><div class="field-code"><span>'+key+'</span></div></div>';}
function renderInspector(){
 const unit=getUnit(),body=el("#inspectorBody"),isNative=unit.native;el("#inspectorTitle").textContent={party:"隊伍立繪",cutin:"攻擊橫條",enemy:"敵人顯示"}[mode];
 let html=isNative?'<div class="source-note">這名角色保留原有顯示參數，可作為參考。<label class="check-line"><input id="unlockNative" type="checkbox" '+(referenceUnlocked?"checked":"")+'>允許調整參考角色</label></div>':'<div class="source-note">'+(mode==="party"?"隊伍立繪使用 r17 的推算／例外配置。":mode==="cutin"?"這名角色缺少原始攻擊橫條裁切，目前沿用舊角色模板。":"這名角色已按 DGDB size 分類套用原生尺寸、對齊與間距，可再微調。")+'你的修改會獨立保留。'+(unit.review.length?'<br>此角色屬於需要人工檢查的例外。':"")+'</div>';
 if(mode==="party"){
  html+='<section class="control-block"><h3>五個戰鬥位置 <small>依畫面由左至右</small></h3><div class="slot-picker">'+[4,3,2,1,0].map(i=>'<button class="'+(slot===i?"active":"")+'" data-slot="'+i+'">'+["隊長","隊員1","隊員2","隊員3","好友"][i]+'<small>img_'+(4-i)+'</small></button>').join("")+'</div><p>'+SLOT_LABELS[slot]+' · img_'+(4-slot)+'</p><label class="check-line"><input id="allSlots" type="checkbox" '+(allSlots?"checked":"")+'>五格皆顯示目前角色</label></section>';
  let n=4-slot;html+='<section class="control-block"><h3>裁切範圍 <small>數值 ÷ 1000 = UV</small></h3>'+control("img_"+n+"_tiling","取景範圍","數值越小，立繪顯示越放大。")+control("img_"+n+"_offsetX","水平偏移")+control("img_"+n+"_offsetY","垂直偏移")+'</section><div class="action-row"><button id="copySlot">複製此格到其他四格</button><button id="resetSlot">還原此格</button></div><p class="hint">各格形狀不同，複製後仍應逐格檢查。</p>';
 }else if(mode==="cutin"){
  html+='<label class="check-line"><input id="showCutin" type="checkbox" '+(showCutin?"checked":"")+'>顯示普通技攻擊橫條</label><section class="control-block"><h3>橫條裁切 <small>原生 60 : 9 UV 比例</small></h3>'+control("img_cutin_tiling","水平取景範圍","垂直範圍由遊戲公式固定為水平的 0.15 倍。")+control("img_cutin_offsetX","水平偏移")+control("img_cutin_offsetY","垂直偏移")+'</section><p class="hint">角色圖像使用原始 CutinChara 網格。攻擊文字與 ATK 取自遊戲主資料，僅供顯示預覽。</p>';
 }else if(unit.enemies.length){
  html+='<section class="control-block"><h3>敵人預覽 <small>'+unit.enemies.length+' 筆引用</small></h3><select id="enemySelect" class="inline-select" aria-label="敵人資料">'+unit.enemies.map((e,i)=>'<option value="'+i+'" '+(i===enemyIndex?"selected":"")+'>敵人 #'+e.fix_id+' · HP '+e.status_hp.toLocaleString()+'</option>').join("")+'</select><label class="check-line"><input id="twoEnemies" type="checkbox" '+(enemyCount===2?"checked":"")+'>兩隻同角色：檢查排列間距</label></section>';
  if(!unit.native)html+='<div class="source-note warn-note">已套用 DGDB size='+unit.dgdbSize+' → '+unit.fields.size_width+'×'+unit.fields.size_height+'、pivot '+unit.fields.pivot+'、間距 '+unit.fields.side_offset+'。尺寸依原有角色分類推定，非逐角色官方原值；仍可微調。<br>映射：'+esc(unit.sizeAdapter.method)+'<button id="resetEnemySize">還原 DGDB 尺寸起點</button></div>';
  html+='<section class="control-block"><h3>角色顯示尺寸 <small>DGDB size '+unit.dgdbSize+'</small></h3>'+control("size_width","寬度")+control("size_height","高度")+control("side_offset","左右排列間距")+'<div class="field-head"><label for="pivotSelect">對齊基準</label><select id="pivotSelect" '+(unit.native&&!referenceUnlocked?"disabled":"")+'><option value="1" '+(fieldValues().pivot===1?"selected":"")+'>中心（pivot 1）</option><option value="2" '+(fieldValues().pivot===2?"selected":"")+'>落地（pivot 2）</option></select></div><div class="action-row"><button data-size-preset="106">小 106</button><button data-size-preset="140">小 140</button><button data-size-preset="216">中 216</button><button data-size-preset="284">中 284</button><button data-size-preset="640">大 640</button></div><p class="hint">預設取自原有角色，僅供起點，不代表這名角色的官方尺寸。</p></section><section class="control-block"><h3>這筆敵人的位置／血條</h3><label class="check-line"><input id="absolutePos" type="checkbox" '+(unit.native&&!referenceUnlocked?"disabled":"")+' '+(getEnemy().pos_absolute===2?"checked":"")+'>指定位置（啟用後不使用自動排列）</label>'+enemyControl("posx_offset","敵人水平位置")+enemyControl("posy_offset","敵人垂直位置")+enemyControl("posz_value","敵人前後深度")+enemyControl("hp_posx_offset","血條水平位置")+enemyControl("hp_posy_offset","血條垂直位置")+'<div class="field-head"><label for="gaugeSelect">血條樣式</label><select id="gaugeSelect" '+(unit.native&&!referenceUnlocked?"disabled":"")+'>'+["自動","小","中","大","Boss"].map((t,i)=>'<option value="'+i+'" '+(getEnemy().hp_gauge_type===i?"selected":"")+'> '+t+'</option>').join("")+'</select></div><button id="copyEnemies" '+(unit.native&&!referenceUnlocked?"disabled":"")+' style="width:100%;margin-top:10px">套用位置設定到此角色所有敵人</button><p class="hint">不修改 HP、攻防、掉落或敵人行動。前後深度需在 Unity 內確認遮擋效果，本頁維持平面預覽。</p></section>';
 }else html+='<div class="empty-enemy">這名角色未出現在目前敵人資料中，因此不放入怪物區。隊伍立繪與攻擊橫條仍可調整。</div>';
 if(mode!=="enemy")html+='<section class="control-block"><h3>完整立繪定位</h3><div class="fullart-map" id="fullartMap"><img alt="完整立繪取景定位圖" src="'+assetUrl("/asset/portrait/"+unit.id+".png")+'"><svg viewBox="0 0 200 200"><rect id="cropWindow" x="0" y="0" width="0" height="0"/></svg></div><p class="hint">點選原圖可將目前格子的取景窗對準該位置。框線是取景外框，實際遮罩以戰鬥預覽為準。</p></section>';
 html+='<section class="control-block" style="margin-top:24px"><h3>調整備註</h3><textarea id="profileNote" class="note-input" maxlength="1000" placeholder="例如：五格已檢查，好友格需稍微上移…">'+esc(profile().note)+'</textarea></section>';
 body.innerHTML=html;
 const bind=(id,type,fn)=>{const e=el("#"+id);if(e)e.addEventListener(type,fn);};
 bind("fullartMap","click",e=>{
  if(unit.native&&!referenceUnlocked)return toast("請先允許調整參考角色。");
  const r=e.currentTarget.getBoundingClientRect(),u=(e.clientX-r.left)/r.width,v=1-(e.clientY-r.top)/r.height;
  const prefix=mode==="party"?"img_"+(4-slot)+"_":"img_cutin_",uv=mode==="party"?layout.portraits[slot].uv:layout.cutinMeshes[0].uv,s=fieldValues()[prefix+"tiling"]/1000;
  const cx=(Math.min(...uv.map(p=>p[0]))+Math.max(...uv.map(p=>p[0])))/2,cy=(Math.min(...uv.map(p=>p[1]))+Math.max(...uv.map(p=>p[1])))/2,sy=mode==="cutin"?.15:1;
  mutate(p=>{p.fields[prefix+"offsetX"]=Math.round(Math.max(-4000,Math.min(4000,(u-cx*s)*1000)));p.fields[prefix+"offsetY"]=Math.round(Math.max(-4000,Math.min(4000,(v-cy*s*sy)*1000)));});
 });
 bind("unlockNative","change",e=>{referenceUnlocked=e.target.checked;renderInspector();});
 for(const e of body.querySelectorAll("[data-slot]"))e.onclick=()=>{slot=+e.dataset.slot;renderInspector();draw();};
 bind("allSlots","change",e=>{allSlots=e.target.checked;draw();});bind("showCutin","change",e=>{showCutin=e.target.checked;draw();});
 for(const e of body.querySelectorAll("[data-field]")){
  e.addEventListener("pointerdown",()=>remember());
  e.addEventListener("keydown",evt=>{if(["ArrowUp","ArrowDown","ArrowLeft","ArrowRight"].includes(evt.key))remember();});
  e.addEventListener("input",()=>{const key=e.dataset.field,n=Number(e.value),[min,max]=limits(key);if(Number.isInteger(n)&&n>=min&&n<=max)mutate(p=>p.fields[key]=n,false);});
 }
 for(const e of body.querySelectorAll("[data-enemy]"))e.onchange=()=>{const n=Number(e.value),[min,max]=limits(e.dataset.enemy);if(Number.isInteger(n)&&n>=min&&n<=max)editEnemy({[e.dataset.enemy]:n});else{toast("欄位數值超出範圍。",true);renderInspector();}};
 bind("pivotSelect","change",e=>mutate(p=>p.fields.pivot=+e.target.value));bind("enemySelect","change",e=>{enemyIndex=+e.target.value;renderInspector();draw();});
 bind("twoEnemies","change",e=>{enemyCount=e.target.checked?2:1;draw();});bind("absolutePos","change",e=>editEnemy({pos_absolute:e.target.checked?2:0}));bind("gaugeSelect","change",e=>editEnemy({hp_gauge_type:+e.target.value}));
 bind("resetEnemySize","click",()=>{mutate(p=>{for(const k of["size_width","size_height","pivot","side_offset"])p.fields[k]=unit.fields[k];});renderInspector();});
 for(const b of body.querySelectorAll("[data-size-preset]"))b.onclick=()=>{if(unit.native&&!referenceUnlocked)return toast("請先允許調整參考角色。");let w=+b.dataset.sizePreset;mutate(p=>Object.assign(p.fields,{size_width:w,size_height:w,pivot:w===640?1:2,side_offset:({106:48,140:65,216:78,284:105,640:320})[w]}));renderInspector();};
 bind("copySlot","click",()=>{if(unit.native&&!referenceUnlocked)return;let n=4-slot;mutate(p=>{for(let i=0;i<5;i++)for(const k of["tiling","offsetX","offsetY"])p.fields["img_"+i+"_"+k]=p.fields["img_"+n+"_"+k];});});
 bind("resetSlot","click",()=>{let n=4-slot;mutate(p=>{for(const k of["tiling","offsetX","offsetY"])p.fields["img_"+n+"_"+k]=unit.fields["img_"+n+"_"+k];});});
 bind("copyEnemies","click",()=>{const e=getEnemy();mutate(p=>{for(const row of unit.enemies)p.enemyOverrides[row.fix_id]=Object.fromEntries(ENEMY_FIELDS.map(k=>[k,e[k]]));});toast("已套用此角色所有敵人的顯示位置。");});
 bind("profileNote","change",e=>mutate(p=>p.note=e.target.value));
 updateLabels();
}
function updateGuide(){
 const rect=el("#cropWindow");if(!rect||!layout||mode==="enemy")return;
 const f=fieldValues(),prefix=mode==="party"?"img_"+(4-slot)+"_":"img_cutin_",uv=mode==="party"?layout.portraits[slot].uv:layout.cutinMeshes[0].uv,s=f[prefix+"tiling"]/1000,sy=mode==="cutin"?.15:1;
 const a=Math.min(...uv.map(p=>p[0])),b=Math.max(...uv.map(p=>p[0])),c=Math.min(...uv.map(p=>p[1])),d=Math.max(...uv.map(p=>p[1]));
 rect.setAttribute("x",(f[prefix+"offsetX"]/1000+a*s)*200);rect.setAttribute("y",(1-f[prefix+"offsetY"]/1000-d*s*sy)*200);rect.setAttribute("width",(b-a)*s*200);rect.setAttribute("height",(d-c)*s*sy*200);
}
function getEnemy(){const unit=getUnit(),base=unit.enemies[enemyIndex]||{};return{...base,...(!comparison?profile().enemyOverrides[base.fix_id]:{})};}
function editEnemy(values,history=true){const id=getUnit().enemies[enemyIndex]?.fix_id;if(!id)return;mutate(p=>p.enemyOverrides[id]={...p.enemyOverrides[id],...values},history);}
function draw(){
 if(!renderer)return;const chosen=getUnit(),f=fieldValues(),seed=[chosen.id,9,207,1,5];
 const partyUnits=Array.from({length:5},(_,i)=>allSlots||i===slot?chosen:byId.get(seed[i])||chosen),partyFields=partyUnits.map(c=>c.id===selected?f:(profiles[c.id]?.fields||c.fields));
 renderer.scene({unit:chosen,fields:f,partyUnits,partyFields,mode,slot,showCutin,showEnemy:!!chosen.enemies.length,enemyCount,enemy:getEnemy()});
}
function fit(){if(!renderer)return;renderer.setView(el("#focusView").value);const v=renderer.view,box=el("#viewport").getBoundingClientRect(),scale=Math.min((box.width-28)/v.w,(box.height-22)/v.h),w=Math.max(30,v.w*scale),h=Math.max(30,v.h*scale);el("#canvasWrap").style.width=w+"px";el("#canvasWrap").style.height=h+"px";renderer.resize(w,h);el("#renderStatus").textContent=Math.round(v.w)+" × "+Math.round(v.h)+" · "+Math.round(scale*100)+"%";}
function undoAction(forward=false){const from=forward?redo:undo,to=forward?undo:redo;if(!from.length)return;to.push(clone(profiles));profiles=from.pop();changed();renderList();renderInspector();}
let drag=null;
function bindCanvas(){
 const canvas=el("#battle");
 canvas.addEventListener("pointerdown",e=>{
  const point=renderer.point(e),hit=renderer.hit(point);if(!hit)return;
  if(hit.kind==="party"){slot=hit.slot;if(!allSlots&&hit.id!==selected)selectUnit(hit.id);setMode("party");}else setMode(hit.kind);
  if(getUnit().native&&!referenceUnlocked)return toast("這名角色是原版參考，請先允許調整。");
  remember();drag={point,hit,start:clone(profile()),enemy:{...getEnemy()}};canvas.setPointerCapture(e.pointerId);
 });
 canvas.addEventListener("pointermove",e=>{
  if(!drag)return;const p=renderer.point(e),dx=p[0]-drag.point[0],dy=p[1]-drag.point[1],{hit,start}=drag;
  if(hit.kind==="enemy")editEnemy({pos_absolute:2,posx_offset:Math.round(Math.max(-2000,Math.min(2000,(drag.enemy.pos_absolute===2?drag.enemy.posx_offset:0)+dx))),posy_offset:Math.round(Math.max(-2000,Math.min(2000,(drag.enemy.pos_absolute===2?drag.enemy.posy_offset:0)-dy)))},false);
  else{const prefix=hit.kind==="party"?"img_"+(4-hit.slot)+"_":"img_cutin_",scale=start.fields[prefix+"tiling"]/1000,sy=hit.kind==="cutin"?.15:1,j=hit.jacobian;
   const x=Math.round(start.fields[prefix+"offsetX"]-1000*scale*(j[0][0]*dx+j[0][1]*dy)),y=Math.round(start.fields[prefix+"offsetY"]-1000*scale*sy*(j[1][0]*dx+j[1][1]*dy));
   mutate(pr=>{pr.fields[prefix+"offsetX"]=Math.max(-4000,Math.min(4000,x));pr.fields[prefix+"offsetY"]=Math.max(-4000,Math.min(4000,y));},false);
  }
 });
 canvas.addEventListener("pointerup",()=>{drag=null;});canvas.addEventListener("pointercancel",()=>{drag=null;});
 canvas.addEventListener("wheel",e=>{
  const hit=renderer.hit(renderer.point(e));if(!hit||getUnit().native&&!referenceUnlocked)return;e.preventDefault();
  if(hit.kind==="enemy"){let factor=Math.exp(e.deltaY*-.001);mutate(p=>{p.fields.size_width=Math.round(Math.max(16,Math.min(1280,p.fields.size_width*factor)));p.fields.size_height=Math.round(Math.max(16,Math.min(1280,p.fields.size_height*factor)));});return;}
  if(hit.kind==="party")slot=hit.slot;setMode(hit.kind);const prefix=hit.kind==="party"?"img_"+(4-hit.slot)+"_":"img_cutin_",f=profile().fields,old=f[prefix+"tiling"]/1000,next=Math.max(.001,Math.min(4,old*Math.exp(e.deltaY*.001))),sy=hit.kind==="cutin"?.15:1;
  mutate(p=>{p.fields[prefix+"tiling"]=Math.round(next*1000);p.fields[prefix+"offsetX"]=Math.round(Math.max(-4000,Math.min(4000,f[prefix+"offsetX"]+hit.uv[0]*(old-next)*1000)));p.fields[prefix+"offsetY"]=Math.round(Math.max(-4000,Math.min(4000,f[prefix+"offsetY"]+hit.uv[1]*(old-next)*sy*1000)));});
 },{passive:false});
}
async function boot(){
 try{
  [catalog,layout]=[window.DG_LOCAL_DATA.catalog,window.DG_LOCAL_DATA.layout];byId=new Map(catalog.units.map(c=>[c.id,c]));storageKey="dg-battle-layout:"+catalog.sourceApkSha256;
  try{const saved=localStorage.getItem(storageKey);if(saved)profiles=validateFile(JSON.parse(saved),catalog);}catch(e){toast("本機草稿無法讀取："+e.message,true);}
  renderer=new Renderer(el("#battle"),el("#overlay"),layout,e=>toast(e.message,true));updateLabels();renderList();renderInspector();draw();fit();bindCanvas();el("#loading").classList.add("hidden");el("#saveStatus").textContent="草稿已就緒 · 僅儲存在本機";
  const frame=()=>{renderer.render();requestAnimationFrame(frame);};frame();
  window.__battleLab={catalog,layout,get profiles(){return clone(profiles);},get selected(){return selected;},get renderer(){return renderer;},selectUnit,setMode,validateFile,createExport,profile:()=>clone(profile())};
 }catch(e){el("#loading").textContent="讀取失敗："+e.message;toast(e.message,true);console.error(e);}
}
el("#search").oninput=()=>{limit=80;renderList();};el("#filter").onchange=()=>{limit=80;renderList();};
el("#nextPending").onclick=()=>{const rows=results(),index=rows.findIndex(c=>c.id===selected),next=[...rows.slice(index+1),...rows.slice(0,index+1)].find(c=>!profiles[c.id]?.completed);if(next)selectUnit(next.id);else toast("目前篩選的角色都已標記完成。");};
for(const b of document.querySelectorAll("[data-mode]"))b.onclick=()=>setMode(b.dataset.mode);
el("#viewportSize").onchange=()=>{renderer.H=+el("#viewportSize").value;draw();fit();};el("#focusView").onchange=fit;window.addEventListener("resize",fit);new ResizeObserver(fit).observe(el("#viewport"));
el("#undoBtn").onclick=()=>undoAction();el("#redoBtn").onclick=()=>undoAction(true);
el("#completeBtn").onclick=()=>{mutate(p=>p.completed=!p.completed);renderList();};
el("#resetBtn").onclick=()=>{if(!profiles[selected])return;remember();delete profiles[selected];changed();renderList();renderInspector();toast("已還原此角色的裁切與 DGDB 尺寸基準。");};
el("#exportBtn").onclick=()=>{const data=createExport(profiles,catalog);download("dg291-presentation-"+new Date().toLocaleDateString("sv-SE")+".json",new Blob([JSON.stringify(data,null,2)],{type:"application/json"}));toast("已匯出 "+data.characters.length+" 名角色的顯示設定。");};
el("#importBtn").onclick=()=>el("#importFile").click();el("#importFile").onchange=async e=>{
 const file=e.target.files[0];if(!file)return;
 try{if(file.size>8*1024*1024)throw Error("檔案超過 8 MiB。");const imported=validateFile(JSON.parse(await file.text()),catalog);remember();profiles={...profiles,...imported};changed();renderList();renderInspector();toast("已匯入 "+Object.keys(imported).length+" 名角色，可使用復原回到匯入前。");}catch(err){toast("匯入失敗："+err.message,true);}finally{e.target.value="";}
};
el("#compareBtn").onpointerdown=e=>{e.target.setPointerCapture(e.pointerId);comparison=true;draw();};
const stopCompare=()=>{comparison=false;draw();};el("#compareBtn").onpointerup=stopCompare;el("#compareBtn").onpointercancel=stopCompare;
el("#captureBtn").onclick=()=>{renderer.render();renderer.capture().toBlob(blob=>download("dg291-preview-"+selected+".png",blob));};
el("#helpBtn").onclick=()=>el("#helpDialog").showModal();el("#closeHelp").onclick=()=>el("#helpDialog").close();
document.addEventListener("keydown",e=>{const typing=/INPUT|TEXTAREA|SELECT/.test(document.activeElement.tagName);if(e.key==="/"&&!typing){e.preventDefault();el("#search").focus();}if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==="z"&&!typing){e.preventDefault();undoAction(e.shiftKey);}});
window.addEventListener("pagehide",()=>{if(catalog){const note=el("#profileNote");if(note&&note.value!==profile().note){let p=clone(profile());p.note=note.value;profiles[selected]=p;}persist();}});boot();
