import { englishAudit as prior } from './audit-english-inline-edits.mjs';

// Preserve all 759 reviewed translations by original Chinese source identity.
// Only html.a changes: four new scoped descriptions, while its historical
// unsupported descriptions remain at the first unchanged unsupported element.
if (prior.length !== 759) throw new Error('Review previous HTML translation catalogue before updating links.');
export const sourceDigest = 'ee2227900d226ace67f2a680fb40eef9a36aa9a6be94e3e392cf3327116b0a95';
const entries = [
  'An inline <a href> emits actual clickable PDF /Link URI annotations over measured searchable text; real multipage positions, TrueType, CSS overrides and CLI output.',
  'Only absolute ASCII HTTPS without credentials or one plain mailto address, up to 2048 characters; no external fetching, HTTP, script/file/relative URLs, nested links, linked images, block descendants, target/download or full HTML navigation. Errors preserve the prior PDF.',
  'Actual PDF URI Link annotation (source only)',
  prior[3], prior[4],
  'Source implementation and real PDF, CLI, TrueType and Native AOT verification cases; exact-head CI and independent review are tracked separately.',
  prior[0], prior[1], prior[2],
  ...prior.slice(5)
];
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 763 ||
    englishAudit.some(value => typeof value !== 'string' || !value.trim() || /[\u3400-\u9fff]/u.test(value)))
  throw new Error('Bounded hyperlink English catalogue is incomplete.');
