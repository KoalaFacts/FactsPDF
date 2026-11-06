// Workflow check: every GitHub Actions workflow that runs on pull_request must
// declare a top-level concurrency group keyed on github.ref and decide
// cancel-in-progress, so a new push to a pull request cancels the runs it
// supersedes instead of queueing behind them.
//
//   node scripts/check-workflows.ts
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

/** The lines of one top-level key's block, the key line included; empty when absent. */
function topLevelBlock(lines: readonly string[], key: string): string[] {
  const start = lines.findIndex((line) => line === `${key}:` || line.startsWith(`${key}: `) || line.startsWith(`${key}:\t`));
  if (start < 0) {
    return [];
  }
  const block = [lines[start]];
  for (let i = start + 1; i < lines.length; i++) {
    const line = lines[i];
    if (line.trim() === '' || line.trimStart().startsWith('#')) {
      continue;
    }
    if (!/^\s/.test(line)) {
      break;
    }
    block.push(line);
  }
  return block;
}

export function checkWorkflow(name: string, text: string): string[] {
  const lines = text.split(/\r?\n/);
  const trigger = topLevelBlock(lines, 'on');
  const onPullRequest =
    /\bpull_request\b(?!_)/.test(trigger[0] ?? '') || trigger.slice(1).some((line) => /^ {2}pull_request:/.test(line));
  if (!onPullRequest) {
    return [];
  }
  const concurrency = topLevelBlock(lines, 'concurrency');
  if (!concurrency.some((line) => /^\s+group:.*\$\{\{[^}]*\bgithub\.ref\b(?!_)/.test(line))) {
    return [`${name}: pull_request workflow has no top-level concurrency group keyed on github.ref`];
  }
  if (!concurrency.some((line) => /^\s+cancel-in-progress:\s*\S/.test(line))) {
    return [`${name}: concurrency group does not set cancel-in-progress`];
  }
  return [];
}

if (import.meta.main) {
  const directory = join(process.cwd(), '.github', 'workflows');
  const files = readdirSync(directory).filter((file) => /\.ya?ml$/.test(file)).sort();
  const problems = files.flatMap((file) => checkWorkflow(file, readFileSync(join(directory, file), 'utf8')));
  for (const problem of problems) {
    console.error(problem);
  }
  if (problems.length > 0) {
    console.error(`check-workflows: ${problems.length} problem(s) in ${files.length} workflows`);
    process.exit(1);
  }
  console.log(`check-workflows: ${files.length} workflows, no problems`);
}
