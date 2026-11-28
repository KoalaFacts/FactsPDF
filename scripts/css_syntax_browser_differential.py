#!/usr/bin/env python3
"""Cross-check an expanded, pinned-WPT-pattern CSS syntax corpus against real Chrome CSSOM.

Browser-side parsing is independent of FactsPDF; this is a syntax-structure
projection, NOT a complete WPT run, browser cascade/layout oracle, or a general
percentage of CSS coverage. The browser is used only in this CI script.
"""
from collections import Counter
from html.parser import HTMLParser
import html
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import tempfile

WPT_REV = "521d168d63dbd206ea7fbe0b74ddd5760e6e668e"
WPT_DIR = "css/css-syntax/"
SOURCE = {
    "numbers": "decimal-points-in-numbers.html",
    "urls": "url-whitespace-consumption.html",
    "unicode": "non-ascii-codepoints.html",
    "escapes": "escaped-eof.html",
    "nested": "invalid-nested-rules.html",
    "at-rules": "at-rule-in-declaration-list.html",
    "variables": "var-with-blocks.html",
    "unclosed": "unclosed-constructs.html",
    "recovery": "at-rule-in-declaration-list.html",
}
# Selectors Level 4 and Nesting as Chrome parses a style rule: each case makes a rule or the browser drops it (generated, probed with CSSStyleSheet.replaceSync on Chrome 158).
SELECTOR_CASES = [
    ':is()',
    ':is(,)',
    ':is(a,)',
    ':is(a,,b)',
    ':is(:foo)',
    ':is(a,:foo)',
    ':is(::before)',
    ':is(a::before)',
    ':is(:before)',
    ':is(::selection)',
    ':where()',
    ':where(:foo, b)',
    ':not()',
    ':not(:foo)',
    ':not(a,)',
    ':not(a b)',
    ':not(a > b)',
    ':not(::before)',
    ':not(a:foo)',
    ':has()',
    ':has(a)',
    ':has(> a)',
    ':has(+ a)',
    ':has(~ a)',
    ':has(a b)',
    ':has(:has(a))',
    ':has(a,)',
    ':has(:foo)',
    ':has(::before)',
    ':has(> > a)',
    ':has(a >)',
    ':has(a, :foo)',
    ':is(:has(a))',
    ':not(:has(a))',
    ':where(:has(a))',
    ':has(:is(:has(a)))',
    ':has(:not(:has(a)))',
    ':nth-child(2 of a)',
    ':nth-child(2 of a, b)',
    ':nth-child(2 of)',
    ':nth-child(2 of a b)',
    ':nth-child(2 of :foo)',
    ':nth-child(2 of a,)',
    ':nth-child(odd of .a)',
    ':nth-child(2n+1 of .a)',
    ':nth-last-child(2 of a)',
    ':nth-of-type(2 of a)',
    ':nth-last-of-type(2 of a)',
    ':nth-child(2 OF a)',
    ':nth-child(2of a)',
    ':lang(en)',
    ':lang(en, fr)',
    ':lang("en")',
    ':lang(*-Latn)',
    ':lang()',
    ':lang(en,)',
    ':lang(123)',
    ':lang("")',
    ':lang(*)',
    ':lang("*-x")',
    ':lang(en-US, "fr", *-CH)',
    ':lang(en fr)',
    ':lang(en-)',
    ':lang(\\31 23)',
    ':dir(ltr)',
    ':dir(rtl)',
    ':dir(foo)',
    ':dir()',
    ':dir(LTR)',
    ':hover',
    ':focus',
    ':active',
    ':visited',
    ':link',
    ':any-link',
    ':focus-visible',
    ':focus-within',
    ':target',
    ':target-within',
    ':checked',
    ':disabled',
    ':enabled',
    ':required',
    ':optional',
    ':read-only',
    ':read-write',
    ':placeholder-shown',
    ':default',
    ':indeterminate',
    ':valid',
    ':invalid',
    ':in-range',
    ':out-of-range',
    ':defined',
    ':scope',
    ':root',
    ':empty',
    ':fullscreen',
    ':modal',
    ':popover-open',
    ':open',
    ':closed',
    ':playing',
    ':paused',
    ':muted',
    ':current',
    ':past',
    ':future',
    ':local-link',
    ':blank',
    ':user-valid',
    ':user-invalid',
    ':autofill',
    ':-webkit-autofill',
    ':-webkit-any(a,b)',
    ':-webkit-full-screen',
    ':-moz-any(a)',
    ':matches(a)',
    ':any(a)',
    ':host',
    ':host(a)',
    ':host-context(a)',
    ':state(x)',
    ':heading',
    ':heading(1)',
    ':nth-col(2)',
    ':current(a)',
    ':foo',
    ':hover(a)',
    ':is',
    ':not',
    ':has',
    ':first-child(2)',
    ':nth-child',
    ':nth-child()',
    ':nth-child(1 of)',
    ':hover:is(a)',
    'a:hover:not(.b)',
    'a::before:hover',
    'a::before:first-child',
    'a::selection:hover',
    'a::before::after',
    'a::before a',
    'a::part(x)',
    '::before',
    '::marker',
    '::first-line',
    '::first-letter',
    '::placeholder',
    '::selection',
    '::backdrop',
    '::-webkit-scrollbar',
    '::-webkit-foo',
    '::foo',
    'a|b',
    '*|b',
    '|b',
    'a|*',
    '*|*',
    '|*',
    '[a|b]',
    '[*|b]',
    '[|b]',
    '[a|b=c]',
    '[|b=c]',
    '[*|b=c]',
    '[b|=c]',
    '[a|b|=c]',
    '[a|b|c]',
    '[a| b]',
    '[a |b]',
    'a| b',
    'a |b',
    'a|',
    '|',
    '*|',
    '[|]',
    '[a=]',
    '[a=""]',
    '[a^=""]',
    '[a$=""]',
    '[a*=""]',
    '[a~=""]',
    '[a|=""]',
    '[a i]',
    '[a=b i]',
    '[a=b s]',
    '[a=b I]',
    '[a=b x]',
    '[a=b i s]',
    '[a = b]',
    '[ a = b ]',
    '[a=b i ]',
    '[a="b"i]',
    '[a=b/**/i]',
    '[a!=b]',
    '[a=1]',
    '[1=a]',
    '[a=b c]',
    '[a=-b]',
    '[a=--]',
    '[a="1"]',
    '[a=b+]',
    '[\\61 ]',
    '[a\\=b]',
    '[a\\]]',
    '[data-x]',
    '[é]',
    '[a\\:b]',
    '[a:b]',
    '[a.b]',
    '[-a]',
    '[--a]',
    '[_a]',
    '[a b]',
    '[a=b][c=d]',
    'a[b]c',
    'a.b#c:d[e]',
    'a.1',
    '#1',
    '#\\31 23',
    '#-1',
    '#--',
    '#_',
    '#\\-1',
    '#a\\.b',
    '#\\#',
    '#é',
    '#\\e9',
    '#\\@',
    '#a#b',
    'a#',
    '#',
    '#.a',
    '.1',
    '.\\31 23',
    '.-1',
    '.--',
    '.\\-1',
    '. a',
    '.',
    '.a.',
    'a..b',
    '.a\\.b',
    '.\\\\',
    '.é',
    'a>>b',
    'a > > b',
    '> a',
    'a >',
    '+ a',
    'a +',
    'a ~ ~ b',
    'a,',
    'a,,b',
    ',a',
    'a,b',
    'a b',
    'a  b',
    'a/**/b',
    'a/**/>/**/b',
    'a/**/.b',
    'a . b',
    'a: hover',
    'a :hover',
    'a:hover',
    'a: not(b)',
    '* *',
    '**',
    '*a',
    'a*',
    '&',
    'a&',
    '&a',
    '&.b',
    '.b&',
    'a:not(&)',
    'a:is(&)',
    ':scope > a',
    'a:nth-child(2n+1)',
    'a:nth-child(2n + 1)',
    'a:nth-child(2n+ 1)',
    'a:nth-child( 2n+1 )',
    'a:nth-child(+2n+1)',
    'a:nth-child(n)',
    'a:nth-child(-n)',
    'a:nth-child(+n)',
    'a:nth-child(- n)',
    'a:nth-child(2n 1)',
    'a:nth-child(2 n)',
    'a:nth-child(2.5)',
    'a:nth-child(2n+1.5)',
    'a:nth-child(1e2)',
    'a:nth-child(2n+)',
    'a:nth-child(foo)',
    'a:nth-child(ODD)',
    'a:nth-child(2N+1)',
    'a:nth-child(+ 2n)',
    'a:nth-child(n+ 2)',
    'a:nth-child(n +2)',
    'a:nth-child(n+ +2)',
    'a:nth-child(-2n-+1)',
    'a:nth-child(99999999999999999999)',
    'a:nth-child(2147483648)',
    'a:nth-child(-n+2147483647)',
    'a:nth-last-child(odd)',
    'a:nth-of-type(n)',
    'a:nth-last-of-type(n)',
    'a:first-child:last-child',
    'a:only-child',
    'p:lang(en):not(:lang(fr))',
    'a:is(b):is(c)',
    'a:is(b c, d > e)',
    'a:is(b + c, d ~ e)',
    'a:is(> b)',
    'a:where(b c)',
    ':not(a + b)',
    ':not(a, b c)',
    ':has(> a, + b)',
    ':has(a > b)',
    ':has(> a b)',
    ':has(a:not(b))',
    ':has(a:hover)',
    ':has(:is(a, b))',
    ':has(&)',
    ':not(:is(a b))',
    'a:not(:not(b))',
    ':is(a):not(:where(b, c))',
    'a:is(.b, #c, [d], :e)',
    ':nth-child(2 Of a)',
    ':nth-child(2 oF a)',
    ':nth-child(2 of  a)',
    ':nth-child(2  of a)',
    ':nth-child( 2 of a )',
    ':nth-child(n of a)',
    ':nth-child(-n+2 of a, b)',
    ':nth-child(of a)',
    ':nth-child(2 of > a)',
    ':nth-child(2 of ,a)',
    ':nth-child(2 of :is(a))',
    ':nth-child(2 of :hover)',
    ':nth-child(2 of &)',
    ':dir("ltr")',
    ':dir(ltr rtl)',
    ':dir( ltr )',
    ':lang( en )',
    ':lang(en en)',
    ':lang(en-GB)',
    ':lang(--)',
    ':lang(-en)',
    ':lang(\\-)',
    ':host()',
    ':host(a b)',
    ':host(a, b)',
    ':host-context()',
    ':host-context(a, b)',
    ':state()',
    ':state(a b)',
    ':state(--x)',
    ':state("a")',
    ':-webkit-any()',
    ':-webkit-any(:foo, a)',
    ':-webkit-any(a b)',
    ':is(a b, )',
    ':IS(a)',
    ':Not(a)',
    ':HAS(a)',
    ':Where(a)',
    ':HOVER',
    ':First-Child',
    'A:Hover',
    ':NTH-CHILD(2)',
    ':LANG(en)',
    ':DIR(ltr)',
    'a:is(:has(b))',
    'a:has(:is(:has(b)))',
    ':has(:where(:has(b)))',
    ':has(:not(:has(b)))',
    ':has(:nth-child(1 of :has(a)))',
    ':has(a:has(b))',
    ':not(:is(:has(a)))',
    ':is(:has(a)):has(b)',
    ':has(:is(a):has(b))',
    ':has(:is(b))',
    ':has(a) :has(b)',
    ':has(a):has(b)',
    '::before:hover',
    '::after:hover',
    '::marker:hover',
    '::first-line:hover',
    '::selection:hover',
    '::placeholder:hover',
    '::before:focus',
    '::before:active',
    '::before:is(a)',
    '::before:not(a)',
    '::backdrop:hover',
    '::-webkit-scrollbar:horizontal',
    '::-webkit-scrollbar-thumb:hover',
    '::file-selector-button:hover',
    ':before',
    ':after',
    ':first-line',
    ':first-letter',
    ':before:hover',
    ':-webkit-foo',
    ':before:is(a)',
    'a::before ',
    'a ::before',
    'a::before b',
    ':is(a)::before',
    ':not(a)::before',
    '::before::after',
    ':before::after',
    'a::BEFORE',
    'a::After',
    'a:BEFORE',
    'a:AFTER',
    '::part(x)',
    '::part(x y)',
    '::part()',
    '::part(x):hover',
    '::slotted(a)',
    '::slotted(a)::before',
    ':host::part(a)',
    '::highlight(x)',
    '::highlight()',
    '::view-transition-group(*)',
    '*::before',
    '*|*::before',
    'a|b::before',
    '::before a',
    ':hover::before',
    '::cue(a)',
    '::cue-region(a)',
    ':buffering',
    ':first',
    ':first-child',
    ':first-of-type',
    ':has-slotted',
    ':last-child',
    ':last-of-type',
    ':left',
    ':only-child',
    ':only-of-type',
    ':picture-in-picture',
    ':right',
    ':seeking',
    ':stalled',
    ':state',
    ':target-current',
    ':volume-locked',
    ':xr-overlay',
    ':-webkit-any-link',
    ':-webkit-drag',
    ':-webkit-animating-full-screen-transition',
    ':-webkit-autofill-and-obscured',
    ':-webkit-autofill-previewed',
    ':-webkit-autofill-selected',
    ':-moz-any-link',
    ':-moz-focusring',
    ':focus-ring',
    ':fullscreen-ancestor',
    ':horizontal',
    ':vertical',
    ':decrement',
    ':increment',
    ':start',
    ':end',
    ':double-button',
    ':single-button',
    ':no-button',
    ':corner-present',
    ':window-inactive',
    ':enabled-text',
    ':autofill-preview',
    ':user-error',
    ':unchecked',
    ':paused-view',
    ':active-view-transition',
    ':target-before',
    ':target-after',
    ':details-content',
    ':tentative',
    ':inert',
    ':open-modal',
    ':not-defined',
    ':interest-source',
    ':interest-target',
    ':pointer-over',
    ':pointer-over-ancestor',
    ':scroll-state',
    ':popover-open-like',
    ':invoker-open',
    ':invoker-closed',
    ':is(a)',
    ':where(a)',
    ':not(a)',
    ':-webkit-any(a)',
    ':state(a)',
    ':heading(a)',
    ':nth-col(a)',
    ':nth-last-col(a)',
    ':lang(a)',
    ':dir(a)',
    ':slotted(a)',
    ':part(a)',
    ':cue(a)',
    ':active-view-transition-type(a)',
    ':dir(ltr)(a)',
    ':target-current(a)',
    ':scroll-state(a)',
    'a:is(:link, :visited)',
    ':nth-child(2 of .a, .b)',
    ':has(> img, + p)',
    'p:not(.a, #b, [c])',
    ':is(h1, h2, h3):where(.x)',
    'html|body',
    ':where(:is(a, b), c)',
    'a:has(> b:not(.c))',
    '.a:is(.b):not(:where(.c))',
    '#a:has(.b ~ .c)',
    'p:has(+ p:has(+ p))',
    'a:nth-child(2n+1 of :not(.x))',
    'ul > li:nth-last-child(-n+3 of .y)',
    'a::before',
    'a::after',
    'a:before',
    'a::first-line',
    'a::part(foo)',
    'a::slotted(b)',
    '::-webkit-input-placeholder',
]

