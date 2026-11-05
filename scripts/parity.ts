// Byte-parity check: renders every examples/*.html with the CLI built from a
// base commit and the CLI built from the working tree, with three option sets,
// and fails when any exit code, stderr text or PDF byte differs.
//
//   node scripts/parity.ts [--base <ref>] [--work <dir>] [--font <path.ttf>]
//                          [--dotnet <path>] [--python <path>] [--jobs <n>]
//
// --work is a parent folder outside the repository (default: the system temp
// folder); each run builds in a new subfolder of it and nothing else there is
// touched, except that the report is also copied to <work>/parity.json. The
// subfolder is removed when every run is identical.
// Defaults: --base origin/main,
// --font /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf.
import { execFile, execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, realpathSync, rmSync, writeFileSync } from 'node:fs';
import { availableParallelism, tmpdir } from 'node:os';
import { isAbsolute, join, relative, resolve, sep } from 'node:path';

export interface OptionSet {
  name: string;
  args: readonly string[];
}

export interface RunResult {
  exitCode: number;
  stderr: string;
  pdf: Buffer | null;
}

export interface Difference {
  example: string;
  set: string;
  reason: string;
}

export interface ParityOptions {
  base: string;
  work: string;
  font: string;
  dotnet: string;
  python: string;
  jobs: number;
}

const defaults: ParityOptions = {
  base: 'origin/main',
  work: tmpdir(),
  font: '/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',
  dotnet: 'dotnet',
  python: 'python3',
  jobs: Math.max(1, Math.min(8, availableParallelism())),
};

export function parseArguments(argv: readonly string[]): ParityOptions {
  const options = { ...defaults };
  for (let i = 0; i < argv.length; i++) {
    const name = argv[i];
    const value = argv[i + 1];
    if (!name.startsWith('--') || value === undefined || value.startsWith('--')) {
      throw new Error(`expected --option value, got ${name}`);
    }
    i++;
    switch (name) {
      case '--base':
      case '--work':
      case '--font':
      case '--dotnet':
      case '--python':
        options[name.slice(2) as 'base' | 'work' | 'font' | 'dotnet' | 'python'] = value;
        break;
      case '--jobs': {
        const jobs = Number(value);
        if (!Number.isInteger(jobs) || jobs < 1) {
          throw new Error(`--jobs must be a positive integer, got ${value}`);
        }
        options.jobs = jobs;
        break;
      }
      default:
        throw new Error(`unknown option ${name}`);
    }
  }
  return options;
}

/** The three option sets every example is rendered with. */
export function optionSets(repo: string, font: string, images: string): OptionSet[] {
  const image = (name: string, file: string) => ['--image', `${name}=${join(images, file)}`];
  return [
    { name: 'defaults', args: [] },
    {
      name: 'subset-letter',
      args: [
        '--font', font, '--subset-fonts', '--page-numbers', '--header-text', 'H', '--title', 'T',
        ...image('logo', 'brand-mark.png'), ...image('chart', 'rgb.png'), ...image('quality-chart', 'rgba.png'),
        '--page-size', 'letter', '--landscape', '--margin', '20mm',
      ],
    },
    {
      name: 'furniture-a5',
      args: [
        '--font', font,
        ...image('logo', 'brand-mark.png'), ...image('chart', 'rgb.png'), ...image('quality-chart', 'rgba.png'),
        '--page-furniture', join(repo, 'examples', 'business-page-furniture.json'),
        '--page-width', '148mm', '--page-height', '210mm',
      ],
    },
  ];
}

