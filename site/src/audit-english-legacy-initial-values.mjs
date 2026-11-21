import { englishAudit as previous } from './audit-english-tab-size-color-adjust.mjs';

// zoom:normal (or 1/100%), contain:none, content-visibility:visible and auto for
// line-break, text-justify and image-rendering join the initial-value no-ops.
// Only the CSS inventory note changes (213 → 219 names); no feature row moves.
if (previous.length !== 783) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '77b3816d589381ed6d46599ddf848c511e7ade0e7811345b0f90fd19bcf327f2';
const changes = new Map([
  [779, previous[779] + ' zoom:normal (or 1/100%), contain:none, content-visibility:visible and auto for line-break, text-justify and image-rendering are initial values; the renderer scales, contains and skips nothing, and line breaking, justification and image interpolation are already its own choice, so they are validated and dropped while other values still fail FPDF1202; this took it from 213 to 219.'],
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 783 || [...changes.keys()].some(index => englishAudit[index] === previous[index]) ||
  !englishAudit[779].includes('from 213 to 219') ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete legacy-initial-values English audit.');
