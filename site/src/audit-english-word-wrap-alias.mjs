import { englishAudit as previous } from './audit-english-background-none.mjs';

// css.overflow-wrap gains the legacy word-wrap alias (scope and limitations),
// and the CSS inventory note records 116 → 117 property names. All three
// strings are replaced in place; no string is added or removed.
if (previous.length !== 779) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '78cb78b1d5185bedd71a5018ef37e8da8dd7069988175c0268495f4b3d76a44d';
const changes = new Map([
  [312, previous[312].replace('accept overflow-wrap:normal/break-word.',
    'accept overflow-wrap:normal/break-word; the legacy word-wrap alias shares its cascade slot, values and applicability.')],
  [313, previous[313].replace('No anywhere, word-wrap alias, RTL', 'No anywhere, RTL')],
  [775, previous[775] + ' On 17 November 2026, the legacy word-wrap alias of overflow-wrap added one CSS property spelling (116 to 117).']
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 779 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete word-wrap alias English audit.');
