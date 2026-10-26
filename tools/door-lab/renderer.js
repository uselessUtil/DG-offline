import{transforms,worldPoint}from"./core.js";
export class DoorRenderer{
 constructor(canvas,data,assetURL,onError){
  this.canvas=canvas;this.data=data;this.assetURL=assetURL;this.onError=onError;this.cache=new Map();this.parts=new Map();
  const gl=this.gl=canvas.getContext("webgl",{alpha:true,antialias:true,preserveDrawingBuffer:true});if(!gl)throw Error("此瀏覽器無法使用 WebGL，請以 Chrome／Edge 開啟並啟用硬體加速。");
  const shader=(type,code)=>{const s=gl.createShader(type);gl.shaderSource(s,code);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(s));return s;};
  this.program=gl.createProgram();gl.attachShader(this.program,shader(gl.VERTEX_SHADER,"attribute vec3 position;attribute vec2 uv;attribute vec4 tint;varying vec2 vUV;varying vec4 vTint;void main(){gl_Position=vec4(position,1.);vUV=uv;vTint=tint;}"));gl.attachShader(this.program,shader(gl.FRAGMENT_SHADER,"precision mediump float;uniform sampler2D image;varying vec2 vUV;varying vec4 vTint;void main(){vec4 c=texture2D(image,vUV)*vTint;if(c.a<0.02)discard;gl_FragColor=c;}"));gl.linkProgram(this.program);if(!gl.getProgramParameter(this.program,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(this.program));gl.useProgram(this.program);
  this.buffer=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,this.buffer);for(const[name,count,offset]of[["position",3,0],["uv",2,12],["tint",4,20]]){const a=gl.getAttribLocation(this.program,name);gl.enableVertexAttribArray(a);gl.vertexAttribPointer(a,count,gl.FLOAT,false,36,offset);}gl.enable(gl.BLEND);gl.blendFunc(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA);gl.disable(gl.CULL_FACE);gl.enable(gl.DEPTH_TEST);gl.depthFunc(gl.LEQUAL);gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,true);
 }
 texture(resource,part="atlas"){
  const key=resource+"/"+part;if(this.cache.has(key))return this.cache.get(key).texture;
  const record={texture:null};this.cache.set(key,record);const image=new Image();image.onload=()=>{try{
   const gl=this.gl;let source=image;
   if(part!=="atlas"){
    const test=document.createElement("canvas");test.width=444;test.height=224;let ctx=test.getContext("2d");ctx.drawImage(image,580,320,444,224,0,0,444,224);const pixels=ctx.getImageData(0,0,444,224).data;let occupied=0;for(let i=0;i<pixels.length;i+=4)if(pixels[i+3]&&Math.max(pixels[i],pixels[i+1],pixels[i+2])>8)occupied++;const split=occupied>1000;
    const rect=part==="upper"?[0,0,1024,316]:split?(part==="boss"?[812,320,188,224]:[592,320,184,224]):[420,104,184,212];
    const c=document.createElement("canvas");c.width=rect[2];c.height=rect[3];ctx=c.getContext("2d");ctx.drawImage(image,...rect,0,0,c.width,c.height);
    if(part==="upper"&&!split)ctx.clearRect(420,104,184,212);
    const p=ctx.getImageData(0,0,c.width,c.height);for(let i=0;i<p.data.length;i+=4)if(p.data[i]===0&&p.data[i+1]===0&&p.data[i+2]===0)p.data[i+3]=0;ctx.putImageData(p,0,0);source=c;
   }
   const t=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,t);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,source);record.texture=t;
   while(this.cache.size>24){const k=this.cache.keys().next().value;if(k===key)break;const old=this.cache.get(k);if(old.texture)gl.deleteTexture(old.texture);this.cache.delete(k);}
  }catch(e){this.onError(e);}};image.onerror=()=>this.onError(Error("內嵌貼圖讀取失敗："+resource));image.src=this.assetURL(resource);return null;
 }
 geometry(family,trans,index,texture,view,tint=null){
  const gl=this.gl,n=family.nodes[index],m=n.mesh,t=trans[index];if(!m||!t.visible||!texture)return;let parent=n.parent;while(parent>=0){if(!trans[parent].visible)return;parent=family.nodes[parent].parent;}
  const scale=2*view.zoom/5.7,aspect=this.canvas.width/this.canvas.height,angle=view.tilt*Math.PI/180,vertices=[];
  for(const i of m.triangles){const p=worldPoint(family,trans,index,m.positions[i]);vertices.push(p[0]*scale,((p[2]-2.9)*Math.cos(angle)+p[1]*Math.sin(angle))*scale*aspect,Math.max(-.95,Math.min(.95,-p[1]*.12)),m.uv[i][0]*t.uvScale[0]+t.uvOffset[0],m.uv[i][1]*t.uvScale[1]+t.uvOffset[1],...(tint||m.colors?.[i]||[1,1,1,1]));}
  gl.bindTexture(gl.TEXTURE_2D,texture);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(vertices),gl.DYNAMIC_DRAW);gl.drawArrays(gl.TRIANGLES,0,m.triangles.length);
 }
 render(area,profile,slot,phase,view){
  const gl=this.gl;gl.viewport(0,0,this.canvas.width,this.canvas.height);gl.clearColor(0,0,0,0);gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);
  const atlas=this.texture(area.resource),family=this.data.families[profile.family];if(!family)return;
  if(view.background){const map=this.data.mapModels[area.mapFamily];if(map){const trans=map.nodes.map(n=>({...n,uvScale:n.uvScale||[1,1],uvOffset:n.uvOffset||[0,0],visible:n.active}));map.nodes.forEach((n,i)=>this.geometry(map,trans,i,atlas,view));}
   else{const mesh={positions:[[-2.5,-.2,1.916],[2.5,-.2,1.916],[2.5,-.2,3.459],[-2.5,-.2,3.459]],uv:[[0,0],[1,0],[1,1],[0,1]],triangles:[0,2,1,0,3,2]};const f={nodes:[{...mesh,mesh,parent:-1}]};this.geometry(f,[{position:[0,0,0],rotation:[0,0,0,1],scale:[1,1,1],uvScale:[1,1],uvOffset:[0,0],visible:true}],0,this.texture(area.resource,"upper"),view);}}
  const trans=transforms(family,profile,slot,phase);family.nodes.forEach((n,i)=>this.geometry(family,trans,i,n.part?this.texture(area.resource,n.part):atlas,view,view.highlight&&n.path===view.node?[.55,1,.85,1]:null));
 }
}
