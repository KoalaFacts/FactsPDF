// Work package B1: a reproducible probe of the existing CLI, not a Node SDK.
import assert from 'node:assert/strict';
import { after, before, test } from 'node:test';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { mkdtemp, readFile, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { isAbsolute, join } from 'node:path';
import { createHash } from 'node:crypto';
import { runProcess } from './node_cli_process.mjs';

const direct = promisify(execFile);
const executable = process.env.FACTSPDF_CLI;
const dll = process.env.FACTSPDF_CLI_DLL;
const prefix = dll ? [dll] : [];
const fonts = JSON.parse(process.env.FACTSPDF_PROBE_FONTS ?? '[]');
const qpdf = process.env.FACTSPDF_QPDF ?? 'qpdf';
const pdftotext = process.env.FACTSPDF_PDFTOTEXT ?? 'pdftotext';
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const ascii = Buffer.from('<html><body><p>Node CLI probe 2026.</p></body></html>', 'utf8');
const unicodeText = '报告 探针 café';
const unicode = Buffer.from(`<html><body><p>${unicodeText}</p></body></html>`, 'utf8');
const fontArgs = () => fonts.flatMap(font => ['--font', font]);
const invoke = (input, options = [], args = ['-', '-']) => runProcess({
  executable, args: [...prefix, ...args, ...options], input,
});
let directory;

before(async () => {
  assert.ok(executable && isAbsolute(executable), 'Set FACTSPDF_CLI to an absolute executable path');
  assert.ok(!dll || isAbsolute(dll), 'FACTSPDF_CLI_DLL must be absolute when using dotnet');
  assert.ok(Array.isArray(fonts) && fonts.length === 2 && fonts.every(isAbsolute),
    'Set FACTSPDF_PROBE_FONTS to a JSON array of explicit Latin and Chinese static TrueType paths');
  directory = await mkdtemp(join(tmpdir(), 'factspdf Node probe '));
  const entries = await Promise.all([executable, ...(dll ? [dll] : []), ...fonts].map(async path => ({
    path, sha256: hash(await readFile(path)),
  })));
  console.log(JSON.stringify({ probe: 'B1-node-cli', node: process.version, platform: process.platform,
    arch: process.arch, invocation: dll ? 'dotnet-managed-dll' : 'direct-executable', inputs: entries }));
});
after(async () => { if (directory) await rm(directory, { recursive: true, force: true }); });

async function inspect(bytes, name, expectedText) {
  const path = join(directory, name);
  await writeFile(path, bytes);
  await direct(qpdf, ['--check', path], { timeout: 10000 });
  const { stdout } = await direct(pdftotext, ['-raw', path, '-'], { timeout: 10000, encoding: 'utf8' });
  assert.equal(stdout.replace(/\s+/gu, ' ').trim(), expectedText);
  console.log(JSON.stringify({ document: name, bytes: bytes.length, sha256: hash(bytes),
    structure: 'qpdf-pass', text: 'exact-after-layout-whitespace-normalization' }));
}

test('ASCII stdin/stdout PDF bytes match direct CLI file invocation', async () => {
  const result = await invoke(ascii);
  assert.equal(result.exitCode, 0, result.stderr.toString());
  assert.equal(result.stdout.subarray(0, 5).toString('ascii'), '%PDF-');
  assert.match(result.stderr.toString(), /Wrote 1 page\(s\)/);
  const inputPath = join(directory, 'source with spaces.html');
  const outputPath = join(directory, 'direct file.pdf');
  await writeFile(inputPath, ascii);
  await direct(executable, [...prefix, inputPath, outputPath], { timeout: 10000 });
  assert.deepEqual(result.stdout, await readFile(outputPath));
  await inspect(result.stdout, 'node-ascii.pdf', 'Node CLI probe 2026.');
});

test('explicit ordered fonts preserve Chinese/Latin UTF-8 and searchable PDF text', async () => {
  const options = [...fontArgs(), '--subset-fonts'];
  const result = await invoke(unicode, options);
  assert.equal(result.exitCode, 0, result.stderr.toString());
  const inputPath = join(directory, '报告 source.html');
  const outputPath = join(directory, '报告 direct.pdf');
  await writeFile(inputPath, unicode);
  await direct(executable, [...prefix, inputPath, outputPath, ...options], { timeout: 10000 });
  assert.deepEqual(result.stdout, await readFile(outputPath));
  await inspect(result.stdout, 'node-unicode.pdf', unicodeText);
});

test('malformed UTF-8 fails without PDF bytes', async () => {
  const result = await invoke(Buffer.from([0xc3, 0x28]));
  assert.equal(result.exitCode, 3);
  assert.equal(result.stdout.length, 0);
  assert.match(result.stderr.toString(), /valid UTF-8/);
});

test('unsupported HTML reports its diagnostic and preserves an existing output file', async () => {
  const invalid = Buffer.from('<html><body><img src="explicit.jpg"></body></html>');
  const outputPath = join(directory, 'keep.pdf');
  await writeFile(outputPath, 'keep');
  const fileResult = await invoke(invalid, ['--overwrite'], ['-', outputPath]);
  assert.equal(fileResult.exitCode, 3);
  assert.match(fileResult.stderr.toString(), /FPDF1102/);
  assert.equal(await readFile(outputPath, 'utf8'), 'keep');
  const stdoutResult = await invoke(invalid);
  assert.equal(stdoutResult.exitCode, 3);
  assert.equal(stdoutResult.stdout.length, 0);
});

test('missing explicit font fails without automatic discovery or partial PDF', async () => {
  const result = await invoke(unicode, ['--font', join(directory, 'absent.ttf')]);
  assert.equal(result.exitCode, 4);
  assert.equal(result.stdout.length, 0);
  assert.ok(result.stderr.length > 0);
});

test('usage errors remain exit 2 rather than a fabricated PDF success', async () => {
  const result = await invoke(ascii, ['--not-a-real-option']);
  assert.equal(result.exitCode, 2);
  assert.equal(result.stdout.length, 0);
  assert.match(result.stderr.toString(), /Usage:/);
});

test('two actual CLI conversions run concurrently with independent PDF bytes', async () => {
  const inputs = [ascii, Buffer.from('<html><body><p>Independent second task.</p></body></html>')];
  const results = await Promise.all(inputs.map(input => invoke(input)));
  assert.notEqual(results[0].pid, results[1].pid);
  assert.notDeepEqual(results[0].stdout, results[1].stdout);
  for (let i = 0; i < results.length; i++) {
    assert.equal(results[i].exitCode, 0, results[i].stderr.toString());
    const serial = await invoke(inputs[i]);
    assert.equal(serial.exitCode, 0, serial.stderr.toString());
    assert.deepEqual(results[i].stdout, serial.stdout);
  }
  await inspect(results[1].stdout, 'node-parallel-second.pdf', 'Independent second task.');
});
