import { englishAudit as previous } from './audit-english-webkit-text-decoration.mjs';

// font-variant and its -caps, -east-asian, -alternates and -position longhands join the
// initial-value no-ops at normal. Only the CSS inventory note changes (228 → 233 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'd0abe019aecdacb27bcbee4ee43fba3ec8709140c01e347c45526f11112e74b7';
const changes = new Map([
  [779, previous[779] + ' font-variant and its -caps, -east-asian, -alternates and -position longhands at normal are initial values; the renderer substitutes no small caps, East Asian forms, alternates or sub/superscript glyphs, so they are validated and dropped while other values still fail FPDF1202; this took it from 228 to 233.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 228 to 233') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete font-variant-initial-values English audit.');
