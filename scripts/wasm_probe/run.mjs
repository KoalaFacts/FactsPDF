import assert from 'node:assert/strict';
import { readFile, writeFile, mkdir, readdir } from 'node:fs/promises';
import { join, relative, isAbsolute, extname, basename } from 'node:path';
import { createHash } from 'node:crypto';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { chromium } from 'playwright';
import { cases, unsupportedHtml } from './fixtures.mjs';
import { styleCases, styleErrorCases } from './style_fixtures.mjs';
import { listAscii, listReport, listErrors } from './list_fixtures.mjs';
import { tableAscii, tableCases, tableErrors, tableBoundaryCases } from './table_cases.mjs';
import { serveStatic } from './static_server.mjs';
import { withDeadline } from './deadline.mjs';
import { captureResourceTimings } from './resource_timings.mjs';
import { finishBrowserCleanup } from './cleanup.mjs';
import { addReaderFiles, inspectReader } from './pdf_interaction.mjs';
import { restrictedFont } from './restricted_font.mjs';
import { inspectImages } from './image_cases.mjs';
import { inspectPngImages } from './png_cases.mjs';
import { inspectTableImages } from './table_image_cases.mjs';
import { inspectTableRichCells } from './table_rich_cases.mjs';
import { inspectTableRowFragments } from './table_row_fragment_cases.mjs';

const direct = promisify(execFile);
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const normalize = text => text.replace(/\s+/gu, ' ').trim();
const root = process.env.FACTSPDF_WASM_ROOT;
const output = process.env.FACTSPDF_PROBE_OUTPUT;
const executable = process.env.FACTSPDF_CLI;
const dll = process.env.FACTSPDF_CLI_DLL;
const fontPaths = JSON.parse(process.env.FACTSPDF_PROBE_FONTS ?? '[]');
const styleManifest = process.env.FACTSPDF_STYLE_MANIFEST ? JSON.parse(await readFile(process.env.FACTSPDF_STYLE_MANIFEST, 'utf8')) : null;
const selectedStyles = styleManifest ? [...styleCases, ...tableCases] : [];
const selectedStyleErrors = styleManifest ? styleErrorCases : [];
const basicCases = [...cases, listAscii, tableAscii, ...tableBoundaryCases, ...(styleManifest ? [listReport] : [])];
const styleFlags = { normal: '--font', bold: '--font-bold', italic: '--font-italic', boldItalic: '--font-bold-italic' };
const styleUrls = {};
const rolePaths = selection => Object.fromEntries(Object.entries(styleManifest.roles)
  .filter(([role]) => selection !== 'courier' || role === 'italic')
  .map(([role, paths]) => [role, selection === 'latin-only' ? paths.slice(0, 1) : paths]));
for (const [name, value] of Object.entries({ FACTSPDF_WASM_ROOT: root, FACTSPDF_PROBE_OUTPUT: output, FACTSPDF_CLI: executable }))
  assert.ok(value && isAbsolute(value), `${name} must be absolute`);
assert.ok(!dll || isAbsolute(dll), 'FACTSPDF_CLI_DLL must be absolute');
assert.ok(fontPaths.length === 2 && fontPaths.every(isAbsolute), 'Supply exactly two explicit absolute font paths');
await mkdir(output, { recursive: true });
const files = new Map(), assets = [];
const types = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript',
  '.json': 'application/json', '.wasm': 'application/wasm' };
