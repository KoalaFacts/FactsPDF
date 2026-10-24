import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { createHash } from 'node:crypto';
import { withDeadline } from './deadline.mjs';

const direct = promisify(execFile);
const hash = bytes => createHash('sha256').update(bytes).digest('hex');

export async function inspectTableRichCells({ page, output, executable, dll }) {
  const root = resolve('artifacts/wasm-evidence/table-rich/fixtures');
  const manifest = JSON.parse(await readFile(join(root, 'table-rich.json'), 'utf8'));
  const imagePath = join(root, 'sample.png');
  const imageBytes = [...await readFile(imagePath)];
  const results = [];
  const call = html => withDeadline(page.evaluate(({ html, bytes }) =>
    window.factspdfProbe.convertImages(html, {}, [{ name: 'sample', bytes: Uint8Array.from(bytes) }], true),
  { html, bytes: imageBytes }), 30000, 'Rich-table WASM conversion');
  let recoveryHash;
  for (const item of manifest.cases) {
    const source = join(output, `table-rich-${item.name}.html`);
    const browserPath = join(output, `table-rich-${item.name}-wasm.pdf`);
    const cliPath = join(output, `table-rich-${item.name}-cli.pdf`);
    const result = await call(item.html);
    assert.equal(result.errorCode, null, JSON.stringify(result));
    assert.equal(result.pageCount, item.pages);
    const bytes = Buffer.from(result.pdfBase64, 'base64');
    await writeFile(browserPath, bytes);
    await writeFile(source, item.html);
    await direct(executable, [...(dll ? [dll] : []), source, cliPath, '--overwrite', '--subset-fonts',
      '--image', `sample=${imagePath}`], { timeout: 90000 });
    const cliBytes = await readFile(cliPath);
    assert.deepEqual(bytes, cliBytes, 'Rich-table WASM and CLI output differ');
    const python = 'import json,sys; from pathlib import Path; from scripts.verify_table_rich_cells import inspect; ' +
      'print(json.dumps(inspect(Path(sys.argv[1]),json.loads(sys.argv[2]),json.loads(sys.argv[3]),qpdf="qpdf")))';
    const inspected = JSON.parse((await direct('/usr/bin/python3',
      ['-c', python, browserPath, JSON.stringify(item), JSON.stringify(manifest.image)], { timeout: 90000 })).stdout);
    results.push({ id: `table-rich-${item.name}`, byteEquality: true, cliSha256: hash(cliBytes), ...inspected });
    if (item.name === 'rich-left') recoveryHash = hash(bytes);
  }
  const invalid = manifest.errors.find(item => item.name === 'overridden-illegal-p');
  const failure = await call(invalid.html);
  assert.equal(failure.errorCode, invalid.code, JSON.stringify(failure));
  assert.equal(failure.pdfBase64, null);
  const recovery = await call(manifest.cases.find(item => item.name === 'rich-left').html);
  assert.equal(recovery.errorCode, null);
  assert.equal(recovery.pageCount, 1);
  assert.equal(hash(Buffer.from(recovery.pdfBase64, 'base64')), recoveryHash, 'Browser failure affected subsequent output');
  results.push({ id: 'table-rich-negative-recovery', diagnostic: invalid.code,
    recoveryByteEquality: true, referenceApproval: manifest.referenceApproval });
  return results;
}
