import { englishAudit as previous } from './audit-english-attribute-selectors.mjs';
// Retain every reviewed translation; only the four source strings at the
// existing attribute-selector record change with this bounded CSS capability.
if (previous.length !== 774) throw new Error('Prior reviewed English catalogue changed.');
export const sourceDigest = 'ae465a57e6f72531b82d4efb7fbd703702f3e9be051172a698d2b30842eeddba';
const updates = new Map([
  [471, 'On accepted HTML attributes, [attr], [attr=value], prefix [attr^=value], suffix [attr$=value], substring [attr*=value], whitespace-word [attr~=value] and language-hyphen [attr|=value] match decoded values. Optional i flag uses ASCII case-insensitive comparison; s selects case-sensitive comparison, the default. They combine with tag/class/ID, group, descendant and child selectors, specificity, !important and match budgets; real PDF text, borders, spacing, images and pagination use their computed styles.'],
  [472, 'Only id/class/title/lang/href/src/alt and nonempty data-* attribute names. Empty non-equality operands, unknown attributes or operators including !=, multiple or unknown flags, whitespace-split operators, pseudo-classes, sibling/nested selectors and full CSS Selectors conformance are unsupported; i folds ASCII letters only, not Unicode. Failure preserves an existing PDF.'],
  [473, 'Actual PDF rendering for bounded attribute prefix, suffix, substring, word and language selection (source).'],
  [474, 'Source tests with a real 44-invoice multi-page PDF against independently authored inline styles, cascade, text positions, explicit images, TrueType Unicode, invalid selector syntax and prior-target preservation; exact-head CI and independent review are separate gates.']
]);
export const englishAudit = Object.freeze(previous.map((entry, index) => updates.get(index) ?? entry));
if (englishAudit.length !== 774 ||
    englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[\u3400-\u9fff]/u.test(value)))
  throw new Error('Incomplete bounded attribute operator English audit.');
