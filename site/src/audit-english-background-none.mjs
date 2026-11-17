import { englishAudit as previous } from './audit-english-opaque-slash-alpha.mjs';

// css.background: a single "none" resets the fill like transparent. Its scope
// string is replaced in place; no string is added or removed.
if (previous.length !== 779) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '64d834d5928bca38e2ccc4f39905a5082994dd50d27dfe0940df337603603b9f';
const updated = previous[269].replace('and CSS-wide values.',
  'and CSS-wide values, plus a single none (any case), a reset equal to transparent; background-color:none still fails.');
export const englishAudit = Object.freeze(previous.map((value, index) => index === 269 ? updated : value));
if (englishAudit.length !== 779 || updated === previous[269] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete background none English audit.');
