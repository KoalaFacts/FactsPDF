import assert from 'node:assert/strict';
import { test } from 'node:test';
import { classifyDifference, classifyDifferences, compareRuns, type Difference, exitCodeOf, failures, optionSets, parseArguments, type RunResult, trailerKinds, workParent } from './parity.ts';

const pdf = (text: string) => Buffer.from(text, 'latin1');
const ok = (bytes: string): RunResult => ({ exitCode: 0, stderr: '', pdf: pdf(bytes) });

test('identical runs compare equal', () => {
  assert.equal(compareRuns(ok('%PDF-1.7 a'), ok('%PDF-1.7 a')), null);
  const rejected: RunResult = { exitCode: 3, stderr: 'FPDF1102 x\n', pdf: null };
  assert.equal(compareRuns(rejected, { ...rejected }), null);
});

test('reports a changed exit code first', () => {
  assert.equal(compareRuns(ok('a'), { exitCode: 3, stderr: 'e', pdf: null }), 'exit code 0 -> 3');
});

test('reports changed diagnostics', () => {
  const reason = compareRuns({ exitCode: 3, stderr: 'FPDF1102\n', pdf: null }, { exitCode: 3, stderr: 'FPDF1103\n', pdf: null });
  assert.match(reason ?? '', /^stderr differs/);
});

test('reports the first differing byte and both lengths', () => {
  assert.equal(compareRuns(ok('%PDF-1.7 abc'), ok('%PDF-1.7 abd')), 'PDF bytes differ at offset 11 (12 -> 12 bytes)');
  assert.equal(compareRuns(ok('%PDF'), ok('%PDF-1.7')), 'PDF bytes differ at offset 4 (4 -> 8 bytes)');
});

test('reports a PDF written by only one side', () => {
  assert.equal(compareRuns({ exitCode: 0, stderr: '', pdf: null }, ok('a')), 'only head wrote a PDF');
  assert.equal(compareRuns(ok('a'), { exitCode: 0, stderr: '', pdf: null }), 'only base wrote a PDF');
});

test('parses options and keeps defaults', () => {
  const options = parseArguments(['--base', 'abc123', '--jobs', '2']);
  assert.equal(options.base, 'abc123');
  assert.equal(options.jobs, 2);
  assert.equal(options.python, 'python3');
});

test('rejects unknown, valueless and invalid options', () => {
  assert.throws(() => parseArguments(['--nope', 'x']), /unknown option --nope/);
  assert.throws(() => parseArguments(['--base']), /expected --option value/);
  assert.throws(() => parseArguments(['--base', '--work']), /expected --option value/);
  assert.throws(() => parseArguments(['--jobs', '0']), /positive integer/);
  assert.throws(() => parseArguments(['main']), /expected --option value/);
});

test('uses the three documented option sets', () => {
  const sets = optionSets('/repo', '/fonts/DejaVuSans.ttf', '/img');
  assert.deepEqual(sets.map((set) => set.name), ['defaults', 'subset-letter', 'furniture-a5']);
  assert.deepEqual(sets[0].args, []);
  const letter = sets[1].args.join(' ');
  for (const part of ['--subset-fonts', '--page-numbers', '--header-text H', '--title T', '--page-size letter', '--landscape', '--margin 20mm', 'logo=/img/brand-mark.png']) {
    assert.ok(letter.includes(part), part);
  }
  const furniture = sets[2].args.join(' ');
  assert.ok(furniture.includes('--page-furniture /repo/examples/business-page-furniture.json'));
  assert.ok(furniture.includes('--page-width 148mm --page-height 210mm'));
  assert.ok(furniture.includes('logo=/img/brand-mark.png'), 'the furniture set renders image examples too');
  assert.ok(!furniture.includes('--subset-fonts'), 'the furniture set embeds the full font');
});

test('a signal or launch failure is an error, not a comparable exit code', () => {
  assert.equal(exitCodeOf(null), 0);
  assert.equal(exitCodeOf({ code: 3, signal: null }), 3);
  assert.throws(() => exitCodeOf({ code: 1, signal: 'SIGKILL' }), /signal SIGKILL/);
  assert.throws(() => exitCodeOf({ code: null, signal: 'SIGTERM' }), /did not run to completion \(signal SIGTERM\)/);
  assert.throws(() => exitCodeOf({ code: 'ENOENT' }), /did not run to completion \(error ENOENT\)/);
});

