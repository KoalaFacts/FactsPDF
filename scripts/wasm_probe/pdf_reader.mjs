// Verification-only reader using Mozilla's actual viewer/find/text-layer code.
// No printing, scripting manager, clipboard grants or product runtime dependency.
import * as pdfjsLib from '/pdf-reader/vendor/build/pdf.mjs';
await import('/pdf-reader/vendor/web/pdf_viewer.mjs');
const { EventBus, PDFLinkService, PDFFindController, PDFViewer } = globalThis.pdfjsViewer;
pdfjsLib.GlobalWorkerOptions.workerSrc = '/pdf-reader/vendor/build/pdf.worker.mjs';
const eventBus = new EventBus();
const linkService = new PDFLinkService({ eventBus });
const findController = new PDFFindController({ eventBus, linkService });
const viewer = new PDFViewer({ container: document.querySelector('#viewerContainer'), eventBus,
  linkService, findController, annotationMode: 0, enablePermissions: true });
linkService.setViewer(viewer);
const evidence = window.readerEvidence = { version: pdfjsLib.version, renderedPages: [], searches: [], copies: 0, pastes: 0 };
eventBus.on('pagesinit', () => { viewer.currentScaleValue = 'page-width'; });
eventBus.on('textlayerrendered', ({ pageNumber, error }) => {
  if (error) evidence.error = String(error);
  else evidence.renderedPages.push(pageNumber);
});
eventBus.on('updatefindmatchescount', ({ matchesCount }) => {
  evidence.matches = matchesCount;
  document.querySelector('#matches').textContent = `${matchesCount.current} / ${matchesCount.total}`;
});
eventBus.on('updatefindcontrolstate', ({ state, matchesCount }) => {
  evidence.findState = state;
  evidence.matches = matchesCount;
});
document.querySelector('#find').addEventListener('submit', event => {
  event.preventDefault();
  const query = document.querySelector('#query').value;
  evidence.matches = null;
  evidence.findState = null;
  evidence.searches.push(query);
  eventBus.dispatch('find', { source: event.target, type: '', query, caseSensitive: true,
    entireWord: false, highlightAll: true, findPrevious: false, matchDiacritics: true });
});
// PDF.js stops propagation after populating clipboardData; observe, never replace it.
document.addEventListener('copy', event => { if (event.isTrusted) evidence.copies++; }, { capture: true });
document.querySelector('#paste').addEventListener('paste', event => { if (event.isTrusted) evidence.pastes++; });
try {
  const pdf = await pdfjsLib.getDocument({ url: new URL(location.href).searchParams.get('pdf'),
    isEvalSupported: false, enableXfa: false }).promise;
  viewer.setDocument(pdf);
  linkService.setDocument(pdf);
  evidence.pages = pdf.numPages;
} catch (error) { evidence.error = String(error); }
