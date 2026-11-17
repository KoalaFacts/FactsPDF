import { englishAudit as previous } from './audit-english-opaque-alpha-hex.mjs';

// CSS Color 4 opaque syntax: rgba()/hsla() aliases and slash alpha exactly 1 or
// 100%. value.color.rgbfunc scope/limitations and the shared colour limitation
// strings are replaced in place; no string is added or removed.
if (previous.length !== 779) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'cb21580d9aed91ede30fdc16917cd032af33f1249bacf9aaf0f4864248ac0702';
const slash = value => value.replace('slash alpha, nesting or colour mixing.',
  'nesting or colour mixing; comma or slash alpha must be exactly 1 or 100%.');
const changes = new Map([
  [170, previous[170].replace('nested expressions, slash alpha,', 'nested expressions, translucent comma or slash alpha,')],
  [204, slash(previous[204])],
  [214, slash(previous[214])],
  [270, slash(previous[270])],
  [386, 'Opaque rgb()/rgba() (aliases) with all three integer channels 0–255 or all three percentage channels 0–100%, in comma or whitespace form. The comma form may add a fourth alpha and the whitespace form a slash alpha (rgb(35 68 107 / 1)); alpha must be exactly 1 or 100%. Actual PDF paint, not parsing only.'],
  [387, 'No translucent or transparent alpha (anything but exactly 1 or 100%), mixed integer/percentage RGB channels, fractional numeric RGB channels, nesting or colour mixing. HSL remains bounded to numeric/deg hue and percentage saturation/lightness, with no turn/rad or general CSS Color support; it accepts the same hsla() alias and slash alpha.']
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 779 || [170, 204, 214, 270].some(index => englishAudit[index] === previous[index]) ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete opaque slash alpha English audit.');