test('refuses a work folder inside the repository', () => {
  assert.throws(() => workParent('/repo', '/repo'), /outside the repository/);
  assert.throws(() => workParent('/repo', '/repo/artifacts'), /outside the repository/);
  assert.equal(workParent('/repo', '/tmp/parity'), '/tmp/parity');
  assert.equal(workParent('/repo', '/repository-sibling'), '/repository-sibling');
  assert.throws(() => workParent('/repo', '/repo/..cache'), /outside the repository/);
});

const failed = (stderr: string): RunResult => ({ exitCode: 3, stderr, pdf: null });
const message = 'FPDF1301: U+4E2D requires an explicitly supplied font.';
const hint = 'Hint: Supply a font with --font path.ttf.';

test('an inserted location line in a failing run is location-added', () => {
  assert.equal(classifyDifference(failed(`${message}\n${hint}\n`), failed(`${message}\nLocation: line 4, column 1.\n${hint}\n`)), 'location-added');
  assert.equal(classifyDifference(failed(`${message}\n`), failed(`${message}\nLocation: line 12, column 30.\n`)), 'location-added');
});

test('anything beyond an inserted location line is no known kind', () => {
  const base = failed(`${message}\n`);
  // A changed or moved location, a changed message, an extra non-location line.
  assert.equal(classifyDifference(failed(`${message}\nLocation: line 1, column 1.\n`), failed(`${message}\nLocation: line 2, column 1.\n`)), null);
  assert.equal(classifyDifference(base, failed('FPDF1302: other.\nLocation: line 1, column 1.\n')), null);
  assert.equal(classifyDifference(base, failed(`${message}\nLocation: line 1, column 1.\nHint: new.\n`)), null);
  assert.equal(classifyDifference(base, failed(`${message}\nLocation: line one.\n`)), null);
  // Changed exit codes, successful runs and written PDFs are never accepted.
  assert.equal(classifyDifference(base, { ...failed(`${message}\nLocation: line 1, column 1.\n`), exitCode: 4 }), null);
  assert.equal(classifyDifference({ exitCode: 0, stderr: '', pdf: null }, { exitCode: 0, stderr: 'Location: line 1, column 1.', pdf: null }), null);
  assert.equal(classifyDifference({ ...base, pdf: pdf('a') }, { ...failed(`${message}\nLocation: line 1, column 1.\n`), pdf: pdf('a') }), null);
});

test('declared kinds accept only their own differences and must be observed', () => {
  const located: Difference = { example: 'a.html', set: 'defaults', reason: 'stderr differs', kind: 'location-added' };
  const other: Difference = { example: 'b.html', set: 'defaults', reason: 'PDF bytes differ', kind: null };
  assert.deepEqual(failures([located], ['location-added']), { unexpected: [], unobserved: [] });
  assert.deepEqual(failures([located], []), { unexpected: [located], unobserved: [] });
  assert.deepEqual(failures([located, other], ['location-added']), { unexpected: [other], unobserved: [] });
  assert.deepEqual(failures([], ['location-added']), { unexpected: [], unobserved: ['location-added'] });
  assert.deepEqual(failures([], []), { unexpected: [], unobserved: [] });
  const added: Difference = { example: 'new.html', set: 'defaults', reason: 'exit code 3 -> 0', kind: 'new-example-rendered' };
  assert.deepEqual(failures([added], ['new-example-rendered']), { unexpected: [], unobserved: [] });
  assert.deepEqual(failures([added], []), { unexpected: [added], unobserved: [] });
});

test('new example can be declared only for genuine newly rendered PDF', () => {
  const old = { exitCode: 3, stderr: 'FPDF1201: unsupported\\n', pdf: null };
  const fresh = ok('%PDF-1.7 new');
  assert.equal(classifyDifference(old, fresh, true), 'new-example-rendered');
  assert.equal(classifyDifference(old, fresh), null, 'existing examples must still be identical');
  assert.equal(classifyDifference(old, { ...fresh, stderr: 'warning' }, true), 'new-example-rendered',
    'a successful converter may issue diagnostics alongside valid output');
  assert.equal(classifyDifference(old, { ...fresh, pdf: pdf('not a PDF') }, true), null);
  assert.equal(classifyDifference(old, { ...fresh, pdf: null }, true), null);
  assert.equal(classifyDifference({ ...old, exitCode: 0 }, fresh, true), null);
  assert.equal(classifyDifference({ ...old, pdf: pdf('previous PDF') }, fresh, true), null);
});

