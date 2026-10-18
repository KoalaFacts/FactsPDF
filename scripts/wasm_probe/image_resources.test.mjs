import assert from 'node:assert/strict';
import { test } from 'node:test';
import * as resources from '../../probes/browser-wasm/wwwroot/resources.mjs';

test('explicit image byte packer snapshots bytes and preserves ordinal names without fetching', () => {
  assert.equal(typeof resources.packImageResources, 'function');
  const oldFetch = globalThis.fetch;
  globalThis.fetch = () => { throw new Error('Image packer must not fetch'); };
  try {
    const bytes = Uint8Array.of(1, 2, 3);
    const result = resources.packImageResources([{ name: 'chart', bytes }, { name: 'Chart', bytes: Uint8Array.of(4) }]);
    bytes.fill(0);
    assert.deepEqual(result.imageBytes, Uint8Array.of(1, 2, 3, 4));
    assert.deepEqual(result.imageLengths, Int32Array.of(3, 1));
    assert.deepEqual(result.imageNames, ['chart', 'Chart']);
  } finally { globalThis.fetch = oldFetch; }
});

test('image packer rejects shape, names, duplicates and count before transport allocation', () => {
  assert.equal(typeof resources.packImageResources, 'function');
  for (const input of [null, {}, [{ name: 'a', bytes: [] }], [{ name: 'a', bytes: new Uint8Array() }],
    [{ name: '../a', bytes: Uint8Array.of(1) }], [{ name: '中', bytes: Uint8Array.of(1) }],
    [{ name: 'a', bytes: Uint8Array.of(1) }, { name: 'a', bytes: Uint8Array.of(2) }],
    Array.from({ length: 17 }, (_, i) => ({ name: `a${i}`, bytes: Uint8Array.of(1) })),
    [{ name: 'a'.repeat(129), bytes: Uint8Array.of(1) }]])
    assert.throws(() => resources.packImageResources(input), { code: 'B2_INPUT_IMAGES' });
});

test('image byte caps are checked before a packed buffer is created, including unused aliases', () => {
  assert.equal(typeof resources.packImageResources, 'function');
  const bytes = new Uint8Array(8_388_608);
  assert.equal(resources.packImageResources([{ name: 'a', bytes }]).imageBytes.length, bytes.length);
  assert.throws(() => resources.packImageResources([{ name: 'a', bytes: new Uint8Array(8_388_609) }]), { code: 'B2_RESOURCE_LIMIT' });
  const entries = Array.from({ length: 4 }, (_, i) => ({ name: `a${i}`, bytes }));
  assert.equal(resources.packImageResources(entries).imageBytes.length, 33_554_432);
  assert.throws(() => resources.packImageResources([...entries, { name: 'extra', bytes: Uint8Array.of(1) }]), { code: 'B2_RESOURCE_LIMIT' });
});
