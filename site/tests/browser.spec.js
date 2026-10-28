import { test, expect } from '@playwright/test';
const base='/FactsPDF/';
test('loads the audited snapshot using local npm-built assets without console failures', async({page})=>{
  const errors=[]; page.on('pageerror',e=>errors.push(e.message));
  await page.goto(base);await expect(page.getByRole('heading',{name:'Can I render it?'})).toBeVisible();
  await expect(page.locator('[data-total]')).toHaveText('418');
  await expect(page.getByRole('searchbox')).toBeVisible();
  await expect(page.locator('tbody tr')).toHaveCount(25);
  await expect(page.getByText('8c946a29',{exact:true}).first()).toBeVisible();
  expect(errors).toEqual([]);
});
test('bounded LTR soft-wrap justification is discoverable without claiming full CSS Text',async({page})=>{
  await page.goto(base+'#q=text-align%3Ajustify');
  await page.getByRole('button',{name:'text-align',exact:true}).click();
  const details=page.getByRole('dialog');
  await expect(details).toBeVisible();
  await expect(details).toContainText('PDF TJ');
  await expect(details).toContainText('start/end');
  await expect(details).toContainText('RTL');
  await page.keyboard.press('Escape');
  await expect(details).not.toBeVisible();
});
test('bounded letter spacing shows real PDF behaviour and strict failure limits',async({page})=>{
  await page.goto(base+'#q=letter-spacing');
  await page.getByRole('button',{name:'letter-spacing',exact:true}).click();
  const details=page.getByRole('dialog');
  await expect(details).toBeVisible();
  await expect(details).toContainText('PDF Tc');
  await expect(details).toContainText('24pt');
  await expect(details).toContainText('FPDF1202');
  await expect(details).toContainText('kerning');
  await page.keyboard.press('Escape');
  await expect(details).not.toBeVisible();
});
test('word-spacing is discoverable as actual PDF positioning with explicit rejection',async({page})=>{
  await page.goto(base+'#q=word-spacing');
  await page.getByRole('button',{name:'word-spacing',exact:true}).click();
  const details=page.getByRole('dialog');
  await expect(details).toBeVisible();
  await expect(details).toContainText('PDF TJ');
  await expect(details).toContainText('24pt');
  await expect(details).toContainText('FPDF1202');
  await page.keyboard.press('Escape');
  await expect(details).not.toBeVisible();
});
test('border-box report sizing is discoverable with strict boundaries',async({page})=>{
  await page.goto(base+'#q=box-sizing');
  await page.getByRole('button',{name:'box-sizing',exact:true}).click();
  const details=page.getByRole('dialog');
  await expect(details).toBeVisible();
  await expect(details).toContainText('border-box');
  await expect(details).toContainText('FPDF1302');
  await expect(details).toContainText('table');
  await page.keyboard.press('Escape');
  await expect(details).not.toBeVisible();
});
test('computed line-height is discoverable with explicit supported bounds',async({page})=>{
  await page.goto(base+'#q=line-height');
  await page.getByRole('button',{name:'line-height',exact:true}).click();
  const details=page.getByRole('dialog');
  await expect(details).toBeVisible();
  await expect(details).toContainText('150%');
  await expect(details).toContainText('1440pt');
  await expect(details).toContainText('calc()');
  await page.keyboard.press('Escape');
  await expect(details).not.toBeVisible();
});
test('filters and detail links survive refresh without inventing versions',async({page})=>{
  await page.goto(base);await page.getByRole('searchbox').fill('border-style');
  await page.getByRole('button',{name:'border-style',exact:true}).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.getByRole('dialog').getByText('部分支持',{exact:true})).toBeVisible();
  await expect(page.getByRole('dialog').getByText('M7',{exact:true}).first()).toBeVisible();
  await expect(page.getByRole('dialog').locator('a[href*="/blob/a085085e"]')).not.toHaveCount(0);
  await page.reload();await expect(page.getByRole('dialog')).toBeVisible();
  await page.keyboard.press('Escape');await expect(page.getByRole('dialog')).not.toBeVisible();
  expect(page.url()).not.toContain('feature=');
});
test('category and status remain independent and empty results are explicit',async({page})=>{
  await page.goto(base+'#category=HTML%E5%85%83%E7%B4%A0&status=full');
  await expect(page.getByTestId('empty')).toBeVisible();
  await page.getByRole('button',{name:'清除筛选',exact:true}).first().click();
  await expect(page.locator('tbody tr')).toHaveCount(25);
});
test('literal search cannot execute template HTML or JavaScript',async({page})=>{
  await page.goto(base);await page.getByRole('searchbox').fill('<img src=x onerror=alert(1)>');
  await expect(page.getByTestId('empty')).toBeVisible();
  await expect(page.locator('img')).toHaveCount(0);
});
test('category data, milestone filtering, language and CSV download work',async({page})=>{
  await page.goto(base);await page.getByLabel('类别筛选').selectOption('CSS属性');
  await page.getByLabel('引入阶段筛选').selectOption('M6');
  await expect(page.locator('tbody tr')).toHaveCount(6);
  await page.getByRole('button',{name:'English',exact:true}).click();
  await expect(page.getByRole('searchbox')).toHaveAttribute('placeholder',/Search/);
  const download=page.waitForEvent('download');await page.getByRole('button',{name:'Export filtered CSV ↓',exact:true}).click();
  const saved=await download;
  expect(saved.suggestedFilename()).toMatch(/\.csv$/);
  const {readFile}=await import('node:fs/promises');
  const {csv,filterFeatures}=await import('../src/model.mjs');
  const data=JSON.parse(await readFile(new URL('../data/compatibility.json',import.meta.url)));
  expect(await readFile(await saved.path(),'utf8')).toBe(csv(filterFeatures(data.features,{category:'CSS属性',milestone:'M6'})));
});
test('unknown routes are safe and pagination can go back',async({page})=>{
  await page.goto(base+'#page=10000&feature=not-real');
  await expect(page.getByRole('dialog')).not.toBeVisible();
  await expect(page.locator('tbody tr')).toHaveCount(18);
  await page.getByRole('button',{name:'上一页',exact:true}).click();
  await expect(page.locator('tbody tr')).toHaveCount(25);
});
test('snapshot load failure is visible and retry recovers',async({page})=>{
  await page.route('**/data/compatibility.json',route=>route.fulfill({status:500,body:'error'}));
  await page.goto(base);await expect(page.getByRole('alert')).toContainText('无法加载');
  await page.unroute('**/data/compatibility.json');
  await page.getByRole('button',{name:'重试',exact:true}).click();await expect(page.locator('[data-total]')).toHaveText('418');
});
test('mobile layout contains controls and visible detail without viewport overflow',async({page})=>{
  await page.setViewportSize({width:390,height:844});await page.goto(base+'#q=width');
  await expect(page.getByRole('searchbox')).toBeVisible();
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  await page.getByRole('button',{name:'width',exact:true}).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  expect(await page.getByRole('dialog').evaluate(el=>el.getBoundingClientRect().width)).toBeLessThanOrEqual(390);
});
test('keyboard shortcut focuses search and status counts are not completion percentages',async({page})=>{
  await page.goto(base);await expect(page.getByRole('searchbox')).toBeVisible();
  await page.keyboard.press('/');await expect(page.getByRole('searchbox')).toBeFocused();
  await expect(page.getByText(/不是标准覆盖率/)).toBeVisible();
});
test('desktop review capture',async({page})=>{
  await page.setViewportSize({width:1440,height:1024});await page.goto(base+'#category=CSS%E5%B1%9E%E6%80%A7');
  await expect(page.getByRole('button',{name:'width',exact:true})).toBeVisible();
  await page.screenshot({path:'test-results/compatibility-desktop.png',fullPage:true});
  await page.getByRole('button',{name:'width',exact:true}).click();
  await page.screenshot({path:'test-results/compatibility-detail.png'});
});

