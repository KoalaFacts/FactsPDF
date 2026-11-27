import { englishAudit as previous } from './audit-english-table-part-display.mjs';

// The library now also targets net8.0 and net9.0. The .NET library row's limitation (index 740),
// used by no other row, is replaced; no string is added.
if (previous.length !== 874) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '01d21b1af67036cea4772de3b988964d8372759636341b9a4c0c08aa5d1ab152';
const entries = [...previous];
entries.splice(740, 1,
  'The library targets net8.0, net9.0 and net10.0 (the CLI and tools only net10.0). Output is byte-for-byte deterministic within one runtime, but compressed streams (fonts, images, content streams) can differ between runtimes because each .NET version ships its own zlib; byte parity and hash pins are authoritative on net10.0 only. Not a stable package/API or other language bindings.');
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 874 || englishAudit[739] !== previous[739] || englishAudit[741] !== previous[741] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete runtime targets English audit.');
