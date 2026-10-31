import { englishAudit as prior } from './audit-english-report-display.mjs';
// Exact ordered source-text snapshot. All previous reviewed translations survive unchanged.
if (prior.length !== 698) throw new Error('Reconcile prior English catalogue.');
export const sourceDigest = "5ba93cc80bcb0e8d14fd0ad8ea4a6d03d684208dcf34d9c6ba8261970e95cb20";
const spans = [[0,0,53],[55,55,2],[58,58,213],[275,271,423],[699,695,3]];
const additions = {
  "53": {
    "text": "Decimal ol and vector-disc ul retain two-level nesting. Bounded list-style-type adds alphabetic/Roman/none choices with real PDF marker gutters, continuation indentation and a marker printed once across pages."
  },
  "54": {
    "text": "No HTML start/reversed/type/value, CSS counters, list-style shorthand, image markers or further list CSS; Roman numbering beyond 3999 rejects. No whole-item keep, widows or orphans."
  },
  "57": {
    "text": "New source-rendered PDF marker regressions, real pagination and a 74-entry report with independent expected labels. Exact-head CI, separate review and human approval are still distinct."
  },
  "271": {
    "text": "Only ol, ul, li: disc, decimal, lower-alpha, upper-alpha, lower-roman, upper-roman or none. Actual PDF glyphs/vector discs, marker gutters and first-line placement across pages; CSS cascade, inheritance and !important supported."
  },
  "272": {
    "text": "Two list levels only; Roman numbers at most 3999; none preserves body indentation. No list-style shorthand, inside, circle/square, counters or images. Invalid values and non-list targets fail explicitly without changing an existing PDF."
  },
  "273": {
    "text": "Bounded CSS list marker layout"
  },
  "274": {
    "text": "Source e137e523: measured marker glyphs and pagination, ten actual PDF regressions, a 74-entry report and Native AOT PDF geometry witness. Exact-head CI and independent review require separate confirmation."
  },
  "698": {
    "computed": "prior[694].replace('65 CSS property names','66 CSS property names') + ' Bounded list-style-type adds decimal, alphabetic, Roman, vector-disc and marker-free choices on ol/ul/li, with true PDF gutters and strict rejection. It is not full CSS Lists.'"
  }
};
const translated = Array(702);
for (const [start, oldStart, count] of spans)
  for (let i=0;i<count;i++) translated[start+i] = prior[oldStart+i];
for (const [index, entry] of Object.entries(additions))
  translated[Number(index)] = entry.text ?? (Number(index) === 698 ? prior[694].replace('65 CSS property names','66 CSS property names') + ' Bounded list-style-type adds decimal, alphabetic, Roman, vector-disc and marker-free choices on ol/ul/li, with true PDF gutters and strict rejection. It is not full CSS Lists.' : undefined);
if (translated.some(value => typeof value !== 'string' || !value.trim()))
  throw new Error('Missing list marker English audit translation.');
export const englishAudit = Object.freeze(translated);
