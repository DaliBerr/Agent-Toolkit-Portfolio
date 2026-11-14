// Original Lilith ink-imp design. PixelFX Grid is used as a pixel-art drawing toolkit.
import { fit, hex, clamp, smooth, TAU } from '../../core/index.js';
import { Grid, curve } from './_lib.js';

export const clips = {
  idle: {frames:16, loop:true, label:'Idle'},
  move: {frames:12, loop:true, label:'Move'},
  attack: {frames:16, loop:false, label:'Attack'},
  hit: {frames:8, loop:false, label:'Hit'},
  death: {frames:20, loop:false, label:'Death'},
};
export const INK = ['#0f1316','#1c2427','#2b3437','#414c4d','#68746f','#a2a89a','#e7dfbd'];
export const SPRITE_SCALE = .8;
const PAPER = ['#73634e','#b8a481','#dfd0a8','#f0e3c0'].map(hex);
const RED = hex('#a84e43'), EYE = hex('#efe5c3'), PUPIL = hex('#402727');
const G = new Grid(-44,-64,89,82);

export function makeMeta(state, design='a') {
  const c=clips[state];
  return {name:`Ink imp ${c.label}`,desc:`A three-quarter isometric ink yokai: ${state}.`,
    tags:['critter','monster','ink','yokai',state],size:[96,96],origin:[49,72],
    duration:c.frames/12,loop:c.loop,fps:12,bg:'ground',
    options:{palette:INK,seed:1,design,spriteScale:SPRITE_SCALE}};
}

// Sample the original pixel grid around the same foot anchor, with no interpolation.
function drawScaled(g,p,scale) {
  if(scale===1){g.draw(p);return;}
  const x0=Math.floor(g.x0*scale),x1=Math.ceil((g.x0+g.w)*scale);
  const y0=Math.floor(g.y0*scale),y1=Math.ceil((g.y0+g.h)*scale);
  for(let y=y0;y<y1;y++)for(let x=x0;x<x1;x++) {
    const source=g.idx(Math.floor((x+.5)/scale),Math.floor((y+.5)/scale));
    if(source<0||!g.c[source])continue;
    if(g.a[source]>=.999)p.dot(x,y,g.c[source]);
    else p.blend(x,y,g.c[source],g.a[source]);
  }
}

function pose(state,t) {
  const c=clips[state], u=clamp(t/(c.frames/12)), a=u*TAU;
  const s={x:0,y:0,sx:1,sy:1,lean:0,arms:0,angry:0,blink:false,mouth:0,flash:false,flatten:0};
  if(state==='idle') {
    s.sy=1+.035*Math.sin(a); s.sx=1-.025*Math.sin(a); s.lean=.015*Math.sin(a);
    s.blink=u>.65&&u<.73;
  } else if(state==='move') {
    s.y=curve(u,[[0,0],[.13,0],[.3,-12],[.45,-16],[.65,-8],[.75,0],[.83,0],[1,0]]);
    s.sx=curve(u,[[0,1],[.12,1.18],[.25,.83],[.45,.96],[.65,.91],[.76,1.25],[.9,.96],[1,1]]);
    s.sy=curve(u,[[0,1],[.12,.76],[.25,1.2],[.45,1],[.65,1.08],[.76,.7],[.9,1.04],[1,1]]);
    s.lean=.06*Math.sin(a);s.arms=s.y<0?.35:0;
  } else if(state==='attack') {
    s.x=curve(u,[[0,0],[.12,-2],[.28,-4],[.33,9],[.39,15],[.52,15],[.62,9],[.77,0],[1,0]]);
    s.y=curve(u,[[0,0],[.28,0],[.36,-8],[.46,-6],[.6,0],[1,0]]);
    s.sx=curve(u,[[0,1],[.25,1.16],[.34,1.03],[.42,1.16],[.55,1.1],[.65,1.15],[.8,1],[1,1]]);
    s.sy=curve(u,[[0,1],[.25,.77],[.34,1.1],[.44,.9],[.55,.9],[.65,.83],[.8,1],[1,1]]);
    s.arms=curve(u,[[0,0],[.24,-.35],[.35,1],[.54,1],[.77,0],[1,0]]);
    s.angry=u>.12&&u<.7?1:0;s.mouth=u>.32&&u<.59?1:0;s.lean=u>.3&&u<.65?.09:0;
  } else if(state==='hit') {
    s.x=curve(u,[[0,0],[.13,-5],[.3,-8],[.5,-4],[.8,0],[1,0]]);
    s.sx=curve(u,[[0,.91],[.2,.83],[.4,1.12],[.7,.98],[1,1]]);
    s.sy=curve(u,[[0,1.08],[.22,1.16],[.42,.86],[.7,1.02],[1,1]]);
    s.lean=-.1*(1-u);s.blink=u<.38;s.flash=u<.12;
  } else if(state==='death') {
    s.x=u<.3?Math.sin(u*TAU*12)*1.6*(1-u/.3):0;
    s.sx=curve(u,[[0,1],[.15,1.07],[.25,.95],[.38,1.2],[.52,1.5],[1,1.5]]);
    s.sy=curve(u,[[0,1],[.15,1.05],[.26,1.13],[.38,.8],[.52,.2],[1,.06]]);
    s.blink=u>.27;s.flatten=smooth(.3,.58,u);s.arms=-smooth(.12,.5,u)*.4;
  }
  return s;
}

