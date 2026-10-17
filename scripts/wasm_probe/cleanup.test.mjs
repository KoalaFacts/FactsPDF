import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { promisify } from 'node:util';
import { test } from 'node:test';

for (const mode of ['stalled', 'rejected', 'normal']) {
  test(`cleanup ${mode}: persists evidence and terminates despite a live handle`, async () => {
    const directory = await mkdtemp(join(tmpdir(), 'factspdf-cleanup-'));
    const reportPath = join(directory, 'report.json');
    const source = `
      import { writeFile } from 'node:fs/promises';
      import { finishBrowserCleanup } from ${JSON.stringify(new URL('./cleanup.mjs', import.meta.url).href)};
      const mode = ${JSON.stringify(mode)};
      // A real active handle keeps this child alive after a mere Promise.race rejection.
      const liveHandle = setInterval(() => {}, 1000);
      const report = { status: 'failed', failure: 'original conversion failure' };
      process.exitCode = 1;
      await finishBrowserCleanup(() => {
        if (mode === 'stalled') return new Promise(() => {});
        if (mode === 'rejected') throw new Error('close rejected');
        clearInterval(liveHandle);
      }, async () => {
        // Evidence must finish its asynchronous write before any forced exit.
        await new Promise(resolve => setTimeout(resolve, 25));
        await writeFile(${JSON.stringify(reportPath)}, JSON.stringify(report));
      }, report, 50);
    `;
    try {
      await assert.rejects(promisify(execFile)(process.execPath, ['--input-type=module', '-e', source],
        { timeout: 3000 }), error => error.code === 1 && !error.killed && error.signal == null);
      const report = JSON.parse(await readFile(reportPath, 'utf8'));
      assert.equal(report.failure, 'original conversion failure');
      assert.equal(report.status, 'failed');
      if (mode === 'stalled') assert.match(report.cleanupError, /Browser cleanup exceeded 50 ms/);
      else if (mode === 'rejected') assert.match(report.cleanupError, /close rejected/);
      else assert.equal(report.cleanupError, undefined);
    } finally { await rm(directory, { recursive: true, force: true }); }
  });
}
