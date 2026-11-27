import { englishAudit as previous } from './audit-english-background-color-adjust.mjs';

// overflow-wrap:anywhere is now accepted: CSS Text 3 makes it break exactly like break-word and
// differ only in min-content sizing, which FactsPDF never computes, so its PDF is byte-identical
// to break-word. The css.overflow-wrap row keeps its place; its scope (364), limitations (365) and
// evidence (368) strings are replaced in place (no count changes).
if (previous.length !== 874) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '6cf6e1d481a4f2864ddfd080e69fc7085e2d8df452a777c9320fd7e031891e4e';
const changes = new Map([
  [364, previous[364].replace('accept overflow-wrap:normal/break-word; the legacy word-wrap alias shares its cascade slot, values and applicability.',
    'accept overflow-wrap:normal/break-word/anywhere; the legacy word-wrap alias shares its cascade slot, values and applicability. Per CSS Text 3 anywhere breaks like break-word and differs only in min-content sizing, which FactsPDF never computes, so its PDF is byte-identical to overflow-wrap:break-word.')],
  [365, previous[365].replace('Only normal/break-word; words fitting on the next line move whole. No anywhere, RTL/complex graphemes,',
    'Only normal/break-word/anywhere; words fitting on the next line move whole. No RTL/complex graphemes,')],
  [368, previous[368].replace('and safe rejection;',
    'and safe rejection; anywhere PDF byte-identical to break-word in paragraphs, headings, inherited containers, list items, td/th and a multi-page table;')],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 874 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  /No anywhere/.test(englishAudit[365]) ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete overflow-wrap anywhere English audit.');
