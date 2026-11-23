import { englishAudit as previous } from './audit-english-quotes.mjs';

// word-break:break-word is now accepted: CSS Text 3 defines it as word-break:normal plus
// overflow-wrap:anywhere whatever overflow-wrap computes to, and the engine has no min-content
// sizing, so it breaks exactly like overflow-wrap:break-word. The css.word-break row keeps its
// place; its scope, limitations and evidence strings are replaced in place (no count changes).
if (previous.length !== 835) throw new Error('Earlier English audit cardinality changed.');
export const sourceDigest = '5b9c455c68be83d3f4f4a620be1b58f9ab4aaa9cc37253a33b65d065ec6cde50';
const scope = previous[324].replace('accept computed word-break:normal/break-all.',
  'accept computed word-break:normal/break-all/break-word; break-word is, per CSS Text 3, normal plus overflow-wrap emergency breaking whatever overflow-wrap computes to, and its PDF is byte-identical to overflow-wrap:break-word.');
const limitations = previous[325].replace('No keep-all, break-word value, ', 'No keep-all, ');
const evidence = 'Candidate code/NUnit for td/th break-all, important, p inheritance/reset, 24-row multipage PDF versus independent explicit-style oracle and protected failures; break-word PDF byte-identical to overflow-wrap:break-word, overflow-wrap:normal ignored, inherited through the cascade and reset by normal/break-all; exact CI/review/main separate.';
const p = previous;
export const englishAudit = Object.freeze([...p.slice(0, 324), scope, limitations, ...p.slice(326, 328), evidence, ...p.slice(329)]);
if (englishAudit.length !== 835 || scope === previous[324] || limitations === previous[325] ||
  englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[㐀-鿿]/u.test(value)))
  throw new Error('Incomplete word-break English audit.');