test('detail Back and Forward restore the filtered list and dialog',async({page})=>{
  await page.goto(base);
  await page.getByRole('searchbox').fill('border-style');
  await page.getByRole('button',{name:'border-style',exact:true}).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await page.goBack();
  await expect(page.getByRole('dialog')).not.toBeVisible();
  await expect(page.getByRole('searchbox')).toHaveValue('border-style');
  await page.goForward();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.locator('#detail-title')).toHaveText('border-style');
});

test('mobile source JSON download remains visible and byte-identical',async({page})=>{
  await page.setViewportSize({width:390,height:844});
  await page.goto(base);
  const link=page.getByRole('link',{name:'下载原始 JSON'});
  await expect(link).toBeVisible();
  const pending=page.waitForEvent('download');await link.click();
  const download=await pending;
  expect(download.suggestedFilename()).toBe('FactsPDF-compatibility.json');
  const {readFile}=await import('node:fs/promises');
  expect(await readFile(await download.path())).toEqual(await readFile(new URL('../data/compatibility.json',import.meta.url)));
  await page.screenshot({path:'test-results/compatibility-mobile.png',fullPage:true});
});

for(const closeWith of ['Escape','button','backdrop']){
  test(`closing clicked details with ${closeWith} restores the list entry`,async({page})=>{
    const errors=[];page.on('pageerror',e=>errors.push(e.message));
    page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
    await page.goto(base);
    await page.getByRole('searchbox').fill('border-style');
    const listUrl=page.url();
    await page.getByRole('button',{name:'border-style',exact:true}).click();
    await expect(page.getByRole('dialog')).toBeVisible();
    const featureUrl=page.url();
    if(closeWith==='Escape')await page.keyboard.press('Escape');
    else if(closeWith==='button')await page.getByRole('button',{name:'关闭详情 / Close details'}).click();
    else await page.mouse.click(2,2);
    await expect(page.getByRole('dialog')).not.toBeVisible();
    await expect(page).toHaveURL(listUrl);
    await page.goForward();
    await expect(page).toHaveURL(featureUrl);
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.locator('#detail-title')).toHaveText('border-style');
    expect(errors).toEqual([]);
  });
}

