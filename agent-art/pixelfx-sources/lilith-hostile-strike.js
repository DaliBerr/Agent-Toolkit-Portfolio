// Lilith hostile strikes: what an enemy melee or dash looks like at the moment it lands. Faces right;
// origin = the attacker's feet. 'slash' is a crimson-edged ink crescent swept across the front;
// 'dash' is an ink streak with speed lines laid along the path. Unity scales both to the attack's reach.
import { Rng, fit, ease, clamp, smooth } from "../../core/index.js";

export const meta = {
  name: "Lilith hostile strike",
  desc: "An enemy blow in red ink, facing right: a crescent swept across the front, or a streak of speed lines along a dash; gone in 0.3 s.",
  tags: ["melee", "slash", "dash", "enemy", "ink", "one-shot", "directional"],
  size: [80, 104],
  origin: [12, 52],
  duration: 0.3,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { kind: "slash", palette: ["#5a0c12", "#9c1a1e", "#d8342c", "#ff6a4a", "#ffc0a0"], seed: 3 },
};

const INK = fit(["#0b0809", "#2a1517", "#4a2226"], 3);
const RED_HEX = ["#5a0c12", "#9c1a1e", "#d8342c", "#ff6a4a", "#ffc0a0"];
let RED = fit(RED_HEX, 5);

export function draw(p, t, o) {
  RED = fit(o.palette || RED_HEX, 5);
  if (o.kind === "dash") dash(p, t, new Rng(o.seed)); else slash(p, t, new Rng(o.seed));
}

// reach of both strikes in pixels from the origin; Unity divides the attack range by this
const REACH = 60;

function slash(p, t) {
  // the blade sweeps from upper to lower front in 0.08 s, then the crescent thins and soaks away
  const sweep = ease.outExpo(clamp(t / 0.08));
  const fade = 1 - smooth(0.1, 0.3, t);
  if (fade <= 0.02) return;
  const a0 = -1.05, a1 = a0 + 2.1 * sweep, R = REACH * 0.78;
  const thick = (u) => 9 * Math.sin(Math.PI * u) * fade + 0.5;
  p.arc(4, 0, R + 2, (u) => thick(u) + 2, a0, a1, INK[0]);
  p.arc(4, 0, R, (u) => thick(u), a0, a1, RED[0]);
  p.arc(4, 0, R + 1.5, (u) => Math.max(0.8, thick(u) * 0.5), a0, a1, (u) => (u > 0.7 ? RED[4] : RED[2]));
  // the leading tip flares while the sweep is still moving
  if (t < 0.1) {
    const tx = 4 + Math.cos(a1) * R, ty = Math.sin(a1) * R;
    p.circle(tx, ty, 2.5 * (1 - t / 0.1) + 0.6, RED[3]);
    p.glow(tx, ty, 8, RED[2], 0.5);
  }
  // ink droplets flicked off the end of the stroke
  if (t > 0.06) {
    const u = clamp((t - 0.06) / 0.2);
    for (let i = 0; i < 5; i++) {
      const a = a1 - 0.2 + i * 0.12, d = R + 4 + u * (8 + i * 2);
      p.circle(4 + Math.cos(a) * d, Math.sin(a) * d + u * u * 4, Math.max(0.5, 1.6 * (1 - u)), INK[0], 1 - u);
    }
  }
}

function dash(p, t, R) {
  // a ragged ink streak along the path, lit red along its upper edge, and speed lines that race ahead
  const grow = ease.outQuart(clamp(t / 0.06));
  const fade = 1 - smooth(0.08, 0.3, t);
  if (fade <= 0.02) return;
  const L = REACH * grow;
  for (let x = 0; x < L; x += 0.5) {
    const u = x / REACH, w = (2 + 4 * Math.sin(Math.PI * Math.min(1, u * 1.1))) * fade;
    if (w < 0.5) continue;
    p.circle(x, 0, w + 1, INK[0]);
    p.circle(x, 0.5, w, INK[1]);
    p.px(x, -w, u > 0.6 ? RED[3] : RED[2]);
    p.px(x, -w + 1, RED[1]);
  }
  for (let i = 0; i < 6; i++) {
    const y = R.range(-12, 12), len = R.range(8, 18), x0 = R.range(0, REACH * 0.6) + t * 120;
    if (x0 > REACH + 8) continue;
    p.line(x0, y, x0 + len * fade, y, (u) => (u > 0.6 ? RED[3] : INK[2]), fade);
  }
}
