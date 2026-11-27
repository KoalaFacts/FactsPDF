import { englishAudit as previous } from './audit-english-table-part-display.mjs';

// The non-standard background-color-adjust becomes an alias of print-color-adjust like
// color-adjust. Only the CSS inventory note (index 870) changes (383 → 384 names); no feature
// row moves and no string is added.
if (previous.length !== 874) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '5515dd2b34a8d714e74aafc3b447c2c171ef0233fb6686f539a6df51bf2de7f7';
const changes = new Map([
  [870, previous[870].replace(/\.$/, '') + '; the non-standard background-color-adjust is an alias of print-color-adjust like color-adjust, so exact or economy is validated and dropped because backgrounds and colours are always painted as declared, while other values still fail FPDF1202 and -webkit-background-color-adjust still fails FPDF1201; this took it from 383 to 384.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 874 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[870].includes('from 383 to 384') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete background-color-adjust English audit.');
