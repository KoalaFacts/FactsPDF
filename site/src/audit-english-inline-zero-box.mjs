import { englishAudit as previous } from './audit-english-root-zero-inset.mjs';

// Inline text accepts a zero margin or padding and a transparent background (also
// background:none) with no effect, since it lays out no box of its own, so resets such as
// a{background-color:transparent} and *{margin:0;padding:0} on inline text render. Only the margin,
// padding and background rows' limitations strings change, each by one appended sentence; no
// count moves.
if (previous.length !== 842) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'e673185fd0d9cd3a9e813210672dfeb0955c070b44c699955e1aaaeaaf9762e0';
const hosts = 'inline text (span/b/strong/i/em/small/u/s/del/ins/a)';
const sentence = {
  margin: ` Exception: a zero margin on ${hosts} is accepted and changes no output; other values still fail FPDF1201.`,
  padding: ` Exception: a zero padding on ${hosts} is accepted and changes no output; other values still fail FPDF1201.`,
  background: ` Exception: a transparent background (including background:none) on ${hosts} is accepted and paints nothing; other values still fail FPDF1201.`,
};
const changed = { 179: 'margin', 195: 'padding', 202: 'background', 248: 'margin', 252: 'margin', 256: 'margin',
  260: 'margin', 262: 'padding', 264: 'padding', 276: 'background' };
const entries = [...previous];
for (const [index, kind] of Object.entries(changed)) entries[index] = previous[index] + sentence[kind];
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 842 || Object.keys(changed).some(i => englishAudit[i] === previous[i]) ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete inline zero box English audit.');
