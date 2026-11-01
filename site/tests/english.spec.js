import { test, expect } from '@playwright/test';
import { readFile } from 'node:fs/promises';
const base='/FactsPDF/';
const han=/[\u3400-\u9fff]/u;
// Even a Chinese browser starts in English unless the visitor explicitly chose Chinese.
test.use({locale:'zh-CN',storageState:{cookies:[],origins:[]}});

test('first visit is English throughout all 418 list records and methodology',async({page})=>{
  const errors=[];page.on('pageerror',error=>errors.push(error.message));
  await page.goto(base);
  await expect(page).toHaveTitle('FactsPDF · Can I Render');
  await expect(page.locator('html')).toHaveAttribute('lang','en');
  await expect(page.locator('[data-total]')).toHaveText('418');
  await expect(page.getByRole('searchbox')).toHaveAttribute('placeholder',/Search elements/);
  await expect(page.getByRole('button',{name:'中文',exact:true})).toBeVisible();
  let count=0;
  for(let index=1;index<=17;index++){
    await expect(page.locator('.pagination > span')).toContainText(`Page ${index} / 17`);
    await expect(page.locator('tbody tr')).toHaveCount(index===17?18:25);
    expect(await page.locator('tbody').innerText()).not.toMatch(han);
    count+=await page.locator('tbody tr').count();
    if(index<17)await page.getByRole('button',{name:'Next page',exact:true}).click();
  }
  expect(count).toBe(418);
  await page.locator('.method summary').click();
  expect(await page.locator('.method').innerText()).not.toMatch(han);
  expect(errors).toEqual([]);
});

test('English details retain actual limits, diagnostics and source links',async({page})=>{
  await page.goto(base+'#feature=css.letter-spacing');
  const dialog=page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog).toContainText('0–24pt');
  await expect(dialog).toContainText('FPDF1202');
  for(const selector of ['.detail-head','.detail-scope','.limitation','.detail-grid','.evidence'])
    expect(await dialog.locator(selector).innerText()).not.toMatch(han);
  await expect(dialog.locator('a[href*="github.com/KoalaFacts/FactsPDF"]')).not.toHaveCount(0);
  await page.getByRole('button',{name:'Close details',exact:true}).click();
  await expect(dialog).not.toBeVisible();
  await page.screenshot({path:'test-results/can-i-render-english-default.png',fullPage:true});
});

test('English and original Chinese search both find the same feature',async({page})=>{
  await page.goto(base);
  const search=page.getByRole('searchbox');
  await search.fill('nonbreaking');
  await expect(page.getByRole('button',{name:'NBSP nonbreaking space',exact:true})).toBeVisible();
  await search.fill('不换行空格');
  await expect(page.getByRole('button',{name:'NBSP nonbreaking space',exact:true})).toBeVisible();
});

test('explicit language choice survives refresh and translates the descriptions',async({page})=>{
  await page.goto(base);
  await page.getByRole('button',{name:'中文',exact:true}).click();
  await expect(page.locator('html')).toHaveAttribute('lang','zh-CN');
  await expect(page.locator('tbody .scope').first()).toContainText('当前文档读取器');
  await page.reload();
  await expect(page.getByRole('button',{name:'English',exact:true})).toBeVisible();
  await expect(page.locator('html')).toHaveAttribute('lang','zh-CN');
  await page.getByRole('button',{name:'English',exact:true}).click();
  await expect(page.locator('tbody .scope').first()).toContainText('document reader');
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('lang','en');
  await expect(page.locator('tbody .scope').first()).toContainText('document reader');
});

test('blocked preference storage does not prevent English startup or language switching',async({page})=>{
  await page.addInitScript(()=>Object.defineProperty(window,'localStorage',{get(){throw new DOMException('Storage blocked','SecurityError');}}));
  await page.goto(base);
  await expect(page.locator('[data-total]')).toHaveText('418');
  await expect(page.locator('html')).toHaveAttribute('lang','en');
  await page.getByRole('button',{name:'中文',exact:true}).click();
  await expect(page.locator('html')).toHaveAttribute('lang','zh-CN');
});

