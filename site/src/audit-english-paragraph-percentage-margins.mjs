import { englishAudit as previous } from './audit-english-inherited-text-flow.mjs';
// Source-bound delta for four existing partial CSS margin records.
if (previous.length !== 774) throw new Error('Previous English audit catalogue changed.');
export const sourceDigest = 'c23e0fe4ecc20069d79159bfe9749e04e2f0fef5579f8dbbde50cdd8b988ed92';
const updates = new Map([
  [178, "Ordinary p/pre/headings outside lists/tables now accept nonnegative percentage top/bottom margins resolved from containing content width, with margin shorthand, LTR margin-block, important cascade and explicit panel inheritance. Ordinary div/section/article/blockquote report panels and standalone hr preserve actual outside the box PDF paint and pagination, with physical/rem/em lengths."],
  [179, "Direct paragraph percentages in lists/tables reject FPDF1201; negative values, vertical auto, calc and margin collapse remain unsupported. Body, image, inline and table box margins remain excluded; unfit PDF geometry FPDF1302 preserves previous output."],
  [243, "Bounded 1–4-value margin shorthand for ordinary p/pre/headings and div/section/article/blockquote panels now accepts nonnegative vertical percentages based on containing content width, including explicit inherit, actual PDF text, paint and pagination. margin:0 auto centres panels; margin:12pt auto 7pt and margin:5% auto 3% give real outside the box spacing. Physical/rem/em, horizontal percentage/auto and LTR margin-block remain supported."],
  [244, "Direct list/table paragraph percentages reject FPDF1201; negative lengths, vertical auto, calc and margin collapse remain unsupported. Body, image and inline margins are excluded; table-cell p horizontal margins still require explicit zero and reject rem/em. Impossible geometry FPDF1302 preserves existing PDF."],
  [255, "LTR margin-block and margin-block-start/margin-block-end map to physical outside top/bottom spacing for ordinary p/pre/headings, div/section/article/blockquote panels and hr. Nonnegative percentages resolve against containing content width and affect real PDF text, paint, multi-page fragments and pagination. Existing physical/rem/em, CSS-wide inherit and important cascade remain in effect; no RTL layout."],
  [256, "List/table paragraph percentages fail FPDF1201; vertical auto, negative values, calc and margin collapse remain unsupported. RTL/vertical writing, body, image, table structure and inline margins excluded. FPDF1201/1202/1302 errors preserve previous PDF; not full CSS Logical Properties."]
]);
export const englishAudit = Object.freeze(previous.map((text,index) => updates.get(index) ?? text));
if (englishAudit.length !== 774 || englishAudit.some(text =>
  typeof text !== 'string' || !text.trim() || /[\u3400-\u9fff]/u.test(text)))
  throw new Error('Incomplete paragraph percent margin translations.');
