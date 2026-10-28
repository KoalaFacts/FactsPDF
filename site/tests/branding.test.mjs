import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');

test('browser title uses the requested Can I Render name', () => {
  assert.equal(read('../index.html').match(/<title>(.*?)<\/title>/)?.[1], 'FactsPDF · Can I Render');
});

test('hero uses the same exact name without a trailing question mark', () => {
  const heading = read('../src/main.js').match(/<h1>(.*?)<\/h1>/)?.[1];
  assert.equal(heading?.replace(/<[^>]*>/g, ''), 'Can I Render');
});

test('site entry documentation uses the requested name', () => {
  assert.equal(read('../../README-SITE.md').split('\n')[0], '# FactsPDF · Can I Render');
});
