// Checks the repository's Markdown documentation:
// - every relative link and anchor resolves;
// - every doc under docs/ is reachable from README.md through relative links;
// - every repository path named in a code span exists.
// Run from the repository root: node scripts/check-docs.ts
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, normalize, relative, sep } from 'node:path';

export interface DocsProblem {
  file: string;
  line: number;
  message: string;
}

// Top-level folders whose paths, when named in a code span, must exist.
const pathRoots = [
  '.github',
  'benchmarks',
  'docs',
  'eng',
  'examples',
  'probes',
  'scripts',
  'shared',
  'site',
  'src',
  'tests',
  'tools',
];

const fencePattern = /^\s*(```|~~~)/;
const imagePattern = /!\[[^\]]*\]\(\s*<?([^)\s>]+)>?(?:\s+(?:"[^"]*"|'[^']*'|\([^)]*\)))?\s*\)/g;
const linkPattern = /\[[^\]]*\]\(\s*<?([^)\s>]+)>?(?:\s+(?:"[^"]*"|'[^']*'|\([^)]*\)))?\s*\)/g;
const referencePattern = /^\s*\[(?!\^)[^\]]+\]:\s*<?(\S+?)>?(?:\s+.*)?$/;
const codeSpanPattern = /`([^`\n]+)`/g;
const headingPattern = /^\s{0,3}(#{1,6})\s+(.*?)\s*#*\s*$/;
const anchorTagPattern = /<a\s+(?:name|id)="([^"]+)"/g;

/** GitHub's heading anchor: lower-case, punctuation dropped, spaces to hyphens. */
export function slugify(heading: string): string {
  const text = heading
    .replace(/`/g, '')
    .replace(/!?\[([^\]]*)\]\([^)]*\)/g, '$1')
    .trim()
    .toLowerCase();
  return text.replace(/[^\p{L}\p{N}\p{M}\s_-]/gu, '').replace(/\s/g, '-');
}

/** The lines of a Markdown file that are prose, with fenced code blanked out. */
/** A line git writes for an unresolved merge conflict. */
const conflictMarkerPattern = /^(?:<{7}(?: |$)|={7}$|>{7}(?: |$))/;

function proseLines(text: string): string[] {
  const lines = text.split(/\r?\n/);
  let inFence = false;
  for (let i = 0; i < lines.length; i++) {
    if (fencePattern.test(lines[i])) {
      inFence = !inFence;
      lines[i] = '';
    } else if (inFence) {
      lines[i] = '';
    }
  }
  return lines;
}

export function anchorsOf(text: string): Set<string> {
  const anchors = new Set<string>();
  const counts = new Map<string, number>();
  for (const line of proseLines(text)) {
    const heading = headingPattern.exec(line);
    if (heading) {
      const slug = slugify(heading[2]);
      const seen = counts.get(slug) ?? 0;
      anchors.add(seen === 0 ? slug : `${slug}-${seen}`);
      counts.set(slug, seen + 1);
    }
    for (const tag of line.matchAll(anchorTagPattern)) {
      anchors.add(tag[1].toLowerCase());
    }
  }
  return anchors;
}

interface Link {
  line: number;
  target: string;
}

function linksOf(text: string): Link[] {
  const links: Link[] = [];
  const lines = proseLines(text);
  for (let i = 0; i < lines.length; i++) {
    let line = lines[i].replace(codeSpanPattern, '');
    // Images first, so a badge [![alt](img)](target) still yields its outer link.
    for (const match of line.matchAll(imagePattern)) {
      links.push({ line: i + 1, target: match[1] });
    }
    line = line.replace(imagePattern, 'image');
    for (const match of line.matchAll(linkPattern)) {
      links.push({ line: i + 1, target: match[1] });
    }
    const reference = referencePattern.exec(line);
    if (reference) {
      links.push({ line: i + 1, target: reference[1] });
    }
  }
  return links;
}

function isExternal(target: string): boolean {
  return /^[a-z][a-z0-9+.-]*:/i.test(target) || target.startsWith('//');
}

/** A code span that names a repository path, without wildcards or placeholders. */
export function namedPath(span: string): string | null {
  const value = span.trim().replace(/[.,;:]$/, '');
  if (!/^[A-Za-z0-9_.\-/]+$/.test(value) || value.includes('//')) {
    return null;
  }
  const root = value.split('/')[0];
  if (!pathRoots.includes(root) || !value.includes('/')) {
    return null;
  }
  return value.replace(/\/$/, '');
}

export function checkDocs(root: string, files: readonly string[]): DocsProblem[] {
  const problems: DocsProblem[] = [];
  const texts = new Map<string, string>();
  for (const file of files) {
    texts.set(file, readFileSync(join(root, file), 'utf8'));
  }
  const anchorCache = new Map<string, Set<string>>();
  const anchorsFor = (file: string): Set<string> => {
    let anchors = anchorCache.get(file);
    if (!anchors) {
      anchors = anchorsOf(texts.get(file) ?? readFileSync(join(root, file), 'utf8'));
      anchorCache.set(file, anchors);
    }
    return anchors;
  };
  const graph = new Map<string, string[]>();

  for (const file of files) {
    const text = texts.get(file)!;
    const targets: string[] = [];
    graph.set(file, targets);
    for (const link of linksOf(text)) {
      if (isExternal(link.target)) {
        continue;
      }
      const [rawPath, anchor] = splitAnchor(link.target);
      let path: string;
      try {
        path = decodeURIComponent(rawPath);
      } catch {
        problems.push({ file, line: link.line, message: `malformed link ${link.target}` });
        continue;
      }
      const resolved =
        path === ''
          ? file
          : toPosix(normalize(path.startsWith('/') ? path.slice(1) : join(dirname(file), path)));
      if (resolved.startsWith('..')) {
        problems.push({ file, line: link.line, message: `link leaves the repository: ${link.target}` });
        continue;
      }
      if (!existsSync(join(root, resolved))) {
        problems.push({ file, line: link.line, message: `broken link ${link.target}` });
        continue;
      }
      if (resolved.endsWith('.md')) {
        targets.push(resolved);
        if (anchor !== null && !anchorsFor(resolved).has(anchor.toLowerCase())) {
          problems.push({ file, line: link.line, message: `missing anchor #${anchor} in ${resolved}` });
        }
      }
    }

    const lines = proseLines(text);
    for (let i = 0; i < lines.length; i++) {
      if (conflictMarkerPattern.test(lines[i])) {
        problems.push({ file, line: i + 1, message: 'merge-conflict marker' });
      }
      for (const span of lines[i].matchAll(codeSpanPattern)) {
        const path = namedPath(span[1]);
        if (path !== null && !existsSync(join(root, path)) && !isIgnored(root, path)) {
          problems.push({ file, line: i + 1, message: `named path does not exist: ${path}` });
        }
      }
    }
  }

  const reachable = new Set<string>();
  const queue = files.includes('README.md') ? ['README.md'] : [];
  while (queue.length > 0) {
    const next = queue.pop()!;
    if (reachable.has(next)) {
      continue;
    }
    reachable.add(next);
    for (const target of graph.get(next) ?? []) {
      if (!reachable.has(target)) {
        queue.push(target);
      }
    }
  }
  for (const file of files) {
    if (file.startsWith('docs/') && !reachable.has(file)) {
      problems.push({ file, line: 1, message: 'not reachable from README.md through relative links' });
    }
  }
  return problems;
}

