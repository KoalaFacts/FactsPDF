import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { anchorsOf, checkDocs, namedPath, slugify, trackedDocs } from './check-docs.ts';

const roots: string[] = [];
after(() => {
  for (const root of roots) {
    rmSync(root, { recursive: true, force: true });
  }
});

function repo(files: Record<string, string>): string {
  const root = mkdtempSync(join(tmpdir(), 'check-docs-'));
  roots.push(root);
  for (const [path, text] of Object.entries(files)) {
    mkdirSync(dirname(join(root, path)), { recursive: true });
    writeFileSync(join(root, path), text);
  }
  return root;
}

function messages(files: Record<string, string>): string[] {
  const root = repo(files);
  const docs = Object.keys(files).filter((file) => file.endsWith('.md'));
  return checkDocs(root, docs).map((p) => `${p.file}:${p.line}: ${p.message}`);
}

test('slugify follows GitHub heading anchors', () => {
  assert.equal(slugify('Library API'), 'library-api');
  assert.equal(slugify('Lengths, margins and spacing'), 'lengths-margins-and-spacing');
  assert.equal(slugify('`white-space` and wrapping'), 'white-space-and-wrapping');
  assert.equal(slugify('First use from [local](x.md) artifacts'), 'first-use-from-local-artifacts');
});

test('duplicate headings get numbered anchors and fenced headings are ignored', () => {
  const anchors = anchorsOf('# Notes\n## Notes\n```\n# Not a heading\n```\n<a id="custom"></a>\n');
  assert.deepEqual([...anchors].sort(), ['custom', 'notes', 'notes-1']);
});

test('a consistent doc set has no problems', () => {
  assert.deepEqual(
    messages({
      'README.md': '[Guide](docs/guide.md#usage) and [image](docs/pic.png)\n',
      'docs/guide.md': '# Guide\n## Usage\nSee `docs/pic.png` and [back](../README.md).\n',
      'docs/pic.png': 'x',
    }),
    [],
  );
});

test('reports a broken relative link', () => {
  assert.deepEqual(messages({ 'README.md': 'See [gone](docs/gone.md).\n' }), [
    'README.md:1: broken link docs/gone.md',
  ]);
});

test('resolves links relative to the linking file', () => {
  assert.deepEqual(
    messages({
      'README.md': '[a](docs/a/x.md)\n',
      'docs/a/x.md': '[sample](../examples/s.html)\n',
      'examples/s.html': '',
    }),
    ['docs/a/x.md:1: broken link ../examples/s.html'],
  );
});

test('reports a missing anchor', () => {
  assert.deepEqual(
    messages({ 'README.md': '[x](docs/g.md#nope)\n', 'docs/g.md': '# Guide\n' }),
    ['README.md:1: missing anchor #nope in docs/g.md'],
  );
});

test('reports a doc that README does not reach', () => {
  assert.deepEqual(
    messages({ 'README.md': '# Readme\n', 'docs/orphan.md': '# Orphan\n' }),
    ['docs/orphan.md:1: not reachable from README.md through relative links'],
  );
});

test('reports a named path that does not exist', () => {
  assert.deepEqual(
    messages({ 'README.md': 'Run `scripts/missing.ts` then `npm test`.\n' }),
    ['README.md:1: named path does not exist: scripts/missing.ts'],
  );
});

test('ignores links and paths inside fenced code, and external links', () => {
  assert.deepEqual(
    messages({
      'README.md': '```\n[x](docs/none.md) `scripts/none.ts`\n```\n[site](https://example.com/a.md)\n',
    }),
    [],
  );
});

test('checks badge, image, titled and reference-style links', () => {
  assert.deepEqual(
    messages({
      'README.md':
        "[![b](docs/b.png)](docs/gone.md)\n![i](docs/none.png)\n[t](docs/t.md 'Title')\n[r]: docs/ref.md\n[^1]: A footnote\n",
      'docs/b.png': '',
    }),
    [
      'README.md:1: broken link docs/gone.md',
      'README.md:2: broken link docs/none.png',
      'README.md:3: broken link docs/t.md',
      'README.md:4: broken link docs/ref.md',
    ],
  );
});

test('reports links that leave the repository or cannot be decoded', () => {
  assert.deepEqual(messages({ 'README.md': '[up](../outside.md)\n[bad](docs/%E0%A4%A.md)\n' }), [
    'README.md:1: link leaves the repository: ../outside.md',
    'README.md:2: malformed link docs/%E0%A4%A.md',
  ]);
});

test('matches HTML anchors without regard to case', () => {
  assert.deepEqual(messages({ 'README.md': '<a id="Foo"></a>\n[x](#foo)\n' }), []);
});

test('allows git-ignored build outputs as named paths', () => {
  const root = repo({ 'README.md': 'Publishes `site/dist` but not `site/missing`.\n', 'site/.gitignore': 'dist/\n' });
  execFileSync('git', ['init', '-q'], { cwd: root });
  assert.deepEqual(
    checkDocs(root, ['README.md']).map((p) => p.message),
    ['named path does not exist: site/missing'],
  );
});

test('only plain repository paths count as named paths', () => {
  assert.equal(namedPath('scripts/check-docs.ts'), 'scripts/check-docs.ts');
  assert.equal(namedPath('docs/'), 'docs');
  assert.equal(namedPath('tests/*Tests.cs'), null);
  assert.equal(namedPath('src/<name>.cs'), null);
  assert.equal(namedPath('dotnet test'), null);
  assert.equal(namedPath('README.md'), null);
  assert.equal(namedPath('https://example.com/docs/x'), null);
});

test('the repository docs pass', () => {
  const root = join(import.meta.dirname, '..');
  assert.deepEqual(checkDocs(root, trackedDocs(root)), []);
});
