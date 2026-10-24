import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { withDeadline } from './deadline.mjs';

const direct = promisify(execFile);

// The preceding independent PNG gate generates these authored inputs; browsers
// receive explicit bytes and use the shared engine, never browser printing.
export async function inspectPngImages({ page, output, executable, dll }) {
  const root = resolve('artifacts/wasm-evidence/png/fixtures');
  const manifest = JSON.parse(await readFile(join(root, 'manifest.json'), 'utf8'));
  const results = [];
  const call = (html, bytes) => withDeadline(page.evaluate(({ html, bytes }) =>
    window.factspdfProbe.convertImages(html, {}, [{ name: 'sample', bytes: Uint8Array.from(bytes) }], true),
  { html, bytes }), 30000, 'PNG WASM conversion');
  for (const item of manifest.cases) {
    const html = '<div style="width:264pt;background-color:#204060">' +
      '<img src="sample" style="width:264pt"></div>' + `<p>PNG-${item.name}</p>`;
    const imagePath = join(root, item.file);
    const source = join(output, `PNG-${item.name}.html`);
    const browserPath = join(output, `PNG-${item.name}-wasm.pdf`);
    const cliPath = join(output, `PNG-${item.name}-cli.pdf`);
    const result = await call(html, [...await readFile(imagePath)]);
    assert.equal(result.errorCode, null, JSON.stringify(result));
    assert.equal(result.pageCount, 1);
    const bytes = Buffer.from(result.pdfBase64, 'base64');
    await writeFile(browserPath, bytes);
    await writeFile(source, html);
    await direct(executable, [...(dll ? [dll] : []), source, cliPath, '--overwrite', '--subset-fonts',
      '--image', `sample=${imagePath}`], { timeout: 90000 });
    assert.deepEqual(bytes, await readFile(cliPath), 'PNG WASM and CLI output differ');
    const python = 'import json,sys; from pathlib import Path; from scripts.verify_png import inspect; ' +
      'print(json.dumps(inspect(Path(sys.argv[1]),json.loads(sys.argv[2]),qpdf="qpdf")))';
    const inspected = JSON.parse((await direct('/usr/bin/python3',
      ['-c', python, browserPath, JSON.stringify(item)], { timeout: 30000 })).stdout);
    results.push({ id: `PNG-${item.name}`, byteEquality: true, ...inspected });
  }
  const invalid = [...await readFile(join(root, 'bad-crc.png'))];
  const failure = await call('<img src="sample">', invalid);
  assert.equal(failure.errorCode, 'FPDF1601');
  assert.equal(failure.pdfBase64, null);
  const recovery = await call('<img src="sample">', [...await readFile(join(root, 'rgba.png'))]);
  assert.equal(recovery.errorCode, null);
  results.push({ id: 'PNG-CRC-recovery', diagnostic: 'FPDF1601', recovery: true });
  return results;
}
