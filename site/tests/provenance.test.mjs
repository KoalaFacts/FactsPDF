import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, copyFile, readFile, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';

// Run the real validator against isolated bytes, never edit the audited source.
test('snapshot validation rejects content drift even when totals and statuses match', async () => {
  const root=await mkdtemp(join(tmpdir(),'factspdf-provenance-'));
  try {
    for(const folder of ['scripts','src','data'])await mkdir(join(root,folder));
    for(const file of ['scripts/validate.mjs','src/model.mjs','data/compatibility.json']){
      await copyFile(new URL('../'+file,import.meta.url),join(root,file));
    }
    const validate=()=>spawnSync(process.execPath,[join(root,'scripts/validate.mjs')],{encoding:'utf8'});
    const original=validate();
    assert.equal(original.status,0,original.stderr);
    const data=JSON.parse(await readFile(join(root,'data/compatibility.json'),'utf8'));
    data.features[0].limitations+=' Synthetic drift for validation test.';
    await writeFile(join(root,'data/compatibility.json'),JSON.stringify(data));
    const changed=validate();
    assert.notEqual(changed.status,0,'changed scope/evidence bytes must fail validation');
    assert.match(changed.stderr,/snapshot SHA-256 mismatch/);
  } finally { await rm(root,{recursive:true,force:true}); }
});
