import { test, expect } from '@playwright/test';
const base='/FactsPDF/';
test('loads original snapshot using local npm-built assets without console failures', async({page})=>{
  const errors=[]; page.on('pageerror',e=>errors.push(e.message));
  await page.goto(base);await expect(page.getByRole('heading',{name:'Can I render it?'})).toBeVisible();
  await expect(page.locator('[data-total]')).toHaveText('418');
  await expect(page.getByRole('searchbox')).toBeVisible();
  await expect(page.locator('tbody tr')).toHaveCount(25);
  await expect(page.getByText('e35722bf',{exact:true}).first()).toBeVisible();
  expect(errors).toEqual([]);
});
test('filters and detail links survive refresh without inventing versions',async({page})=>{
  await page.goto(base);await page.getByRole('searchbox').fill('border-style');
  await page.getByRole('button',{name:'border-style',exact:true}).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.getByRole('dialog').getByText('部分支持',{exact:true})).toBeVisible();
  await expect(page.getByRole('dialog').getByText('M7',{exact:true}).first()).toBeVisible();
  await expect(page.getByRole('dialog').locator('a[href*="/blob/e35722"]')).not.toHaveCount(0);
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
  const download=page.waitForEvent('download');await page.getByRole('button',{name:'Export filtered CSV',exact:true}).click();
  expect((await download).suggestedFilename()).toMatch(/\.csv$/);
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
