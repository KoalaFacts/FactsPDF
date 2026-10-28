import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { JSDOM } from 'jsdom';

// Declaration-level guard; actual hover/cascade is exercised by Playwright.
test('selected category explicitly retains readable text on hover', () => {
  const css = readFileSync(new URL('../src/caniuse.css', import.meta.url), 'utf8');
  const dom = new JSDOM('<style></style>');
  try {
    dom.window.document.querySelector('style').textContent = css;
    const rules = [...dom.window.document.styleSheets[0].cssRules];
    const rule = rules.find(r => r.selectorText === '#app .category.selected:hover:not(:disabled)');
    assert.ok(rule, 'selected hover must explicitly preserve a readable foreground/background pair');
    const luminance = color => {
      assert.match(color, /^rgb\(\d+, \d+, \d+\)$/);
      const rgb = color.match(/\d+/g).map(Number).map(c => c / 255);
      const linear = rgb.map(c => c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
      return linear[0] * 0.2126 + linear[1] * 0.7152 + linear[2] * 0.0722;
    };
    const a = luminance(rule.style.getPropertyValue('color'));
    const b = luminance(rule.style.getPropertyValue('background'));
    assert.ok((Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05) >= 4.5);
  } finally {
    dom.window.close();
  }
});
