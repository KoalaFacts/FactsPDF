import { englishAudit as previous } from './audit-english-font-family.mjs';

// box-sizing is accepted on <html> and inline text, where no width or height applies, so the
// universal reset *{box-sizing:border-box} reaches every block. Only the css.box-sizing row's
// limitations and applicability strings change, in place; no count moves.
if (previous.length !== 842) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '885fb68832180584bdaad254b98b1e3fccbfce603e7b0d0064fad82d7b638b5f';
const entries = [...previous];
entries[228] = previous[228].replace('No table structural/inline box-sizing,',
  'No box-sizing on table structure (a universal *{box-sizing:border-box} reset still fails FPDF1201 on a page with a table); <html> and inline text (span/b/strong/i/em/small/u/s/del/ins/a) accept it without changing output, because width and height do not apply there, and a child\'s inherit reads their value. No');
entries[230] = previous[230].replace('Not table/col/row/cell boxes or inline.',
  '<html> and inline text accept it with no effect. Not table/col/row/cell boxes.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 842 || englishAudit[228] === previous[228] || englishAudit[230] === previous[230] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete box-sizing English audit.');
