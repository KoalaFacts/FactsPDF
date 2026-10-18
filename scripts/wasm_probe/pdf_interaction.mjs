import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { withDeadline } from './deadline.mjs';

export async function addReaderFiles(files) {
  for (const [file, url, type] of [
    ['pdf_reader.html', 'index.html', 'text/html'], ['pdf_reader.mjs', 'reader.mjs', 'text/javascript'],
    ...['build/pdf.mjs', 'build/pdf.worker.mjs', 'web/pdf_viewer.mjs', 'web/pdf_viewer.css'].map(path =>
      [`node_modules/pdfjs-dist/${path}`, `vendor/${path}`, path.endsWith('.css') ? 'text/css' : 'text/javascript']),
  ]) files.set(`/pdf-reader/${url}`, { bytes: await readFile(new URL(file, import.meta.url)), type });
}

// Search expectations are authored from the source fixture, never extracted from the PDF.
const queries = {
  'D1-report': ['D1-BEFORE', 'D1-AFTER', 'D1-END'],
  'lists-report': ['C2-010', 'C2-N03', '10.'],
  'styles-report': ['operation', '粗斜体 恢复斜体 恢复粗体'],
  'styles-records': ['Team 通过', 'Approved 备注', 'Note 优先'],
  'styles-long': ['I001 备注 café', 'J001 优先 action'],
  'styles-latin': ['Normal café Bold café Italic café Bold italic café'],
  'styles-courier': ['Courier ASCII 斜体 café normal again.'],
};
const compact = value => value.replace(/\s+/gu, ' ').trim();

export async function inspectReader(context, origin, url, item, screenshot) {
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(String(error)));
  try {
    await page.goto(`${origin}/pdf-reader/index.html?pdf=${encodeURIComponent(url)}`);
    await page.waitForFunction(() => window.readerEvidence?.renderedPages.length || window.readerEvidence?.error);
    assert.equal(await page.evaluate(() => window.readerEvidence.error), undefined);
    await page.locator('#query').fill('ABSENT-C1-PHRASE-98421');
    await page.locator('#find button').click();
    await page.waitForFunction(() => window.readerEvidence.findState === 1 && window.readerEvidence.matches?.total === 0);
    assert.equal(await page.locator('.textLayer .highlight').count(), 0);
    const results = [];
    for (const query of queries[item.name]) {
      await page.locator('#query').fill(query);
      await page.locator('#find button').click();
      await page.waitForFunction(query => window.readerEvidence.completedQuery === query &&
        [0, 2].includes(window.readerEvidence.findState) && window.readerEvidence.matches?.total > 0, query);
      await page.locator('.textLayer .highlight.selected').first().waitFor({ state: 'visible' });
      // Select the rendered search match, including every style span it crosses.
      // Browser copy/paste remains the native keyboard operation, with no clipboard API grant.
      await page.waitForFunction(query => {
        document.querySelector('#query').blur();
        document.querySelector('#viewerContainer').focus();
        const highlights = [...document.querySelectorAll('.textLayer .highlight.selected')];
        if (!highlights.length) return false;
        const range = document.createRange();
        range.setStartBefore(highlights[0]); range.setEndAfter(highlights.at(-1));
        const selection = getSelection(); selection.removeAllRanges(); selection.addRange(range);
        return selection.toString().replace(/\s+/gu, ' ').trim() === query;
      }, query);
      const selected = await page.evaluate(() => getSelection().toString());
      assert.equal(compact(selected), query, 'Rendered PDF selection differs from source phrase');
      await page.keyboard.press('Control+c');
      await page.locator('#paste').fill('');
      await page.locator('#paste').focus();
      await page.keyboard.press('Control+v');
      await page.waitForFunction(() => document.querySelector('#paste').value.length > 0);
      const pasted = await page.locator('#paste').inputValue();
      assert.equal(compact(pasted), query, 'Actual PDF keyboard copy/paste differs');
      const state = await page.evaluate(() => window.readerEvidence);
      assert.equal(state.copies, results.length + 1);
      assert.equal(state.pastes, results.length + 1);
      results.push({ query, selected, pasted, matches: state.matches.total, trustedCopyAndPaste: true });
    }
    await page.screenshot({ path: screenshot });
    assert.deepEqual(errors, []);
    const state = await page.evaluate(() => window.readerEvidence);
    assert.equal(state.pages, item.pages);
    return { viewer: `Mozilla PDF.js ${state.version}`, method: 'rendered find UI and native Control+C / Control+V into textarea',
      clipboardPermissionGrants: false, printing: false, absentQueryMatches: 0, pages: state.pages, results };
  } catch (error) {
    await page.screenshot({ path: screenshot.replace(/\.png$/, '-failure.png'), timeout: 5000 }).catch(() => {});
    throw error;
  } finally { await withDeadline(page.close(), 5000, 'PDF reader page cleanup'); }
}