/** Build outputs such as site/dist are named in docs but ignored by git. */
function isIgnored(root: string, path: string): boolean {
  for (const candidate of [path, `${path}/`]) {
    try {
      execFileSync('git', ['check-ignore', '-q', '--no-index', candidate], { cwd: root, stdio: 'ignore' });
      return true;
    } catch {
      // Exit status 1: not ignored.
    }
  }
  return false;
}

function splitAnchor(target: string): [string, string | null] {
  const hash = target.indexOf('#');
  return hash < 0 ? [target, null] : [target.slice(0, hash), target.slice(hash + 1)];
}

function toPosix(path: string): string {
  return path.split(sep).join('/');
}

export function trackedDocs(root: string): string[] {
  const output = execFileSync('git', ['ls-files', '-z', '--', '*.md'], { cwd: root, encoding: 'utf8' });
  return output
    .split('\0')
    .filter((file) => file !== '' && !/(^|\/)(node_modules|bin|obj)\//.test(file))
    .filter((file) => existsSync(join(root, file)) && statSync(join(root, file)).isFile())
    .sort();
}

if (import.meta.main) {
  const root = process.cwd();
  const files = trackedDocs(root);
  const problems = checkDocs(root, files);
  for (const problem of problems) {
    console.error(`${relative(root, join(root, problem.file))}:${problem.line}: ${problem.message}`);
  }
  if (problems.length > 0) {
    console.error(`check-docs: ${problems.length} problem(s) in ${files.length} docs`);
    process.exit(1);
  }
  console.log(`check-docs: ${files.length} docs, no problems`);
}