async function loadDirectory(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) await loadDirectory(path);
    else if (entry.isFile()) {
      const bytes = await readFile(path), name = relative(root, path).replaceAll('\\', '/');
      files.set(`/${name}`, { bytes, type: types[extname(name)] ?? 'application/octet-stream' });
      assets.push({ name, bytes: bytes.length, sha256: hash(bytes) });
    }
  }
}
await loadDirectory(root);
assert.ok(files.has('/index.html') && assets.some(a => a.name.endsWith('.wasm')), 'Publish the real WASM host first');
files.set('/', files.get('/index.html'));
const inputs = [...basicCases, ...listErrors, ...tableErrors, ...selectedStyles, ...selectedStyleErrors, { name: 'unsupported', html: unsupportedHtml }].map(item => {
  const bytes = Buffer.from(item.html, 'utf8');
  files.set(`/probe-inputs/${item.name}.html`, { bytes, type: 'text/html' });
  return { name: item.name, bytes: bytes.length, sha256: hash(bytes) };
});
files.set('/probe-inputs/invalid.html', { bytes: Buffer.from([0xc3, 0x28]), type: 'text/html' });
const fonts = [];
for (let i = 0; i < fontPaths.length; i++) {
  const bytes = await readFile(fontPaths[i]);
  files.set(`/probe-fonts/${i}.ttf`, { bytes, type: 'font/ttf' });
  fonts.push({ path: fontPaths[i], bytes: bytes.length, sha256: hash(bytes) });
}
if (styleManifest) {
  for (const [role, paths] of Object.entries(styleManifest.roles)) {
    styleUrls[role] = [];
    for (const [index, path] of paths.entries()) {
      assert.ok(isAbsolute(path));
      const bytes = await readFile(path), url = `/style-fonts/${role}-${index}.ttf`;
      assert.equal(hash(bytes), styleManifest.fonts.find(f => f.role === role && f.order === index).sha256);
      files.set(url, { bytes, type: 'font/ttf' }); styleUrls[role].push(url);
    }
  }
  await addReaderFiles(files);
}
let restrictedFixture;
if (styleManifest) {
  const original = files.get(styleUrls.boldItalic[0]).bytes;
  const { bytes, ...change } = restrictedFont(original);
  files.set('/probe-fonts/restricted.ttf', { bytes, type: 'font/ttf' });
  restrictedFixture = { ...change, originalSha256: hash(original), sha256: hash(bytes),
    purpose: 'private synthetic negative only; restrictive embedding bit added, never used in successful output' };
}
files.set('/probe-fonts/empty.ttf', { bytes: Buffer.alloc(0), type: 'font/ttf' });
files.set('/probe-fonts/corrupt.ttf', { bytes: Buffer.from([1, 2, 3]), type: 'font/ttf' });
const report = { probe: 'B2-browser-wasm', status: 'running', node: process.version,
  platform: process.platform, arch: process.arch, source: {},
  build: { properties: JSON.parse(await readFile(process.env.FACTSPDF_WASM_BUILD_INFO, 'utf8')), assets,
    uncompressedAssetBytes: assets.filter(a => !/\.(br|gz)$/.test(a.name)).reduce((n, a) => n + a.bytes, 0),
    mode: 'single-threaded non-AOT .NET browser-wasm; SDK runtime defaults, no application AOT' },
  inputs, fonts, styleFonts: styleManifest, restrictedFixture, browser: { chromiumSandbox: true }, cases: [], errors: [], console: [],
  resources: null, resourceTimingCapture: { status: 'pending' },
  memoryScope: 'GC.GetTotalMemory(false) snapshots around font parsing and conversion; not allocation totals, JS heap, linear memory, RSS or peak memory',
  unavailableMetrics: ['peak browser/process RSS', 'WASM linear-memory capacity', 'GC peak heap'] };
