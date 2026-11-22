import { englishAudit as previous } from './audit-english-background-container-initial-values.mjs';

// aspect-ratio at auto and the mask longhands at their initial values (mask-repeat repeat,
// mask-size auto, mask-origin and mask-clip border-box, mask-mode match-source, mask-composite add,
// mask-type luminance) join the initial-value no-ops. Only the CSS inventory note changes
// (275 → 283 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '75a0de7219842fe092062e6a8791953235f415ea22fd89faf6a02c6b76963c42';
const changes = new Map([
  [779, previous[779] + ' aspect-ratio at auto, mask-repeat at repeat, mask-size at auto, mask-origin and mask-clip at border-box, mask-mode at match-source, mask-composite at add and mask-type at luminance are initial values; images always keep their pixel aspect ratio, other boxes have no preferred ratio and no mask is ever applied, so they are validated and dropped while 16 / 9, no-repeat, cover, alpha and other values still fail FPDF1202; this took it from 275 to 283.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 275 to 283') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete aspect-mask-initial-values English audit.');
