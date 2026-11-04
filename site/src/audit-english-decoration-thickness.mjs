import { englishAudit as previous } from './audit-english-links.mjs';

// Retain all historical 763 translations; update only four existing
// decoration feature prose fields and the appended inventory source string.
if (previous.length !== 763) throw new Error('Historical English translation catalogue changed.');
export const sourceDigest = '11bb862f18601b6f4a1888e0e6fe706b0b45910098c1faaf9a0d631fa4b4036d';
const entries = [...previous];
entries[282] = "Report text supports one real searchable PDF underline, overline or strike-through, with optional solid/dashed/dotted stroke, opaque colour and bounded author width. text-decoration-thickness:auto preserves the legacy font-relative ink; physical pt/px/mm/cm/in/pc/q, computed element em or html-root rem resolve to 0.2–12pt. Shorthand accepts an optional thickness and resets omitted thickness to auto. Independent cascade, !important, inherit/initial/unset, all 148 standard opaque CSS named colours, text-decoration-color/text-decoration-style, currentColor, wrapping and pagination continue to affect actual PDF paint.";
entries[283] = "Exactly one line, with bounded 0.2–12pt custom ink width or legacy auto; no zero/negative, percentages, from-font, calc, unbounded width, offset, double/wavy, multiple simultaneous lines, alpha, RTL, complex shaping or browser descendant propagation. Unsupported values and standalone-image use reject explicitly without replacing an existing PDF; this is not full CSS Text Decoration.";
entries[285] = "Supported p/h1–h6 and span/b/strong/i/em/small/u/s/del/ins/a text and bounded fixed-column table-cell text; actual PDF glyph measurement, TrueType fonts, line/page breaks and existing output/resource limits apply. No standalone image decoration thickness.";
entries[286] = "Source implementation and strict tests committed: 44-entry multi-page quotation revision register compared against independent physical-point HTML oracle; actual PDF ink, font, CSS cascade, CLI, invalid input and protected-output retry. Native AOT/qpdf/Poppler verification path committed. Exact-head CI, independent review, human approval and distribution licensing are separate.";
entries[759] = previous[759] + " As of 5 November 2026, bounded text-decoration-thickness supports auto or 0.2–12pt resolved physical/em/rem lengths and an optional fourth shorthand term. Unsupported percent/from-font/calc and out-of-scope targets reject. All 418 stable IDs remain; accepted bounded CSS property names increase from 107 to 108, not full CSS Text Decoration or a distributed package.";
export const englishAudit = Object.freeze(entries);
if (englishAudit.length !== 763 || englishAudit.some(v => typeof v !== 'string' || !v.trim() || /[\u3400-\u9fff]/u.test(v)))
  throw new Error('Decoration-thickness English translation catalogue is incomplete.');