report.source.head = (await direct('git', ['rev-parse', 'HEAD'])).stdout.trim();
report.source.dirty = Boolean((await direct('git', ['status', '--porcelain'])).stdout.trim());
if (process.env.FACTSPDF_EXPECTED_SHA) {
  report.source.expectedHead = process.env.FACTSPDF_EXPECTED_SHA;
  assert.equal(report.source.head, report.source.expectedHead, 'Run must execute the exact expected commit');
}
const server = await serveStatic(files);
let browser, page;
try {
  // Explicitly preserve sandboxing and the self-XSS warning; no unsafe fallback.
  browser = await chromium.launch({ channel: 'chromium', headless: true, chromiumSandbox: true,
    args: ['--enable-automation'], // CDP requires this to expose the actual browser command line.
    ignoreDefaultArgs: ['--unsafely-disable-devtools-self-xss-warnings', '--enable-unsafe-swiftshader'], timeout: 20000 });
  report.browser.version = browser.version();
  const browserSession = await browser.newBrowserCDPSession();
  report.browser.arguments = (await browserSession.send('Browser.getBrowserCommandLine')).arguments;
  for (const forbidden of ['--no-sandbox', '--disable-setuid-sandbox', '--disable-web-security',
    '--unsafely-disable-devtools-self-xss-warnings', '--enable-unsafe-swiftshader'])
    assert.ok(!report.browser.arguments.some(a => a.split('=')[0] === forbidden), `Unsafe browser argument: ${forbidden}`);
  const context = await browser.newContext({ serviceWorkers: 'block' });
  await context.route('**/*', route => new URL(route.request().url()).origin === server.origin
    ? route.continue() : route.abort('blockedbyclient'));
  page = await context.newPage();
  page.on('console', message => report.console.push({ type: message.type(), text: message.text() }));
  page.on('pageerror', error => report.errors.push(error.message));
  await page.goto(server.origin);
  await page.waitForFunction(() => window.factspdfProbe?.ready || window.factspdfProbe?.error, null, { timeout: 60000 });
  report.runtime = await page.evaluate(() => window.factspdfProbe.info);
  assert.equal(await page.evaluate(() => window.factspdfProbe.error), undefined);
  assert.equal(report.runtime.isBrowser, true);
  if (process.platform === 'linux') {
    const processes = (await browserSession.send('SystemInfo.getProcessInfo')).processInfo;
    report.browser.renderers = [];
    for (const renderer of processes.filter(p => p.type === 'renderer')) {
      const status = await readFile(`/proc/${renderer.id}/status`, 'utf8');
      const seccomp = Number(status.match(/^Seccomp:\s*(\d+)/m)?.[1]);
      const noNewPrivs = Number(status.match(/^NoNewPrivs:\s*(\d+)/m)?.[1]);
      assert.equal(seccomp, 2, 'Renderer must use seccomp filtering');
      assert.equal(noNewPrivs, 1, 'Renderer must set NoNewPrivs');
      report.browser.renderers.push({ pid: renderer.id, seccomp, noNewPrivs });
    }
    assert.ok(report.browser.renderers.length > 0, 'No protected renderer process observed');
  }
  const call = (name, fontUrls) => withDeadline(page.evaluate(async ({ name, fontUrls }) =>
    window.factspdfProbe.convert(`/probe-inputs/${name}.html`, fontUrls, fontUrls.length > 0), { name, fontUrls }),
    30000, `Browser conversion ${name}`);
  for (const item of basicCases) {
    const chosenPaths = item.fonts === 'style-normal' ? styleManifest.roles.normal : item.fonts ? fontPaths : [];
    const fontUrls = item.fonts === 'style-normal' ? styleUrls.normal : item.fonts ? ['/probe-fonts/0.ttf', '/probe-fonts/1.ttf'] : [];
    const samples = [];
    for (let attempt = 0; attempt < 4; attempt++) {
      const result = await call(item.name, fontUrls);
      assert.equal(result.errorCode, null, JSON.stringify(result));
      assert.equal(result.pageCount, item.pages);
      const bytes = Buffer.from(result.pdfBase64, 'base64');
      assert.equal(bytes.subarray(0, 5).toString(), '%PDF-');
      samples.push({ convertMs: result.convertMs, resourceLoadMs: result.resourceLoadMs,
        managedHeapBeforeBytes: result.managedHeapBeforeBytes, managedHeapAfterBytes: result.managedHeapAfterBytes,
        sha256: hash(bytes) });
      if (attempt === 0) {
        const browserPath = join(output, `${item.name}-wasm.pdf`), cliPath = join(output, `${item.name}-cli.pdf`);
        const inputPath = join(output, `${item.name}.html`);
        await writeFile(browserPath, bytes); await writeFile(inputPath, item.html);
        const args = [...(dll ? [dll] : []), inputPath, cliPath, '--overwrite',
          ...(chosenPaths.length ? [...chosenPaths.flatMap(p => ['--font', p]), '--subset-fonts'] : [])];
        await direct(executable, args, { timeout: 30000 });
        const cliBytes = await readFile(cliPath);
        const rasterEvidence = await inspectPair(browserPath, cliPath, item);
        assert.deepEqual(bytes, cliBytes, 'Browser WASM PDF differs from same-input CLI PDF');
        let viewerInteraction;
        if (item.reader) {
          const pdfUrl = `/pdf-reader/${item.name}.pdf`;
          files.set(pdfUrl, { bytes, type: 'application/pdf' });
          viewerInteraction = await inspectReader(context, server.origin, pdfUrl, item, join(output, `${item.name}-viewer.png`));
        }
        report.cases.push({ name: item.name, bytes: bytes.length, sha256: hash(bytes), cliSha256: hash(cliBytes),
          pages: item.pages, byteEquality: true, qpdf: 'pass', text: 'pass', boundingBoxes: 'equal', rasterPages: 'equal', rasterEvidence, samples, viewerInteraction });
      } else assert.equal(samples[attempt].sha256, samples[0].sha256, 'Repeated conversion differs');
    }
  }
  // Regress the actual JS wrapper precedence (not only the managed adapter),
  // and prove a real browser-WASM furniture PDF matches the managed CLI bytes.
  const furnitureFixture = { html: '<p>Regional report.</p>',
    json: '{"default":{"headerLeft":{"template":"ACME"},"footerRight":{"template":"Page {page} of {pages}"}}}' };
  const furnitureResults = await withDeadline(page.evaluate(({ html, json }) => {
    const good = new TextEncoder().encode(json);
    const broken = new TextEncoder().encode('{bad');
    const invalidImages = [{ name: 'bad/name', bytes: new Uint8Array([1]) }];
    const probe = window.factspdfProbe;
    return {
      simultaneous: probe.convertPageFurniture(html, {}, invalidImages, broken),
      imageOnly: probe.convertPageFurniture(html, {}, invalidImages, good),
      valid: probe.convertPageFurniture(html, {}, [], good),
      recovered: probe.convertPageFurniture(html, {}, [], good),
    };
  }, furnitureFixture), 45000, 'Protected browser page-furniture precedence and parity');
  assert.equal(furnitureResults.simultaneous.errorCode, 'B2_INPUT_FURNITURE',
    'Malformed configuration must outrank JS image validation');
  assert.equal(furnitureResults.imageOnly.errorCode, 'B2_INPUT_IMAGES',
    'Valid configuration must not mask the original JS image error');
  for (const result of [furnitureResults.simultaneous, furnitureResults.imageOnly]) {
    assert.equal(result.pdfBase64, null);
    assert.equal(result.pageCount, 0);
  }
  assert.equal(furnitureResults.valid.errorCode, null, JSON.stringify(furnitureResults.valid));
  assert.equal(furnitureResults.valid.pageCount, 1);
  assert.equal(furnitureResults.recovered.errorCode, null);
  assert.equal(furnitureResults.recovered.pdfBase64, furnitureResults.valid.pdfBase64,
    'Malformed attempts must not contaminate later conversions');
  const furniturePdf = Buffer.from(furnitureResults.valid.pdfBase64, 'base64');
  assert.equal(furniturePdf.subarray(0, 5).toString(), '%PDF-');
  const furnitureHtmlPath = join(output, 'page-furniture-browser.html');
  const furnitureJsonPath = join(output, 'page-furniture-browser.json');
  const furnitureWasmPath = join(output, 'page-furniture-browser-wasm.pdf');
  const furnitureCliPath = join(output, 'page-furniture-browser-cli.pdf');
  await writeFile(furnitureHtmlPath, furnitureFixture.html);
  await writeFile(furnitureJsonPath, furnitureFixture.json);
  await writeFile(furnitureWasmPath, furniturePdf);
  await direct(executable, [...(dll ? [dll] : []), furnitureHtmlPath, furnitureCliPath,
    '--page-furniture', furnitureJsonPath, '--subset-fonts'], { timeout: 45000 });
  const furnitureCliPdf = await readFile(furnitureCliPath);
  assert.deepEqual(furniturePdf, furnitureCliPdf, 'Actual browser furniture PDF differs from CLI');
  const furnitureText = (await direct('pdftotext', ['-raw', furnitureWasmPath, '-'], { timeout: 15000 })).stdout;
  const furniturePages = (await direct('pdfinfo', [furnitureWasmPath], { timeout: 15000 })).stdout;
  assert.match(furniturePages, /^Pages:\\s+1\\s*$/m);
  for (const marker of ['ACME', 'Page 1 of 1', 'Regional report.'])
    assert.ok(furnitureText.replace(/\\s+/gu, ' ').includes(marker),
      `Missing independently specified furniture/body marker: ${marker}`);
  report.cases.push({ name: 'page-furniture-browser-actual-pdf-and-error-priority',
    pageCount: 1, bytes: furniturePdf.length, sha256: hash(furniturePdf),
    cliSha256: hash(furnitureCliPdf), byteEquality: true,
    malformedJsonBeforeInvalidImages: true, originalImageErrorPreserved: true,
    recoveredAfterErrors: true });
  for (const item of [...listErrors, ...tableErrors]) {
    const result = await call(item.name, []);
    assert.equal(result.errorCode, item.code, JSON.stringify(result));
    assert.equal(result.pdfBase64, null);
    report.cases.push({ name: item.name, errorCode: result.errorCode, noPdf: true });
  }
  const listRecovery = await call(listAscii.name, []);
  assert.equal(listRecovery.errorCode, null);
  assert.equal(hash(Buffer.from(listRecovery.pdfBase64, 'base64')), report.cases.find(item => item.name === listAscii.name).sha256);
  report.cases.push({ name: 'lists-recovery', byteEquality: true });
  const tableRecovery = await call(tableAscii.name, []);
  assert.equal(tableRecovery.errorCode, null);
  assert.equal(hash(Buffer.from(tableRecovery.pdfBase64, 'base64')), report.cases.find(item => item.name === tableAscii.name).sha256);
  report.cases.push({ name: 'tables-recovery', byteEquality: true });
  const styleCall = (name, roles, subset = true) => withDeadline(page.evaluate(async ({ name, roles, subset }) =>
    window.factspdfProbe.convertStyles(`/probe-inputs/${name}.html`, roles, subset), { name, roles, subset }),
    90000, `Browser style conversion ${name}`);
  let styleRecoveryHash;
  for (const item of selectedStyles) {
    assert.ok(Number.isInteger(item.pages), 'Reviewed style fixture page count is required');
    const selected = rolePaths(item.selection);
    const urls = Object.fromEntries(Object.entries(selected).map(([role, paths]) => [role, styleUrls[role].slice(0, paths.length)]));
    for (const subset of item.selection === 'latin-only' ? [true, false] : [true]) {
      const name = `${item.name}-${subset ? 'subset' : 'full'}`;
      const result = await styleCall(item.name, urls, subset);
      assert.equal(result.errorCode, null, JSON.stringify(result));
      assert.equal(result.pageCount, item.pages);
      const bytes = Buffer.from(result.pdfBase64, 'base64');
      const browserPath = join(output, `${name}-wasm.pdf`), cliPath = join(output, `${name}-cli.pdf`);
      const inputPath = join(output, `${item.name}.html`);
      await writeFile(browserPath, bytes); await writeFile(inputPath, item.html);
      const args = [...(dll ? [dll] : []), inputPath, cliPath, '--overwrite',
        ...Object.entries(selected).flatMap(([role, paths]) => paths.flatMap(path => [styleFlags[role], path])),
        ...(subset ? ['--subset-fonts'] : [])];
      await direct(executable, args, { timeout: 90000 });
      const cliBytes = await readFile(cliPath);
      const rasterEvidence = await inspectPair(browserPath, cliPath, { ...item, ignoreLayoutWhitespace: true });
      assert.deepEqual(bytes, cliBytes, 'Four-role browser WASM bytes differ from CLI');
      const pdfUrl = `/pdf-reader/${name}.pdf`;
      files.set(pdfUrl, { bytes, type: 'application/pdf' });
      const viewerInteraction = await inspectReader(context, server.origin, pdfUrl, item, join(output, `${name}-viewer.png`));
      report.cases.push({ name, bytes: bytes.length, pages: item.pages, sha256: hash(bytes), cliSha256: hash(cliBytes),
        byteEquality: true, qpdf: 'pass', text: 'pass', boundingBoxes: 'equal', rasterPages: 'equal', rasterEvidence,
        viewerInteraction,
        convertMs: result.convertMs, resourceLoadMs: result.resourceLoadMs,
        managedHeapBeforeBytes: result.managedHeapBeforeBytes, managedHeapAfterBytes: result.managedHeapAfterBytes });
      if (item.name === 'styles-report') styleRecoveryHash = hash(bytes);
    }
  }
  for (const item of selectedStyleErrors) {
    const roles = Object.fromEntries(item.roles.map(role => [role, item.latinOnly ? styleUrls[role].slice(0, 1) : styleUrls[role]]));
    const result = await styleCall(item.name, roles);
    assert.equal(result.errorCode, item.code, JSON.stringify(result)); assert.equal(result.pdfBase64, null);
    report.cases.push({ name: item.name, errorCode: result.errorCode, errorMessage: result.errorMessage, noPdf: true });
  }
  if (styleManifest) {
    for (const [url, expected] of [['missing', 'B2_RESOURCE_HTTP'], ['empty', 'B2_RESOURCE_EMPTY'], ['corrupt', 'FPDF1501'], ['restricted', 'FPDF1505']]) {
      const result = await styleCall('ascii', { boldItalic: [`/probe-fonts/${url}.ttf`] });
      assert.equal(result.errorCode, expected); assert.equal(result.pdfBase64, null);
      report.cases.push({ name: `style-resource:${url}`, errorCode: expected, noPdf: true });
    }
    const overBudget = await styleCall('styles-report', styleUrls, false);
    assert.equal(overBudget.errorCode, 'FPDF1401', JSON.stringify(overBudget));
    assert.equal(overBudget.pdfBase64, null);
    report.cases.push({ name: 'style-output-budget', errorCode: 'FPDF1401', noPdf: true });
    const result = await styleCall('styles-report', styleUrls);
    assert.equal(result.errorCode, null);
    assert.equal(hash(Buffer.from(result.pdfBase64, 'base64')), styleRecoveryHash);
    report.cases.push({ name: 'style-recovery', byteEquality: true });
  }
  if (styleManifest) report.images = await inspectImages({ page, context, origin: server.origin,
    files, output, executable, dll, styleUrls, fontPaths: styleManifest.roles.normal });
  if (styleManifest) report.pngImages = await inspectPngImages({ page, output, executable, dll });
  if (styleManifest) report.tableImages = await inspectTableImages({ page, output, executable, dll });
  if (styleManifest) report.tableRichCells = await inspectTableRichCells({ page, output, executable, dll });
  if (styleManifest) report.tableRowFragments = await inspectTableRowFragments({ page, output, executable, dll });
  for (const [name, fontUrls, expected] of [
    ['bilingual', ['/probe-fonts/missing.ttf'], 'B2_RESOURCE_HTTP'],
    ['ascii', ['/probe-fonts/empty.ttf'], 'B2_RESOURCE_EMPTY'],
    ['invalid', [], 'B2_INPUT_UTF8'], ['unsupported', [], 'FPDF1102'],
    ['bilingual', [], 'FPDF1301'], ['bilingual', ['/probe-fonts/0.ttf'], 'FPDF1504'],
    ['ascii', ['/probe-fonts/corrupt.ttf'], 'FPDF1501'],
  ]) {
    const result = await call(name, fontUrls);
    assert.equal(result.errorCode, expected, JSON.stringify(result));
    assert.equal(result.pdfBase64, null);
    report.cases.push({ name: `${name}:${expected}`, errorCode: result.errorCode, noPdf: true });
  }
  const recovery = await call('ascii', []);
  assert.equal(recovery.errorCode, null);
  assert.equal(hash(Buffer.from(recovery.pdfBase64, 'base64')), report.cases[0].sha256);
  assert.ok(server.requests.some(r => r.path.endsWith('.wasm') && r.status === 200), 'No actual WASM asset fetched');
  assert.deepEqual(report.errors, []);
  report.status = 'browser-contract-passed';
} catch (error) {
  report.status = 'failed'; report.failure = error.stack ?? String(error);
  process.exitCode = 1;
} finally {
  report.requests = server.requests;
  // Persist the outcome before cleanup, so a stuck browser cannot erase the evidence.
  await writeFile(join(output, 'report.json'), JSON.stringify(report, null, 2) + '\n');
  const { entries, ...capture } = await captureResourceTimings(page ? () => page.evaluate(() =>
    performance.getEntriesByType('resource').map(r => ({
      name: new URL(r.name).pathname, initiatorType: r.initiatorType, durationMs: r.duration,
      transferSize: r.transferSize, encodedBodySize: r.encodedBodySize, decodedBodySize: r.decodedBodySize,
    }))) : null);
  report.resources = entries;
  report.resourceTimingCapture = capture;
  if (capture.status !== 'captured' && report.status === 'browser-contract-passed') {
    report.status = 'failed'; report.failure = `Resource timing evidence unavailable: ${capture.reason}`;
    process.exitCode = 1;
  }
  await writeFile(join(output, 'report.json'), JSON.stringify(report, null, 2) + '\n');
  await finishBrowserCleanup(() => browser?.close(), async () => {
    await server.close();
    await writeFile(join(output, 'report.json'), JSON.stringify(report, null, 2) + '\n');
    // Keep reviewable provenance and assertions in normal CI logs as well as the
    // artifact report. Never serialize PDF/font/image bytes into logs.
    console.log(JSON.stringify({ status: report.status, cases: report.cases.length, report: join(output, 'report.json'),
      source: report.source, browser: report.browser, runtime: report.runtime, buildProperties: report.build.properties,
      styleFonts: report.styleFonts?.fonts.map(({ role, order, bytes, sha256 }) => ({ role, order, bytes, sha256 })),
      styleResults: report.cases.filter(item => item.name.startsWith('style')),
      listResults: report.cases.filter(item => item.name.startsWith('lists')),
      tableResults: report.cases.filter(item => item.name.startsWith('tables')),
      failure: report.failure }));
  }, report);
}

