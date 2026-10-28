import { html, mount, state, compute, each, onMount, onDispose, watch } from '@purityjs/core';
import { STATUSES, validateDataset, filterFeatures, counts, safeUrl, parseRoute, routeHash, pageSlice, csv } from './model.mjs';
import './style.css';

const categoriesEn={'HTML元素':'HTML elements','HTML属性与解析':'HTML attributes & parsing','CSS属性':'CSS properties','CSS取值':'CSS values','选择器与语法':'Selectors & syntax','文字语言与字体':'Writing systems & fonts','接入与平台':'Bindings & platforms','分页与PDF':'Pagination & PDF'};
const statusText={zh:{full:'完整支持*',partial:'部分支持',none:'不支持',unknown:'未验证',na:'不适用'},en:{full:'Full within scope*',partial:'Partial',none:'Unsupported',unknown:'Unverified',na:'Not applicable'}};
let unmountApp;
async function boot(){
  const app=document.querySelector('#app');
  app.replaceChildren(Object.assign(document.createElement('p'),{textContent:'正在加载兼容性快照…',className:'boot-message'}));
  try{
    const response=await fetch(`${import.meta.env.BASE_URL}data/compatibility.json`);
    if(!response.ok)throw new Error(`HTTP ${response.status}`);
    const data=validateDataset(await response.json());
    app.replaceChildren();
    ({unmount:unmountApp}=mount(()=>Tracker(data),app));
  }catch(error){
    app.replaceChildren();
    const box=document.createElement('section');box.className='load-error';box.setAttribute('role','alert');
    const h=document.createElement('h1');h.textContent='无法加载兼容性快照';
    const p=document.createElement('p');p.textContent=`未显示缓存或伪造的支持状态。${String(error.message)}`;
    const button=document.createElement('button');button.textContent='重试';button.addEventListener('click',boot);
    box.append(h,p,button);app.append(box);
  }
}
function Tracker(data){
  const locale=state('zh');
  const route=state(parseRoute(location.hash,data));
  const toast=state('');
  let previousFocus=null,timer;
  const t=(zh,en)=>locale()==='zh'?zh:en;
  const catText=c=>locale()==='zh'?c:categoriesEn[c]||c;
  const filtered=compute(()=>filterFeatures(data.features,route()));
  const pagination=compute(()=>pageSlice(filtered(),route().page));
  const selected=compute(()=>data.features.find(f=>f.id===route().feature)||null);
  const totals=counts(data.features);
  const milestones=[...new Set(data.features.map(f=>f.introduced_milestone).filter(Boolean))].sort((a,b)=>Number(a.slice(1))-Number(b.slice(1)));
  function change(patch,reset=true){route({...route.peek(),...(reset?{page:1}:{}),...patch});}
  function clear(){route({q:'',category:'all',status:'all',milestone:'all',feature:'',page:1});}
  function notice(message){toast(message);clearTimeout(timer);timer=setTimeout(()=>toast(''),3500);}
  function close(){
    if(history.state?.factsPdfDetail)history.back();
    else change({feature:''},false);
    previousFocus?.focus();
  }
  function open(id,event){
    previousFocus=event?.currentTarget||document.activeElement;
    const next={...route.peek(),feature:id};
    const url=location.pathname+location.search+routeHash(next);
    if(url!==location.pathname+location.search+location.hash){
      if(route.peek().feature)history.replaceState(history.state,'',url);
      else history.pushState({factsPdfDetail:true},'',url);
    }
    route(next);
  }
  function exportCsv(){
    const url=URL.createObjectURL(new Blob([csv(filtered())],{type:'text/csv;charset=utf-8'}));
    const a=document.createElement('a');a.href=url;a.download=`FactsPDF-${data.commit.slice(0,8)}-filtered.csv`;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
  }
  const featureUrl=()=>location.origin+location.pathname+routeHash(route());
  async function copy(){try{await navigator.clipboard.writeText(featureUrl());notice(t('链接已复制','Link copied'));}catch{notice(t('无法访问剪贴板，请复制下方链接','Clipboard unavailable; copy the link below'));}}
  onMount(()=>{
    const dialog=document.querySelector('#feature-dialog');
    watch(()=>{
      const current=route();
      const url=location.pathname+location.search+routeHash(current);
      if(url!==location.pathname+location.search+location.hash)history.replaceState(history.state,'',url);
      queueMicrotask(()=>{
        if(current.feature&&!dialog.open){dialog.showModal();dialog.querySelector('.close')?.focus();}
        else if(!current.feature&&dialog.open)dialog.close();
      });
    });
    watch(()=>{document.documentElement.lang=locale()==='zh'?'zh-CN':'en';});
    const update=()=>route(parseRoute(location.hash,data));
    const key=e=>{if(e.key==='/' && !['INPUT','TEXTAREA','SELECT'].includes(e.target.tagName) && !dialog.open){e.preventDefault();document.querySelector('#query')?.focus();}};
    window.addEventListener('hashchange',update);window.addEventListener('keydown',key);
    onDispose(()=>{window.removeEventListener('hashchange',update);window.removeEventListener('keydown',key);clearTimeout(timer);});
  });
  function badge(f){return html`<span class=${'badge '+f.status}>${()=>statusText[locale()][f.status]}</span>`;}
  function detail(f){
    const parent=data.features.find(x=>x.id===f.parent_id);
    const children=data.features.filter(x=>x.parent_id===f.id);
    const testLink=f.test_reference?.startsWith('https://')?safeUrl(f.test_reference):null;
    return html`
      <header class="detail-head"><div><span class="eyebrow">${()=>catText(f.category)}</span><h2 id="detail-title">${f.feature}</h2>${badge(f)}</div><button class="close" aria-label="关闭详情 / Close details" @click=${close}>×</button></header>
      <div class="detail-body">
        <p class="detail-scope">${f.scope}</p>
        <div class="limitation"><h3>${()=>t('限制与边界','Limitations')}</h3><p>${f.limitations||'—'}</p></div>
        <dl class="detail-grid">
          <div><dt>${()=>t('已知引入阶段','Known introduction')}</dt><dd>${f.introduced_milestone||t('首见待核验','Not established')}</dd></div>
          <div><dt>${()=>t('发布版本','Released version')}</dt><dd>${f.introduced_release||t('未指定；非发布承诺','Not assigned; no release claim')}</dd></div>
          <div><dt>${()=>t('适用范围','Applies to')}</dt><dd>${f.applicability}</dd></div>
          <div><dt>${()=>t('解析 / 渲染阶段','Implementation layer')}</dt><dd>${f.stage}</dd></div>
          <div><dt>${()=>t('规划归属（不代表支持）','Planning (not support)')}</dt><dd>${f.planning_bucket}</dd></div>
          <div><dt>${()=>t('错误代码','Diagnostic')}</dt><dd><code>${f.failure_code||'—'}</code></dd></div>
        </dl>
        ${parent?html`<div class="related"><h3>${()=>t('上级能力','Parent capability')}</h3><button @click=${e=>open(parent.id,e)}>${parent.feature}</button></div>`:null}
        ${children.length?html`<div class="related"><h3>${()=>t('分项能力','Subfeatures')}</h3>${children.map(child=>html`<button @click=${e=>open(child.id,e)}>${child.feature} · ${()=>statusText[locale()][child.status]}</button>`)}</div>`:null}
        ${f.example?html`<h3>${()=>t('范围内示例','Scoped example')}</h3><pre><code>${f.example}</code></pre>`:null}
        <section class="evidence"><h3>${()=>t('核对证据','Audit evidence')}</h3><p>${f.evidence_level}</p><p class="mono">${f.last_audited_date} · ${f.last_audited_commit.slice(0,8)}</p>
          ${f.sources.map((source,index)=>html`<a href=${safeUrl(source)} target="_blank" rel="noopener noreferrer">${()=>t('查看来源','Source')} ${index+1} ↗ <span>${new URL(source).pathname.split('/').pop()}</span></a>`)}
          <p class="test-reference">${()=>t('测试定位：','Test reference: ')}${testLink?html`<a href=${testLink} target="_blank" rel="noopener noreferrer">${f.test_reference}</a>`:(f.test_reference||t('未关联独立测试','No individual test linked'))}</p>
        </section>
        <div class="share"><button @click=${copy}>${()=>t('复制此项链接','Copy feature link')}</button><input aria-label="Feature link" readonly .value=${featureUrl} /></div>
      </div>`;
  }
  // Purity 0.3.4 emits a text placeholder for a lone tbody expression.
  // Adjacent empty interpolation forces comment slots, which the HTML table
  // parser keeps inside tbody instead of foster-parenting before the table.
  return html`
    <a class="skip-link" href="#results" @click=${e=>{e.preventDefault();document.querySelector('#results')?.focus();}}>${()=>t('跳到结果','Skip to results')}</a>
    <header class="topbar"><a class="brand" href=${import.meta.env.BASE_URL}><span class="mark">F</span><span>FactsPDF <small>COMPATIBILITY</small></span></a><nav><span class="early">${()=>t('早期工程 · 非发布清单','Early engineering · not a release')}</span><button class="language" @click=${()=>locale(locale()==='zh'?'en':'zh')}>${()=>locale()==='zh'?'English':'中文'}</button><a class="repo" href="https://github.com/KoalaFacts/FactsPDF" target="_blank" rel="noopener noreferrer">GitHub ↗</a></nav></header>
    <div class="page-shell">
      <section class="hero"><div><div class="eyebrow">HTML · CSS · TEXT · RUNTIME</div><h1>Can I Render</h1><p>${()=>t('查支持、看边界、找证据。不是每一个绿色，都代表整个标准。','Check support. Read the limits. Follow the evidence. A green subfeature is not an entire standard.')}</p></div><div class="snapshot"><label for="snapshot">${()=>t('当前数据快照','Data snapshot')}</label><select id="snapshot" aria-label="Data snapshot"><option>${data.dataset_version}</option></select><a href=${safeUrl(data.commit_url)} target="_blank" rel="noopener noreferrer"><span class="mono">${data.commit.slice(0,8)}</span> · ${data.audited_at} ↗</a></div></section>
      <section class="statbar" aria-label="Snapshot status counts">
        <div class="stat total"><span class="stat-num" data-total>${data.features.length}</span><span>${()=>t('跟踪记录','Tracked records')}</span></div>
        ${STATUSES.map(s=>html`<button class=${()=>`stat ${s} ${route().status===s?'active':''}`} aria-pressed=${()=>route().status===s?'true':'false'} @click=${()=>change({status:route.peek().status===s?'all':s})}><span class="stat-num">${totals[s]}</span><span><i class=${'dot '+s}></i>${()=>statusText[locale()][s]}</span></button>`)}
      </section>
      <p class="inventory-note">${()=>t(`* 完整支持仅针对该行定义的范围。${data.features.length} 是跟踪记录数，不是标准覆盖率或产品完成率。`,`* Full means the scope explicitly defined by that row. ${data.features.length} is an inventory count, not standards coverage or product completion.`)}</p>
      <div class="workspace">
        <aside class="sidebar"><h2>${()=>t('能力分类','Browse capabilities')}</h2><button class=${()=>route().category==='all'?'category selected':'category'} @click=${()=>change({category:'all'})}><span>${()=>t('全部能力','All capabilities')}</span><b>${data.features.length}</b></button>
          ${Object.entries(data.categories).map(([c,n])=>html`<button class=${()=>route().category===c?'category selected':'category'} @click=${()=>change({category:c})}><span>${()=>catText(c)}</span><b>${n}</b></button>`)}
          <section class="scope-note"><h3>${()=>t('不要混淆','Keep these separate')}</h3><p>${()=>t('语法可解析 ≠ 能正确渲染','Parsable syntax ≠ rendered support')}</p><p>${()=>t('文档文字语言 ≠ 编程语言接入','Writing system ≠ language binding')}</p><p>${()=>t('计划实现 ≠ 已经支持','Planned ≠ supported')}</p></section>
        </aside>
        <main id="results" tabindex="-1">
          <div class="searchbox"><span aria-hidden="true">⌕</span><input id="query" type="search" role="searchbox" aria-label="Search compatibility" placeholder=${()=>t('搜索元素、属性、取值或限制…','Search elements, properties, values or limitations…')} .value=${()=>route().q} @input=${e=>change({q:e.target.value})} /><kbd>/</kbd></div>
          <div class="toolbar"><label><span>${()=>t('类别','Category')}</span><select aria-label=${()=>t('类别筛选','Category filter')} .value=${()=>route().category} @change=${e=>change({category:e.target.value})}><option value="all">${()=>t('全部类别','All categories')}</option>${Object.keys(data.categories).map(c=>html`<option value=${c}>${()=>catText(c)}</option>`)}</select></label><label><span>${()=>t('支持状态','Status')}</span><select aria-label=${()=>t('支持状态筛选','Support status filter')} .value=${()=>route().status} @change=${e=>change({status:e.target.value})}><option value="all">${()=>t('全部状态','All statuses')}</option>${STATUSES.map(s=>html`<option value=${s}>${()=>statusText[locale()][s]}</option>`)}</select></label><label><span>${()=>t('引入阶段','Introduced')}</span><select aria-label=${()=>t('引入阶段筛选','Introduction milestone filter')} .value=${()=>route().milestone} @change=${e=>change({milestone:e.target.value})}><option value="all">${()=>t('全部阶段','All milestones')}</option>${milestones.map(m=>html`<option value=${m}>${m}</option>`)}<option value="unknown">${()=>t('首见待核验','Not established')}</option></select></label><button class="reset" @click=${clear}>${()=>t('清除筛选','Clear filters')}</button></div>
          <a class="download-link" href=${`${import.meta.env.BASE_URL}data/compatibility.json`} download="FactsPDF-compatibility.json">${()=>t('下载原始 JSON','Download source JSON')} ↓</a>
          <div class="results-heading"><h2>${()=>route().category==='all'?t('全部能力', 'All capabilities'):catText(route().category)} <span>${()=>filtered().length}</span></h2><button class="export" @click=${exportCsv}>${()=>t('导出筛选结果 CSV','Export filtered CSV')} ↓</button></div>
          <div class="table-wrap"><table><caption class="visually-hidden">FactsPDF compatibility with scoped status and audit evidence</caption><thead><tr><th>${()=>t('元素 / 能力','Feature')}</th><th>${()=>t('当前支持','Support')}</th><th>${()=>t('范围与限制','Scope & limitations')}</th><th>${()=>t('引入阶段','Since')}</th></tr></thead><tbody>
            ${null}${each(()=>pagination().items,item=>html`<tr><td><button class="feature-button" @click=${e=>open(item().id,e)}>${()=>item().feature}</button><small>${()=>catText(item().category)}</small></td><td><span class=${()=>`badge ${item().status}`}>${()=>statusText[locale()][item().status]}</span></td><td><p class="scope">${()=>item().scope}</p><p class="limits">${()=>item().limitations}</p></td><td><span class="milestone">${()=>item().introduced_milestone||'—'}</span><small>${()=>item().introduced_milestone?t('阶段，非版本','Not a release'):t('首见待核验','Not established')}</small></td></tr>`,item=>item.id)}
          </tbody></table></div>
          ${()=>filtered().length===0?html`<section class="empty" data-testid="empty"><h3>${t('没有匹配记录','No matching records')}</h3><p>${t('未找到不等于支持或不支持，请调整筛选。','No match is not a support claim. Try another filter.')}</p><button @click=${clear}>${t('清除筛选','Clear filters')}</button></section>`:null}
          <div class="pagination"><span aria-live="polite">${()=>`${t('第','Page')} ${pagination().page} / ${pagination().totalPages} · ${filtered().length} ${t('条记录','records')}`}</span><div><button ?disabled=${()=>pagination().page<=1} aria-label=${()=>t('上一页','Previous page')} @click=${()=>change({page:pagination().page-1},false)}>← ${()=>t('上一页','Previous')}</button><button ?disabled=${()=>pagination().page>=pagination().totalPages} aria-label=${()=>t('下一页','Next page')} @click=${()=>change({page:pagination().page+1},false)}>${()=>t('下一页','Next')} →</button></div></div>
          <details class="method"><summary>${()=>t('这份数据如何判定？','How is support determined?')}</summary><p>${data.audit_method}</p><p>${data.css_inventory_note}</p><p>${()=>t('界面语言可切换；原始审计范围与限制保留中文，不生成未经审核的翻译。','The interface is bilingual. Original audit notes remain in Chinese; no unreviewed translation is substituted.')}</p><h3>${()=>t('已记录的文档差异','Recorded documentation drift')}</h3>${data.documentation_drift.map(d=>html`<p><code>${d.path}</code> — ${d.problem}</p>`)}</details>
        </main>
      </div>
      <footer><span>FactsPDF · ${()=>t('工程能力查询，不是发布承诺','Engineering inventory, not a release promise')}</span><span>Purity.js · npm <code>@purityjs/core</code> · <span class="mono">${__SITE_COMMIT__.slice(0,8)}</span></span></footer>
    </div>
    <dialog id="feature-dialog" aria-labelledby="detail-title" @cancel=${e=>{e.preventDefault();close();}} @click=${e=>{if(e.target===e.currentTarget)close();}}>${each(()=>selected()?[selected()]:[],item=>detail(item()),item=>item.id)}</dialog>
    <div class="toast" role="status" aria-live="polite">${()=>toast()}</div>
  `;
}
boot();