function paper(g,x,y,tilt=0,alpha=1,large=false,squash=1) {
  const w=large?12:5,h=large?15:13;
  const P=(a,b)=>[x+a+tilt*b,y+b*squash];
  g.poly([P(-1,-1),P(w,-1),P(w+1,h-3),P(w-1,h+1),P(-1,h)],PAPER[0],alpha);
  g.poly([P(0,0),P(w-1,0),P(w,h-3),P(w-1,h-1),P(0,h-1)],PAPER[2],alpha);
  g.line(...P(0,0),...P(0,h-2),PAPER[3],alpha);
  if(large) {
    g.line(...P(2,5),...P(4,6),hex('#192227'),alpha);
    g.line(...P(8,6),...P(10,5),hex('#192227'),alpha);
    g.line(...P(4,11),...P(7,11),hex('#192227'),alpha);
    g.set(...P(6,1),RED,alpha);g.set(...P(6,2),RED,alpha);
  } else {
    g.line(...P(2,2),...P(2,8),RED,alpha);
    g.line(...P(1,3),...P(3,3),RED,alpha);
    g.line(...P(1,6),...P(3,5),RED,alpha);
    g.set(...P(3,9),RED,alpha);
  }
}

function body(g,C,s,t,design,hidePaper=false) {
  const Q=(x,y)=>[s.x+x*s.sx+s.lean*(-y),s.y+y*s.sy];
  const poly=(pts,c)=>g.poly(pts.map(q=>Q(...q)),c);
  const tube=(pts,r,c)=>g.tube(pts.map(q=>Q(...q)),r,c);
  const ellipse=(x,y,rx,ry,c)=>g.ellipse(...Q(x,y),rx*s.sx,ry*s.sy,c);
  const ph=t*TAU*.75;
  // Rear ink feelers and the asymmetric dried-brush crown.
  tube([[-11,-11],[-20,-9],[-23,-3]],1.5,C[1]);
  if(design==='b') {
    poly([[-6,-27],[-9,-40],[-3,-46],[3,-42],[0,-35],[7,-29]],C[2]);
    poly([[-6,-34],[-5,-40],[-1,-43],[-2,-36]],C[3]);
  } else {
    poly([[-11,-25],[-14+Math.sin(ph),-39],[-5,-34],[-1,-26]],C[2]);
    poly([[5,-26],[13,-36],[14,-25],[10,-22]],C[1]);
    poly([[-12,-34],[-12,-37],[-6,-33],[-4,-29]],C[3]);
  }
  const rx=design==='c'?13.5:16, ry=design==='c'?18:15;
  const centerY=design==='c'?-18:-15;
  const [cx,cy]=Q(0,centerY), RX=rx*s.sx, RY=ry*s.sy;
  g.shade(cx-RX-1,cy-RY-1,cx+RX+1,cy+RY+1,(x,y)=>{
    const nx=(x-cx)/RX, ny=(y-cy)/RY;
    if(nx*nx+ny*ny>1)return 0;
    let c=nx>.4||ny>.55?1:2;
    if((nx+.34)**2/.32+(ny+.48)**2/.4<1)c=3;
    if(nx<-.36&&ny<-.14&&nx*nx+ny*ny>.61)c=4;
    if(nx<-.48&&ny<-.5&&nx*nx+ny*ny>.72)c=5;
    if(s.flash)c=Math.max(3,c+2);
    return C[Math.min(5,c)];
  });
  // Thick liquid feet and a short curled tail make the baseline legible.
  ellipse(-2,-2,14,3,C[1]);
  tube([[-9,-4],[-11,0],[-8,1]],1.7,C[2]);
  tube([[2,-3],[1,2],[5,2]],1.6,C[2]);
  tube([[-11,-5],[-20,-3],[-24,-6]],1.2,C[1]);
  g.line(...Q(-9,-1),...Q(-4,0),C[3]);
  // Near arm sweeps out of the body; the three fingertips remain connected.
  const ax=17+s.arms*2, ay=-12-s.arms*3;
  tube([[11,-15],[ax,ay],[20+s.arms*2,ay+1]],2.6,C[2]);
  for(let k=0;k<3;k++)tube([[20+s.arms*2,ay+1+k*.6],[24+s.arms*.5,ay+1+k*1.8]],.8,C[k===0?3:1]);
  g.outline((x,y,nx,ny)=>ny<0&&nx<0?C[1]:C[0]);
  // Ivory eyes sit on the visible front/right plane. The far eye is smaller.
  const eye=(x,y,w,h)=>{
    const [X,Y]=Q(x,y);
    if(s.blink){g.line(X,Y+2,X+w,Y+1,C[5]);return;}
    g.poly([[X,Y],[X+w,Y+.5+s.angry],[X+w-1,Y+h],[X,Y+h-1]],EYE);
    g.line(X+Math.max(1,w-2),Y+1,X+Math.max(1,w-2),Y+h-1,PUPIL);
    if(s.angry)g.line(X-1,Y-1,X+w,Y+1,C[0]);
  };
  if(design==='b') {
    ellipse(2,-16,8,6,C[0]);ellipse(2,-16,6.5,5,EYE);
    ellipse(4,-16,2.5,3,RED);ellipse(5,-16,1,2,C[0]);
  } else if(design!=='c') {
    eye(-6,-17,4,4);eye(3,-17,6,5);
  }
  const [mx,my]=Q(3,-7);
  if(s.mouth){g.ellipse(mx,my,4,3,C[0]);g.set(mx-2,my-2,EYE);g.set(mx+1,my-2,EYE);g.line(mx,my+1,mx+2,my+1,RED);}
  else {g.line(mx-3,my,mx+2,my+1,C[0]);g.set(mx+1,my+1,EYE);}
  if(!hidePaper) {
    if(design==='c')paper(g,...Q(-4,-29),.04*Math.sin(ph),1,true);
    else paper(g,...Q(-8,-33),-.12+.09*Math.sin(ph-1)+.1*s.arms-.06*clamp(-s.y/16),1,false);
  }
}

