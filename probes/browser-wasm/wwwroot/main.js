import { loadResources, loadStyleResources, packImageResources } from './resources.mjs';

const probe = window.factspdfProbe = { ready: false };
const started = performance.now();
try {
  const { dotnet } = await import('./_framework/dotnet.js');
  const runtime = await dotnet.create();
  const config = runtime.getConfig();
  const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
  const adapter = exports.FactsPDF.WasmProbe.BrowserExports;
  probe.info = { isBrowser: adapter.IsBrowser(), mainAssemblyName: config.mainAssemblyName,
    runtimeOptions: config.runtimeOptions ?? [],
    runtimeInitializeMs: performance.now() - started, navigationToReadyMs: performance.now(),
    timingScope: 'runtime initialization includes dotnet.js import, resource fetch, compilation and assembly export binding' };
  probe.convert = async (inputUrl, fontUrls = [], subsetFonts = false) => {
    try {
      const resourceStart = performance.now();
      const { html, fonts } = await loadResources(inputUrl, fontUrls);
      const resourceLoadMs = performance.now() - resourceStart;
      const convertStart = performance.now();
      const result = JSON.parse(adapter.Convert(html, fonts[0] ?? new Uint8Array(), fonts[1] ?? new Uint8Array(), subsetFonts));
      return { ...result, resourceLoadMs, convertMs: performance.now() - convertStart };
    } catch (error) {
      return { pdfBase64: null, pageCount: 0, errorCode: error.code ?? 'B2_HOST_ERROR', errorMessage: String(error) };
    }
  };
  probe.convertStyles = async (inputUrl, fontUrlsByRole = {}, subsetFonts = false) => {
    try {
      const resourceStart = performance.now();
      const { html, fontBytes, fontLengths, fontRoles } = await loadStyleResources(inputUrl, fontUrlsByRole);
      const resourceLoadMs = performance.now() - resourceStart;
      const convertStart = performance.now();
      const result = JSON.parse(adapter.ConvertStyles(html, fontBytes, fontLengths, fontRoles, subsetFonts));
      return { ...result, resourceLoadMs, convertMs: performance.now() - convertStart };
    } catch (error) {
      return { pdfBase64: null, pageCount: 0, errorCode: error.code ?? 'B2_HOST_ERROR', errorMessage: String(error) };
    }
  };
  // Explicit bytes only: this entry performs no image fetch or src resolution.
  probe.convertPackedImages = (html, fontData, imageData, subsetFonts = true) =>
    JSON.parse(adapter.ConvertImages(html, fontData.fontBytes ?? new Uint8Array(),
      fontData.fontLengths ?? new Int32Array(), fontData.fontRoles ?? new Int32Array(),
      imageData.imageBytes, imageData.imageLengths, imageData.imageNames, subsetFonts));
  probe.convertImages = (html, fontData = {}, images = [], subsetFonts = true) => {
    try {
      const { imageBytes, imageLengths, imageNames } = packImageResources(images);
      const convertStart = performance.now();
      const result = probe.convertPackedImages(html, fontData, { imageBytes, imageLengths, imageNames }, subsetFonts);
      return { ...result, convertMs: performance.now() - convertStart };
    } catch (error) {
      return { pdfBase64: null, pageCount: 0, errorCode: error.code ?? 'B2_HOST_ERROR', errorMessage: String(error) };
    }
  };
  probe.ready = true;
  document.getElementById('status').textContent = 'Runtime ready. Use the probe runner to generate and independently verify PDFs.';
} catch (error) {
  probe.error = String(error);
  document.getElementById('status').textContent = `Runtime failed: ${error}`;
}
