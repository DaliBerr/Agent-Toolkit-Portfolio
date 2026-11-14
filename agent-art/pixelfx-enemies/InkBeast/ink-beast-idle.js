import { makeMeta, drawBeast } from './_ink-beast.js';
export const meta = makeMeta('idle');
export const draw = (p,t,o) => drawBeast(p,t,o,'idle');
