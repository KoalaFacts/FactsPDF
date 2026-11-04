import { englishAudit as baseline } from './audit-english-named-colours.mjs';

// Three existing CSS property values and the audit-methodology source string
// changed. No CSS property was added, and 750 independently indexed English
// translations retain their original positions.
if (baseline.length !== 750) throw new Error('Reconcile named-colour English catalogue first.');
export const sourceDigest = '35455ac38cbc8b06e000ccac5f6bb32c1e3a199e32d482fee8a232d45a5bafca';
const changes = Object.freeze({
  157: '#rgb/#rrggbb, 148 standard opaque CSS named colours, Opaque HSL and bounded integer and percentage rgb()/rgba(...,1), opaque hsl()/hsla(...,1), and actual PDF text, font layout, cascade and pagination. Within this supported text context, color:currentColor resolves to the computed inherited text ink from the parent.',
  158: 'No transparent text ink, alpha compositing, system colours, nested expressions, slash alpha, mixed percentage/integer RGB, fractional numeric RGB or full CSS Color. HSL is limited to number/degree hue and percentages. currentColor on text only reuses inherited computed parent ink.',
  189: 'Transparent or 148 standard opaque CSS named colours, Opaque HSL and bounded hex/RGB values paint report-box backgrounds and page fragments. background-color:currentColor resolves to the final computed text colour on this element, including stylesheet cascade, important and inheritance.',
  190: 'currentColor is bounded to final opaque computed text ink; no transparency, gradients, nested colour functions or image layers. Existing applicable elements and table-cell restrictions remain; all prior HSL/RGB value limits still apply.',
  253: 'The single-colour background shorthand accepts its bounded transparent, hex, 148 standard opaque CSS named colours, RGB, Opaque HSL and CSS-wide values. background:currentColor paints the final computed text ink on this element through the same PDF fill, cascade, and pagination path. No images, gradients, position, repeat or multiple layers.',
  745: baseline[745] + ' Additive computed-currentColor source audit at 3dbcded4f5d39b16b79bbff7aac4132305659d4a updates only three CSS property rows: color uses inherited parent text ink, background-color and its single-colour shorthand paint the final element text colour. The separate 48-record HEX PDF oracle, native verification and protected failures are source evidence, not human approval, a distributed binary, or customer rights. All other 415 stable feature records retain original identities.'
});
export const englishAudit = Object.freeze(baseline.map((value,index)=>
  Object.hasOwn(changes,index) ? changes[index] : value));
if (englishAudit.length !== 750 ||
    englishAudit.some(value=>typeof value !== 'string' || !value.trim() ||
      /[\u3400-\u9fff]/u.test(value)))
  throw new Error('Incomplete currentColor English audit.');
