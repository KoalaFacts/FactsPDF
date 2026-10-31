import { readFileSync } from 'node:fs';

// Authored content/number expectations, never read from generated PDF output.
const parts = ['列表报告 / List report'];
for (let i = 1; i <= 12; i++) {
  parts.push(`${i}. C2-${String(i).padStart(3, '0')} 北京团队核对第 ${i} 项记录。Beijing team checks record ${i} and preserves every report identifier.`);
  if (i === 3) parts.push(...Array(38).fill('跨页长项继续记录复核步骤。Continue this long item without repeating its marker.'));
  if (i === 5) parts.push('C2-N01 检查附件与来源。Check attachments and sources.',
    'C2-N02 保留续行缩进和层级。Keep continuation indentation and hierarchy.');
  if (i === 8) parts.push('1. C2-N03 子步骤从一开始。Nested numbering starts at one.',
    '2. C2-N04 子步骤继续。Nested numbering continues.');
}
parts.push('独立列表重新编号。A separate list restarts numbering.',
  '1. C2-R01 新列表第一项。First item in a separate list.', '2.',
  '3. C2-R03 空项仍占编号。Empty items still count.',
  'C2-END 列表结束，正文恢复原来的内容宽度。End of lists; ordinary text resumes.');

export const listReport = {
  name: 'lists-report', html: readFileSync(new URL('../../examples/lists-report.html', import.meta.url), 'utf8'),
  text: parts.join(' '), pages: 5, fonts: 'style-normal', ignoreLayoutWhitespace: true, reader: true,
};
export const listAscii = {
  name: 'lists-ascii', html: '<ol><li>First<ul><li>Nested</li></ul></li><li><p style="break-before:page">Second</p></li></ol>',
  text: '1. First Nested 2. Second', pages: 2, fonts: false,
};
export const listErrors = [
  { name: 'lists-third-level', html: '<ol><li><ul><li><ol></ol></li></ul></li></ol>', code: 'FPDF1101' },
  { name: 'lists-attribute', html: '<ol start="0"><li>A</li></ol>', code: 'FPDF1103' },
  { name: 'lists-gutter', html: '<ol style="width:10pt"><li>A</li></ol>', code: 'FPDF1302' },
  { name: 'lists-marker-style', html: '<ol><li style="font-weight:bold"><p style="font-weight:normal">A</p></li></ol>', code: 'FPDF1507' },
];
