import { englishAudit as previous } from './audit-english-script.mjs';

// meta now accepts named and Open Graph metadata. Its scope and limitation strings are replaced in
// place (indices 86 and 87, used only by html.meta); its evidence string already exists. No other
// string moves.
if (previous.length !== 872) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '69160811116fdba4811df5f5c83fdf31a468adb4a2219ef79385acf246df0b1d';
const entries = [...previous];
entries.splice(86, 2,
  'Explicit charset=utf-8 inside head; named metadata given as name or property with content (viewport, description, author, generator, og:* and the like) is accepted and changes nothing in the render.',
  'Other meta semantics (http-equiv, itemprop) are unsupported; name=color-scheme and supported-color-schemes fail FPDF1103, since they change the canvas and text colours; metadata is not written to the PDF document information; head only; no encoding sniffing.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 872 || englishAudit[88] !== previous[88] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete meta-element English audit.');
