import { englishAudit as previous } from './audit-english-initial-value-no-ops.mjs';

// box-shadow, text-shadow, transform, filter and background-image join the
// initial-value-only rows at none: the shared scope and limitation strings are
// replaced in place and the CSS inventory note records 126 → 131 names.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '8aeffd3df292cd8d7c377016d9112c7c14287287f4871f938e8ca73c0a2d4835';
const changes = new Map([
  [220, previous[220].replace('opacity:1 or 100%, and inherit/initial/unset',
    'opacity:1 or 100%, box-shadow/text-shadow/transform/filter/background-image:none, and inherit/initial/unset')],
  [221, previous[221]
    .replace('stacking order or transparency:', 'stacking order, transparency, shadows, transforms, filters or background images:')
    .replace('opacity:0.5)', 'opacity:0.5, box-shadow:0 0 2pt #000, transform:rotate(90deg), background-image:url(…))')],
  [779, previous[779] + ' Five more names accepted only at none (box-shadow, text-shadow, transform, filter, background-image) took it from 126 to 131.']
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[221].includes('background-image:url') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete none-initial English audit.');
