import { englishAudit as baseline } from './audit-english-percent-panel.mjs';

// Source-bound additive overlay: all other English descriptions are unchanged.
if (baseline.length !== 749) throw new Error('Prior English audit catalogue changed.');
const scopeIndices = new Set([153,160,166,178,182,185,186,187,188,191,194,196,215,219,223,224,228,231,235,239,243,245,247,249,262,266,279,353]);
export const sourceDigest = '0531e351a482c2b17cc289395ea7544906aab10fad47260597d5df92281bcc38';
export const englishAudit = Object.freeze(baseline.map((value, i) => {
  if (scopeIndices.has(i)) return value + ' Existing bounded physical CSS values now accept pc (12pt) and q (0.25mm), affecting actual PDF geometry, text and page breaks under the same element/value limits.';
  if (i === 354)
    return 'The pc/q physical units now work only in existing supported CSS lengths. CLI page margins, calc, unsupported CSS elements/properties and unplaceable values remain excluded; failure must preserve the previous PDF.';
  if (i === 744)
    return value + ' Source-only pc/q audit ca3aef91e20792782edb49004369dfbf1e9712b3 extends 36 scoped CSS properties plus absolute length values; the 418 stable IDs and 83 CSS names are unchanged. Native 40-row PDF and recovery checks, review and publication remain separate.';
  return value;
}));
if (englishAudit.length !== 749 ||
    englishAudit.some(x => typeof x !== 'string' || !x.trim() || /[\u3400-\u9fff]/u.test(x)))
  throw new Error('Incomplete bounded physical-unit audit translation.');
