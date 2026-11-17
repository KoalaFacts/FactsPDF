import { englishAudit as previous } from './audit-english-stale-value-rows.mjs';

// border-style: hidden is accepted on border edges as none: three changed
// source strings replace their predecessors in place.
if (previous.length !== 778) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'b87174a035bc3f32900ed086fd935e1d5eca277c06eb3839bd1cdd348cbceaed';
const changes = new Map([
  [211, 'solid / none / hidden / dashed / dotted; 1–4 value expansion; on borders hidden equals none (there are no collapsing table borders).'],
  [212, 'No double/groove/ridge/inset/outset; outline-style does not accept hidden; related declarations on p inside table cells are rejected even when overridden.'],
  [411, 'No double/groove/ridge/inset/outset (hidden equals none); strokes too thin or edges too short fail FPDF1302; rounded boxes still require solid borders.']
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 778 || englishAudit.some(value =>
  typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete hidden border-style English audit.');