test('th-default-centred accepts only rendered PDFs of existing examples with a th', () => {
  const before = ok('%PDF-1.7 left');
  const after = ok('%PDF-1.7 centred');
  assert.equal(classifyDifference(before, after, false, true), 'th-default-centred');
  assert.notEqual(classifyDifference(before, after), 'th-default-centred', 'an example without a th is never this kind');
  assert.equal(classifyDifference(before, after, true, true), null, 'a new example is not an existing one');
  assert.equal(classifyDifference(before, { ...after, exitCode: 3, pdf: null }, false, true), null);
  assert.equal(classifyDifference({ exitCode: 3, stderr: 'FPDF1507\n', pdf: null }, after, false, true), null);
  assert.equal(classifyDifference(before, ok('not a PDF'), false, true), null);
});

test('inline-replaced-baseline accepts only rendered PDFs of existing examples with an img or svg', () => {
  const before = ok('%PDF-1.7 block');
  const after = ok('%PDF-1.7 baseline');
  assert.equal(classifyDifference(before, after, false, false, true), 'inline-replaced-baseline');
  assert.notEqual(classifyDifference(before, after), 'inline-replaced-baseline', 'an example without an image is never this kind');
  assert.equal(classifyDifference(before, after, true, false, true), null, 'a new example is not an existing one');
  assert.equal(classifyDifference(before, { ...after, exitCode: 3, pdf: null }, false, false, true), null);
});

test('list-default-margin explains rendered differences and decorated edges of existing examples with a list', () => {
  const before = ok('%PDF-1.7 flush');
  const after = ok('%PDF-1.7 spaced');
  const hasList = true;
  const kinds = (b: RunResult, a: RunResult, newExample = false, hasTh = false, list = hasList, block = false) =>
    classifyDifferences(b, a, newExample, hasTh, false, list, block);
  assert.ok(kinds(before, after).includes('list-default-margin'));
  assert.ok(kinds(before, after, false, true).includes('list-default-margin'), 'a table header on the same page does not hide the list');
  assert.ok(!kinds(before, after, false, false, false).includes('list-default-margin'), 'an example without a list is never this kind');
  assert.deepEqual(kinds(before, after, true), [], 'a new example is not an existing one');
  assert.ok(!kinds(before, { ...after, exitCode: 3, pdf: null }).includes('list-default-margin'));
  assert.deepEqual(kinds(before, { ...after, stderr: 'warning\n' }), [], 'changed diagnostics are not a layout difference');
  assert.deepEqual(kinds(before, ok('not a PDF')), []);
  const failed = { exitCode: 3, stderr: 'FPDF1302: Decorated box fragment exceeds the usable page area.\n', pdf: null };
  assert.ok(kinds(before, failed).includes('list-default-margin'));
});

test('block-margin-collapse explains rendered differences of existing examples with block containers', () => {
  const before = ok('%PDF-1.7 added');
  const after = ok('%PDF-1.7 collapsed');
  const kinds = (b: RunResult, a: RunResult, newExample = false, hasTh = false, list = false, block = true) =>
    classifyDifferences(b, a, newExample, hasTh, false, list, block);
  assert.ok(kinds(before, after).includes('block-margin-collapse'));
  assert.ok(kinds(before, after, false, true).includes('block-margin-collapse'), 'a table header on the same page does not hide the panel');
  assert.ok(kinds(before, after, false, false, true).includes('list-default-margin'), 'a list and panels explain it either way');
  assert.ok(!kinds(before, after, false, false, false, false).includes('block-margin-collapse'), 'an example without blocks is never this kind');
  assert.deepEqual(kinds(before, after, true), [], 'a new example is not an existing one');
  assert.ok(!kinds(before, { ...after, exitCode: 3, pdf: null }).includes('block-margin-collapse'));
  const failed = { exitCode: 3, stderr: 'FPDF1302: Decorated box fragment exceeds the usable page area.\n', pdf: null };
  assert.ok(kinds(failed, before).includes('block-margin-collapse'));
  assert.deepEqual(kinds({ ...failed, stderr: 'FPDF1302: Line height exceeds the usable page height.\n' }, before), []);
});

test('title-written explains a PDF that only gained the document title', () => {
  const before = ok('%PDF-1.7 a');
  const after = ok('%PDF-1.7 a /Title <FEFF0054>');
  const kinds = (b: RunResult, a: RunResult, newExample = false, hasTitle = true) =>
    classifyDifferences(b, a, newExample, false, false, false, false, hasTitle);
  assert.equal(kinds(before, after)[0], 'title-written');
  assert.ok(!kinds(before, after, false, false).includes('title-written'), 'an example without a title element is never this kind');
  assert.ok(!kinds(before, after, true).includes('title-written'), 'a new example is not an existing one');
  assert.ok(!kinds(after, before).includes('title-written'), 'losing a title is not this kind');
  assert.ok(!kinds(before, ok('%PDF-1.7 b')).includes('title-written'), 'a difference without a new /Title is not this kind');
  assert.ok(!kinds(before, { ...after, stderr: 'warning\n' }).includes('title-written'), 'changed diagnostics are not this kind');
});

