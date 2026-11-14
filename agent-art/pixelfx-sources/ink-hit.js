// Lilith ink hit: a projectile strikes a body. Flash star and spark streaks adapt the shape language of
// pixelfx impacts/hit-spark (patch-gg/pixelfx, MIT; Sprite Fusion impact, Copyright (c) 2026 Hugo Duprez).
// Facing right = direction of the blow. Origin = impact point (mid-body, not the floor).
// part: 'all' previews both layers; 'ink' is the dark splash (never tinted in Unity);
// 'accent' is a grayscale layer that Unity tints with the attack's core color or faction color.
import { Rng, fit, ease, clamp, smooth, WHITE } from "../../core/index.js";

export const meta = {
  name: "Ink hit",
  desc: "A projectile bursts on a body: a pale four-point flash, an ink blot that spits droplets along the blow and spatters, a few accent streaks; gone in 0.4 s.",
  tags: ["impact", "hit", "ink", "combat", "directional", "one-shot"],
  size: [72, 56],
  origin: [26, 30],
  duration: 0.42,
  loop: false,
  fps: 30,
  bg: "dark",
  options: {
    palette: ["#101417", "#1b2225", "#303b3b", "#7e8a86", "#b9c2b4", "#dfe3d2", "#f4f1e2"],
    part: "all",
    scale: 1,
    seed: 7,
  },
};

const GRAY = ["#6f6f6f", "#a6a6a6", "#d6d6d6", "#ffffff"];

