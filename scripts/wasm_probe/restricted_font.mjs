import assert from 'node:assert/strict';

// Negative fixture only: add the restrictive-embedding bit to a private copy.
// It is never used by a successful PDF and never saved/uploaded as an original font.
export function restrictedFont(original) {
  const bytes = Buffer.from(original);
  for (let i = 0; i < bytes.readUInt16BE(4); i++) {
    const record = 12 + i * 16;
    if (bytes.toString('ascii', record, record + 4) !== 'OS/2') continue;
    const offset = bytes.readUInt32BE(record + 8) + 8;
    const originalFsType = bytes.readUInt16BE(offset);
    assert.equal(originalFsType, 0, 'Negative fixture must start with an unrestricted verification font');
    bytes.writeUInt16BE(2, offset);
    return { bytes, offset, originalFsType, restrictedFsType: 2 };
  }
  throw new Error('OS/2 table absent');
}
