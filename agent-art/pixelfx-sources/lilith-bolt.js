// Lilith element bolts: the small in-flight body of the fire / ice / thunder cores, sized for a 32 PPU
// projectile (~0.7 world units long). Flies right; origin = the head (the projectile's collision centre),
// so the tail trails behind whatever direction Unity rotates it to. A dark ink rim keeps each bolt
// readable over the painted grass. Seamless loop.
import { Rng, fit, clamp } from "../../core/index.js";

export const meta = {
  name: "Lilith element bolt",
  desc: "A small ink-rimmed element bolt flying right: a fire comet with flickering tongues, an ice shard with a running glint, or a crackling thunder orb.",
  tags: ["projectile", "bolt", "fire", "ice", "lightning", "ink", "loop", "directional"],
  size: [44, 22],
  origin: [32, 11],
  duration: 0.4,
  loop: true,
  fps: 20,
  bg: "dark",
  options: { element: "fire", seed: 5 },
};

const TAU = Math.PI * 2;
const PAL = {
  fire: ["#140c0c", "#5a1410", "#b42a14", "#ec5a1c", "#ffa030", "#ffe07a", "#fff8e0"],
  ice: ["#0c1218", "#123a5e", "#2a72b0", "#4ab0e6", "#8adcff", "#d0f6ff", "#ffffff"],
  thunder: ["#14120a", "#4a3a0e", "#a07a14", "#e8b424", "#ffd84a", "#fff29a", "#ffffff"],
};
const frac = (v) => v - Math.floor(v);

export function draw(p, t, o) {
  const el = PAL[o.element] ? o.element : "fire";
  const c = fit(PAL[el], 7), ph = t / meta.duration, R = new Rng(o.seed);
  if (el === "fire") fire(p, ph, c, R);
  else if (el === "ice") ice(p, ph, c, R);
  else thunder(p, ph, c, R);
}

function fire(p, ph, c, R) {
  // tail: three tongues of flame that lick back and sway, cooling from yellow to red; drawn twice,
  // first one pixel fatter in ink so the whole comet keeps a dark rim over bright ground
  const tongue = (k, pad, colorAt) => {
    const off = (k - 1) * 1.4, L = 12 + 4 * Math.sin(TAU * (ph * 2 + k / 3));
    for (let d = 0; d < L; d += 0.5) {
      const u = d / L, w = 3.4 * (1 - u) * (k === 1 ? 1 : 0.75);
      const y = off * (1 - u * 0.3) + Math.sin(TAU * (ph * 2 + u * 0.8 + k / 3)) * 1.4 * u;
      p.circle(-d - 1, y, Math.max(0.5, w + pad), colorAt(u));
    }
  };
  for (let k = 0; k < 3; k++) tongue(k, 1, () => c[0]);
  p.circle(0, 0, 5, c[0]);
  for (let k = 0; k < 3; k++) tongue(k, 0, (u) => (u < 0.25 ? c[5] : u < 0.5 ? c[4] : u < 0.75 ? c[3] : c[2]));
  // embers peel off the tail and drift back
  for (let i = 0; i < 6; i++) {
    const s = R.range(0, 1), v = R.range(-2.5, 2.5), u = frac(ph * 2 + i / 6 + s * 0.1);
    p.dot(-8 - u * 22, v + u * v, u < 0.5 ? c[5] : c[3], 1 - u);
  }
  // head: hot body lit from the front, white core
  p.circle(0, 0, 4, c[3]);
  p.circle(0.6, -0.4, 3, c[4]);
  p.circle(1, -0.5, 1.8, c[5]);
  p.circle(1.4, -0.8, 0.9, c[6]);
  p.glow(0, 0, 9, c[4], 0.35);
}

function ice(p, ph, c, R) {
  // frost wake: thin streaks and motes left hanging behind the shard
  for (let i = 0; i < 2; i++) {
    const y = i ? -2.2 : 2.4, L = 16 + 3 * Math.sin(TAU * (ph + i * 0.5));
    p.line(-7, y, -7 - L, y * 1.2, (u) => (u < 0.4 ? c[4] : c[3]), (u) => 1 - u);
  }
  for (let i = 0; i < 7; i++) {
    const y = R.range(-4, 4), u = frac(ph + i / 7);
    p.dot(-6 - u * 26, y * (0.6 + u * 0.5), u < 0.35 ? c[6] : c[4], 1 - u);
  }
  // the shard: an ink rim, a lit top facet, a shaded bottom facet, a cold rear
  const pts = [[8, 0], [-1, -4], [-8, 0], [-1, 4]];
  p.poly(pts.map(([x, y]) => [x * 1.18 + 0.4, y * 1.3]), c[0]);
  p.poly([[8, 0], [-1, -4], [-8, 0]], c[4]);
  p.poly([[8, 0], [-8, 0], [-1, 4]], c[2]);
  p.poly([[6, 0], [0, -2.5], [-3, 0]], c[5]);
  p.line(-7, 0, 7, 0, c[3]);
  // a glint runs down the facet once a loop
  const g = 7 - frac(ph) * 15;
  if (g > -6) { p.px(g, -1, c[6]); p.px(g - 1, -1, c[6]); p.px(g - 0.5, -2, c[5]); }
  p.glow(2, 0, 8, c[4], 0.3);
}

function thunder(p, ph, c, R) {
  // the arc pattern changes a few times a loop; draw each pattern from its own seed so frames agree
  const step = Math.floor(ph * 4), J = new Rng(31 + step * 17);
  // trailing sparks
  for (let i = 0; i < 6; i++) {
    const y = R.range(-3.5, 3.5), u = frac(ph * 2 + i / 6);
    p.dot(-5 - u * 22, y + J.range(-1, 1), u < 0.4 ? c[6] : c[4], 1 - u);
  }
  // jagged arcs: two crack back along the tail, two jump off the orb
  // a jagged arc: steps along its direction, each joint jittered sideways but never far off the line
  const bolt = (x0, y0, a, L, n, col) => {
    const ca = Math.cos(a), sa = Math.sin(a), pts = [[x0, y0]];
    for (let k = 1; k <= n; k++) {
      const d = (L * k) / n, j = k === n ? J.range(-0.8, 0.8) : J.range(-1.6, 1.6);
      pts.push([x0 + ca * d - sa * j, y0 + sa * d + ca * j]);
    }
    p.polyline(pts, col);
  };
  bolt(-4, -1, Math.PI + 0.12, 18, 5, c[0]);
  bolt(-4, -2, Math.PI + 0.12, 18, 5, c[5]);
  bolt(-4, 1, Math.PI - 0.12, 14, 4, c[4]);
  // short sparks crawl over the orb's rim
  for (let k = 0; k < 3; k++) {
    const a = J.range(0, TAU), x = Math.cos(a) * 4.6, y = Math.sin(a) * 4.6;
    bolt(x, y, a, 3.5, 2, k ? c[5] : c[6]);
  }
  // orb: ink rim, pulsing yellow body, white-hot core
  const pulse = 0.5 + 0.5 * Math.sin(TAU * ph * 2);
  p.circle(0, 0, 4.8, c[0]);
  p.circle(0, 0, 3.8, c[3]);
  p.circle(0.3, -0.3, 2.8 + pulse * 0.5, c[4]);
  p.circle(0.5, -0.5, 1.6, c[5]);
  p.circle(0.5, -0.5, 0.8, c[6]);
  p.glow(0, 0, 9, c[5], 0.3 + 0.2 * pulse);
}