/** Explains how two runs of the same example and option set differ, or null when identical. */
export function compareRuns(base: RunResult, head: RunResult): string | null {
  if (base.exitCode !== head.exitCode) {
    return `exit code ${base.exitCode} -> ${head.exitCode}`;
  }
  if (base.stderr !== head.stderr) {
    return `stderr differs: ${JSON.stringify(base.stderr.slice(0, 200))} -> ${JSON.stringify(head.stderr.slice(0, 200))}`;
  }
  if ((base.pdf === null) !== (head.pdf === null)) {
    return base.pdf === null ? 'only head wrote a PDF' : 'only base wrote a PDF';
  }
  if (base.pdf !== null && head.pdf !== null && !base.pdf.equals(head.pdf)) {
    const length = Math.min(base.pdf.length, head.pdf.length);
    let offset = 0;
    while (offset < length && base.pdf[offset] === head.pdf[offset]) {
      offset++;
    }
    return `PDF bytes differ at offset ${offset} (${base.pdf.length} -> ${head.pdf.length} bytes)`;
  }
  return null;
}

/** The exit code of a finished process; a launch failure or a signal is an error, never a result. */
export function exitCodeOf(error: { code?: unknown; signal?: unknown } | null): number {
  if (error === null) {
    return 0;
  }
  if (typeof error.code === 'number' && (error.signal === null || error.signal === undefined)) {
    return error.code;
  }
  const cause = error.signal ? `signal ${String(error.signal)}` : `error ${String(error.code)}`;
  throw new Error(`the CLI did not run to completion (${cause})`);
}

function run(file: string, args: readonly string[], cwd: string): Promise<{ code: number; stderr: string }> {
  return new Promise((done, fail) => {
    execFile(file, args, { cwd, maxBuffer: 64 * 1024 * 1024, encoding: 'utf8' }, (error, _stdout, stderr) => {
      try {
        done({ code: exitCodeOf(error), stderr });
      } catch (failure) {
        fail(new Error(`${failure instanceof Error ? failure.message : String(failure)}: ${args.join(' ')}`));
      }
    });
  });
}

async function render(dotnet: string, cli: string, input: string, set: OptionSet, cwd: string): Promise<RunResult> {
  mkdirSync(cwd, { recursive: true });
  const output = join(cwd, 'out.pdf');
  rmSync(output, { force: true });
  // A relative output path keeps any diagnostic text identical between base and head.
  const result = await run(dotnet, [cli, input, 'out.pdf', '--overwrite', ...set.args], cwd);
  return { exitCode: result.code, stderr: result.stderr, pdf: existsSync(output) ? readFileSync(output) : null };
}

function buildCli(dotnet: string, source: string, output: string): string {
  execFileSync(
    dotnet,
    ['build', join(source, 'src', 'FactsPDF.Cli', 'FactsPDF.Cli.csproj'), '-c', 'Release', '-o', output, '-nologo', '-v', 'q'],
    { stdio: ['ignore', 'ignore', 'inherit'] },
  );
  return join(output, 'FactsPDF.Cli.dll');
}

async function pool<T>(items: readonly T[], jobs: number, work: (item: T) => Promise<void>): Promise<void> {
  let next = 0;
  const workers = Array.from({ length: Math.min(jobs, items.length) }, async () => {
    while (next < items.length) {
      await work(items[next++]);
    }
  });
  await Promise.all(workers);
}

/** Rejects a --work folder inside the repository, which the run would litter or clobber. */
export function workParent(repo: string, work: string): string {
  const parent = resolve(work);
  const real = (path: string) => (existsSync(path) ? realpathSync(path) : path);
  const inside = relative(real(resolve(repo)), real(parent));
  if (inside === '' || (inside !== '..' && !inside.startsWith(`..${sep}`) && !isAbsolute(inside))) {
    throw new Error(`--work must be outside the repository: ${parent}`);
  }
  return parent;
}

