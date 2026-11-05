import assert from 'node:assert/strict';
import { test } from 'node:test';
import { compareRuns, exitCodeOf, optionSets, parseArguments, type RunResult, workParent } from './parity.ts';

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