# @namespace placement and namespace-prefixed selectors in an HTML document (generated, probed on Chrome 158).
NAMESPACE_CASES = [
    '@namespace svg url(http://www.w3.org/2000/svg);svg|rect{color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);svg|*{color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);*|rect{color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);|rect{color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);rect{color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);x|rect{color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);SVG|rect{color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);svg|rect,p{color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);svg|rect,nope|p{color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);[svg|fill]{color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);[*|fill]{color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);[|fill]{color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);[x|fill]{color:red}',
    '@namespace url(http://www.w3.org/1999/xhtml);p{color:red}',
    '@namespace url(http://www.w3.org/1999/xhtml);*|p{color:red}',
    '@namespace url(http://www.w3.org/1999/xhtml);|p{color:red}',
    '@namespace url(http://www.w3.org/2000/svg);rect{color:red}',
    '@namespace "http://www.w3.org/2000/svg";rect{color:red}',
    '@namespace svg "http://www.w3.org/2000/svg";svg|rect{color:red}',
    '@namespace svg url("http://www.w3.org/2000/svg");svg|rect{color:red}',
    '@namespace url();p{color:red}',
    '@namespace "";p{color:red}',
    '@namespace svg url(foo);svg|p{color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);@namespace svg url(http://www.w3.org/2000/svg);svg|p{color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);@namespace svg url(http://www.w3.org/1999/xhtml);svg|p{color:red}',
    'p{color:red}@namespace svg url(http://www.w3.org/2000/svg);svg|rect{color:red}',
    'p{}@namespace url(foo);p{color:red}',
    '@import url(x.css);@namespace svg url(http://www.w3.org/2000/svg);svg|rect{color:red}',
    '@charset "utf-8";@namespace svg url(http://www.w3.org/2000/svg);svg|rect{color:red}',
    '@layer a;@namespace svg url(http://www.w3.org/2000/svg);svg|rect{color:red}',
    '@layer a{}@namespace svg url(http://www.w3.org/2000/svg);svg|rect{color:red}',
    '@media all{@namespace url(foo);}p{color:red}',
    '@media all{@namespace svg url(http://www.w3.org/2000/svg);svg|rect{color:red}}',
    '@namespace;rect{color:red}',
    '@namespace svg;svg|rect{color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg) screen;svg|rect{color:red}',
    '@namespace url(http://www.w3.org/2000/svg) svg;svg|rect{color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg){}svg|rect{color:red}',
    '@NAMESPACE svg url(http://www.w3.org/2000/svg);svg|rect{color:red}',
    '@namespace svg url( http://www.w3.org/2000/svg );svg|rect{color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);svg|rect:not(svg|g){color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);:is(svg|rect, p){color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);svg|rect:has(svg|g){color:red}',
    '@namespace svg url(http://www.w3.org/2000/svg);svg|*:nth-child(1 of svg|g){color:red}',
]

