// Automatic table layout oracle: renders every synthetic fixture in
// tests/fixtures/tables-auto/ with the FactsPDF CLI and with headless Chrome,
// both using the same TrueType font, and compares the width and position of
// every table cell.
//
//   node scripts/table-auto-layout-oracle.ts --cli <FactsPDF.Cli> --chrome <chrome>
//        --font <regular.ttf> --font-bold <bold.ttf> [--tolerance <pt>] [--out <dir>]
//
// FactsPDF: each cell paints the fixture's cell background (#eef3f7); its
// rectangles are read back from the uncompressed page content stream, in
// source order. Chrome: the same HTML with a reference-only stylesheet that
// loads the two fonts as data URLs and turns off kerning and ligatures (the
// engine applies neither), and an A4 body 523.28pt wide (the CLI's default
// page content width); a script reads getBoundingClientRect() of every td/th.
// Positions are relative to the first cell's left edge. The run fails when any
// cell's left edge or width differs by more than the tolerance (default
// 0.15pt: Chrome lays out in 1/64px units, and a percentage column that sets the
// table width multiplies that rounding). The browser and fonts are test tools only.
import { execFileSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

export interface Cell {
  x: number;
  width: number;
}

interface Options {
  cli: string;
  chrome: string;
  font: string;
  fontBold: string;
  tolerance: number;
  out: string | null;
}

function parseArguments(argv: readonly string[]): Options {
  const values = new Map<string, string>();
  for (let i = 0; i < argv.length; i += 2) {
    const name = argv[i];
    const value = argv[i + 1];
    if (!name.startsWith('--') || value === undefined) {
      throw new Error(`expected --option value, got ${name}`);
    }
    values.set(name.slice(2), value);
  }
  const required = (name: string) => {
    const value = values.get(name);
    if (value === undefined) {
      throw new Error(`--${name} is required`);
    }
    return value;
  };
  return {
    cli: required('cli'),
    chrome: required('chrome'),
    font: required('font'),
    fontBold: required('font-bold'),
    tolerance: Number(values.get('tolerance') ?? '0.15'),
    out: values.get('out') ?? null,
  };
}

/** The cell rectangles (x and width in pt) a FactsPDF page paints with the cell background, in paint order. */
export function pdfCells(pdf: string): Cell[] {
  const cells: Cell[] = [];
  const fill = /q 0\.933 0\.953 0\.969 rg (-?[\d.]+) (-?[\d.]+) ([\d.]+) ([\d.]+) re f Q/g;
  for (const match of pdf.matchAll(fill)) {
    cells.push({ x: Number(match[1]), width: Number(match[3]) });
  }
  return cells;
}

/** The fixture HTML with the reference-only Chrome stylesheet and measuring script added before </head>. */
export function chromePage(html: string, font: Buffer, fontBold: Buffer): string {
  const face = (data: Buffer, weight: string) =>
    `@font-face{font-family:OracleFont;font-weight:${weight};src:url(data:font/ttf;base64,${data.toString('base64')}) format("truetype")}`;
  const style = `<style>${face(font, 'normal')}${face(fontBold, 'bold')}`
    + '*{font-family:OracleFont!important;font-kerning:none!important;font-variant-ligatures:none!important}'
    + 'html,body{margin:0;padding:0}body{width:523.28pt}</style>';
  const script = '<script>document.fonts.ready.then(()=>{const cells=[...document.querySelectorAll("td,th")]'
    + '.map(c=>{const r=c.getBoundingClientRect();return{x:r.left,width:r.width}});'
    + 'const fonts=[...document.fonts].map(f=>f.status);'
    + 'const pre=document.createElement("pre");pre.id="oracle";pre.textContent=JSON.stringify({cells,fonts});'
    + 'document.body.appendChild(pre)})</script>';
  return html.replace('</head>', style + '</head>').replace('</body>', script + '</body>');
}

function relativeCells(cells: Cell[], scale: number): Cell[] {
  const origin = cells.length === 0 ? 0 : cells[0].x;
  return cells.map((cell) => ({ x: (cell.x - origin) * scale, width: cell.width * scale }));
}

function main(): void {
  const options = parseArguments(process.argv.slice(2));
  const repo = resolve(import.meta.dirname, '..');
  const fixtures = join(repo, 'tests', 'fixtures', 'tables-auto');
  const work = mkdtempSync(join(tmpdir(), 'table-auto-oracle-'));
  const font = readFileSync(options.font);
  const fontBold = readFileSync(options.fontBold);
  const version = execFileSync(options.chrome, ['--version'], { encoding: 'utf8' }).trim();
  const report: { chrome: string; tolerance: number; fixtures: unknown[]; maxDifference: number } =
    { chrome: version, tolerance: options.tolerance, fixtures: [], maxDifference: 0 };
  let failed = false;
  try {
    for (const name of readdirSync(fixtures).filter((file) => file.endsWith('.html')).sort()) {
      const html = readFileSync(join(fixtures, name), 'utf8');
      const pdfPath = join(work, name.replace(/\.html$/, '.pdf'));
      execFileSync(options.cli, [join(fixtures, name), pdfPath, '--subset-fonts',
        '--font', options.font, '--font-bold', options.fontBold], { stdio: 'pipe' });
      const engine = relativeCells(pdfCells(readFileSync(pdfPath, 'latin1')), 1);
      const pagePath = join(work, name);
      writeFileSync(pagePath, chromePage(html, font, fontBold));
      const dom = execFileSync(options.chrome, ['--headless=new', '--disable-gpu', '--no-first-run',
        `--user-data-dir=${join(work, 'profile')}`, '--virtual-time-budget=10000', '--dump-dom',
        pathToFileURL(pagePath).href], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, stdio: ['ignore', 'pipe', 'ignore'] });
      const json = /<pre id="oracle">([^<]*)<\/pre>/.exec(dom);
      if (json === null) {
        throw new Error(`${name}: Chrome produced no measurement`);
      }
      const measured = JSON.parse(json[1].replace(/&quot;/g, '"').replace(/&amp;/g, '&')) as { cells: Cell[]; fonts: string[] };
      // A face no text uses (bold, in a fixture without bold text) stays unloaded.
      if (!measured.fonts.includes('loaded') || measured.fonts.some((status) => status === 'error' || status === 'loading')) {
        throw new Error(`${name}: Chrome did not load the oracle fonts (${measured.fonts.join(', ')})`);
      }
      const browser = relativeCells(measured.cells, 0.75);
      if (browser.length !== engine.length) {
        throw new Error(`${name}: Chrome measured ${browser.length} cells, FactsPDF painted ${engine.length}`);
      }
      let max = 0;
      const differences = browser.map((cell, i) => {
        const dx = engine[i].x - cell.x;
        const dw = engine[i].width - cell.width;
        max = Math.max(max, Math.abs(dx), Math.abs(dw));
        return { chrome: cell, factspdf: engine[i], dx: Number(dx.toFixed(4)), dw: Number(dw.toFixed(4)) };
      });
      report.maxDifference = Math.max(report.maxDifference, max);
      const pass = max <= options.tolerance;
      failed ||= !pass;
      report.fixtures.push({ name, cells: differences.length, maxDifference: Number(max.toFixed(4)), pass, differences });
      console.log(`${pass ? 'PASS' : 'FAIL'} ${name}: ${differences.length} cells, max difference ${max.toFixed(4)}pt`);
    }
  } finally {
    rmSync(work, { recursive: true, force: true });
  }
  console.log(`${version}; tolerance ${options.tolerance}pt; max difference ${report.maxDifference.toFixed(4)}pt`);
  if (options.out !== null) {
    mkdirSync(options.out, { recursive: true });
    writeFileSync(join(options.out, 'table-auto-oracle.json'), JSON.stringify(report, null, 2) + '\n');
  }
  if (failed) {
    process.exitCode = 1;
  }
}

if (import.meta.main) {
  main();
}
