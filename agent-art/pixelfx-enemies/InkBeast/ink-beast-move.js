import { makeMeta, drawBeast } from './_ink-beast.js';
export const meta = makeMeta('move');
export const draw = (p,t,o) => drawBeast(p,t,o,'move');
