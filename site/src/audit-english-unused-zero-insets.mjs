import { englishAudit as previous } from './audit-english-table-part-margins.mjs';

// ul, ol and table lay out with a zero margin, rows, row groups, columns and column groups take no
// padding, and br lays out no box, so the rest of *{margin:0;padding:0} is accepted there with no
// effect. Only the margin and padding rows' limitations strings change, each by one appended
// sentence; no count moves.
if (previous.length !== 842) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'c1f65d6280d6532da3dc2faff0646489c68cb15c4de826bbc302d1380ae168f4';
const marginSentence = ' A zero margin on ul, ol, table and br is the initial value and is accepted with no change to output; nonzero values and inherit still fail FPDF1201.';
const paddingSentence = ' A zero padding on table rows, row groups, columns and column groups (tr/thead/tbody/tfoot/col/colgroup) and on br is accepted with no change to output; nonzero values and inherit still fail FPDF1201.';
const margins = [179, 248, 252, 256, 260];
const paddings = [195, 262, 264];
const entries = [...previous];
for (const index of margins) entries[index] = previous[index] + marginSentence;
for (const index of paddings) entries[index] = previous[index] + paddingSentence;
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 842 || [...margins, ...paddings].some(i => englishAudit[i] === previous[i]) ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete unused zero inset English audit.');
