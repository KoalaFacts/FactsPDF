import { englishAudit as previous } from './audit-english-flex-layout.mjs';

// ::before and ::after (and :before/:after) generate boxes from string content. The
// pseudo-element and content rows move from rejected to partial with their own scope, limitation,
// stage and evidence; the inventory note gains content (396 to 397).
if (previous.length !== 886) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'ae2eaea7cd2ddb8e74666130b39b997532207526060629458dc6f21d4252c1cc';
const entries = [...previous];
// Splices run from the end, so each index refers to the earlier layer.
entries.splice(882, 1,
  previous[882].replace(/\.$/, '') + "; string content on ::before and ::after generates pseudo-element boxes, adding the content name; this took it from 396 to 397.");
entries.splice(597, 2,
  "::before and ::after (and the legacy :before and :after) generate a box at the start or end of an element's content: string content makes inline text by default, and a block box inside a display:flex container or with display:block/flex on the pseudo-element, which takes part in its parent's layout with width, height, borders, backgrounds and flex-grow (such as flex-grow:1 divider lines either side of a title). A pseudo-element inherits from its element, cascades its own rules and counts as a type selector in specificity; pseudo-element rules without content (such as *::before{box-sizing:border-box}) generate nothing.");
entries.splice(391, 0,
  "content: a string on ::before/::after (\"\" makes an empty box; several strings are joined) generates the pseudo-element box, and none and normal generate nothing; content on an ordinary element changes nothing, as in a browser.",
  "content accepts only none, normal and strings (several are joined, escapes decoded); counter(), attr(), url() and quotes such as open-quote fail FPDF1202. display accepts only inline, block, flex or none (inline-block and others fail FPDF1202); block pseudo-elements only in ordinary report panels, li and body (in a paragraph or a table FPDF1201), and an inline pseudo-element takes no box properties (padding and the like fail FPDF1201); generated content on hr and other elements fails FPDF1201, and an img generates no pseudo-element box (as in a browser). ::marker, ::first-line and other pseudo-elements, pseudo-classes and selectors continuing after a pseudo-element fail FPDF1203; structural pseudo-classes such as :not(:empty) are not implemented. Errors leave the previous PDF untouched.",
  "CSS selectors, cascade and PDF layout of generated boxes",
  "Candidate-source NUnit (GeneratedContentTests: divider geometry, inline text before and after, block pseudo-elements, no-output cases and failure codes) and a comparison with Chrome print output (word origins within 0.12pt horizontally, divider fills within 0.37pt); test suites not run locally, exact CI, independent review and main still to be confirmed.");
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 889 ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[\u3400-\u9fff]/u.test(value)))
  throw new Error('Incomplete generated content English audit.');
