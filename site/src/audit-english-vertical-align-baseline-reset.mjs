import { englishAudit as previous } from './audit-english-writing-direction-initial-values.mjs';

// vertical-align:baseline (the initial value) is validated and dropped outside td/th cells and
// still fails FPDF1202 on cells. Only the vertical-align limitations change; the accepted
// property-name count stays at 243.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '23a7b55ca523b9cb1a67361be475c0fcbf630a1a437343084b970e957fd33996';
const changes = new Map([
  [304, 'Not general CSS vertical-align. Outside td/th the initial value baseline changes no layout, so it is validated and dropped (PDF bytes equal the undeclared page) and any other value fails FPDF1201; on td/th, baseline/sub/super, lengths and percentages fail FPDF1202. middle/bottom on a body row requiring independently fragmented cells fails FPDF1302 without overwriting the original PDF. No rowspan or inline baseline alignment.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[304].includes('initial value baseline') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete vertical-align-baseline-reset English audit.');
