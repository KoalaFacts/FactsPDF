import { englishAudit as previous } from './audit-english-overflow-fragment-initial-values.mjs';

// rotate, scale, translate and perspective at none, transform-style at flat and mask, mask-image,
// -webkit-mask, -webkit-mask-image and shape-outside at none join the initial-value
// no-ops. Only the CSS inventory note changes (256 → 266 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'f3cedf4bd8a51545914a42ea357de6b54a044d25c6ae849e312267678ad01ee0';
const changes = new Map([
  [779, previous[779] + ' rotate, scale, translate and perspective at none, transform-style at flat and mask, mask-image, -webkit-mask, -webkit-mask-image and shape-outside at none are initial values; like transform:none they move nothing, and no mask or float shape is ever applied, so they are validated and dropped while rotate:45deg, preserve-3d, url() and other values still fail FPDF1202; this took it from 256 to 266.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 256 to 266') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete transform-mask-initial-values English audit.');
