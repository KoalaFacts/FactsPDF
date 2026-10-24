import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { withDeadline } from './deadline.mjs';

const direct = promisify(execFile);

export async function inspectTableImages({ page, output, executable, dll }) {
  const root = resolve('artifacts/wasm-evidence/table-images/fixtures');
  const manifest = JSON.parse(await readFile(join(root, 'table-images.json'), 'utf8'));
  const imagePath = join(root, 'rgba.png');
  const imageBytes = [...await readFile(imagePath)];
  const results = [];
  const call = html => withDeadline(page.evaluate(({ html, bytes }) =>
    window.factspdfProbe.convertImages(html, {}, [{ name: 'sample', bytes: Uint8Array.from(bytes) }], true),
  { html, bytes: imageBytes }), 30000, 'Table-image WASM conversion');
  for (const item of manifest.cases) {
    const source = join(output, `table-image-${item.name}.html`);
    const browserPath = join(output, `table-image-${item.name}-wasm.pdf`);
    const cliPath = join(output, `table-image-${item.name}-cli.pdf`);
    const result = await call(item.html);
    assert.equal(result.errorCode, null, JSON.stringify(result));
    assert.equal(result.pageCount, item.pages);
    const bytes = Buffer.from(result.pdfBase64, 'base64');
    await writeFile(browserPath, bytes);
    await writeFile(source, item.html);
    await direct(executable, [...(dll ? [dll] : []), source, cliPath, '--overwrite', '--subset-fonts',
      '--image', `sample=${imagePath}`], { timeout: 90000 });
    assert.deepEqual(bytes, await readFile(cliPath), 'Table-image WASM and CLI output differ');
    const python = 'import json,sys; from pathlib import Path; from scripts.verify_table_images import inspect; ' +
      'print(json.dumps(inspect(Path(sys.argv[1]),json.loads(sys.argv[2]),json.loads(sys.argv[3]),qpdf="qpdf")))';
    const inspected = JSON.parse((await direct('/usr/bin/python3',
      ['-c', python, browserPath, JSON.stringify(item), JSON.stringify(manifest.image)], { timeout: 45000 })).stdout);
    results.push({ id: `table-image-${item.name}`, byteEquality: true, ...inspected });
  }
  const invalid = manifest.cases[0].html.replace('<img src="sample">', '<span><img src="sample"></span>');
  const failure = await call(invalid);
  assert.equal(failure.errorCode, 'FPDF1101');
  assert.equal(failure.pdfBase64, null);
  const recovery = await call(manifest.cases[0].html);
  assert.equal(recovery.errorCode, null);
  assert.equal(recovery.pageCount, 1);
  results.push({ id: 'table-image-mixed-content-recovery', diagnostic: 'FPDF1101', recovery: true });
  return results;
}
