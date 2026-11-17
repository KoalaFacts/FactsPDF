import { englishAudit as previous } from './audit-english-word-wrap-alias.mjs';

// HSL hue accepts deg, grad, rad and turn within ±3600 degrees. The shared
// colour limitation strings are replaced in place; no string is added or removed.
if (previous.length !== 779) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'bd266d77766aea14a054d67d24c640943b2b6709ada2e16f351ece9d56a7f5ce';
const units = value => value
  .replace('numeric/deg hue', 'numeric or deg/grad/rad/turn hue within ±3600 degrees')
  .replace('with no turn/rad or general CSS Color support', 'with no general CSS Color support')
  .replace('number/degree hue', 'number or deg/grad/rad/turn hue within ±3600 degrees');
const changed = [170, 204, 214, 270, 387];
export const englishAudit = Object.freeze(previous.map((value, index) => changed.includes(index) ? units(value) : value));
if (englishAudit.length !== 779 || changed.some(index => englishAudit[index] === previous[index]) ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value) || /turn\/rad or/u.test(value)))
  throw new Error('Incomplete HSL hue unit English audit.');
