import assert from 'node:assert/strict';
import { test } from 'node:test';
import { runProcess } from './node_cli_process.mjs';

const child = (source, options = {}) => runProcess({
  executable: process.execPath, args: ['-e', source], timeoutMs: 3000, ...options,
});
const reaped = (error, code) => {
  assert.equal(error.code, code);
  assert.ok(error.pid > 0);
  assert.throws(() => process.kill(error.pid, 0), { code: 'ESRCH' });
  return true;
};

test('UTF-8 and arbitrary binary stdout remain exact bytes; stderr stays separate', async () => {
  const input = Buffer.from('报告 UTF-8 café\n');
  const result = await child("process.stdin.pipe(process.stdout); process.stderr.write('diagnostic');", { input });
  assert.deepEqual(result.stdout, input);
  assert.equal(result.stderr.toString(), 'diagnostic');
  assert.equal(result.exitCode, 0);
  const binary = await child('process.stdout.write(Buffer.from([0, 255, 128, 13, 10]));');
  assert.deepEqual(binary.stdout, Buffer.from([0, 255, 128, 13, 10]));
});

test('argv is passed literally without shell expansion', async () => {
  const literal = 'font with spaces; $(echo unwanted) & 中文.ttf';
  const result = await child('process.stdout.write(process.argv[1]);', {
    args: ['-e', 'process.stdout.write(process.argv[1]);', literal],
  });
  assert.equal(result.stdout.toString(), literal);
});

test('nonzero exit retains code and diagnostic, without inventing a success', async () => {
  const result = await child("process.stderr.write('FPDF1102: unsupported'); process.exitCode = 3;");
  assert.equal(result.exitCode, 3);
  assert.equal(result.stdout.length, 0);
  assert.equal(result.stderr.toString(), 'FPDF1102: unsupported');
});

test('missing executable rejects with the actual spawn error', async () => {
  await assert.rejects(runProcess({ executable: '/missing/factspdf-node-probe-cli' }), { code: 'ENOENT' });
});

test('timeout kills and reaps the child before rejecting', async () => {
  await assert.rejects(child('setInterval(() => {}, 1000);', { timeoutMs: 150 }), error => reaped(error, 'TIMEOUT'));
});

test('running cancellation kills and reaps the child before rejecting', async () => {
  const controller = new AbortController();
  const pending = child('setInterval(() => {}, 1000);', { signal: controller.signal });
  const timer = setTimeout(() => controller.abort(), 150);
  try { await assert.rejects(pending, error => reaped(error, 'ABORTED')); }
  finally { clearTimeout(timer); }
});

test('pre-cancellation never starts even a missing executable', async () => {
  const controller = new AbortController(); controller.abort();
  await assert.rejects(runProcess({ executable: '/missing/cli', signal: controller.signal }), error => {
    assert.equal(error.code, 'ABORTED'); assert.equal(error.pid, undefined); return true;
  });
});

for (const stream of ['stdout', 'stderr']) {
  test(`${stream} byte limit stops and reaps a noisy child`, async () => {
    await assert.rejects(child(`setInterval(() => process.${stream}.write(Buffer.alloc(4096)), 1);`, {
      maxStdoutBytes: 1024, maxStderrBytes: 1024,
    }), error => reaped(error, 'OUTPUT_LIMIT'));
  });
}

test('early stdin closure with nonzero exit is reported without an unhandled EPIPE', async () => {
  const result = await child('process.exit(4);', { input: Buffer.alloc(4 * 1024 * 1024) });
  assert.equal(result.exitCode, 4);
});

test('parallel calls have independent input, output and cancellation state', async () => {
  const inputs = [Buffer.from('first 中文'), Buffer.from('second café')];
  const results = await Promise.all(inputs.map(input => child('process.stdin.pipe(process.stdout);', { input })));
  results.forEach((result, i) => assert.deepEqual(result.stdout, inputs[i]));
  assert.notEqual(results[0].pid, results[1].pid);
});

test('invalid limits fail before starting a process', async () => {
  for (const timeoutMs of [0, -1, NaN, Infinity, 2 ** 31]) {
    await assert.rejects(child('', { timeoutMs }), { name: 'RangeError' });
  }
});
