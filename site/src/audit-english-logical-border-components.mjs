import { englishAudit as prior } from './audit-english-logical-borders.mjs';
// Retain 746 previously reviewed English values; only one audited row plus
// the two global audit strings changed in the exact source snapshot.
if(prior.length!==750)throw new Error('Reconcile prior logical border translations first.');
export const sourceDigest='0d6d8e5e0ee62442611c41dde0e222ba72fa09caf77acb3c1a88a54fcbc42291';
const patches=Object.freeze({
  191: prior[191]+' The 18 additional scoped LTR logical border component names cover inline/block paired width/style/colour and each of four logical single-side width/style/colour components. Paired values accept one or two terms, side values one; physical PDF stroke, text geometry, inheritance, independent cascade, important and pagination are real.',
  192: prior[192].replace('Logical border shorthands do not implement RTL, vertical writing or logical border-width/style/colour component longhands; no new element applicability is implied.','The old shorthand package did not include component longhands; the 18 enumerated logical component properties are now supported.')+' RTL, vertical writing, alpha, calc, double borders, mixed CSS-wide values, unlisted logical border components and no new element applicability is implied; unsupported values still reject with explicit errors.',
  745: prior[745]+' On 4 November 2026 source 99328f071395d5d7cc1eedad9138f05ee118fb0f adds 18 LTR logical border component spellings to one existing CSS audit record; all 418 IDs persist and the 417 other records retain provenance. The audited scoped CSS name count increases from 89 to 107. Authored independent physical PDF evidence for 44 entries, fonts, CLI and Native AOT/Poppler with negative-recovery source tests remains separate from exact CI, independent review, human approval, new package and customer rights.',
  746: prior[746].replace('No RTL, vertical writing or logical border component longhands.','The earlier six-shorthand note predates these 18 component names.')+' Bounded LTR logical border width/style/colour pairs and single sides now have actual PDF paint and layout: accepted CSS name count is now 107 (previously 89), preserving 418 stable IDs. No RTL, vertical writing or complete CSS Borders.'
});
export const englishAudit=Object.freeze(prior.map((value,index)=>Object.hasOwn(patches,index)?patches[index]:value));
if(englishAudit.length!==750||englishAudit.some(v=>typeof v!=='string'||!v.trim()||/[\u3400-\u9fff]/u.test(v)))throw new Error('Incomplete border component English translation.');