# Conditional group rules read as rule lists: a loose declaration starts a qualified rule whose prelude runs to the next block (generated, probed on Chrome 158).
RULE_LIST_CASES = [
    '@media all{p{color:red}}',
    '@media all{color:red;p{color:red}div{color:red}}',
    '@media all{p{color:red}color:red;div{color:red}}',
    '@media all{p{color:red}color:red}',
    '@media all{color:red}',
    '@media all{color:red;}',
    '@media all{;p{color:red}}',
    '@media all{p;div{color:red}span{color:red}}',
    '@media all{unknown-prop:1;p{color:red}div{color:red}}',
    '@media all{--x:1;p{color:red}div{color:red}}',
    '@media all{--x:{a};p{color:red}div{color:red}}',
    '@media all{a:b;c:d;p{color:red}div{color:red}}',
    '@media all{a:b{color:red}p{color:red}}',
    '@media all{p{color:red}@unknown x;div{color:red}}',
    '@media all{@unknown x;p{color:red}div{color:red}}',
    '@media all{p{color:red}@unknown x{}div{color:red}}',
    '@media all{color:red;@layer a;p{color:red}div{color:red}}',
    '@media all{color:red;@media all{p{color:red}}div{color:red}}',
    '@media all{@media all{color:red;p{color:red}div{color:red}}span{color:red}}',
    '@media all{p{color:red} ; div{color:red}}',
    '@media all{p{color:red}}div{color:red}}',
    '@media print{p{color:red}}',
    '@media print{color:red;p{color:red}div{color:red}}',
    '@media print{p{color:red}color:red;div{color:red}}',
    '@media print{p{color:red}color:red}',
    '@media print{color:red}',
    '@media print{color:red;}',
    '@media print{;p{color:red}}',
    '@media print{p;div{color:red}span{color:red}}',
    '@media print{unknown-prop:1;p{color:red}div{color:red}}',
    '@media print{--x:1;p{color:red}div{color:red}}',
    '@media print{--x:{a};p{color:red}div{color:red}}',
    '@media print{a:b;c:d;p{color:red}div{color:red}}',
    '@media print{a:b{color:red}p{color:red}}',
    '@media print{p{color:red}@unknown x;div{color:red}}',
    '@media print{@unknown x;p{color:red}div{color:red}}',
    '@media print{p{color:red}@unknown x{}div{color:red}}',
    '@media print{color:red;@layer a;p{color:red}div{color:red}}',
    '@media print{color:red;@media all{p{color:red}}div{color:red}}',
    '@media print{@media all{color:red;p{color:red}div{color:red}}span{color:red}}',
    '@media print{p{color:red} ; div{color:red}}',
    '@media print{p{color:red}}div{color:red}}',
    '@supports (color:red){p{color:red}}',
    '@supports (color:red){color:red;p{color:red}div{color:red}}',
    '@supports (color:red){p{color:red}color:red;div{color:red}}',
    '@supports (color:red){p{color:red}color:red}',
    '@supports (color:red){color:red}',
    '@supports (color:red){color:red;}',
    '@supports (color:red){;p{color:red}}',
    '@supports (color:red){p;div{color:red}span{color:red}}',
    '@supports (color:red){unknown-prop:1;p{color:red}div{color:red}}',
    '@supports (color:red){--x:1;p{color:red}div{color:red}}',
    '@supports (color:red){--x:{a};p{color:red}div{color:red}}',
    '@supports (color:red){a:b;c:d;p{color:red}div{color:red}}',
    '@supports (color:red){a:b{color:red}p{color:red}}',
    '@supports (color:red){p{color:red}@unknown x;div{color:red}}',
    '@supports (color:red){@unknown x;p{color:red}div{color:red}}',
    '@supports (color:red){p{color:red}@unknown x{}div{color:red}}',
    '@supports (color:red){color:red;@layer a;p{color:red}div{color:red}}',
    '@supports (color:red){color:red;@media all{p{color:red}}div{color:red}}',
    '@supports (color:red){@media all{color:red;p{color:red}div{color:red}}span{color:red}}',
    '@supports (color:red){p{color:red} ; div{color:red}}',
    '@supports (color:red){p{color:red}}div{color:red}}',
    '@layer x{p{color:red}}',
    '@layer x{color:red;p{color:red}div{color:red}}',
    '@layer x{p{color:red}color:red;div{color:red}}',
    '@layer x{p{color:red}color:red}',
    '@layer x{color:red}',
    '@layer x{color:red;}',
    '@layer x{;p{color:red}}',
    '@layer x{p;div{color:red}span{color:red}}',
    '@layer x{unknown-prop:1;p{color:red}div{color:red}}',
    '@layer x{--x:1;p{color:red}div{color:red}}',
    '@layer x{--x:{a};p{color:red}div{color:red}}',
    '@layer x{a:b;c:d;p{color:red}div{color:red}}',
    '@layer x{a:b{color:red}p{color:red}}',
    '@layer x{p{color:red}@unknown x;div{color:red}}',
    '@layer x{@unknown x;p{color:red}div{color:red}}',
    '@layer x{p{color:red}@unknown x{}div{color:red}}',
    '@layer x{color:red;@layer a;p{color:red}div{color:red}}',
    '@layer x{color:red;@media all{p{color:red}}div{color:red}}',
    '@layer x{@media all{color:red;p{color:red}div{color:red}}span{color:red}}',
    '@layer x{p{color:red} ; div{color:red}}',
    '@layer x{p{color:red}}div{color:red}}',
    '@container (min-width:1px){p{color:red}}',
    '@container (min-width:1px){color:red;p{color:red}div{color:red}}',
    '@container (min-width:1px){p{color:red}color:red;div{color:red}}',
    '@container (min-width:1px){p{color:red}color:red}',
    '@container (min-width:1px){color:red}',
    '@container (min-width:1px){color:red;}',
    '@container (min-width:1px){;p{color:red}}',
    '@container (min-width:1px){p;div{color:red}span{color:red}}',
    '@container (min-width:1px){unknown-prop:1;p{color:red}div{color:red}}',
    '@container (min-width:1px){--x:1;p{color:red}div{color:red}}',
    '@container (min-width:1px){--x:{a};p{color:red}div{color:red}}',
    '@container (min-width:1px){a:b;c:d;p{color:red}div{color:red}}',
    '@container (min-width:1px){a:b{color:red}p{color:red}}',
    '@container (min-width:1px){p{color:red}@unknown x;div{color:red}}',
    '@container (min-width:1px){@unknown x;p{color:red}div{color:red}}',
    '@container (min-width:1px){p{color:red}@unknown x{}div{color:red}}',
    '@container (min-width:1px){color:red;@layer a;p{color:red}div{color:red}}',
    '@container (min-width:1px){color:red;@media all{p{color:red}}div{color:red}}',
    '@container (min-width:1px){@media all{color:red;p{color:red}div{color:red}}span{color:red}}',
    '@container (min-width:1px){p{color:red} ; div{color:red}}',
    '@container (min-width:1px){p{color:red}}div{color:red}}',
    '@media all{p{color:red}}@media all{color:red;p{color:red}}div{color:red}',
    '@media all{color:red;}p{color:red}',
    '@media {p{color:red}}',
    '@media{color:red;p{color:red}div{color:red}}',
    '@media all{@media all{color:red;p{color:red}div{color:red}}}',
    '@media all{@layer a{color:red;p{color:red}div{color:red}}span{color:red}}',
]