async function inspectPair(browserPath, cliPath, item) {
  const tool = (name, fallback) => process.env[name] ?? fallback;
  const outputs = [];
  // Retain both renderable outputs even if a later inspector rejects either PDF.
  for (const path of [browserPath, cliPath])
    await direct(tool('FACTSPDF_PDFTOPPM', 'pdftoppm'),
      ['-png', '-r', '144', path, path.replace(/\.pdf$/, '')], { timeout: 30000 });
  for (const path of [browserPath, cliPath]) {
    await direct(tool('FACTSPDF_QPDF', 'qpdf'), ['--check', path], { timeout: 15000 });
    const text = (await direct(tool('FACTSPDF_PDFTOTEXT', 'pdftotext'), ['-raw', path, '-'], { timeout: 15000 })).stdout;
    const info = (await direct(tool('FACTSPDF_PDFINFO', 'pdfinfo'), [path], { timeout: 15000 })).stdout;
    const pages = Number(info.match(/Pages:\s+(\d+)/)?.[1]);
    assert.ok(Number.isInteger(pages) && pages > 0 && pages <= 1000, 'Invalid PDF page count');
    const bbox = (await direct(tool('FACTSPDF_PDFTOTEXT', 'pdftotext'), ['-bbox', path, '-'], { timeout: 15000 })).stdout;
    const prefix = path.replace(/\.pdf$/, '');
    const pixels = [];
    for (let page = 1; page <= pages; page++) {
      const file = `${prefix}-${String(page).padStart(String(pages).length, '0')}.png`;
      pixels.push({ file: basename(file), sha256: hash(await readFile(file)) });
    }
    outputs.push({ text, pages, bbox, pixels });
  }
  for (const result of outputs) {
    assert.equal(item.ignoreLayoutWhitespace ? result.text.replace(/\s/gu, '') : normalize(result.text),
      item.ignoreLayoutWhitespace ? item.text.replace(/\s/gu, '') : item.text);
    assert.equal(result.pages, item.pages);
  }
  assert.equal(outputs[0].bbox, outputs[1].bbox, 'Text positions differ');
  assert.deepEqual(outputs[0].pixels.map(p => p.sha256), outputs[1].pixels.map(p => p.sha256), 'Page raster differs');
  return { format: 'png', dpi: 144, wasm: outputs[0].pixels, cli: outputs[1].pixels };
}
