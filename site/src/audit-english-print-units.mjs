import { englishAudit as baseline } from './audit-english-percent-panel.mjs';

// Source-bound additive overlay: all other English descriptions are unchanged.
if (baseline.length !== 749) throw new Error('Prior English audit catalogue changed.');
const scopeIndices = new Set([153,160,166,178,182,185,186,187,188,191,194,196,215,219,223,224,228,231,235,239,243,245,247,249,262,266,279,353]);
export const sourceDigest = 'f554464d6dd85f69d6746947edfdd547b963ae0a6c0da7cbac00f7a21ecaf48d';
export const englishAudit = Object.freeze(baseline.map((value, i) => {
  const physical = scopeIndices.has(i)
    ? value + ' Existing bounded physical CSS values now accept pc (12pt) and q (0.25mm), affecting actual PDF geometry, text and page breaks under the same element/value limits.'
    : value;
  if (i === 153)
    return physical + ' Seven bounded font-size keywords now use 7.2/9/10/12/14.4/18/24pt. Smaller and larger use 5/6 and 6/5 of the computed parent font size and affect real PDF glyphs, wraps and page breaks.';
  if (i === 154)
    return physical.replace('font-size keywords', 'other font-size keywords');
  if (i === 156)
    return physical + ' A separate 40-entry font-size keyword/absolute-point PDF oracle with source NUnit and Native AOT checks has been added; final CI, independent review and publication remain distinct.';
  if (i === 354)
    return 'The pc/q physical units now work only in existing supported CSS lengths. CLI page margins, calc, unsupported CSS elements/properties and unplaceable values remain excluded; failure must preserve the previous PDF.';
  if (i === 744)
    return value + ' Source-only pc/q audit ca3aef91e20792782edb49004369dfbf1e9712b3 extends 36 scoped CSS properties plus absolute length values; the 418 stable IDs and 83 CSS names are unchanged. Native 40-row PDF and recovery checks, review and publication remain separate. The new bounded font-size keyword audit at b95f1bd3c5d8275bff21f399c9cf59a2d413c024 adds one scoped feature record and actual PDF equality/Native AOT regression paths while preserving 417 other source histories.';
  return physical;
}));
if (englishAudit.length !== 749 ||
    englishAudit.some(x => typeof x !== 'string' || !x.trim() || /[\u3400-\u9fff]/u.test(x)))
  throw new Error('Incomplete bounded physical-unit audit translation.');
