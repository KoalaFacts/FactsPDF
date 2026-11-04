import { englishAudit as baseline } from './audit-english-print-units.mjs';

// Only the existing css.font-weight scope, limitation, evidence and global
// audit-method description changed; preserve all other 745 translations.
if (baseline.length !== 749) throw new Error('Reconcile prior English audit catalogue first.');
export const sourceDigest = '5d5c30fcd8f9bb56253430142205001cab1437fe51a89f4c1bb323b64feb2bac';
const changes = Object.freeze({
  257: 'Existing normal/bold, numeric hundred-step 100–900 (100–500 normal and 600–900 bold), and bounded bolder/lighter. The latter select the supplied bold or normal role; at the endpoint they remain in that role. Inheritance, cascade and !important affect real PDF glyph metrics, wraps and pages.',
  258: 'Only explicitly supplied normal/bold (and italic combinations) font chains. Relative words do not synthesize intermediate weights or implement full nine-weight relative font matching. Non-hundred numeric weights, variable font axes and font-family matching are unsupported; FPDF1507 for a missing role, FPDF1504 for glyphs.',
  259: 'Existing 64-row numeric-weight report and byte control are retained. A new 48-row bolder/lighter report, explicit normal/bold oracle, NUnit cascade/font/pagination cases, Native AOT qpdf/Poppler and protected-output checks are source prepared. Final-head CI, independent review and human page approval require separate evidence.',
  744: baseline[744] + ' The 2026-11-04 bounded source-only bolder/lighter audit at e94987af9b07c2d7502c25b41625b85aa393f63f extends only the existing css.font-weight row with a real 48-entry paginated report and independently specified normal/bold role PDF oracle. All 418 IDs and 83 CSS names are retained; no package, human approval or licence is implied.'
});
export const englishAudit = Object.freeze(baseline.map((text, index) =>
  Object.hasOwn(changes, index) ? changes[index] : text));
if (englishAudit.some(x => typeof x !== 'string' || !x.trim() || /[\u3400-\u9fff]/u.test(x)))
  throw new Error('Invalid relative-weight English translations.');