function scatter(g,C,state,t,o) {
  let start,life,side;
  if(state==='attack'){start=.48;life=.35;side=1;}
  else if(state==='hit'){start=.03;life=.38;side=-1;}
  else if(state==='death'){start=.5;life=.65;side=0;}
  else return;
  const age=t-start;if(age<0||age>=life)return;
  const u=age/life, alpha=1-smooth(.55,1,u);
  for(let j=0;j<7;j++) {
    const v=(j*17+o.seed*3)%11, sign=side|| (j%2?1:-1);
    const x=(side?side*19:0)+sign*(4+j*2)*u;
    const y=-8-(8+v)*u+25*u*u;
    g.ellipse(x,y,1+(j%3===0),1.1+(j%2),C[j%3===0?3:1],alpha);
  }
}

export function drawImp(p,time,o,state) {
  const c=clips[state], D=c.frames/12;
  if(!c.loop&&time>=D-1e-7)return;
  let t=c.loop?((time%D)+D)%D:time;
  t=Math.floor((t+1e-5)*12)/12;
  const u=t/D, s=pose(state,t), C=fit(o.palette,7), design=o.design||'a';
  G.clear();
  if(state==='move'&&u>=.16&&u<.32) {
    G.line(-4,s.y+1,-8,-1,C[1]);G.set(-8,0,C[3]);
  }
  if(state==='move'&&u>=.33&&u<.5)G.ellipse(-7,-6+((u-.33)/.17)*5,1,1.7,C[2]);
  if(state==='move'&&u>=.75&&u<.92) {
    const age=(u-.75)/.17;
    for(let j=0;j<4;j++)G.ellipse((j%2?1:-1)*(15+age*5+j),-3*(1-age),1.1,.8,C[1],1-age);
  }
  if(state==='death'&&u>=.53) {
    const fade=1-smooth(.69,.93,u), rx=22*(1-smooth(.72,.96,u)*.8);
    G.ellipse(0,1,rx,4.3,C[0],fade);G.ellipse(-2,0,rx*.8,2.8,C[2],fade);
    G.ellipse(-7,-1,5,1,C[3],fade*.8);
  } else {
    body(G,C,s,t,design,state==='death'&&u>=.28);
  }
  if(state==='death'&&u>=.28&&u<.93)paper(G,-8+12*Math.sin((u-.28)*3),Math.min(-2,-35+(u-.28)*60),-.12+.4*Math.sin((u-.28)*4),1-smooth(.8,.93,u),false,1-.85*smooth(.73,.87,u));
  scatter(G,C,state,t,o);
  if(state==='attack'&&u>=.36&&u<.61) {
    const a=smooth(.36,.45,u)*(1-smooth(.49,.61,u));
    G.tube([[27,-23],[35,-18],[38,-10],[34,-5]],(v)=>.7+1.5*Math.sin(v*Math.PI),C[3],a);
    G.tube([[25,-21],[33,-16],[34,-9]],.7,C[4],a*.7);
  }
  drawScaled(G,p,o.spriteScale??SPRITE_SCALE);
}

export const previewPose = (state, frame) => {
  const s=pose(state,frame/12);
  return {...s,x:s.x*SPRITE_SCALE,y:s.y*SPRITE_SCALE};
};