test('a broad kind never hides a narrower one declared on its own', () => {
  // A page with a th and a div that moved its header text: a later change declaring only
  // th-default-centred must accept it, although the margin kinds also explain it.
  const before = ok('%PDF-1.7 left');
  const after = ok('%PDF-1.7 centred');
  const kinds = classifyDifferences(before, after, false, true, false, false, true);
  assert.equal(kinds[0], 'th-default-centred');
  assert.ok(kinds.includes('block-margin-collapse'));
  const difference: Difference = { example: 'x.html', set: 'defaults', reason: 'bytes', kind: kinds[0], kinds };
  assert.deepEqual(failures([difference], ['th-default-centred']), { unexpected: [], unobserved: [] });
  assert.deepEqual(failures([difference], ['block-margin-collapse']), { unexpected: [], unobserved: [] });
  assert.equal(failures([difference], ['location-added']).unexpected.length, 1, 'an unrelated kind still fails it');
});

test('a page count in the Wrote line and a moved decorated bottom edge are the orphans/widows kind', () => {
  const wrote = (pages: number, bytes: number, body: string) => ({ ...ok(body), stderr: `Wrote ${pages} page(s), ${bytes} bytes.\r\n` });
  assert.equal(classifyDifference(wrote(3, 10, '%PDF-1.7 a'), wrote(4, 12, '%PDF-1.7 b')), 'orphans-widows-initial-2');
  const failed = { exitCode: 3, stderr: 'FPDF1302: Decorated box fragment exceeds the usable page area.\nLocation: line 1, column 1.\n', pdf: null };
  assert.equal(classifyDifference(ok('%PDF-1.7 a'), failed), 'orphans-widows-initial-2');
  assert.equal(classifyDifference(failed, ok('%PDF-1.7 a')), 'orphans-widows-initial-2');
  assert.equal(classifyDifference(ok('%PDF-1.7 a'), { ...failed, stderr: 'FPDF1302: Line height exceeds the usable page height.\n' }), null);
});

test('reads Parity-Expect trailers and rejects unknown kinds', () => {
  assert.deepEqual(trailerKinds('Subject\n\nBody.\n\nParity-Expect: location-added\nCo-Authored-By: x\n'), ['location-added']);
  assert.deepEqual(trailerKinds('Subject\n\nParity-Expect: location-added\nParity-Expect: location-added\n'), ['location-added']);
  assert.deepEqual(trailerKinds('Subject\n\nParity-Expect: new-example-rendered\n'), ['new-example-rendered']);
  assert.deepEqual(trailerKinds('Subject mentions Parity-Expect: location-added inline\n'), []);
  assert.throws(() => trailerKinds('Parity-Expect: anything\n'), /unknown expected difference kind "anything"/);
});

test('parses --expect and --expect-from', () => {
  const options = parseArguments(['--expect', 'location-added', '--expect', 'location-added', '--expect-from', 'HEAD^2']);
  assert.deepEqual(options.expect, ['location-added']);
  assert.equal(options.expectFrom, 'HEAD^2');
  assert.deepEqual(parseArguments([]).expect, []);
  assert.equal(parseArguments([]).expectFrom, null);
  assert.throws(() => parseArguments(['--expect', 'bytes']), /unknown expected difference kind/);
});

test('a changed rendered page is the declared orphans/widows initial-value kind', () => {
  assert.equal(classifyDifference(ok('%PDF-1.7 a'), ok('%PDF-1.7 b')), 'orphans-widows-initial-2');
  assert.equal(classifyDifference(ok('a'), { ...ok('b'), stderr: 'warning' }), null, 'diagnostics must not change');
  assert.equal(classifyDifference(ok('a'), { exitCode: 0, stderr: '', pdf: null }), null);
  assert.equal(classifyDifference(ok('a'), { ...ok('b'), exitCode: 3 }), null);
  const moved: Difference = { example: 'a.html', set: 'defaults', reason: 'PDF bytes differ', kind: 'orphans-widows-initial-2' };
  assert.deepEqual(failures([moved], []), { unexpected: [moved], unobserved: [] });
  assert.deepEqual(failures([moved], ['orphans-widows-initial-2']), { unexpected: [], unobserved: [] });
});
