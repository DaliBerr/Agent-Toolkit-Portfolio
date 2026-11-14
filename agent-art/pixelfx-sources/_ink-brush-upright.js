// Close, upright brush release drawn for Lilith. Local +x is the shot direction.
import { fit, hex, Rng, clamp, smooth, ease, TAU } from '../../core/index.js';

function path(u, shift = 0) {
  return [-1 + 32 * u - 17 * u * u + shift, -37 + 54 * u];
}

function ring(p, x, y, r, c, alpha) {
  let last = null;
  for (let a = 0; a <= TAU + .12; a += .16) {
    const point = [x + Math.cos(a) * r, y + Math.sin(a) * r];
    if (last) p.line(...last, ...point, c, alpha);
    last = point;
  }
}

function ribbon(p, t, ramp, shift, width, alpha = 1) {
  const head = ease.outCubic(clamp(t / .16));
  const tail = smooth(.25, .64, t);
  if (head <= tail || t >= .64) return;
  p.shade(-8 + shift, -39, 31 + shift, 20, (x, y, out) => {
    const u = (y + 37) / 54;
    if (u < tail || u > head || u < 0 || u > 1) return;
    const [cx] = path(u, shift);
    const taper = Math.pow(Math.sin(Math.PI * u), .7);
    const tip = Math.min(1, Math.sqrt(Math.max(0, head - u) * 12));
    const w = (.65 + width * taper) * (1 - tail * .35) * tip;
    if (w < .2) return;
    const q = (x - cx) / w + .5;
    if (q < 0 || q > 1) return;
    // A few continuous dry bristle channels, rather than noisy holes.
    if (u > .12 && u < .85 && q > .3 && q < .82 && Math.sin(q * 24 + u * 5) > .91) return;
    out.c = ramp[q > .86 ? 3 : q > .64 ? 2 : q > .18 ? 1 : 0];
    out.a = alpha * (1 - smooth(.39, .64, t)) * Math.min(1, (head - u) * 16 + .2);
  });
}

export function createUpright(o, id, accents) {
  const ramp = fit(o.palette, 7), C = accents.map(hex), rng = new Rng(o.seed + 821);
  const drops = Array.from({length: 7}, () => ({u: rng.range(.28, .97), x: rng.range(1, 7), speed: rng.range(3, 11), size: rng.range(.7, 1.7)}));
  let t = 0;
  return {
    update(dt) { t += dt; },
    draw(p) {
      ribbon(p, t, ramp, -3, 1.3, .55);
      ribbon(p, t, ramp, 0, id === 'hammer' || id === 'burning' ? 5.8 : 4.2);
      if (t < .025 || t >= .66) return;
      const e = (1 - smooth(.23, .62, t)) * Math.min(1, t / .06);
      const head = ease.outCubic(clamp(t / .16));
      for (let j = 0; j < 5; j++) {
        const u = .18 + j * .16;
        if (u > head) continue;
        const [x, y] = path(u);
        if (id === 'fire' || id === 'burning') {
          const h = (4 + j % 3 * 2) * e;
          p.poly([[x + 1, y + 2], [x + 5, y], [x + 3, y - h], [x + 1, y - h * .6]], C[j % 2], e);
          p.line(x + 3, y, x + 3, y - h * .6, C[2], e * .8);
        } else if (id === 'ice' || id === 'mirror') {
          const s = (id === 'ice' ? 2.4 : 1.8) * e;
          p.poly([[x + 1, y], [x + 3, y - s * 1.8], [x + 5, y], [x + 3, y + s]], C[1], e);
          p.line(x + 3, y - s * 1.8, x + 5, y, C[3], e);
        } else if (id === 'thunder') {
          p.polyline([[x + 1, y - 4], [x + 6, y - 1], [x + 2, y + 1], [x + 5, y + 5]], C[1], e);
          if (t < .2) p.line(x + 2, y - 3, x + 5, y - 1, C[3], e);
        } else if (id === 'arrow' || id === 'spear' || id === 'light' || id === 'curtain') {
          const [nx, ny] = path(Math.min(.99, u + .12));
          const offset = id === 'curtain' ? (j % 3 - 1) * 2 : 2;
          p.line(x + offset, y, nx + offset, ny, C[id === 'light' ? 2 : j % 3], e * .85);
        } else if (id === 'hammer' || id === 'quake') {
          p.polyline([[x - 2, y - 2], [x + 4, y], [x, y + 3], [x + 4, y + 5]], C[j % 3], e * .9);
        } else if (id === 'wind' || id === 'heir') {
          const [nx, ny] = path(Math.min(.98, u + .12), id === 'heir' ? -5 : 4);
          p.line(x + (id === 'heir' ? -5 : 4), y, nx, ny, C[j % 3], e * .7);
        } else if (id === 'poison' && j % 2 === 0) {
          ring(p, x + 4, y, 1.8 + j % 2, C[1], e);
          p.dot(x + 3, y - 1, C[2], e);
        } else if (id === 'summon' && j === 4) {
          ring(p, x + 1, y, 4 * e, C[1], e);
          p.line(x - 2, y, x + 4, y, C[2], e);
        } else if (id === 'turret' && j % 2 === 0) {
          p.polyline([[x - 2, y - 3], [x + 4, y - 3], [x + 4, y + 3], [x + 1, y + 3]], C[2], e);
        } else if (id === 'spirit' && j % 2 === 0) {
          ring(p, x + 1, y, 2.5 * e, C[1], e * .8);
        }
      }
      const age = clamp((t - .12) / .48);
      for (const d of drops) {
        if (d.u > head || t < .12) continue;
        const [x, y] = path(d.u);
        p.rect(x + d.x + age * 3, y + age * d.speed, d.size, d.size, ramp[1], e * .75);
      }
    },
  };
}
