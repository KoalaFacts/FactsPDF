import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { validateDataset, counts } from '../src/model.mjs';
const bytes=readFileSync(new URL('../data/compatibility.json',import.meta.url));
const sha256=createHash('sha256').update(bytes).digest('hex');
// Change only with a reviewed, intentional snapshot update and provenance docs.
const expectedSha256='982dae6a6ff6335f788fbe73f56d20d993b2812e2a06b6efcb7d45ae9ebe70a8';
if(sha256!==expectedSha256)throw new Error(`Compatibility snapshot SHA-256 mismatch: expected ${expectedSha256}, received ${sha256}`);
const d=validateDataset(JSON.parse(bytes));
console.log(JSON.stringify({dataset:d.dataset_version,audit_commit:d.commit,records:d.features.length,counts:counts(d.features),sha256},null,2));
