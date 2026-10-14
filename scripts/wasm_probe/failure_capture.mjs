// Run the actual protected browser, then force a normal downstream CLI failure.
// This verifies evidence collection without adding fault injection to the engine/host.
import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const output = join(process.env.FACTSPDF_PROBE_OUTPUT, 'expected-failure');
await assert.rejects(promisify(execFile)(process.execPath, [fileURLToPath(new URL('./run.mjs', import.meta.url))], {
  env: { ...process.env, FACTSPDF_CLI: '/usr/bin/false', FACTSPDF_CLI_DLL: '', FACTSPDF_PROBE_OUTPUT: output },
  timeout: 120000,
}), error => error.code === 1 && error.killed !== true);
const report = JSON.parse(await readFile(join(output, 'report.json'), 'utf8'));
assert.equal(report.status, 'failed');
assert.match(report.failure, /Command failed: \/usr\/bin\/false/);
assert.equal(report.resourceTimingCapture.status, 'captured');
assert.ok(report.resources.some(entry => entry.name.endsWith('.wasm') && entry.decodedBodySize > 0));
assert.ok(report.resources.some(entry => entry.name === '/probe-inputs/ascii.html'));
assert.ok(report.browser.renderers.length > 0);
assert.equal((await readFile(join(output, 'ascii-wasm.pdf'))).subarray(0, 5).toString(), '%PDF-');
assert.equal(report.source.head, process.env.FACTSPDF_EXPECTED_SHA);
console.log(JSON.stringify({ status: 'expected-failure-evidence-passed', resources: report.resources.length, report: join(output, 'report.json') }));
