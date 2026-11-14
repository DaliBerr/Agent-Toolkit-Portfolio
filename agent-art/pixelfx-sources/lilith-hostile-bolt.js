// Lilith hostile bolts: enemy projectiles in flight. Same frame and pivot as lilith-bolt (44x22, origin at
// the head) so the bullet presenter can swap between them. Enemy shots read as red ink: an ink-black
// talisman arrow with a crimson glow, and a frost shard rimmed and haloed in red. Seamless loop.
import { Rng, fit } from "../../core/index.js";

export const meta = {
  name: "Lilith hostile bolt",
  desc: "An enemy shot flying right in red ink: an ink-black talisman arrow trailing crimson streaks, or a frost shard rimmed and haloed in red.",
  tags: ["projectile", "bolt", "enemy", "ink", "arrow", "ice", "loop", "directional"],
  size: [44, 22],
  origin: [32, 11],
  duration: 0.4,
  loop: true,
  fps: 20,
  bg: "dark",
  options: { kind: "arrow", palette: ["#5a0c12", "#9c1a1e", "#d8342c", "#ff6a4a", "#ffc0a0"], seed: 9 },
};

const TAU = Math.PI * 2;
const INK = fit(["#0b0809", "#2a1517", "#4a2226"], 3);
const RED_HEX = ["#5a0c12", "#9c1a1e", "#d8342c", "#ff6a4a", "#ffc0a0"];
let RED = fit(RED_HEX, 5);
const ICE = fit(["#123a5e", "#2a72b0", "#4ab0e6", "#8adcff", "#e6faff"], 5);
const frac = (v) => v - Math.floor(v);

export function draw(p, t, o) {
  RED = fit(o.palette || RED_HEX, 5);
  const ph = t / meta.duration, R = new Rng(o.seed);
  if (o.kind === "ice") ice(p, ph, R); else arrow(p, ph, R);
}

function arrow(p, ph, R) {
  // crimson streaks peel off behind the fletching
  for (let i = 0; i < 2; i++) {
    const y = i ? -1.6 : 1.6, L = 12 + 3 * Math.sin(TAU * (ph + i * 0.5));
    p.line(-12, y, -12 - L, y * 1.4, (u) => (u < 0.4 ? RED[3] : RED[1]), (u) => 1 - u);
  }
  for (let i = 0; i < 5; i++) {
    const y = R.range(-3, 3), u = frac(ph + i / 5);
    p.dot(-12 - u * 18, y, u < 0.4 ? RED[3] : RED[1], 1 - u);
  }
  // shaft with an ink rim
  p.rect(-13, -1.5, 18, 3, INK[0]);
  p.line(-12, 0, 4, 0, INK[2]);
  // fletching: two ink vanes with a red paper talisman tucked between
  p.poly([[-9, -1], [-14, -4.5], [-15, -1]], INK[0]);
  p.poly([[-9, 1], [-14, 4.5], [-15, 1]], INK[0]);
  p.rect(-11, -1, 4, 2, RED[2]);
  // broad head, red-hot edge, pulsing glow
  p.poly([[9, 0], [2, -4], [3, 0], [2, 4]], INK[0]);
  p.poly([[8, 0], [3, -2.6], [4, 0]], RED[2]);
  p.px(7, 0, RED[4]);
  p.glow(5, 0, 7, RED[2], 0.3 + 0.12 * Math.sin(TAU * ph));
}

function ice(p, ph, R) {
  // red-tinged frost wake
  for (let i = 0; i < 6; i++) {
    const y = R.range(-4, 4), u = frac(ph + i / 6);
    p.dot(-6 - u * 24, y * (0.6 + u * 0.5), u < 0.35 ? ICE[4] : RED[2], 1 - u);
  }
  p.line(-7, 2.4, -22, 3, (u) => (u < 0.4 ? ICE[3] : RED[1]), (u) => 1 - u);
  // the shard: a red rim instead of ink, so it reads as hostile
  const pts = [[8, 0], [-1, -4], [-8, 0], [-1, 4]];
  p.poly(pts.map(([x, y]) => [x * 1.2 + 0.4, y * 1.35]), RED[0]);
  p.poly([[8, 0], [-1, -4], [-8, 0]], ICE[3]);
  p.poly([[8, 0], [-8, 0], [-1, 4]], ICE[1]);
  p.poly([[6, 0], [0, -2.5], [-3, 0]], ICE[4]);
  p.line(-7, 0, 7, 0, ICE[2]);
  const g = 7 - frac(ph) * 15;
  if (g > -6) { p.px(g, -1, ICE[4]); p.px(g - 1, -1, ICE[4]); }
  p.glow(2, 0, 9, RED[2], 0.28);
}
