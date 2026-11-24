import { englishAudit as previous } from './audit-english-inline-zero-box.mjs';

// A zero margin is the initial value on list items and on table rows, row groups, columns, column
// groups and cells, so *{margin:0} is accepted there with no effect. Only the margin rows' limitations strings change, each by one appended sentence;
// no count moves.
if (previous.length !== 842) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '24194881e4a80ce36794be78e216033c33a3d19f72ffc4dcc1c0301c34de854b';
const sentence = ' A zero margin on li and on table rows, row groups, columns, column groups and cells (tr/thead/tbody/col/colgroup/td/th) is the initial value and is accepted with no change to output; nonzero values still fail FPDF1201.';
const changed = [179, 248, 252, 256, 260];
const entries = [...previous];
for (const index of changed) entries[index] = previous[index] + sentence;
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 842 || changed.some(i => englishAudit[i] === previous[i]) ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete table part margin English audit.');
