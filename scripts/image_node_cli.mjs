// Verification-only argv adapter through the existing shell-free Node process helper.
import { runProcess } from './node_cli_process.mjs';
const [executable, ...args] = process.argv.slice(2);
const result = await runProcess({ executable, args, timeoutMs: 90000 });
process.stdout.write(result.stdout); process.stderr.write(result.stderr);
process.exitCode = result.exitCode ?? 1;