test('closing a directly loaded detail stays on the lookup page',async({page})=>{
  await page.goto(base+'#feature=css.width');
  await expect(page.getByRole('dialog')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).not.toBeVisible();
  await expect(page.getByRole('searchbox')).toBeVisible();
  expect(new URL(page.url()).pathname).toBe(base);
  expect(page.url()).not.toContain('feature=');
});

test('English filters expose translated accessible names',async({page})=>{
  await page.goto(base);await page.getByRole('button',{name:'English',exact:true}).click();
  for(const name of ['Category filter','Support status filter','Introduction milestone filter']){
    await expect(page.getByRole('combobox',{name,exact:true})).toBeVisible();
  }
});

test('literal all search survives refresh and detail history',async({page})=>{
  await page.goto(base);await page.getByRole('searchbox').fill('all');
  await expect(page).toHaveURL(/#q=all$/);
  const count=await page.locator('.results-heading h2 span').textContent();
  expect(Number(count)).toBeGreaterThan(0);
  await page.reload();await expect(page.getByRole('searchbox')).toHaveValue('all');
  await expect(page.locator('.results-heading h2 span')).toHaveText(count);
  await page.locator('.feature-button').first().click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await page.keyboard.press('Escape');await expect(page.getByRole('dialog')).not.toBeVisible();
  await expect(page.getByRole('searchbox')).toHaveValue('all');
  await expect(page).toHaveURL(/#q=all$/);
});

test('uppercase identifier search works in a Turkish browser context',async({browser})=>{
  const context=await browser.newContext({locale:'tr-TR'});
  try{
    const page=await context.newPage();await page.goto('http://127.0.0.1:4173'+base);
    await page.getByRole('searchbox').fill('WIDTH');
    await expect(page.getByRole('button',{name:'width',exact:true})).toBeVisible();
  }finally{await context.close();}
});

test('audited PDF text decoration can be searched and its limitations read',async({page})=>{
  await page.goto('/FactsPDF/#q=text-decoration');
  await expect(page.getByRole('button',{name:'text-decoration',exact:true})).toBeVisible();
  await page.getByRole('button',{name:'text-decoration',exact:true}).click();
  await expect(page.getByRole('dialog')).toContainText('line-through');
  await expect(page.getByRole('dialog')).toContainText('overline');
});

test('ASCII report casing shows real render limits and source-linked evidence',async({page})=>{
  await page.goto(base+'#q=text-transform%3Auppercase');
  await page.getByRole('button',{name:'text-transform',exact:true}).click();
  const details=page.getByRole('dialog');
  await expect(details).toBeVisible();
  await expect(details).toContainText('ASCII');
  await expect(details).toContainText('FPDF1305');
  await expect(details).toContainText('capitalize');
  await expect(details).toContainText('FPDF1504');
  await page.keyboard.press('Escape');
  await expect(details).not.toBeVisible();
});
