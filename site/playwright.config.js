import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir:'./tests',testMatch:['browser.spec.js','theme.spec.js','english.spec.js'],workers:1,retries:0,
  reporter:[['list'],['json',{outputFile:'test-results/browser-results.json'}]],
  // Preserve the existing Chinese interaction coverage explicitly. english.spec.js
  // overrides storageState to test the real first-visit default with no preference.
  use:{storageState:{cookies:[],origins:[{origin:'http://127.0.0.1:4173',localStorage:[{name:'factspdf.locale',value:'zh'}]}]},launchOptions:process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH?{executablePath:process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH}:undefined,baseURL:'http://127.0.0.1:4173',viewport:{width:1280,height:900},trace:'retain-on-failure',screenshot:'only-on-failure'},
  webServer:{command:'npm run preview',url:'http://127.0.0.1:4173/FactsPDF/',reuseExistingServer:!process.env.CI,timeout:30000}
});
