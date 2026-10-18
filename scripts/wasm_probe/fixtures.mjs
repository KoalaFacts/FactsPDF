export const cases = [
  { name: 'ascii', html: '<html><body><p>Node CLI probe 2026.</p></body></html>',
    text: 'Node CLI probe 2026.', pages: 1, fonts: false },
  { name: 'bilingual', html: '<html><body><p>报告 探针 café</p></body></html>',
    text: '报告 探针 café', pages: 1, fonts: true },
  { name: 'layout', html: '<html><body><div style="width:120pt;padding:8pt;border:1pt solid #123456;background-color:#eef4fa"><p>Layout probe with enough words to wrap inside the explicit narrow block.</p></div><p style="break-before:page">Second page 报告 café</p></body></html>',
    text: 'Layout probe with enough words to wrap inside the explicit narrow block. Second page 报告 café', pages: 2, fonts: true },
];
export const unsupportedHtml = '<html><body><video></video></body></html>';
