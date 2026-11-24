import { englishAudit as previous } from './audit-english-box-sizing-inline.mjs';

// box-sizing:border-box on a <table> makes its explicit width include padding and borders, and
// columns, groups, rows, cells and cell paragraphs accept box-sizing with no effect, so universal
// resets pass on pages with tables. Only the css.box-sizing limitations and applicability strings
// change, in place; no count moves.
if (previous.length !== 842) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'f17a951e34e6bc5d80db8bd2cdbe8991a7564c77a2e129253f69f0bb1e9e1695';
const entries = [...previous];
entries[228] = previous[228].replace(
  'No box-sizing on table structure (a universal *{box-sizing:border-box} reset still fails FPDF1201 on a page with a table);',
  'On a table, border-box makes an explicit width include its padding and borders; col/colgroup/thead/tbody/tfoot/tr/td/th and cell paragraphs accept box-sizing without changing output (none takes a width with padding or borders there), so a universal *{box-sizing:border-box} reset also passes on pages with tables;');
entries[230] = previous[230].replace('<html> and inline text accept it with no effect. Not table/col/row/cell boxes.',
  '<html>, inline text and table structure (col/row/cell boxes and cell paragraphs) accept it with no effect; the table box width supports border-box.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 842 || englishAudit[228] === previous[228] || englishAudit[230] === previous[230] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete box-sizing table English audit.');
