import { defineConfig } from 'vite';
import { purity } from '@purityjs/vite-plugin';
import { readFileSync } from 'node:fs';
export default defineConfig({
  base:'/FactsPDF/',
  plugins:[purity(),{
    name:'audited-snapshot-asset',
    generateBundle(){this.emitFile({type:'asset',fileName:'data/compatibility.json',source:readFileSync(new URL('./data/compatibility.json',import.meta.url))});}
  }],
  define:{__SITE_COMMIT__:JSON.stringify(process.env.GITHUB_SHA||'local')},
  build:{target:'es2022',sourcemap:false}
});
