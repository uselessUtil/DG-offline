const imageCache=new Map(),geometryCache=new Map();
export const getImage=url=>{if(!imageCache.has(url)){let im=new Image();const p=new Promise((res,rej)=>{im.onload=()=>res(im);im.onerror=()=>rej(Error("素材無法載入："+url));});im.src=assetUrl(url);imageCache.set(url,p);if(imageCache.size>32)imageCache.delete(imageCache.keys().next().value);}return imageCache.get(url);};
const colorNames=["gray","red","blue","yellow","purple","green"];
const xy=(m,x,y)=>[m[0][0]*x+m[0][1]*y+m[0][3],m[1][0]*x+m[1][1]*y+m[1][3]];
function shader(gl,type,source){const s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(s));return s;}
function boundary(item){const counts=new Map();for(let i=0;i<item.triangles.length;i+=3){let t=item.triangles.slice(i,i+3);for(let j=0;j<3;j++){let a=t[j],b=t[(j+1)%3],k=a<b?a+":"+b:b+":"+a;counts.set(k,(counts.get(k)||0)+1);}}return[...counts].filter(([,n])=>n===1).map(([k])=>k.split(":").map(Number));}
export class Renderer{
 constructor(canvas,overlay,layout,onError){
  this.canvas=canvas;this.overlay=overlay;this.layout=layout;this.onError=onError;this.items=[];this.H=1136;this.view={x:0,y:0,w:640,h:1136};this.texts=[];this.textures=new Map();this.dirty=true;
  const gl=canvas.getContext("webgl",{alpha:false,antialias:true,preserveDrawingBuffer:true});if(!gl)throw Error("此瀏覽器不支援 WebGL，請使用 Chrome 或 Edge。");this.gl=gl;
  const v=shader(gl,gl.VERTEX_SHADER,"attribute vec2 pos;attribute vec2 uv;uniform vec4 view;varying vec2 tex;void main(){vec2 p=(pos-view.xy)/view.zw;gl_Position=vec4(p.x*2.0-1.0,1.0-p.y*2.0,0.,1.);tex=uv;}");
  const f=shader(gl,gl.FRAGMENT_SHADER,"precision mediump float;varying vec2 tex;uniform sampler2D image;uniform vec4 color;void main(){gl_FragColor=texture2D(image,tex)*color;}");
  const p=gl.createProgram();gl.attachShader(p,v);gl.attachShader(p,f);gl.linkProgram(p);if(!gl.getProgramParameter(p,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(p));gl.useProgram(p);this.program=p;
  this.pos=gl.getAttribLocation(p,"pos");this.uv=gl.getAttribLocation(p,"uv");this.uView=gl.getUniformLocation(p,"view");this.uColor=gl.getUniformLocation(p,"color");
  this.buffer=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,this.buffer);gl.enableVertexAttribArray(this.pos);gl.enableVertexAttribArray(this.uv);gl.vertexAttribPointer(this.pos,2,gl.FLOAT,false,16,0);gl.vertexAttribPointer(this.uv,2,gl.FLOAT,false,16,8);
  gl.enable(gl.BLEND);gl.blendFunc(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA);gl.disable(gl.DEPTH_TEST);
  const white=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,white);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,1,1,0,gl.RGBA,gl.UNSIGNED_BYTE,new Uint8Array([255,255,255,255]));this.white=white;
 }
 texture(url){if(!url)return this.white;if(this.textures.has(url))return this.textures.get(url);this.textures.set(url,null);getImage(url).then(im=>{const g=this.gl,t=g.createTexture();g.bindTexture(g.TEXTURE_2D,t);g.pixelStorei(g.UNPACK_FLIP_Y_WEBGL,true);g.texImage2D(g.TEXTURE_2D,0,g.RGBA,g.RGBA,g.UNSIGNED_BYTE,im);g.texParameteri(g.TEXTURE_2D,g.TEXTURE_WRAP_S,g.CLAMP_TO_EDGE);g.texParameteri(g.TEXTURE_2D,g.TEXTURE_WRAP_T,g.CLAMP_TO_EDGE);g.texParameteri(g.TEXTURE_2D,g.TEXTURE_MIN_FILTER,g.LINEAR);g.texParameteri(g.TEXTURE_2D,g.TEXTURE_MAG_FILTER,g.LINEAR);this.textures.set(url,t);if(this.textures.size>26){const keep=new Set((this.state?.partyUnits||[]).map(c=>"/asset/portrait/"+c.id+".png"));for(const[k,v]of this.textures){if(this.textures.size<=22)break;if(k.includes("/asset/portrait/")&&!keep.has(k)&&v){g.deleteTexture(v);this.textures.delete(k);}}}this.dirty=true;}).catch(this.onError);return null;}
 draw(item){const g=this.gl,t=this.texture(item.texture);if(!t)return;const data=[];for(const i of item.triangles){data.push(item.points[i][0],item.points[i][1],item.uv[i][0],item.uv[i][1]);}g.bindTexture(g.TEXTURE_2D,t);g.bufferData(g.ARRAY_BUFFER,new Float32Array(data),g.STREAM_DRAW);g.uniform4fv(this.uColor,item.color||[1,1,1,1]);g.drawArrays(g.TRIANGLES,0,item.triangles.length);}
 mesh(node,mapper,texture,scale=[1,1],offset=[0,0],tag=null){const item={points:node.positions.map(p=>mapper(p)),uv:node.uv.map(p=>[p[0]*scale[0]+offset[0],p[1]*scale[1]+offset[1]]),baseUV:node.uv,triangles:node.triangles,texture:texture||node.material?.texture,color:[1,1,1,1],tag,scale};this.items.push(item);return item;}
 quad(texture,x,y,w,h,uv=null,color=null,tag=null){const item={points:[[x,y+h],[x+w,y+h],[x+w,y],[x,y]],uv:uv||[[0,0],[1,0],[1,1],[0,1]],baseUV:uv||[[0,0],[1,0],[1,1],[0,1]],triangles:[0,1,2,0,2,3],texture,color:color||[1,1,1,1],tag,scale:[1,1]};this.items.push(item);return item;}
 sprite(name,x,y,w,h,color,tag){const s=this.layout.sprites[name];if(!s)return null;return this.spriteRect(s,x,y,w,h,color,tag);}
 spriteRect(s,x,y,w,h,color,tag){const im=imageCache.get(s.texture);this.texture(s.texture);let dims=this.assetSize(s.texture);const[a,b,sw,sh]=s.rect,W=dims[0],H=dims[1];return this.quad(s.texture,x,y,w,h,[[a/W,1-(b+sh)/H],[(a+sw)/W,1-(b+sh)/H],[(a+sw)/W,1-b/H],[a/W,1-b/H]],color,tag);}
 assetSize(url){if(this.layout.textureSizes?.[url])return this.layout.textureSizes[url];const n=url.split("/").pop();if(n==="AtlasElement.png"||n==="AtlasInGameStatusAilment.png"||n==="AtlasInGameUI2.png")return[512,512];if(n==="AtlasResident_16.png")return[1024,2048];if(n==="AtlasInGameDialog.png")return[1024,1024];if(n==="AtlasDialog.png")return[512,1024];return[2048,2048];}
 widget(row,mapper,spriteOverride=null){
  const d=row.data,m=row.matrix,s=spriteOverride?this.layout.sprites[spriteOverride]:row.sprite;if(!s)return;
  const pivot=d.mPivot??4,px=[0,.5,1][pivot%3],py=1-Math.floor(pivot/3)*.5;
  const W=Math.hypot(m[0][0],m[1][0]),H=Math.hypot(m[0][1],m[1][1]);if(!W||!H)return;
  const[oX,oY,oW,oH]=s.rect,[iX,iY,iW,iH]=s.inner,dims=this.assetSize(s.texture);
  const left=iX-oX,right=oX+oW-iX-iW,top=iY-oY,bottom=oY+oH-iY-iH;
  const sliced=row.class==="UISlicedSprite"&&(left||right||top||bottom);
  const xs=sliced?[-px,-px+left/W,1-px-right/W,1-px]:[-px,1-px],ys=sliced?[-py,-py+bottom/H,1-py-top/H,1-py]:[-py,1-py];
  const us=sliced?[oX,iX,iX+iW,oX+oW].map(x=>x/dims[0]):[oX/dims[0],(oX+oW)/dims[0]];
  const vs=sliced?[oY+oH,iY+iH,iY,oY].map(y=>1-y/dims[1]):[1-(oY+oH)/dims[1],1-oY/dims[1]];
  for(let j=0;j<ys.length-1;j++)for(let i=0;i<xs.length-1;i++){
   const item={nativeName:row.name,points:[[xs[i],ys[j]],[xs[i+1],ys[j]],[xs[i+1],ys[j+1]],[xs[i],ys[j+1]]].map(p=>mapper(xy(m,...p))),uv:[[us[i],vs[j]],[us[i+1],vs[j]],[us[i+1],vs[j+1]],[us[i],vs[j+1]]],triangles:[0,1,2,0,2,3],texture:s.texture,color:d.mColor||[1,1,1,1]};this.items.push(item);
  }
 }
 text(value,x,y,size=24,align="left",color="white",angle=0){this.texts.push({value,x,y,size,align,color,angle});}
 async enemyGeometry(id){if(!geometryCache.has(id)){geometryCache.set(id,window.DG_LOCAL_DATA.geometries[id]||null);this.dirty=true;}return geometryCache.get(id);}

 scene(state){
  this.state=state;this.items=[];this.texts=[];const L=this.layout,H=this.H,unit=state.unit,fields=state.fields;
  const foot=p=>[p[0]+320,H-480-p[1]],cutinY=H-372,cut=p=>[p[0]+320,cutinY-p[1]];
  this.quad(null,0,0,640,H,null,[0,0,0,1]);
  // Read actual map and panel pixels from the same APK image store.
  this.quad("/asset/image?key=restored-map/map_0001/upper",0,H/2-400,640,198);
  const gridY=H-846;for(let y=0;y<5;y++)for(let x=0;x<5;x++)this.quad("/asset/image?key=atlas/AtlasPanelIcon",x*128,gridY+y*128,128,128,this.panelUV("panel_blank"),[.55,.55,.55,1]);
  this.quad(null,0,gridY,640,640,null,[0,0,0,.25]);
  this.mesh(L.battleFrame,p=>[p[0]+320,H/2-p[1]]);
  for(const row of L.headerMeshes)this.mesh(row,p=>[p[0]+320,480-p[1]]);
  // Header uses native glyph art when available; neutral background keeps crop readable.
  this.text("1/1F",625,62,23,"right");this.text("0",43,28,24);this.text("0",55,61,22);
  if(unit.enemies.length&&state.showEnemy){
   const count=state.enemyCount,enemy=state.enemy,side=fields.side_offset;let x=-count*side;
   for(let i=0;i<count;i++){
    x+=side;let ex=enemy.pos_absolute===2?enemy.posx_offset:x,ey=enemy.pos_absolute===2?enemy.posy_offset:0;
    const rootY=H/2-222,pivot=fields.pivot===2?-155+fields.size_height/2:0;
    if(!geometryCache.has(unit.id))this.enemyGeometry(unit.id);let geom=geometryCache.get(unit.id);
    geom=geom||{positions:[[-.5,-.5,0],[.5,-.5,0],[.5,.5,0],[-.5,.5,0]],uv:[[0,0],[1,0],[1,1],[0,1]],triangles:[0,1,2,0,2,3]};
    this.mesh(geom,p=>[320+ex+p[0]*fields.size_width,rootY-ey-pivot-p[1]*fields.size_height],"/asset/portrait/"+unit.id+".png",[1,1],[0,0],{kind:"enemy",index:i,id:unit.id});
    const gauge=enemy.hp_gauge_type|| (fields.pivot===1?4:fields.size_width<170?1:fields.size_width<270?2:3),label=["S","M","L","Boss"][gauge-1]||"M";
    const gx=enemy.hp_gauge_type>0?enemy.hp_posx_offset:ex+(enemy.pos_absolute===2?enemy.hp_posx_offset:0);
    const gy=enemy.hp_gauge_type>0?enemy.hp_posy_offset:(L.gaugeRootPositions?.[label]?.y??-100)+(enemy.pos_absolute===2?enemy.posy_offset+enemy.hp_posy_offset:0);
    for(const row of L.gauges[label]||[])if(row.sprite&&!/collision/i.test(row.name))this.widget(row,p=>[320+gx+p[0],rootY-gy-p[1]]);
    this.text("NEXT 1",320+gx+70,rootY-gy-10,18,"right");x+=side;
   }
  }
  // Actual normal-skill portrait mesh; native vertical UV scale is horizontal*9/60.
  if(state.showCutin){
   this.widget(L.cutinWidgets.find(w=>w.name==="CutinBase"),cut,"cutin_"+(colorNames[unit.element-1]||"gray"));
   this.mesh(L.cutinMeshes[0],cut,"/asset/portrait/"+unit.id+".png",[fields.img_cutin_tiling/1000,fields.img_cutin_tiling/1000*.15],[fields.img_cutin_offsetX/1000,fields.img_cutin_offsetY/1000],{kind:"cutin",id:unit.id});
   this.sprite("Normal-skill",.5,cutinY-96.5,260,49);this.text("ATK "+unit.attack,620,cutinY-14,31,"right");
   this.text(unit.skills.find(Boolean)||"Normal Skill",620,cutinY+29,29,"right");
  }
  for(const row of L.partyWidgets)if(row.name==="InGamePlayerPartyBG")this.widget(row,foot);
  for(let i=0;i<5;i++){const color=colorNames[state.partyUnits[i].element-1]||"gray";this.mesh(L.backs[color][i],foot);}
  for(let i=0;i<5;i++){const c=state.partyUnits[i],f=state.partyFields[i],n=4-i;
   this.mesh(L.portraits[i],foot,"/asset/portrait/"+c.id+".png",[f["img_"+n+"_tiling"]/1000,f["img_"+n+"_tiling"]/1000],[f["img_"+n+"_offsetX"]/1000,f["img_"+n+"_offsetY"]/1000],{kind:"party",slot:i,id:c.id});
  }
  for(let i=0;i<5;i++){const color=colorNames[state.partyUnits[i].element-1]||"gray";this.mesh(L.frames[color][i],foot);}
  for(const row of L.partyStatic)this.mesh(row,foot);
  for(const row of L.partyWidgets)if(row.name==="Gauge")this.widget(row,foot);
  this.text(state.partyUnits.reduce((s,c)=>s+c.hp,0)+"/"+state.partyUnits.reduce((s,c)=>s+c.hp,0),212.5,H-164,24,"right","white",-.17);
  this.text("Leader",623,H-13,18,"right");
  // Element hand cards use the original panel atlas and original128px cells.
  const symbols=["kaze","hikari","yami","mizu","mizu"],handY=H-335;
  for(let i=0;i<5;i++)this.sprite(symbols[i],i*128+5,handY,118,112);
  this.dirty=true;
 }
 panelUV(name){
  // Coordinates are injected from the r17 atlas manifest, never guessed.
  const sp=this.layout.panelSprites?.[name]||this.layout.panelSprites?.panel_blank;
  if(!sp)return[[0,0],[0,0],[0,0],[0,0]];
  const[x,y,w,h]=sp.rect,[W,H]=sp.sheet;
  return[[x/W,1-(y+h)/H],[(x+w)/W,1-(y+h)/H],[(x+w)/W,1-y/H],[x/W,1-y/H]];
 }
 setView(focus){
  const H=this.H;this.view=focus==="party"?{x:0,y:H-230,w:640,h:230}:focus==="cutin"?{x:0,y:H-470,w:640,h:230}:focus==="enemy"?{x:0,y:Math.max(0,H/2-560),w:640,h:670}:{x:0,y:0,w:640,h:H};this.dirty=true;
 }
 resize(cssW,cssH){
  const d=Math.min(devicePixelRatio||1,2);this.canvas.width=Math.round(cssW*d);this.canvas.height=Math.round(cssH*d);this.overlay.width=this.canvas.width;this.overlay.height=this.canvas.height;this.dirty=true;
 }
 render(){
  if(!this.dirty||!this.state)return;this.dirty=false;const g=this.gl,v=this.view;
  g.viewport(0,0,this.canvas.width,this.canvas.height);g.clearColor(0,0,0,1);g.clear(g.COLOR_BUFFER_BIT);g.uniform4fv(this.uView,[v.x,v.y,v.w,v.h]);for(const item of this.items)this.draw(item);
  const c=this.overlay.getContext("2d"),sx=this.overlay.width/v.w,sy=this.overlay.height/v.h;c.clearRect(0,0,this.overlay.width,this.overlay.height);c.save();c.scale(sx,sy);c.translate(-v.x,-v.y);
  for(const t of this.texts){c.save();c.translate(t.x,t.y);c.rotate(t.angle);c.font="900 "+t.size+'px Impact,"Arial Narrow","Microsoft JhengHei",sans-serif';c.textAlign=t.align;c.textBaseline="alphabetic";c.strokeStyle="#000";c.lineWidth=Math.max(2,t.size*.14);c.lineJoin="round";c.strokeText(t.value,0,0);c.fillStyle=t.color;c.fillText(t.value,0,0);c.restore();}
  if(this.state.mode){const target=this.items.find(it=>it.tag?.kind===this.state.mode&&(this.state.mode!=="party"||it.tag.slot===this.state.slot));if(target){c.strokeStyle="#e4a7ff";c.lineWidth=1.5/sx;c.setLineDash([5/sx,3/sx]);c.beginPath();for(const[a,b]of boundary(target)){c.moveTo(...target.points[a]);c.lineTo(...target.points[b]);}c.stroke();c.setLineDash([]);}}
  c.restore();
 }
 point(e){const r=this.canvas.getBoundingClientRect(),v=this.view;return[v.x+(e.clientX-r.left)/r.width*v.w,v.y+(e.clientY-r.top)/r.height*v.h];}
 hit(p){
  for(const it of [...this.items].reverse()){
   if(!it.tag)continue;
   for(let k=0;k<it.triangles.length;k+=3){const ids=it.triangles.slice(k,k+3),[a,b,c]=ids.map(i=>it.points[i]),det=(b[0]-a[0])*(c[1]-a[1])-(c[0]-a[0])*(b[1]-a[1]);if(Math.abs(det)<1e-6)continue;
    const u=((p[0]-a[0])*(c[1]-a[1])-(c[0]-a[0])*(p[1]-a[1]))/det,v=((b[0]-a[0])*(p[1]-a[1])-(p[0]-a[0])*(b[1]-a[1]))/det;
    if(u>=-.001&&v>=-.001&&u+v<=1.001){const [ta,tb,tc]=ids.map(i=>it.baseUV[i]);return{...it.tag,item:it,uv:[ta[0]+u*(tb[0]-ta[0])+v*(tc[0]-ta[0]),ta[1]+u*(tb[1]-ta[1])+v*(tc[1]-ta[1])],jacobian:[[(tb[0]-ta[0])*(c[1]-a[1])-(tc[0]-ta[0])*(b[1]-a[1]),(tc[0]-ta[0])*(b[0]-a[0])-(tb[0]-ta[0])*(c[0]-a[0])],[(tb[1]-ta[1])*(c[1]-a[1])-(tc[1]-ta[1])*(b[1]-a[1]),(tc[1]-ta[1])*(b[0]-a[0])-(tb[1]-ta[1])*(c[0]-a[0])]].map(row=>row.map(x=>x/det))};}
   }
  }return null;
 }
 capture(){const c=document.createElement("canvas");c.width=this.canvas.width;c.height=this.canvas.height;c.getContext("2d").drawImage(this.canvas,0,0);c.getContext("2d").drawImage(this.overlay,0,0);return c;}
}
