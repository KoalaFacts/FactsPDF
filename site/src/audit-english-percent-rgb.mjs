import { englishAudit as baseline } from './audit-english-hsl.mjs';

// Ten scoped RGB value/paint rows change; 418 stable feature IDs and the
// existing 83 accepted CSS property names are preserved. Source SHA-256
// covers every ordered Chinese audit string, not merely the visible summary.
if (baseline.length !== 750) throw new Error('Reconcile the HSL English source first.');
export const sourceDigest = '2f1d9f80399e2c2d60404e1a153cc66c902a04a013a066d065c5c31bcf37b6a7';
const addition = ' Three uniformly percentage-valued RGB channels are now supported in comma or modern space rgb(), and comma rgba(...,1). Values are 0–100% including decimal fractions; half-byte results round to nearest byte with ties up. This changes real PDF ink without changing layout, cascade or supported elements.';
const limitations = 'No translucent or transparent alpha, mixed integer/percentage RGB channels, fractional numeric RGB channels, slash alpha, nesting or colour mixing. HSL remains bounded to numeric/deg hue and percentage saturation/lightness, with no turn/rad or general CSS Color support.';
const evidence = 'Current source includes a 48-record multi-page report, independently specified hex PDF-byte oracle, managed API/CLI checks, Linux/Windows Native AOT qpdf/Poppler and protected-output recovery. Exact-head CI, separate independent code review, human PDF approval, software packaging and distribution require distinct gates.';
const changes = Object.freeze({
  157: baseline[157] + addition,
  158: limitations + ' Text currentColor and other full CSS Color values remain unsupported.',
  159: evidence,
  189: baseline[189] + addition,
  190: limitations + ' Existing background element applicability still applies; gradients remain unsupported.',
  191: baseline[191] + addition,
  192: limitations + ' Border shorthand still accepts only bounded width/style/single colour, not border images or double styles.',
  201: baseline[201] + addition,
  202: limitations + ' Border currentColor remains available; transparent border alpha does not.',
  253: baseline[253] + addition,
  254: limitations + ' Background shorthand remains one solid colour only: no images, position, repeat or multiple layers.',
  364: 'Opaque rgb() with all three integer channels 0–255 or all three percentage channels 0–100%, using comma or whitespace forms. Comma rgba(...,1) permits exactly opaque numeric alpha. Actual PDF paint, not parsing only.',
  365: limitations,
  367: evidence,
  745: baseline[745] + ' A subsequent 2026-11-04 source-only percent-RGB audit at 6a79f3f69f381f39a2cfb83bc2aa505e34c0af0d updates nine existing CSS colour properties and the existing RGB-value row, with real 48-record PDF-byte/oracle and native checks. The 418 IDs and 83 CSS property names are retained; this does not grant software distribution, customer usage, human PDF approval or public release.'
});
export const englishAudit = Object.freeze(baseline.map((text, index) =>
  Object.hasOwn(changes, index) ? changes[index] : text));
if (englishAudit.length !== 750 ||
    englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[\u3400-\u9fff]/u.test(value)))
  throw new Error('Incomplete percent-RGB English catalogue.');
