import { englishAudit as fluent } from './audit-english-fluent-sdk.mjs';
import { englishAudit as lastLine } from './audit-english-last-line-alignment.mjs';
// Preserve the complete reviewed fluent-SDK catalogue and the reviewed
// last-line corrections without silently dropping either independent change.
if (fluent.length !== 772 || lastLine.length !== 772)
  throw new Error('Prior reviewed English catalogues changed.');
export const sourceDigest = 'b9469b953a406591f3580b00d8c8cc568740e3b64d3e2ba0d4555c9b78de109b';
const updatedLastLine = new Set([175, 176, 768]);
export const englishAudit = Object.freeze(fluent.map((value, index) =>
  index === 767
    ? value + ' On 5 November 2026, source 09a186b4e6f33355db3014ef0b0fcd61df224bc3 added bounded text-align-last PDF line geometry with a 44-record multipage report and independently authored line-position oracle. The original 418 feature IDs remain; exact integrated checks and review are separate.'
    : updatedLastLine.has(index) ? lastLine[index] : value));
if (englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[\u3400-\u9fff]/u.test(value)))
  throw new Error('Incomplete merged English catalogue.');
