import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { validateDataset, counts } from '../src/model.mjs';
const bytes=readFileSync(new URL('../data/compatibility.json',import.meta.url));
const sha256=createHash('sha256').update(bytes).digest('hex');
// Change only with a reviewed, intentional snapshot update and provenance docs.
const expectedSha256='905afc11e685c100f1b6d1e4254abae90dd4161dbe33f30304e4bb3d6b141353';
if(sha256!==expectedSha256)throw new Error(`Compatibility snapshot SHA-256 mismatch: expected ${expectedSha256}, received ${sha256}`);
const d=validateDataset(JSON.parse(bytes));
console.log(JSON.stringify({dataset:d.dataset_version,audit_commit:d.commit,records:d.features.length,counts:counts(d.features),sha256},null,2));
