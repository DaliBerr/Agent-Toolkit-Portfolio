// Original Lilith split-waist wolf. Grid toolkit: PixelFX, MIT.
import { hex, fit, clamp, smooth, TAU } from '../../core/index.js';
import { Grid, curve } from './_lib.js';

export const clips = {idle:{frames:16,loop:true},move:{frames:12,loop:true},attack:{frames:16,loop:false},hit:{frames:8,loop:false},death:{frames:16,loop:false}};
export const RAMP=['#151b1e','#263034','#3a4547','#535f5d','#7c857b','#d8d2b3','#ede2c0'];
export const SPRITE_SCALE=.7;
const RED=hex('#ad5c4d'), RED_DARK=hex('#653b39'), EYE=hex('#e6bd6c');
const G=new Grid(-62,-70,124,89);
export function makeMeta(state){const c=clips[state];return {name:`Ink beast ${state}`,desc:'A small isometric wolf with a visibly separated waist, bound by three ink cords. '+state,tags:['critter','wolf','ink','split-waist',state],size:[128,96],origin:[64,72],duration:c.frames/12,loop:c.loop,fps:12,bg:'ground',options:{palette:RAMP,seed:1,spriteScale:SPRITE_SCALE}};}

function pose(state,u){
 const s={x:0,y:0,front:0,rear:0,crouch:0,stretch:0,jaw:0,blink:false,flash:0,fall:0,fade:1};
 const a=u*TAU;
 if(state==='idle'){s.y=Math.sin(a)*.65;s.front=Math.sin(a-.4)*.5;s.blink=u>.69&&u<.79;}
 if(state==='move'){s.y=-Math.abs(Math.sin(a))*2;s.front=Math.sin(a)*1.2;s.rear=-Math.sin(a)*.8;}
 if(state==='attack'){
  s.x=curve(u,[[0,0],[.27,-3],[.375,3],[.5,5],[.63,1],[1,0]]);
  s.y=curve(u,[[0,0],[.30,1],[.375,-5],[.5,-4],[.625,0],[1,0]]);
  s.stretch=curve(u,[[0,0],[.30,-1],[.375,8],[.5,9],[.65,3],[.86,0],[1,0]]);
  s.crouch=curve(u,[[0,0],[.27,4],[.375,0],[.5,0],[.625,3],[.88,0],[1,0]]);
  s.jaw=curve(u,[[0,0],[.25,1],[.375,4],[.44,0],[.5,1],[.62,0],[1,0]]);
 }
 if(state==='hit'){s.x=curve(u,[[0,-2],[.18,-5],[.4,-3],[1,0]]);s.crouch=curve(u,[[0,1],[.25,3],[.55,1],[1,0]]);s.blink=u<.4;s.flash=u<.15?1:0;s.stretch=Math.sin(u*Math.PI)*2;}
 if(state==='death'){s.fall=smooth(.06,.43,u);s.y=3*s.fall;s.stretch=3*s.fall;s.blink=true;s.fade=1-smooth(.53,.92,u);}
 return s;
}

