import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { createHash } from 'node:crypto';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { withDeadline } from './deadline.mjs';
import { inspectReader } from './pdf_interaction.mjs';

const direct = promisify(execFile);
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const root = resolve('tests/fixtures/jpeg');

export async function inspectImages({ page, context, origin, files, output, executable, dll, styleUrls, fontPaths }) {
  const contract = JSON.parse(await readFile(join(root, 'acceptance-cases.json'), 'utf8'));
  const corpus = JSON.parse(await readFile(join(root, 'manifest.json'), 'utf8'));
  const results = [];
  const call = async (html, images, fonts = false) => withDeadline(page.evaluate(async ({ html, images, fonts, styleUrls }) => {
    const { loadStyleResources } = await import('/resources.mjs');
    const data = fonts ? await loadStyleResources('/probe-inputs/D1-report.html', { normal: styleUrls.normal }) : {};
    return window.factspdfProbe.convertImages(html, data,
      images.map(image => ({ name: image.name, bytes: Uint8Array.from(image.bytes) })), true);
  }, { html, images, fonts, styleUrls }), 30000, 'Explicit image WASM conversion');
  let reportHash;
  for (const item of contract.cases.filter(item => item.applies_to.includes('wasm-private'))) {
    const html = item.html ?? await readFile(join(root, item.html_file), 'utf8');
    if (item.fonts) files.set('/probe-inputs/D1-report.html', { bytes: Buffer.from(html), type: 'text/html' });
    const images = await Promise.all(Object.entries(item.resources).map(async ([name, filename]) =>
      ({ name, bytes: [...await readFile(join(root, filename))] })));
    const result = await call(html, images, Boolean(item.fonts));
    if (item.expect.code) {
      assert.equal(result.errorCode, item.expect.code, JSON.stringify(result)); assert.equal(result.pdfBase64, null);
      results.push({ id: item.id, errorCode: result.errorCode, noPdf: true }); continue;
    }
    assert.equal(result.errorCode, null, JSON.stringify(result)); assert.equal(result.pageCount, item.expect.page_count);
    const bytes = Buffer.from(result.pdfBase64, 'base64');
    const browserPath = join(output, `${item.id}-wasm.pdf`), cliPath = join(output, `${item.id}-cli.pdf`);
    const inputPath = join(output, `${item.id}.html`);
    await writeFile(browserPath, bytes); await writeFile(inputPath, html);
    const args = [...(dll ? [dll] : []), inputPath, cliPath, '--overwrite',
      ...Object.entries(item.resources).flatMap(([name, filename]) => ['--image', `${name}=${join(root, filename)}`]),
      ...(item.fonts ? ['--subset-fonts', ...fontPaths.flatMap(path => ['--font', path])] : [])];
    await direct(executable, args, { timeout: 90000 });
    assert.deepEqual(bytes, await readFile(cliPath), 'Image WASM and CLI bytes differ');
    const python = 'import json,sys; from pathlib import Path; from scripts.verify_images import inspect; print(json.dumps(inspect(Path(sys.argv[1]),json.loads(sys.argv[2]),Path(sys.argv[3]))))';
    const inspected = JSON.parse((await direct('/usr/bin/python3', ['-c', python, browserPath, JSON.stringify(item), root], { timeout: 30000 })).stdout);
    let viewerInteraction;
    if (item.id === 'D1-report') {
      files.set('/pdf-reader/D1-report.pdf', { bytes, type: 'application/pdf' });
      viewerInteraction = await inspectReader(context, origin, '/pdf-reader/D1-report.pdf',
        { name: 'D1-report', pages: 1, captureTop: true }, join(output, 'D1-report-viewer.png'));
      reportHash = hash(bytes);
    }
    results.push({ id: item.id, byteEquality: true, ...inspected, viewerInteraction });
  }
  for (const item of corpus.cases) {
    const result = await call('<p>ASCII</p>', [{ name: 'unused', bytes: [...await readFile(join(root, item.file))] }]);
    assert.equal(result.errorCode, item.expected_load === 'accept' ? null : item.expected_load, item.file);
    if (result.errorCode) assert.equal(result.pdfBase64, null);
    results.push({ id: `corpus:${item.file}`, result: item.expected_load });
  }
  const count = await call('<p>ASCII</p>', Array.from({ length: 17 }, (_, i) => ({ name: `i${i}`, bytes: [1] })));
  assert.equal(count.errorCode, 'B2_INPUT_IMAGES'); assert.equal(count.pdfBase64, null);
  results.push({ id: 'D1-default-image-count', errorCode: count.errorCode, noPdf: true });
  const cap = await page.evaluate(() => window.factspdfProbe.convertImages('<p>ASCII</p>', {},
    [{ name: 'chart', bytes: new Uint8Array(8_388_609) }]));
  assert.equal(cap.errorCode, 'B2_RESOURCE_LIMIT'); assert.equal(cap.pdfBase64, null);
  results.push({ id: 'D1-default-byte-cap-js', errorCode: cap.errorCode, noPdf: true });
  const directCap = await page.evaluate(() => window.factspdfProbe.convertPackedImages('<p>ASCII</p>', {},
    { imageBytes: new Uint8Array(8_388_609), imageLengths: Int32Array.of(8_388_609), imageNames: ['chart'] }));
  assert.equal(directCap.errorCode, 'FPDF1603'); assert.equal(directCap.pdfBase64, null);
  results.push({ id: 'D1-default-byte-cap-direct-dotnet', errorCode: directCap.errorCode, noPdf: true });
  const html = await readFile(join(root, 'report.html'), 'utf8');
  const recovery = await call(html, [{ name: 'chart', bytes: [...await readFile(join(root, 'rgb-420.jpg'))] }], true);
  assert.equal(recovery.errorCode, null); assert.equal(hash(Buffer.from(recovery.pdfBase64, 'base64')), reportHash);
  results.push({ id: 'D1-recovery', byteEquality: true });
  return results;
}