def cases():
    output = []
    def add(family, context, css, wpt=False):
        output.append({
            "id": f"{family}-{len(output):03d}",
            "family": family,
            "context": context,
            "css": css,
            "reference": WPT_DIR + SOURCE[family] if wpt and family in SOURCE else None,
            "derivation": "adapted WPT behavior pattern" if wpt else "generated complementary case"
        })
    selectors = [
        "p", ".note", "#summary", "section p", "div > p", "h1, h2",
        "article.note > p", "p.note.strong", ":is(p, .note)",
        ":where(.note)", "p:not(.disabled)", "section:has(p)"
    ]
    properties = [
        "color:red", "color:#12aaff", "font-size:12px", "width:40%",
        "opacity:.5", "display:grid", "background-color:blue",
        "line-height:1.25"
    ]
    # Generated, independent CSSOM/AST rule structures: 12 x 8 = 96.
    for selector in selectors:
        for value in properties:
            add("rules", "stylesheet", selector + "{" + value + "}")
    # Decimal syntax patterns, adapted from WPT decimals / dimensions.
    for value in ["1", "1.0", ".1", "0.25", "1e0", "1E2", "+2", "1.5",
                  "0", "2e+0", "1e-1", "2.75"]:
        add("numbers", "inline", "line-height:" + value, True)
    for value in ["1px", ".5em", "1.25pt", "2e1px", "3%", "0"]:
        add("numbers", "inline", "width:" + value, True)
    # Exact WPT URL whitespace variations plus additional valid URL forms.
    for value in ['url("foo")', 'url( "foo")', 'url("foo" )',
                  "url(foo.png)", "url( foo.png )", "url(a\\)b)",
                  "url(test.svg)", 'url("a b.svg")',
                  "url(abc-def)", "url(a/b/c.png)"]:
        add("urls", "inline", "background-image:" + value, True)
    # Escaped identifiers and non-ASCII names; no URL/network access.
    for css in [".café{color:red}", ".中文{color:red}", "#über{color:red}",
                ".\\61{color:blue}", ".\\6eote{color:red}",
                "café{width:2px}", ".a\\+b{color:green}",
                ".\\31 23{color:blue}", ".foo\\:bar{color:blue}",
                ".\\1F600{color:blue}", ".aa\\26 bb{color:blue}"]:
        add("unicode", "stylesheet", css, True)
    for css in ["col\\6fr:red", "--Theme:var(--x, red)", "--theme:blue",
                "--name:hello\\", "color:blue", "--文字:green"]:
        add("escapes", "inline", css, True)
    # Comment token boundaries, including no invented space.
    for sep in ["", " ", "\n", "/*x*/", "/**/", " /*x*/ ", "/*x*/\n"]:
        add("comments", "stylesheet", "p" + sep + ".note{color:red}")
        add("comments", "inline", "color:" + sep + "red")
    # Nested component grammar held as valid custom properties.
    for value in ["var(--a)", "var(--a, red)", "var(--a, {x:y})",
                  "calc(100% - 2px)", "'hello'", '"a b"',
                  "(1 + 2)", "[x, y]", "{a:b}", "var(--a, var(--b, red))"]:
        add("variables", "inline", "--payload:" + value, True)
    for value in ["var(--a)", "calc(100% - 2px)", "var(--a, red)",
                  "min(1px, 2px)", "clamp(1px, 50%, 100px)"]:
        add("variables", "inline", "width:" + value, True)
    # At-rules and nested rule trees, neither evaluated nor fetched.
    for prefix in ["@media print", "@media screen", "@media all",
                   "@supports (display: grid)", "@supports (color: red)"]:
        for rule in ["p{color:red}", "p{color:red}div{color:blue}",
                     ".note{width:10px}"]:
            add("at-rules", "stylesheet", prefix + "{" + rule + "}", True)
    for selector in [".a", "p", "article", "#item"]:
        for child in ["& .b", "&>span", "&:hover"]:
            add("nested", "stylesheet", selector + "{color:red;" +
                child + "{color:blue}}", True)
    # WPT unclosed constructs (only browser-comparable parsed structures).
    for value in ["p{color:red", "p{color:blue",
                  "p{width:12px", "p{opacity:.5",
                  "p{color:red;font-size:12px",
                  "p{--payload:var(--a)", "p{display:grid"]:
        add("unclosed", "stylesheet", value, True)
    # CSS recovery: malformed declaration followed by an intact declaration.
    for bad in ["invalid value", "!invalid", "*color:red",
                "missing", ":bad", "/bad", "color",
                "@unknown x;", "broken()"]:
        add("recovery", "inline", bad + "; color:red", True)
    known = {(x["context"], x["css"]) for x in output}
    for family, sources, suffix in (("selectors", SELECTOR_CASES, "{color:red}"), ("namespaces", NAMESPACE_CASES, ""),
                                    ("rulelists", RULE_LIST_CASES, "")):
        for source in sources:
            css = source + suffix
            if ("stylesheet", css) not in known:
                known.add(("stylesheet", css))
                add(family, "stylesheet", css)
    # Source dedup is intentional: even test inputs must be distinct.
    assert len(output) >= 800, len(output)
    assert len({(x["context"], x["css"]) for x in output}) == len(output), "duplicate case"
    return output


