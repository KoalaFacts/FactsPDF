import { englishAudit as previous } from './audit-english-list-type.mjs';

// The library now also targets net8.0 and net9.0, and output is byte-identical per .NET runtime
// version only. The .NET library row's limitation (index 742), used by no other row, is replaced;
// no string is added.
if (previous.length !== 876) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = 'adc467128d19d0c2394c9012cd93209db227523bad5da9e1b82fe15f9541c644';
const entries = [...previous];
entries.splice(742, 1,
  'The library targets net8.0, net9.0 and net10.0 (the CLI and tools only net10.0). Output is byte-identical for the same input, options and .NET runtime version; different .NET versions may produce different bytes (for example in compressed streams: fonts, images, content streams, because each ships its own zlib) with the same rendered content; byte parity and hash pins are authoritative on net10.0 only. Not a stable package/API or other language bindings.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 876 || englishAudit[741] !== previous[741] || englishAudit[743] !== previous[743] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete runtime targets English audit.');
