import { englishAudit as previous } from './audit-english-box-sizing-tables.mjs';

// <html> and <body> accept a zero margin, and <html> a zero padding, with no effect (the page
// margin options frame the content), so resets such as html,body{margin:0;padding:0} render.
// Only the margin rows' and the padding rows' limitations strings change, in place; no count moves.
if (previous.length !== 842) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'b2c553d03a6aea68392e2451c8f182df3a58a9b55e2f4f4bc3d527fe163ae510';
const rootZero = '<html> and <body> accept only a zero margin, which changes no output (the page margin options frame the content); a nonzero value still fails FPDF1201.';
const entries = [...previous];
entries[179] = previous[179].replace('Body, image, inline and table box margins remain excluded;',
  `${rootZero} List, image, inline and table box margins remain excluded;`);
entries[195] = `${previous[195]} <html> accepts only a zero padding, which changes no output; a nonzero value still fails FPDF1201.`;
entries[248] = previous[248].replace('Body, image and inline margins are excluded;',
  `${rootZero} List, table, image and inline margins are excluded;`);
entries[252] = previous[252].replace('Body, list, table, image and inline boxes are excluded.',
  '<html> and <body> accept only zero, which changes no output; list, table, image and inline boxes are excluded.');
entries[256] = previous[256].replace('Body/list/table/image/inline margins',
  'Apart from a zero margin on <html> or <body>, body/list/table/image/inline margins');
entries[260] = previous[260].replace('RTL/vertical writing, body, image, table structure and inline margins excluded.',
  'RTL/vertical writing, image, table structure and inline margins excluded; body and html accept only a zero margin, with no effect.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 842 || [179, 195, 248, 252, 256, 260].some(i => englishAudit[i] === previous[i]) ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete root zero inset English audit.');
