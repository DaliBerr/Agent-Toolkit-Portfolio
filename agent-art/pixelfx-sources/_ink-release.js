// Ink-brush launch cues. Base stroke adapts Sprite Fusion slash, MIT, Copyright (c) 2026 Hugo Duprez.
import { create as createInk, meta as inkMeta } from './ink-brush.js';
import { hex, Rng, clamp, smooth, ease, TAU } from '../../core/index.js';
export const variants = {
 fire: {word:'火', accent:['#632b26','#ad4c34','#db8850','#f2c57d'], radius:39},
 ice: {word:'冰', accent:['#244c61','#54879a','#9fcbd1','#dde7d8'], radius:38},
 thunder: {word:'雷', accent:['#44385c','#786298','#b69bc4','#e5d5df'], radius:40},
 mirror: {word:'镜', accent:['#42465b','#737e8b','#b6b9b4','#e5e0c9'], radius:38},
 arrow: {word:'箭', accent:['#3f4539','#69715a','#a2a58a','#d4ceb0'], radius:36},
 blade: {word:'刃', accent:['#303a39','#566360','#829085','#bec4b2'], radius:40},
 spear: {word:'矛', accent:['#34423f','#5b7a70','#9db2a0','#d7d8bc'], radius:38},
 hammer: {word:'锤', accent:['#514239','#89705b','#b39a77','#ddd0ad'], radius:41},
 light: {word:'光', accent:['#5c5743','#a99a68','#d9cb93','#f1e5bb'], radius:38},
 curtain: {word:'幕', accent:['#4a4054','#796b84','#b3a4b4','#dcd1cf'], radius:40},
 wind: {word:'风', accent:['#344f4f','#618a80','#9cbcac','#d0d7bd'], radius:39},
 poison: {word:'毒', accent:['#414d30','#718346','#a4af60','#d0c787'], radius:40},
 quake: {word:'震', accent:['#504b42','#837c65','#b1a78d','#d6cfb5'], radius:41},
 burning: {word:'燃', accent:['#552b2a','#9b4737','#d77b46','#eab578'], radius:41},
 summon: {word:'召', accent:['#3d4052','#74758c','#afafc2','#dcd8d8'], radius:40},
 heir: {word:'嗣', accent:['#364a56','#607d92','#9cb4c4','#d2d9d4'], radius:38},
 turret: {word:'塔', accent:['#4f433c','#826e59','#b2a17d','#d9ceb0'], radius:39},
 spirit: {word:'灵', accent:['#374c46','#63867b','#a3b6a2','#d4d8bf'], radius:39},
};
export function makeMeta(id) {
 const v=variants[id];
 return {...inkMeta,origin:[40,66],name:`Ink release ${id}`,desc:`A brief ${id} accent on a local ink-brush release; no projectile, hit or persistent zone.`,tags:['ink','release',id,'one-shot'],options:{...inkMeta.options,radius:v.radius,palette:['#101417','#1b2225','#303b3b',...v.accent]}};
}
function arc(p,cx,cy,rx,ry,a0,a1,c,alpha=1) {
 let last=null;
 for(let a=a0;a<=a1;a+=.07){const xy=[cx+Math.cos(a)*rx,cy+Math.sin(a)*ry];if(last)p.line(...last,...xy,c,alpha);last=xy;}
}
export function makeRelease(id){return (o)=>{
 const ink=createInk(o), rng=new Rng(o.seed+1703), C=variants[id].accent.map(hex);
 const points=Array.from({length:12},()=>({a:rng.range(-2.5,.7),r:rng.range(23,38),v:rng.range(3,13),s:rng.range(1,3),phase:rng.range(0,TAU)}));
 let t=0;
 return {update(dt){t+=dt;ink.update(dt);},draw(p){
  // Move the anchor 16px forward while preserving the original painted pixels.
  const tx=p.tx,ty=p.ty; const nx=p.toX(4,26),ny=p.toY(4,26);p.tx=nx;p.ty=ny;
  try {
  ink.draw(p); if(t<=.018 || t>=.60)return;
  const e=(1-smooth(.18,.58,t))*Math.min(1,t/.055), u=ease.outExpo(clamp(t/.11));
  const X=27,Y=-29;
  // Small flourishes stay at the brush release point instead of becoming travelling attacks.
  if(id==='fire'||id==='burning'){
   for(let j=0;j<(id==='fire'?5:8);j++){let x=10+j*5,y=-39+Math.sin(j*.8)*4, h=(7+Math.sin(j*2+1)*4)*e;
    p.poly([[x-2,y],[x+2,y],[x+1,y-h*.5],[x+3,y-h],[x-1,y-h*.7]],C[j%2],e);
    p.line(x,y-2,x+1,y-h*.65,C[2],e*.85);
   }
  }else if(id==='ice'){
   for(let j=0;j<6;j++){let x=7+j*7,y=-35+Math.sin(j*.6)*4,h=(5+j%3*3)*e;
    p.poly([[x-2,y],[x,y-h],[x+3,y-1],[x,y+3]],C[1],e);p.line(x,y-h,x,y+1,C[3],e);
   }
  }else if(id==='thunder'){
   for(let j=0;j<3;j++){let y=-39+j*9,x=5+j*4;
    p.polyline([[x,y],[x+11,y-4],[x+8,y+1],[x+24,y-2],[x+18,y+5],[x+39,y+1]],C[1],e);
    if(t<.16)p.polyline([[x+1,y],[x+11,y-4],[x+8,y+1],[x+24,y-2]],C[3],e);
   }
  }else if(id==='mirror'){
   for(let j=0;j<4;j++){let x=14+j*9,y=-38+Math.sin(j)*10,s=(3+j%2)*e;
    p.poly([[x-s,y],[x,y-s*1.4],[x+s,y],[x,y+s*1.4]],C[1],e);p.line(x-s,y,x,y-s*1.4,C[3],e);
   }
  }else if(id==='arrow'||id==='spear'){
   const len=(id==='spear'?44:32)*u;
   for(let j=-2;j<=2;j++)p.line(7+Math.abs(j)*3,-25+j*2,7+len,-25+j*.2,C[j===0?3:1],e*(j===0?1:.7));
   p.poly([[7+len,-25],[len-1,-29],[len+1,-25],[len-1,-21]],C[2],e);
  }else if(id==='hammer'){
   for(let j=0;j<5;j++)p.beam(13+j*5,-42,22+j*4,-21,2+(j===2?2:0),C[j===2?2:0],e);
   arc(p,34,-17,13*u,4*u,0,TAU,C[1],e);
  }else if(id==='light'){
   p.beam(6,-29,47*u,-29,2,C[2],e);p.line(5,-30,47*u,-30,C[3],e);
   p.line(30,-38,30,-20,C[1],e*.7);p.line(24,-29,37,-29,C[3],e);
  }else if(id==='curtain'){
   for(let j=0;j<8;j++){let x=14+j*4; p.line(x,-45+Math.sin(j*.5)*2,x+7*u,-18+Math.sin(j*.5)*2,C[j%3],e*.8);}
   arc(p,21,-30,22*u,12,-1.3,1.25,C[2],e);
  }else if(id==='wind'){
   for(let j=0;j<3;j++)arc(p,17+j*2,-29,24*u+j*4,6+j*4,-2.9,.9,C[j],e*(1-j*.15));
  }else if(id==='poison'){
   for(let j=0;j<5;j++){let x=11+j*8,y=-33-Math.sin(j+t*7)*5,r=(2+j%2)*e;
    arc(p,x,y,r,r,0,TAU,C[1+j%2],e);p.dot(x-1,y-1,C[3],e);
   }
  }else if(id==='quake'){
   for(let j=0;j<3;j++)arc(p,24,-23,8+16*u-j*5,3+7*u-j,-1.6,1.6,C[j%3],e);
   p.polyline([[5,-25],[14,-28],[19,-23],[28,-28],[35,-22],[43,-25]],C[2],e);
  }else if(id==='summon'){
   arc(p,27,-29,13*u,9*u,0,TAU,C[1],e);
   p.polyline([[27,-41],[42,-29],[27,-17],[12,-29],[27,-41]],C[2],e);
  }else if(id==='heir'){
   for(let j=0;j<2;j++)arc(p,21+j*13,-29-j*5,9*u,5*u,-2.9,2,C[1+j],e);
   p.line(15,-29,42,-34,C[1],e*.6);
  }else if(id==='turret'){
   for(let j of [-1,1])p.polyline([[27+j*5,-42],[27+j*13,-42],[27+j*13,-19],[27+j*5,-19]],C[2],e);
   p.line(22,-30,33,-30,C[3],e);
  }else if(id==='spirit'){
   for(let j=0;j<3;j++)arc(p,24+j*4,-30,5+j*4,4+j*3,-2.4+t*3,2.1+t*3,C[j],e);
  }
  // Sparse local dust gives each release a finish without replacing the projectile.
  for(let j=0;j<points.length;j++){
   const q=points[j], age=clamp((t-.07)/.5), x=8+Math.cos(q.a)*(q.r+q.v*age), y=-28+Math.sin(q.a)*(q.r*.4+q.v*age*.4);
   if(j%3===0)p.rect(x,y,q.s*.7,q.s*.7,C[j%3],e*.75);
  }
 } finally {p.tx=tx;p.ty=ty;}
 }};
};}


