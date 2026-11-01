import { englishAudit as previous } from './audit-english-list-controls.mjs';
// Exact-source audited display delta; retain all previously reviewed translations.
if (previous.length !== 706) throw new Error('LTR alignment audit baseline moved.');
export const sourceDigest='e3e1a41213028bafb0a4e4ead0678656db17f1584df30bbf2ebee90078934d58';
const revised=Object.freeze({
  158: 'left/center/right, plus LTR-only start/end mapped to actual left/right PDF glyph measurement and positioning. Bounded text-align:justify distributes remaining U+0020 space on automatically wrapped report lines. Inherited font metrics, text indent, TJ painting and pagination remain measured.',
  159: 'start/end only describe left-to-right horizontal text; direction:rtl and HTML dir are unsupported. Justification does not expand final lines, explicit br, nowrap or lines with no spaces. No justify-all, bidi/RTL or CJK inter-character justification. Inline start/end/justify rejects with FPDF1201; unsupported values with FPDF1202. Not complete CSS Text.',
  352: 'In horizontal LTR reports, justify uses real PDF TJ spacing only on automatically wrapped nonfinal lines with U+0020. start/end map respectively to physical left/right and preserve exact glyph X positions, wrapping and pagination.',
  353: 'start/end do not enable RTL/vertical writing; direction:rtl and HTML dir are rejected. No justify-all, Bidi, CJK per-character justification; explicit br, final/nowrap and space-free lines remain ragged. Inline spans cannot declare start/end/justify.',
  702: previous[702] + ' Bounded text-align:start/end now selects the actual left/right horizontal PDF text positions in LTR reports, including inherited styles, measured fonts and page continuation. It does not accept RTL, direction or vertical writing, and is not a full CSS Text implementation.'
});
export const englishAudit=Object.freeze(previous.map((value,index)=>revised[index]??value));
if(englishAudit.length!==706||englishAudit.some(value=>typeof value!=='string'||!value.trim()||/[\u3400-\u9fff]/u.test(value)))
  throw new Error('Invalid LTR alignment English display catalogue.');
