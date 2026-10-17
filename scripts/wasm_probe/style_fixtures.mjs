import { pathToFileURL } from 'node:url';

// Independently authored expected text; do not derive expectations by parsing generated PDFs.
const css = '<style>body{font-size:12pt;line-height:1.5}div{width:190pt;padding:8pt;border:1pt solid #475569}p{margin-bottom:8pt}.next{break-before:page}</style>';
const report = [
  ['样式核验 / Font styles', '样式核验 / Font styles'],
  ['北京团队 / Beijing team', '北京团队 / Beijing team'],
  ['<strong>复核通过 / Review approved</strong>', '复核通过 / Review approved'],
  ['<em>备注 café / Note café</em>', '备注 café / Note café'],
  ['<strong><em>优先处理 / Priority action</em></strong>', '优先处理 / Priority action'],
  ['连续词 oper<strong>ation</strong> 与中文换行，保持真实字体宽度。', '连续词 operation 与中文换行，保持真实字体宽度。'],
  ['<strong><em>粗斜体 <span style="font-weight:normal">恢复斜体</span> <span style="font-style:normal">恢复粗体</span></em></strong>', '粗斜体 恢复斜体 恢复粗体'],
];
const records = Array.from({ length: 60 }, (_, i) => {
  const id = `R${String(i + 1).padStart(3, '0')}`;
  return { html: `<p>${id} 北京 / Team <b>通过 / Approved</b> <i>备注 / Note</i> <b><i>优先 / Priority</i></b></p>`,
    text: `${id} 北京 / Team 通过 / Approved 备注 / Note 优先 / Priority` };
});
const longItalic = Array.from({ length: 90 }, (_, i) => `I${String(i + 1).padStart(3, '0')} 备注 café`).join(' ');
const longBoldItalic = Array.from({ length: 90 }, (_, i) => `J${String(i + 1).padStart(3, '0')} 优先 action`).join(' ');
export const styleCases = [
  { name: 'styles-report', html: css + '<body><div>' + report.map(([html]) => `<p>${html}</p>`).join('') +
    '<p class=next>显式分页 / Next page</p><p><i>斜体中文与 English italic</i></p><p><b><i>粗斜体中文与 English bold italic</i></b></p></div></body>',
    text: report.map(([, text]) => text).join(' ') + ' 显式分页 / Next page 斜体中文与 English italic 粗斜体中文与 English bold italic', pages: 2 },
  { name: 'styles-records', html: css + '<body><div>' + records.map(r => r.html).join('') + '</div></body>',
    text: records.map(r => r.text).join(' '), pages: 5 },
  { name: 'styles-long', html: css + `<body><div><p style="font-style:italic">${longItalic}</p><p style="font-weight:bold;font-style:italic">${longBoldItalic}</p></div></body>`,
    text: longItalic + ' ' + longBoldItalic, pages: 3 },
  { name: 'styles-latin', html: css + '<body><p>Normal café <b>Bold café</b> <i>Italic café</i> <b><i>Bold italic café</i></b></p></body>',
    text: 'Normal café Bold café Italic café Bold italic café', pages: 1, selection: 'latin-only' },
  { name: 'styles-courier', html: css + '<body><p>Courier ASCII <i>斜体 café</i> normal again.</p></body>',
    text: 'Courier ASCII 斜体 café normal again.', pages: 1, selection: 'courier' },
];
const phraseRoles = {
  'styles-report': [['normal', '北京团队 / Beijing team'], ['bold', '复核通过 / Review approved'],
    ['italic', '备注 café / Note café'], ['boldItalic', '优先处理 / Priority action'],
    ['italic', '恢复斜体'], ['bold', '恢复粗体'], ['bold', 'ation']],
  'styles-records': [['normal', '北京 / Team'], ['bold', '通过 / Approved'], ['italic', '备注 / Note'], ['boldItalic', '优先 / Priority']],
  'styles-long': [['italic', '备注 café'], ['boldItalic', '优先 action']],
  'styles-latin': [['normal', 'Normal café'], ['bold', 'Bold café'], ['italic', 'Italic café'], ['boldItalic', 'Bold italic café']],
  'styles-courier': [['normal', 'Courier ASCII'], ['italic', '斜体 café'], ['normal', 'normal again.']],
};
for (const item of styleCases) item.phraseRoles = phraseRoles[item.name];

export const styleErrorCases = [
  { name: 'styles-missing-bold', html: '<p><b>A</b></p>', roles: [], code: 'FPDF1507' },
  { name: 'styles-missing-italic', html: '<p><i>A</i></p>', roles: ['bold'], code: 'FPDF1507' },
  { name: 'styles-missing-both', html: '<p><b><i>A</i></b></p>', roles: ['bold', 'italic'], code: 'FPDF1507' },
  { name: 'styles-missing-glyph', html: '<p><i>中</i></p>', roles: ['normal', 'italic'], latinOnly: true, code: 'FPDF1504' },
  { name: 'styles-no-normal', html: '<p>中</p>', roles: ['italic'], code: 'FPDF1301' },
  { name: 'styles-unsupported', html: '<style>.absent{font-style:oblique}</style><p>A</p>', roles: [], code: 'FPDF1202' },
];
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href)
  console.log(JSON.stringify({ cases: styleCases, errors: styleErrorCases }));
