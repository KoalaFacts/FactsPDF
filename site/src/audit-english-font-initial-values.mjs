import { englishAudit as previous } from './audit-english-screen-gesture-hints.mjs';

// font-feature-settings (and its -webkit-/-moz- aliases), font-variation-settings,
// font-variant-ligatures, font-variant-numeric, font-stretch, font-optical-sizing and
// font-size-adjust join the initial-value no-ops at normal/auto/none.
// Only the CSS inventory note changes (201 → 210 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '1828ab9646e58199eb5cae79025b884adf88f39c6929a7aafa2b437573adde7f';
const changes = new Map([
  [779, previous[779] + ' font-feature-settings (with its -webkit-/-moz- aliases), font-variation-settings, font-variant-ligatures, font-variant-numeric and font-stretch at normal, font-optical-sizing:auto and font-size-adjust:none are initial values; the renderer applies no OpenType features, variation axes, optical sizing, aspect adjustment or width selection beyond the font as given, so they are validated and dropped while other values still fail FPDF1202; this took it from 201 to 210.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 201 to 210') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete font-initial-values English audit.');
