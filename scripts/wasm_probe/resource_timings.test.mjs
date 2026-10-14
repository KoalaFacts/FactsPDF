import assert from 'node:assert/strict';
import { test } from 'node:test';
import { performance } from 'node:perf_hooks';
import { serveStatic } from './static_server.mjs';
import { captureResourceTimings } from './resource_timings.mjs';

test('retains completed fetch timings when later verification fails', async () => {
  const server = await serveStatic(new Map([['/timing-fixture', { bytes: Buffer.from('loaded'), type: 'text/plain' }]]));
  const report = {};
  try {
    try {
      await (await fetch(`${server.origin}/timing-fixture`)).arrayBuffer();
      // Node publishes fetch timing entries on the next event-loop turn.
      await new Promise(setImmediate);
      throw new Error('Conversion verification failed');
    } catch (error) { report.failure = error.message; }
    finally {
      report.capture = await captureResourceTimings(async () => performance.getEntriesByType('resource')
        .filter(entry => entry.name === `${server.origin}/timing-fixture`).map(entry => entry.toJSON()));
    }
    assert.equal(report.failure, 'Conversion verification failed');
    assert.equal(report.capture.status, 'captured');
    assert.equal(report.capture.entries.length, 1);
    assert.equal(report.capture.entries[0].name, `${server.origin}/timing-fixture`);
    assert.ok(Number.isFinite(report.capture.entries[0].duration));
  } finally { await server.close(); }
});

test('a missing or closed page is unavailable, not an empty successful measurement', async () => {
  assert.deepEqual(await captureResourceTimings(null), { status: 'unavailable', reason: 'Browser page was not created', entries: null });
  const result = await captureResourceTimings(async () => { throw new Error('Page closed'); });
  assert.equal(result.status, 'unavailable');
  assert.match(result.reason, /Page closed/);
  assert.equal(result.entries, null);
});

test('an unresponsive page cannot stall evidence cleanup', async () => {
  const result = await captureResourceTimings(() => new Promise(() => {}), 20);
  assert.equal(result.status, 'unavailable');
  assert.match(result.reason, /exceeded 20 ms/);
  assert.equal(result.entries, null);
});
