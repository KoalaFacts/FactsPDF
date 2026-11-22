import { englishAudit as previous } from './audit-english-typography-initial-values.mjs';

// background-attachment at scroll, background-blend-mode at normal, background-origin at
// padding-box, container-type at normal and container at none join the initial-value no-ops.
// Only the CSS inventory note changes (270 → 275 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '10a18365046543a97c05ba2dde1eb649a65ff14f7cb65f5226a1b9072b19d04f';
const changes = new Map([
  [779, previous[779] + ' background-attachment at scroll, background-blend-mode at normal, background-origin at padding-box, container-type at normal and container at none are initial values; the renderer never paints a background image and evaluates no container query, so they are validated and dropped while fixed, multiply, content-box, inline-size and other values still fail FPDF1202; this took it from 270 to 275.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 270 to 275') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete background-container-initial-values English audit.');
