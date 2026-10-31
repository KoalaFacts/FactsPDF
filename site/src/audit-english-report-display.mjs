import { englishAudit as prior } from './audit-english-logical-side-longhands.mjs';
// Reuse unchanged approved translations; bind every display change to the
// actual audited Chinese-source order rather than guessing from a count.
if (prior.length !== 694) throw new Error('Reconcile previous English audit first.');
export const sourceDigest = '77db520519cdb14cc1a8020c0214b8362390c18cb73e38d8619e9e28aced2bec';
const spans = [[0,0,195],[200,195,1],[201,198,1],[202,197,1],[203,199,491],[695,691,3]];
const additions = Object.freeze({
  "195": "Ordinary report p/h1–h6 and div/section/article/blockquote blocks outside lists/tables may use display:none to remove their entire block subtree, or display:block to keep existing block flow. Stylesheet and inline declarations follow specificity, !important and source order. Hidden text, borders, backgrounds, margins and pagination space do not appear in the final PDF.",
  "196": "Only display:none or display:block. CSS-wide inherit/initial/unset, inline/flex/grid/contents and all other display values reject. No body/html, span, image, hr, list or table declarations. Hidden markup still undergoes strict HTML/CSS parsing, resource checks and budgets; hiding cannot bypass validation. No hidden attribute, media queries or JavaScript display.",
  "197": "CSS parsing, cascade, document-tree suppression and PDF layout",
  "198": "Standalone ordinary report paragraphs/headings, div/section/article panels and blockquote, excluding list/table descendants",
  "199": "Source commit includes NUnit real HTML-to-PDF byte parity, a paginated 40-entry report, removed paint/spacing, cascade, CLI/API parity and protected prior-PDF recovery. Exact-head CI, independent review and release remain separate."
});
const translated = Array(698);
for (const [start,previous,count] of spans)
  for(let i=0;i<count;i++) translated[start+i] = prior[previous+i];
for (const [position,translation] of Object.entries(additions))
  translated[Number(position)] = translation;
translated[694] = prior[690].replace('64 CSS property names','65 CSS property names') + ' Scoped display:block/none now applies only to ordinary non-list/non-table report blocks. A hidden block disappears before PDF font/layout/paint, while all hidden input and resources remain strictly validated; all other display values and elements fail explicitly.';
if (translated.some(value => typeof value !== 'string' || !value.trim()))
  throw new Error('Missing report display translation.');
export const englishAudit = Object.freeze(translated);