test('load failures and retry use English for a new visitor',async({page})=>{
  await page.route('**/data/compatibility.json',route=>route.fulfill({status:503,body:'unavailable'}));
  await page.goto(base);
  await expect(page.getByRole('alert')).toContainText('Unable to load compatibility snapshot');
  expect(await page.getByRole('alert').innerText()).not.toMatch(han);
  await page.unroute('**/data/compatibility.json');
  await page.getByRole('button',{name:'Retry',exact:true}).click();
  await expect(page.locator('[data-total]')).toHaveText('418');
  await expect(page.locator('html')).toHaveAttribute('lang','en');
});

test('English display does not rewrite the downloaded original audit',async({page})=>{
  await page.goto(base);
  const pending=page.waitForEvent('download');
  await page.getByRole('link',{name:'Download source JSON'}).click();
  const download=await pending;
  expect(await readFile(await download.path())).toEqual(await readFile(new URL('../data/compatibility.json',import.meta.url)));
});

test('empty-result clearing keeps rows, empty state and pagination consistent across history',async({page})=>{
  const errors=[];
  page.on('pageerror',error=>errors.push(error.message));
  page.on('console',message=>{if(message.type()==='error')errors.push(message.text());});
  await page.goto(base);
  const search=page.getByRole('searchbox');
  const empty=page.getByTestId('empty');
  const pager=page.locator('.pagination > span');
  async function full(){
    await expect(search).toHaveValue('');
    await expect(page).toHaveURL(/\/FactsPDF\/$/);
    await expect(page.locator('.results-heading h2 span')).toHaveText('418');
    await expect(page.locator('tbody tr')).toHaveCount(25);
    await expect(empty).toHaveCount(0);
    await expect(pager).toHaveText('Page 1 / 17 · 418 records');
    await expect(page.getByRole('button',{name:'Next page',exact:true})).toBeEnabled();
  }
  for(let cycle=0;cycle<2;cycle++){
    await search.fill('zz-no-such-feature-qa');
    await expect(empty).toBeVisible();
    await expect(page.locator('tbody tr')).toHaveCount(0);
    await expect(pager).toHaveText('Page 1 / 1 · 0 records');
    await empty.getByRole('button',{name:'Clear filters',exact:true}).click();
    await full();
    await page.locator('.reset').click();await full();
  }
  await search.fill('zz-no-such-feature-qa');await expect(empty).toBeVisible();
  // A real hash navigation creates a history entry; clear replaces that entry.
  await page.evaluate(()=>{location.hash='#q=FPDF1102';});
  await expect(empty).toHaveCount(0);
  // <hr>, <blockquote> and now <pre> render, so the actual unsupported-element search has 79 records.
  await expect(pager).toHaveText('Page 1 / 4 · 79 records');
  await page.locator('.reset').click();await full();
  await page.goBack();
  await expect(search).toHaveValue('zz-no-such-feature-qa');
  await expect(empty).toBeVisible();
  await expect(page.locator('tbody tr')).toHaveCount(0);
  await expect(pager).toHaveText('Page 1 / 1 · 0 records');
  await page.goForward();await full();
  await page.getByRole('button',{name:'Next page',exact:true}).click();
  await expect(pager).toHaveText('Page 2 / 17 · 418 records');
  await page.locator('.reset').click();await full();
  expect(errors).toEqual([]);
});

test('first-visit English discloses actual PDF section breaks and strict limitations', async({page})=>{
  await page.goto('/FactsPDF/#feature=css.page-break-before');
  const detail=page.getByRole('dialog');
  await expect(detail).toBeVisible();
  await expect(detail).toContainText('always');
  await expect(detail).toContainText('PDF');
  await expect(detail).toContainText('FPDF1201/1202');
  expect(await detail.innerText()).not.toMatch(/[\u3400-\u9fff]/u);
  await page.getByRole('button',{name:'Close details',exact:true}).click();
  await page.goto('/FactsPDF/#feature=css.page-break-inside');
  await expect(page.getByRole('dialog')).toContainText('Unsupported');
});

test('fixed-height PDF business cards show complete en-AU limits, not Chinese fallback', async ({page}) => {
  await page.goto(base+'#feature=css.height');
  const dialog=page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog).toContainText('FPDF1302');
  await expect(dialog).toContainText('one-page');
  for(const selector of ['.detail-scope','.limitation','.evidence']) {
    expect(await dialog.locator(selector).innerText()).not.toMatch(han);
  }
});
