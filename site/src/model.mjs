export const STATUSES = ['full','partial','none','unknown','na'];
const SHA=/^[0-9a-f]{40}$/;
export function safeUrl(value) {
  if (typeof value!=='string' || !value.startsWith('https://')) return null;
  try {const u=new URL(value);return u.protocol==='https:'&&!u.username&&!u.password?u.href:null;}catch{return null;}
}
export function validateDataset(data) {
  if (!data || data.schema_version!==1 || !SHA.test(data.commit) || !Array.isArray(data.features) || !data.features.length) throw new Error('Invalid compatibility snapshot');
  if (!safeUrl(data.commit_url) || !data.dataset_version || !data.categories) throw new Error('Missing snapshot provenance');
  const ids=new Set(), group={};
  for(const f of data.features) {
    if (!f || typeof f.id!=='string' || !/^[a-zA-Z0-9._-]+$/.test(f.id) || ids.has(f.id)) throw new Error('Missing or duplicate feature ID');
    if (!STATUSES.includes(f.status) || !f.category || !f.feature || typeof f.scope!=='string' || !f.evidence_level) throw new Error(`Missing scope/evidence: ${f.id}`);
    if (!SHA.test(f.last_audited_commit) || !Array.isArray(f.sources) || !f.sources.length || f.sources.some(s=>!safeUrl(s))) throw new Error(`Invalid provenance: ${f.id}`);
    ids.add(f.id); group[f.category]=(group[f.category]??0)+1;
  }
  for(const f of data.features) {
    if (f.parent_id && (!ids.has(f.parent_id) || f.parent_id===f.id)) throw new Error(`Invalid parent: ${f.id}`);
    let node=f; const seen=new Set();
    while(node?.parent_id) {if(seen.has(node.id))throw new Error('Cyclic feature hierarchy');seen.add(node.id);node=data.features.find(x=>x.id===node.parent_id);}
  }
  if (JSON.stringify(Object.entries(group).sort())!==JSON.stringify(Object.entries(data.categories).sort())) throw new Error('Category totals disagree');
  return data;
}
export function counts(features) {
  const tally=Object.fromEntries(STATUSES.map(s=>[s,0]));
  for(const f of features)tally[f.status]++;
  return tally;
}
export function filterFeatures(features, filters={}) {
  const words=(filters.q??'').trim().toLowerCase().split(/\s+/u).filter(Boolean);
  return features.filter(f=>{
    if(filters.category && filters.category!=='all' && filters.category!==f.category)return false;
    if(filters.status && filters.status!=='all' && filters.status!==f.status)return false;
    if(filters.milestone && filters.milestone!=='all') {
      if(filters.milestone==='unknown' ? Boolean(f.introduced_milestone) : f.introduced_milestone!==filters.milestone)return false;
    }
    const hay=[f.id,f.feature,f.scope,f.limitations,f.applicability,f.stage,f.planning_bucket,f.failure_code].join(' ').toLowerCase();
    return words.every(word=>hay.includes(word));
  });
}
export function parseRoute(hash, dataset) {
  const p=new URLSearchParams(hash.replace(/^#/,''));
  const categories=Object.keys(dataset.categories);
  const milestones=[...new Set(dataset.features.map(f=>f.introduced_milestone).filter(Boolean))];
  const n=Number(p.get('page'));
  return {
    q:(p.get('q')??'').slice(0,200),
    category:categories.includes(p.get('category'))?p.get('category'):'all',
    status:STATUSES.includes(p.get('status'))?p.get('status'):'all',
    milestone:[...milestones,'unknown'].includes(p.get('milestone'))?p.get('milestone'):'all',
    feature:dataset.features.some(f=>f.id===p.get('feature'))?p.get('feature'):'',
    page:Number.isSafeInteger(n)&&n>0?Math.min(n,10000):1
  };
}
export function routeHash(s) {
  const p=new URLSearchParams();
  for(const key of ['q','feature'])if(s[key])p.set(key,s[key]);
  for(const key of ['category','status','milestone'])if(s[key]&&s[key]!=='all')p.set(key,s[key]);
  if(s.page>1)p.set('page',String(s.page));
  return p.size?`#${p}`:'';
}
export function pageSlice(features, requested=1, size=25) {
  const totalPages=Math.max(1,Math.ceil(features.length/size));
  const page=Math.min(totalPages,Math.max(1,Math.floor(Number(requested))||1));
  return {page,totalPages,items:features.slice((page-1)*size,page*size)};
}
export function csv(features) {
  const keys=['id','category','feature','status','scope','limitations','introduced_milestone','introduced_release','last_audited_commit','last_audited_date','evidence_level','sources'];
  const field=v=>{
    let s=Array.isArray(v)?v.join(' | '):String(v??'');
    if(/^[\s]*[=+@-]|^[\t\r]/.test(s))s="'"+s;
    return '"'+s.replaceAll('"','""')+'"';
  };
  return '\uFEFF'+[keys.map(field).join(','),...features.map(f=>keys.map(k=>field(f[k])).join(','))].join('\r\n');
}
