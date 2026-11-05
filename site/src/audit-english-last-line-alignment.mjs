import { englishAudit as previous } from './audit-english-cli-location.mjs';
// Reuse every stable translation; only four existing source strings gained bounded alignment detail.
if (previous.length !== 772) throw new Error('Prior reviewed English catalogue changed.');
export const sourceDigest = '2c6de376907522ca551503681b6832d573f43b1ab86a13c3ce0591c63c59b256';
const updates = new Map([
  [175, previous[175] + ' The bounded independent text-align-last:auto/left/center/right/start/end/justify property positions the actual final and forced-break PDF lines, including measured U+0020 justification in paragraph, heading and fixed-column flat-cell text.'],
  [176, previous[176] + ' text-align-last rejects RTL, vertical writing, inline declarations and unknown values; auto preserves previous bytes and unsupported input fails without replacing the PDF.'],
  [767, previous[767] + ' On 5 November 2026, source 61da678fbfcada09bf9ae5ee10eee414d1879c27 added bounded text-align-last PDF line geometry with 44-entry multipage source and independently authored manual-line oracle. Only the existing css.text-align audit record is updated; all 418 feature IDs remain, and fresh checks and review are separately recorded.'],
  [768, previous[768] + ' On 5 November 2026, bounded text-align-last added one CSS property spelling (115 to 116), with final/forced line alignment in printable report text and flat fixed-column cells, not full CSS Text.']
]);
export const englishAudit = Object.freeze(previous.map((entry, i) => updates.get(i) ?? entry));
if (englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[\u3400-\u9fff]/u.test(value)))
  throw new Error('Incomplete English last-line alignment translation audit.');
