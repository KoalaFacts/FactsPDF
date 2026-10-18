import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

// Expected content and tracks are authored independently of generated PDFs.
const rows = [
  ['编号 / ID', '状态 / Status', '说明 / Description'],
  ['E1-001', '常规 / Normal', '明确列宽保持稳定。Explicit columns keep every row aligned.'],
  ['E1-002', '通过 / Approved', '北京团队完成复核。Beijing team approved the record.'],
  ['E1-003', '备注 / Note', '斜体使用真实字形。Italic text uses supplied font outlines.'],
  ['E1-004', '优先 / Priority', '优先处理本条记录。Give this record priority action.'],
  ['E1-005', '组合 / Mixed', '常规 粗体 斜体 粗斜体。Normal, bold, italic, and bold italic.'],
  ['E1-006', '换行 / Wrapping', '第一行保留完整语义。Second line remains inside its fixed column and wraps at the available content width.'],
];
const text = rows.flat().join(' ');
const phraseRoles = [['bold', '编号 / ID'], ['normal', '常规 / Normal'], ['bold', '通过 / Approved'],
  ['italic', '备注 / Note'], ['boldItalic', '优先 / Priority']];
const before = Array.from({ length: 24 }, (_, i) => `${i + 1}. LEAD-${String(i + 1).padStart(2, '0')} 表格之前的记录 / Record before the table.`).join(' ');
export const tableCases = [
  { name: 'tables-report', html: readFileSync(new URL('../../examples/tables-report.html', import.meta.url), 'utf8'), pages: 1,
    text: '固定列表格 / Fixed-column report 显式列宽、真实字体、单页完整表格。Explicit widths, real fonts, and one complete table. ' + text + ' E1-END 所有记录保持顺序。Every record remains in source order.',
    phraseRoles, rows, tracks: [70, 130, 300], tableX: 36, tablePage: 1, captureTop: true },
  { name: 'tables-page-movement', html: readFileSync(new URL('../../examples/tables-page-movement.html', import.meta.url), 'utf8'), pages: 2,
    text: '整表换页 / Whole-table movement ' + before + ' ' + text + ' AFTER-ONE 表格之后的第一项 / First item after the table. AFTER-TWO 顺序与缩进保持不变 / Order and indentation remain intact. MOVE-END 表格完整位于下一页。The whole table starts on the next page.',
    phraseRoles, rows, tracks: [70, 130, 300], tableX: 41, tablePage: 2, tableTop: 36, captureTop: true },
];
export const tableAscii = { name: 'tables-points', html: readFileSync(new URL('../../tests/fixtures/tables-e1/points.html', import.meta.url), 'utf8'),
  text: 'ID Category Description R001 Overview One complete row R002 Detail First line Second line', pages: 1, fonts: false, ignoreLayoutWhitespace: true };
const manifest = JSON.parse(readFileSync(new URL('../../tests/fixtures/tables-e1/manifest.json', import.meta.url), 'utf8'));
export const tableErrors = manifest.cases.filter(item => item.expected.code).map(item => ({
  name: `tables-${item.id}`, html: readFileSync(new URL(`../../tests/fixtures/tables-e1/${item.file}`, import.meta.url), 'utf8'), code: item.expected.code,
}));
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href)
  console.log(JSON.stringify({ cases: tableCases, ascii: tableAscii, errors: tableErrors }));
