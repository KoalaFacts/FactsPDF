import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { createHash } from 'node:crypto';
import { withDeadline } from './deadline.mjs';

const direct = promisify(execFile);
const hash = bytes => createHash('sha256').update(bytes).digest('hex');

// A4 fixtures only. The public .NET API probe owns custom 240x100pt options.
export async function inspectTableRowFragments({ page, output, executable, dll }) {
  const root = resolve('artifacts/wasm-evidence/table-row-fragments/fixtures');
  const manifest = JSON.parse(await readFile(join(root, 'table-row-fragments.json'), 'utf8'));
  const imagePath = join(root, 'sample.png');
  const imageBytes = [...await readFile(imagePath)];
  const call = html => withDeadline(page.evaluate(({ html, bytes }) =>
    window.factspdfProbe.convertImages(html, {}, [{ name: 'sample', bytes: Uint8Array.from(bytes) }], true),
  { html, bytes: imageBytes }), 30000, 'Continued-table WASM conversion');
  const results = [];
  let recoveryHash;
  for (const item of manifest.cases.filter(item => item.entry === 'cli')) {
    assert.equal(Object.keys(item.options).length, 0, 'Browser continuation fixture must use A4 defaults');
    const source = join(output, `table-row-fragments-${item.name}.html`);
    const browserPath = join(output, `table-row-fragments-${item.name}-wasm.pdf`);
    const cliPath = join(output, `table-row-fragments-${item.name}-cli.pdf`);
    const result = await call(item.html);
    assert.equal(result.errorCode, null, JSON.stringify(result));
    assert.equal(result.pageCount, item.pages);
    const bytes = Buffer.from(result.pdfBase64, 'base64');
    await writeFile(browserPath, bytes);
    await writeFile(source, item.html);
    await direct(executable, [...(dll ? [dll] : []), source, cliPath, '--overwrite', '--subset-fonts',
      '--image', `sample=${imagePath}`], { timeout: 90000 });
    const cliBytes = await readFile(cliPath);
    assert.deepEqual(bytes, cliBytes, 'Continued-table WASM/CLI bytes differ');
    const python = 'import json,sys; from pathlib import Path; from scripts.verify_table_row_fragments import inspect; ' +
      'print(json.dumps(inspect(Path(sys.argv[1]),json.loads(sys.argv[2]),json.loads(sys.argv[3]),qpdf="qpdf")))';
    const inspected = JSON.parse((await direct('/usr/bin/python3',
      ['-c', python, browserPath, JSON.stringify(item), JSON.stringify(manifest.image)], { timeout: 90000 })).stdout);
    results.push({ id: `table-row-fragments-${item.name}`, byteEquality: true, cliSha256: hash(cliBytes), ...inspected });
    if (item.name === 'A1') recoveryHash = hash(bytes);
  }
  const invalid = manifest.errors.find(item => item.name === 'a4-atomic-line');
  const failure = await call(invalid.html);
  assert.equal(failure.errorCode, invalid.code, JSON.stringify(failure));
  assert.equal(failure.pdfBase64, null);
  const recovery = await call(manifest.cases.find(item => item.name === 'A1').html);
  assert.equal(recovery.errorCode, null);
  assert.equal(recovery.pageCount, 3);
  assert.equal(hash(Buffer.from(recovery.pdfBase64, 'base64')), recoveryHash, 'Browser failure changed subsequent output');
  results.push({ id: 'table-row-fragments-atomic-negative-recovery', diagnostic: invalid.code,
    recoveryByteEquality: true, referenceApproval: manifest.referenceApproval });
  return results;
}
