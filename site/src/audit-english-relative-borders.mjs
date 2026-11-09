import { englishAudit as previous } from './audit-english-cell-text-flow.mjs';

// Reconcile four changed source strings from the bounded computed border-width
// expansion; preserve every other reviewed English translation and index.
if (previous.length !== 776) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '01c05c6425d5bfd2fd421c83881ef942ed76bf2ff1cc21bd55c716eb3877bbe4';
const changes = new Map([
  [203, previous[203] + ' Bounded nonnegative em/rem values on already-applicable border shorthands now resolve from the final element and HTML-root font sizes and change real PDF stroke widths, content boxes, and pagination. Physical and LTR logical sides share the computed point geometry; unsupported declarations still fail.'],
  [205, previous[205] + ' Existing physical border sides also accept bounded computed em/rem widths, using the own final font and HTML root respectively, for real PDF paint and page fragments, not just declaration recognition.'],
  [208, previous[208] + ' Existing physical and LTR logical border-width sides and 1–4 side shorthands also accept bounded nonnegative em/rem, computed from the own final font and HTML root, changing actual PDF content widths, border strokes and repeated table header geometry.'],
  [209, 'Only bounded nonnegative border widths are supported; no percentages, negative values or calc(). The keywords thin, medium and thick resolve to 0.75pt, 2.25pt and 3.75pt. Relative em/rem use the computed element or HTML-root font. Border declarations on paragraphs inside table cells are still rejected even if overridden; invalid lengths fail FPDF1202, unplaceable geometry fails FPDF1302 and prior PDF output remains intact.']
]);
export const englishAudit = Object.freeze(previous.map((value, index) => changes.get(index) ?? value));
if (englishAudit.length !== 776 || englishAudit.some(value =>
  typeof value !== 'string' || !value.trim() || /[\u3400-\u9fff]/u.test(value)))
  throw new Error('Incomplete font-relative border-width English audit.');
