import assert from 'node:assert/strict';
import { test } from 'node:test';
import { withDeadline } from './deadline.mjs';

test('a stalled operation rejects with a named deadline failure', async () => {
  await assert.rejects(withDeadline(new Promise(() => {}), 20, 'WASM conversion'),
    { code: 'B2_TIMEOUT', message: 'WASM conversion exceeded 20 ms' });
});
test('successful and failed operations keep their original results', async () => {
  assert.equal(await withDeadline(Promise.resolve(42), 1000, 'conversion'), 42);
  const failure = new Error('original failure');
  await assert.rejects(withDeadline(Promise.reject(failure), 1000, 'conversion'), error => error === failure);
});