export function drawBeast(p,time,o,state){
 const c=clips[state],D=c.frames/12;
 if(!c.loop&&time>=D-1e-7)return;
 const t=Math.floor(((c.loop?((time%D)+D)%D:time)+1e-6)*12)/12,u=t/D;
 const s=pose(state,u); if(s.fade<=0)return;
 const C=fit(o.palette,7),a=u*TAU;
 G.clear();
 const point=(x,y,front=false)=>[x+s.x+(front?s.stretch:0),y+s.y+s.crouch*(front?.8:.5)];
 const P=(pts,col,front=false)=>G.poly(pts.map(([x,y])=>point(x,y,front)),col);
 const T=(pts,r,col,front=false)=>G.tube(pts.map(([x,y])=>point(x,y,front)),r,col);
 const L=(pts,col,front=false)=>T(pts,.6,col,front);
 function leg(x,y,near,front,phase){
  const stride=state==='move'?Math.sin(a+phase)*4:0;
  const lift=state==='move'?Math.max(0,Math.cos(a+phase))*3:0;
  const jump=state==='attack'&&u>=.375&&u<.57;
  const footX=x+stride+(jump?(front?5:-4):0),footY=(near?1:-5)-lift-(jump?5:0);
  const kneeX=front?x-1+stride*.4:x+3+stride*.4, kneeY=y+8;
  const col=near?C[2]:C[1];
  T([[x,y],[kneeX,kneeY],[footX,footY-2]],near?2.1:1.6,C[1],front);
  T([[x,y],[kneeX,kneeY],[footX,footY-2]],near?1.6:1.1,col,front);
  P([[footX-2,footY-3],[footX+3,footY-3],[footX+4,footY],[footX-2,footY]],col,front);
  if(near){L([[x-.7,y+1],[kneeX-.7,kneeY],[footX-1,footY-3]],C[3],front);L([[footX+2,footY-1],[footX+3,footY-1]],C[4],front);}
 }
 // Far legs sit higher on the isometric ground than the near pair.
 leg(-11,-21,false,false,Math.PI);leg(15,-22,false,true,0);
 // A low bushy tail, well away from the waist gap.
 const wag=state==='move'?Math.sin(a)*2:Math.sin(a)*1;
 P([[-22,-23],[-29,-24],[-31,-20],[-36,-20+wag],[-34,-15+wag],[-28,-14],[-20,-18]],C[1]);
 P([[-23,-22],[-28,-22],[-29,-18],[-33,-17+wag],[-28,-16],[-21,-18]],C[2]);
 L([[-24,-22],[-27,-21],[-29,-18]],C[3]);
 // Rear half. The ragged edge ends at x=-6, with no ordinary flesh across the waist.
 P([[-23,-28],[-18,-32],[-10,-31],[-6,-29],[-7,-26],[-5,-24],[-8,-22],[-7,-18],[-11,-15],[-16,-16],[-19,-14],[-20,-17],[-24,-21]],C[1]);
 P([[-22,-27],[-17,-30],[-10,-29],[-8,-27],[-8,-24],[-10,-21],[-9,-19],[-13,-17],[-20,-18]],C[2]);
 P([[-21,-27],[-17,-30],[-11,-29],[-9,-27],[-13,-26],[-18,-25]],C[3]);
 L([[-20,-27],[-17,-29],[-12,-28]],C[4]);
 leg(-18,-19,true,false,0);
 // Thin ink tethers deliberately leave background visible between the two halves.
 const left=point(-6,-26),right=point(3,-27,true);
 for(let j=0;j<3;j++){
  const sag=(j-1)*2+Math.sin(a+j)*.6;
  G.tube([left,[left[0]+(right[0]-left[0])*.42,-24+s.y+sag+s.crouch*.5],right],j===1?.8:.55,C[j===1?3:2]);
 }
 // Front ribcage and spiky ruff.
 P([[3,-29],[8,-34],[14,-33],[18,-29],[20,-24],[18,-18],[15,-16],[8,-17],[4,-20],[5,-23],[2,-25]],C[1],true);
 P([[5,-28],[9,-32],[14,-31],[17,-27],[17,-23],[15,-19],[10,-19],[6,-22]],C[2],true);
 P([[6,-28],[10,-31],[14,-30],[15,-27],[10,-25],[7,-25]],C[3],true);
 P([[10,-29],[12,-37],[16,-35],[19,-37],[22,-32],[21,-26],[17,-22],[15,-25],[12,-23]],C[1],true);
 L([[8,-30],[11,-31],[13,-30]],C[4],true);
 leg(10,-20,true,true,Math.PI);
 // Long wolf face, pointed ears, ivory cheek/muzzle; never a rounded feline head.
 const headDown=s.crouch*.25;
 const H=(pts,col)=>P(pts.map(([x,y])=>[x,y+headDown]),col,true);
 H([[14,-34],[12,-46],[18,-43],[20,-38],[24,-39],[28,-43],[30,-34],[33,-29],[38,-25],[36,-21],[30,-20],[24,-23],[19,-24],[16,-29]],C[1]);
 H([[15,-35],[14,-43],[17,-41],[19,-35],[23,-36],[27,-39],[28,-33],[31,-28],[35,-25],[34,-23],[29,-22],[23,-25],[19,-25]],C[2]);
 H([[15,-40],[15,-43],[17,-40],[18,-36]],C[4]);
 H([[26,-36],[27,-39],[28,-34]],C[3]);
 H([[18,-34],[21,-35],[26,-33],[29,-29],[26,-28],[22,-29],[20,-27],[17,-28]],C[3]);
 H([[20,-32],[24,-32],[27,-29],[23,-28],[21,-29]],C[4]);
 H([[18,-28],[21,-27],[23,-25],[20,-26],[19,-24],[17,-27]],C[3]);
 H([[26,-28],[29,-28],[35,-25],[34,-23],[30,-22],[25,-24],[23,-26]],C[5]);
 H([[28,-27],[34,-25],[33,-24],[29,-24]],C[6]);
 H([[34,-26],[38,-25],[37,-22],[34,-23]],C[0]);
 // Jaw hinges below the long muzzle and closes sharply on the contact frame.
 H([[24,-24],[28,-22+s.jaw],[34,-22+s.jaw],[34,-20+s.jaw],[29,-20+s.jaw],[24,-23]],C[0]);
 H([[26,-23],[29,-22+s.jaw],[33,-22+s.jaw],[32,-21+s.jaw],[28,-21+s.jaw]],C[5]);
 if(s.jaw>1)H([[29,-23],[30,-23],[30,-21]],C[6]);
 H(s.blink?[[23,-30],[26,-29],[26,-28],[23,-29]]:[[23,-31],[26,-30],[25,-28],[23,-29]],s.blink?C[0]:EYE);
 if(!s.blink)H([[25,-30],[26,-30],[25,-28]],C[0]);
 // A muted cinnabar binding at the front waist anchors the anomalous joint.
 H([[3,-29],[5,-30],[7,-21],[5,-20]],RED_DARK);
 L([[4,-28],[6,-22]],RED,true);
 T([[5,-24],[2,-22],[4,-20],[6,-22],[8,-20],[10,-21]],.65,RED,true);
 // Colored edge pixels belong to the anatomy. Do not dilate an extra black outline.
 if(state==='hit'&&s.flash)for(let i=0;i<G.c.length;i++)if(G.c[i]===C[2]||G.c[i]===C[3])G.c[i]=C[4];
 // Collapse around the paws and tip to the side before a clean fade.
 const fall=s.fall,scale=o.spriteScale??SPRITE_SCALE;
 for(let yy=Math.floor(G.y0*scale);yy<Math.ceil((G.y0+G.h)*scale);yy++)for(let xx=Math.floor(G.x0*scale);xx<Math.ceil((G.x0+G.w)*scale);xx++){
  const sourceY=((yy+.5)/scale-2*fall)/(1-.73*fall),sourceX=(xx+.5)/scale+fall*sourceY*.2;
  const i=G.idx(sourceX,sourceY);if(i<0||!G.c[i])continue;
  const alpha=G.a[i]*s.fade;
  if(alpha>=.999)p.dot(xx,yy,G.c[i]);else p.blend(xx,yy,G.c[i],alpha);
 }
 // Preserve one readable eye pixel at the small in-game size.
 if(!s.blink&&s.fade===1){const eye=point(24,-29+headDown,true);p.dot(Math.round(eye[0]*scale),Math.round(eye[1]*scale),EYE);}
}