export async function checkParity(repo: string, options: ParityOptions): Promise<{ runs: number; report: string; exitCodes: Record<string, Record<string, number>>; differences: Difference[] }> {
  if (!existsSync(options.font)) {
    throw new Error(`font not found: ${options.font} (pass --font)`);
  }
  const baseCommit = execFileSync('git', ['rev-parse', '--verify', `${options.base}^{commit}`], { cwd: repo, encoding: 'utf8' }).trim();
  const parent = workParent(repo, options.work);
  mkdirSync(parent, { recursive: true });
  const work = mkdtempSync(join(parent, 'factspdf-parity-'));
  const tree = join(work, 'base-tree');
  let identical = false;
  execFileSync('git', ['worktree', 'prune'], { cwd: repo });
  execFileSync('git', ['worktree', 'add', '--detach', tree, baseCommit], { cwd: repo, stdio: 'ignore' });
  try {
    console.log(`parity: base ${baseCommit.slice(0, 12)} vs working tree`);
    const baseCli = buildCli(options.dotnet, tree, join(work, 'base-cli'));
    const headCli = buildCli(options.dotnet, repo, join(work, 'head-cli'));
    const images = join(work, 'images');
    execFileSync(options.python, [join(repo, 'scripts', 'png_fixtures.py'), '--output', images], { stdio: 'ignore' });

    const examples = readdirSync(join(repo, 'examples'))
      .filter((name) => name.endsWith('.html'))
      .sort();
    const sets = optionSets(repo, resolve(options.font), images);
    const cases = examples.flatMap((example) => sets.map((set) => ({ example, set })));
    const differences: Difference[] = [];
    const exitCodes: Record<string, Record<string, number>> = {};
    await pool(cases, options.jobs, async ({ example, set }) => {
      const input = join(repo, 'examples', example);
      const id = `${example}.${set.name}`;
      const base = await render(options.dotnet, baseCli, input, set, join(work, 'runs', 'base', id));
      const head = await render(options.dotnet, headCli, input, set, join(work, 'runs', 'head', id));
      const tally = (exitCodes[set.name] ??= {});
      tally[head.exitCode] = (tally[head.exitCode] ?? 0) + 1;
      const reason = compareRuns(base, head);
      if (reason !== null) {
        differences.push({ example, set: set.name, reason });
      }
    });
    for (const set of sets) {
      if ((exitCodes[set.name]?.['0'] ?? 0) === 0) {
        throw new Error(`no example rendered with the ${set.name} option set, so it checks nothing`);
      }
    }
    differences.sort((a, b) => `${a.example}.${a.set}`.localeCompare(`${b.example}.${b.set}`));
    const report = JSON.stringify({ base: baseCommit, runs: cases.length, exitCodes, differences }, null, 2) + '\n';
    writeFileSync(join(work, 'parity.json'), report);
    writeFileSync(join(parent, 'parity.json'), report);
    identical = differences.length === 0;
    return { runs: cases.length, report: join(parent, 'parity.json'), exitCodes, differences };
  } finally {
    try {
      execFileSync('git', ['worktree', 'remove', '--force', tree], { cwd: repo, stdio: 'ignore' });
    } catch (error) {
      console.error(`parity: could not remove the worktree ${tree}: ${error instanceof Error ? error.message : String(error)}`);
    }
    if (identical) {
      // Only the subfolder this run created; keep it for inspection when anything differs.
      rmSync(work, { recursive: true, force: true });
    }
  }
}

if (import.meta.main) {
  const repo = resolve(import.meta.dirname, '..');
  try {
    const options = parseArguments(process.argv.slice(2));
    const { runs, report, exitCodes, differences } = await checkParity(repo, options);
    for (const [set, tally] of Object.entries(exitCodes)) {
      console.log(`parity: ${set} exit codes ${JSON.stringify(tally)}`);
    }
    for (const difference of differences) {
      console.error(`${difference.example} [${difference.set}]: ${difference.reason}`);
    }
    console.log(`parity: ${runs - differences.length}/${runs} runs identical (report: ${report})`);
    process.exit(differences.length === 0 ? 0 : 1);
  } catch (error) {
    console.error(`parity: ${error instanceof Error ? error.message : String(error)}`);
    process.exit(2);
  }
}