export function draw(p, t, o) {
  const pal = fit(o.palette, 7);
  const ink = [pal[0], pal[1], pal[2]];
  const acc = o.part === "accent" ? fit(GRAY, 4) : [pal[3], pal[4], pal[5], pal[6]];
  const showInk = o.part !== "accent", showAcc = o.part !== "ink";
  const S = o.scale || 1, R = new Rng(o.seed);

  // per-hit variation, drawn once from the seed so every frame agrees
  const drops = Array.from({ length: 9 }, (_, i) => ({
    a: R.range(-0.55, 0.55) + (i < 2 ? R.range(-2.6, 2.6) : 0), // two strays fly off sideways
    v: R.range(16, 30) * (i < 2 ? 0.6 : 1),
    r: R.range(1.2, 2.6),
    d: R.range(0, 0.04),
  }));
  const specks = Array.from({ length: 10 }, () => ({ a: R.range(-1.1, 1.1), v: R.range(9, 20), life: R.range(0.22, 0.38) }));
  const streaks = Array.from({ length: 5 }, (_, i) => ({ a: -0.8 + i * 0.4 + R.range(-0.12, 0.12), v: R.range(20, 32), L: R.range(4, 7) }));
  const tilt = R.range(-0.12, 0.12);
  const spikes = Array.from({ length: 7 }, (_, i) => {
    const fwd = i < 4; // four along the blow, three short ones around
    return { a: fwd ? R.range(-0.6, 0.6) : R.range(1.6, 4.7), L: fwd ? R.range(7, 12) : R.range(3, 5), w: fwd ? R.range(0.9, 1.4) : 0.8 };
  });

  // 1) the flash: two frames of a pale lopsided star, longest ray along the blow
  if (showAcc && t < 0.07) {
    const k = t < 0.035 ? 1 : 0.55;
    const rays = [[tilt, 13], [tilt + Math.PI, 5], [tilt - Math.PI / 2, 8], [tilt + Math.PI / 2, 6]];
    for (const [a, L] of rays) {
      const len = L * S * k;
      for (let d = 0; d <= len; d += 0.5) {
        const u = d / len, w = (t < 0.035 ? 1.6 : 0.8) * (1 - u);
        const c = u < 0.35 ? acc[3] : u < 0.7 ? acc[2] : acc[1];
        p.px(Math.cos(a) * d, Math.sin(a) * d, c);
        if (w > 0.5) { p.px(Math.cos(a) * d - Math.sin(a), Math.sin(a) * d + Math.cos(a), acc[2]); }
      }
    }
    if (t < 0.035) p.circle(0, 0, 2 * S, acc[3]);
  }

  // 2) the splat: a small core with tapered spikes, the long ones along the blow; it bursts out
  //    in three frames, holds, then breaks up from the tips inward and dithers away
  if (showInk) {
    const grow = ease.outExpo(clamp(t / 0.07));
    const shrink = 1 - smooth(0.09, 0.24, t); // the splat soaks away by shrinking, not by checkerboard
    if (shrink > 0.08) {
      const core = (1.5 + 2.5 * grow) * S * (0.4 + 0.6 * shrink);
      for (const sp of spikes) {
        // a tapered wedge from the core out to a round bead, like a flicked drop of ink
        const L = sp.L * S * grow * shrink, ca = Math.cos(sp.a), sa = Math.sin(sp.a);
        const bw = sp.w * S * 1.6, nx = -sa * bw, ny = ca * bw;
        p.poly([[nx, ny], [ca * L, sa * L], [-nx, -ny]], ink[0]);
        if (L > 3) p.circle(ca * (L + 0.5), sa * (L + 0.5), Math.max(0.6, sp.w * S * 0.8 * shrink), ink[0]);
      }
      p.circle(0, 0, core, ink[0]);
      p.circle(0.6, 0.6, core * 0.7, ink[1]); // body of the blot, lit from top-left
      if (t > 0.03 && core > 2) p.px(-1, -1, ink[2]); // a wet glint
    }
  }

  // 2b) the attack's color clings to the splat's leading edge for a few frames
  if (showAcc && t > 0.03 && t < 0.16) {
    const grow = ease.outExpo(clamp(t / 0.07)), r = (1.5 + 2.5 * grow) * S + 1, k = 1 - smooth(0.08, 0.16, t);
    p.arc(0, 0, r, 1, -1.0, 1.0, acc[t < 0.08 ? 2 : 1], k);
  }

  // 3) droplets thrown along the blow: fast out, drag, shrink, each with a lit lip in the accent
  for (const d of drops) {
    const tt = t - d.d; if (tt <= 0) continue;
    const u = clamp(tt / 0.34), dist = d.v * S * ease.outQuart(u);
    const x = Math.cos(d.a) * dist + 2, y = Math.sin(d.a) * dist + 10 * u * u; // slight sag
    const r = d.r * S * (1 - u * 0.85);
    if (r < 0.5) continue;
    const alpha = 1 - smooth(0.6, 1, u);
    if (showInk) { p.circle(x, y, r, ink[0], alpha); if (r > 1.2) p.px(x - 0.5, y - 0.5, ink[2]); }
    if (showAcc && u < 0.7) p.dot(x + Math.cos(d.a) * r, y + Math.sin(d.a) * r - 0.5, acc[u < 0.3 ? 2 : 1], alpha);
  }

  // 4) fine spatter that lands round the hit and dithers out
  if (showInk) for (const s of specks) {
    const u = clamp(t / s.life); if (u >= 1) continue;
    const dist = s.v * S * ease.outQuart(Math.min(1, u * 2));
    p.dot(Math.cos(s.a) * dist + 3, Math.sin(s.a) * dist, ink[2], 1 - u);
  }

  // 5) accent streaks: short, fast, the color of the attack
  if (showAcc && t > 0.02 && t < 0.2) {
    for (const s of streaks) {
      const u = clamp((t - 0.02) / 0.18), d0 = s.v * S * ease.outExpo(u), L = s.L * S * (1 - u);
      if (L < 1) continue;
      const ca = Math.cos(s.a), sa = Math.sin(s.a);
      p.line(ca * d0, sa * d0, ca * (d0 - L), sa * (d0 - L), (q) => (q < 0.4 ? acc[3] : acc[1]));
    }
  }
}