class PreReader(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.depth = 0
        self.text = []
    def handle_starttag(self, tag, attrs):
        if tag == "pre" and ("id", "results") in attrs: self.depth += 1
    def handle_endtag(self, tag):
        if tag == "pre" and self.depth: self.depth -= 1
    def handle_data(self, data):
        if self.depth: self.text.append(data)


SCRIPT = r"""
"use strict";
const input = JSON.parse(document.getElementById("cases").textContent);
function decls(style) {
  if (!style) return [];
  return Array.from(style).map(name => ({
    name: name,
    important: style.getPropertyPriority(name) === "important"
  }));
}
function rules(items) {
  return Array.from(items).map(rule => {
    const className = rule.constructor.name;
    let kind;
    if (className === "CSSStyleRule") kind = "style";
    else if (className === "CSSMediaRule") kind = "@media";
    else if (className === "CSSSupportsRule") kind = "@supports";
    else if (className === "CSSNamespaceRule") kind = "@namespace";
    else if (className === "CSSLayerBlockRule") kind = "@layer";
    else if (className === "CSSLayerStatementRule") kind = "@layer";
    else if (className === "CSSContainerRule") kind = "@container";
    else kind = "@unsupported:" + className;
    return {
      kind: kind,
      declarations: decls(rule.style),
      children: rule.cssRules ? rules(rule.cssRules) : []
    };
  });
}
const result = [];
for (const item of input) {
  try {
    let data;
    if (item.context === "stylesheet") {
      const sheet = new CSSStyleSheet();
      sheet.replaceSync(item.css);
      data = { rules: rules(sheet.cssRules) };
    } else {
      const el = document.createElement("span");
      el.style.cssText = item.css;
      data = { declarations: decls(el.style) };
    }
    result.push({id:item.id, data:data});
  } catch(error) {
    result.push({id:item.id, error:String(error)});
  }
}
document.getElementById("results").textContent = JSON.stringify(result);
"""


def chrome_oracle(binary, entries, work):
    embedded = json.dumps(entries, ensure_ascii=False, separators=(",", ":"))
    # Cannot allow JSON to close the non-executable <script type=json> element.
    embedded = embedded.replace("<", "\\u003c").replace(">", "\\u003e").replace("&", "\\u0026")
    content = ("<!doctype html><html><meta charset=utf-8><title>CSS Syntax oracle</title>"
               '<script type="application/json" id="cases">' + embedded + "</script>"
               '<pre id="results"></pre><script>' + SCRIPT + "</script></html>")
    page = work / "reference.html"
    page.write_text(content, encoding="utf-8")
    args = [binary, "--headless=new", "--no-sandbox", "--disable-gpu",
            "--disable-dev-shm-usage", "--disable-background-networking",
            "--disable-extensions", "--no-first-run",
            "--user-data-dir=" + str(work / "chrome-profile"),
            "--virtual-time-budget=5000", "--dump-dom", page.as_uri()]
    proc = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", timeout=80)
    if proc.returncode:
        raise RuntimeError("Chrome failed: " + proc.stderr[-2500:])
    pre = PreReader()
    pre.feed(proc.stdout)
    if not pre.text:
        raise RuntimeError("Chrome did not return oracle JSON; stderr: " + proc.stderr[-1500:])
    decoded = json.loads("".join(pre.text))
    if len(decoded) != len(entries):
        raise AssertionError("Browser oracle omitted cases")
    return decoded


def main():
    if len(sys.argv) != 3:
        raise SystemExit("Usage: css_syntax_browser_differential.py <probe-dll> <artifact-dir>")
    probe = Path(sys.argv[1]).resolve()
    target = Path(sys.argv[2]); target.mkdir(parents=True, exist_ok=True)
    browser = next((shutil.which(x) for x in ("google-chrome", "chromium", "chromium-browser") if shutil.which(x)), None)
    if not browser:
        raise RuntimeError("CI must provide a real Chrome/Chromium browser")
    browser_version = subprocess.check_output([browser, "--version"], text=True).strip()
    entries = cases()
    with tempfile.TemporaryDirectory(prefix="factspdf-css-oracle-") as temp:
        work = Path(temp)
        fixture = work / "cases.json"
        fixture.write_text(json.dumps(entries, ensure_ascii=False), encoding="utf-8")
        browser_data = chrome_oracle(browser, entries, work)
        engine_process = subprocess.run(["dotnet", str(probe), str(fixture)],
                                        capture_output=True, text=True, encoding="utf-8", timeout=80)
        if engine_process.returncode:
            raise RuntimeError("FactsPDF probe failed: " + engine_process.stderr[-2000:])
        engine_data = json.loads(engine_process.stdout)
    a = {item["id"]: item for item in browser_data}
    b = {item["id"]: item for item in engine_data}
    categories = {}
    mismatches = []
    for item in entries:
        name = item["family"]
        stat = categories.setdefault(name, {"passed": 0, "failed": 0, "wpt_pattern_cases": 0})
        if item["reference"]: stat["wpt_pattern_cases"] += 1
        oracle = a[item["id"]]
        actual = b[item["id"]]
        if oracle.get("error") is None and oracle.get("data") == actual.get("data"):
            stat["passed"] += 1
        else:
            stat["failed"] += 1
            mismatches.append({
                "id": item["id"], "family": name, "context": item["context"],
                "css": item["css"], "reference": item["reference"],
                "browser": oracle, "factspdf": actual
            })
    report = {
        "wpt_revision": WPT_REV,
        "scope": "Deterministic WPT-pattern-inspired plus generated CSSOM syntax-tree structural projections. Not the full upstream WPT harness, computed-style equivalence, or PDF rendering compatibility.",
        "browser_version": browser_version,
        "dotnet_probe": str(probe.name),
        "runner_os": platform.platform(),
        "cases": len(entries),
        "wpt_pattern_cases": sum(1 for x in entries if x["reference"]),
        "passed": sum(x["passed"] for x in categories.values()),
        "failed": len(mismatches),
        "families": categories,
        "mismatches": mismatches,
        "note": "CSSOM discards invalid declarations and performs property validation; AST preserves syntactically valid unknown properties. Only directly comparable projections are used. Detailed failures remain in this artifact."
    }
    dest = target / "browser-differential.json"
    dest.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k:report[k] for k in ("browser_version","cases","wpt_pattern_cases","passed","failed","families")}, indent=2))
    for case in mismatches[:8]:
        print("DIFF", case["id"], case["css"], case["browser"], case["factspdf"], file=sys.stderr)
    if mismatches:
        raise AssertionError(str(len(mismatches)) + " browser oracle disagreements")


if __name__ == "__main__":
    main()
