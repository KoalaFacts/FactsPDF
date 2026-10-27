import { test, expect } from '@playwright/test';
const base='/FactsPDF/';

test('feature-led support panels use readable titles and solid status tiles',async({page})=>{
  await page.goto(base+'#q=border-style');
  const row=page.locator('tbody tr').first();
  await expect(row).toBeVisible();
  expect(await row.locator('.feature-button').evaluate(el=>parseFloat(getComputedStyle(el).fontSize))).toBeGreaterThanOrEqual(20);
  expect((await row.locator('.badge').boundingBox()).height).toBeGreaterThanOrEqual(70);
  await expect(row.locator('.badge')).toContainText('部分支持');
});

for(const width of [320,390,768,1440]){
  test(`all feature notes fit without sideways scrolling at ${width}px`,async({page})=>{
    await page.setViewportSize({width,height:900});
    await page.goto(base+'#category=CSS%E5%B1%9E%E6%80%A7');
    await expect(page.locator('tbody tr')).toHaveCount(25);
    expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1)).toBe(true);
    const row=page.locator('tbody tr').first();
    const box=await row.boundingBox();
    expect(box.x+box.width).toBeLessThanOrEqual(width+1);
    expect(await row.locator('.scope').evaluate(el=>parseFloat(getComputedStyle(el).fontSize))).toBeGreaterThanOrEqual(13);
    await page.getByRole('button',{name:'English',exact:true}).click();
    expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1)).toBe(true);
    const english=await row.boundingBox();
    expect(english.x+english.width).toBeLessThanOrEqual(width+1);
  });
}

test('status legend stays textual, keyboard usable and scope-qualified',async({page})=>{
  await page.goto(base);
  await expect(page.locator('[data-total]')).toHaveText('418');
  for(const status of ['full','partial','none','unknown','na']){
    await expect(page.locator('.stat.'+status)).toHaveAttribute('aria-pressed','false');
    await expect(page.locator('.stat.'+status)).not.toHaveText(/^\d+$/);
  }
  await expect(page.getByText(/不是标准覆盖率/)).toBeVisible();
  await page.keyboard.press('/');
  await expect(page.getByRole('searchbox')).toBeFocused();
  await page.getByRole('searchbox').fill('border-style');
  await page.getByRole('button',{name:'border-style',exact:true}).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).not.toBeVisible();
});

test('theme desktop, detail, mobile and English review captures',async({page})=>{
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.setViewportSize({width:1440,height:1100});
  await page.goto(base+'#category=CSS%E5%B1%9E%E6%80%A7');
  await expect(page.locator('tbody tr')).toHaveCount(25);
  await page.screenshot({path:'test-results/caniuse-desktop.png'});
  await page.getByRole('searchbox').fill('border-style');
  await page.getByRole('button',{name:'border-style',exact:true}).click();
  await page.screenshot({path:'test-results/caniuse-detail.png'});
  await page.keyboard.press('Escape');
  await page.getByRole('button',{name:'English',exact:true}).click();
  await page.screenshot({path:'test-results/caniuse-english.png'});
  await page.setViewportSize({width:390,height:844});
  await page.goto(base+'#q=width');
  await expect(page.getByRole('button',{name:'width',exact:true})).toBeVisible();
  await page.screenshot({path:'test-results/caniuse-mobile.png',fullPage:true});
  expect(errors).toEqual([]);
});


test('snapshot load failure offers an accessible high-contrast retry action', async({page})=>{
  await page.route('**/data/compatibility.json',route=>route.fulfill({status:503,body:'unavailable'}));
  await page.goto(base);
  const retry=page.getByRole('button',{name:'重试',exact:true});
  await expect(retry).toBeVisible();
  const getContrast=()=>retry.evaluate(el=>{
    const style=getComputedStyle(el);
    const luminosity=color=>{
      const rgb=color.match(/\d+/g).slice(0,3).map(Number).map(n=>n/255);
      const linear=rgb.map(n=>n<=0.04045?n/12.92:Math.pow((n+0.055)/1.055,2.4));
      return linear[0]*0.2126+linear[1]*0.7152+linear[2]*0.0722;
    };
    const a=luminosity(style.color),b=luminosity(style.backgroundColor);
    return (Math.max(a,b)+0.05)/(Math.min(a,b)+0.05);
  });
  expect(await getContrast()).toBeGreaterThanOrEqual(4.5);
  await retry.hover();
  expect(await getContrast()).toBeGreaterThanOrEqual(4.5);
  await page.unroute('**/data/compatibility.json');
  await retry.click();
  await expect(page.locator('[data-total]')).toHaveText('418');
});
