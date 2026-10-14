// Internal verification helper, not a supported SDK API or a process-tree sandbox.
import { spawn } from 'node:child_process';
import { performance } from 'node:perf_hooks';

const failure = (code, message, pid) => Object.assign(new Error(message), { code, pid });

export async function runProcess({ executable, args = [], input = Buffer.alloc(0),
  timeoutMs = 10000, signal, maxStdoutBytes = 64 * 1024 * 1024, maxStderrBytes = 1024 * 1024 }) {
  for (const value of [timeoutMs, maxStdoutBytes, maxStderrBytes]) {
    if (!Number.isInteger(value) || value <= 0 || value > 2147483647) {
      throw new RangeError('Timeout and byte limits must be positive 32-bit integers');
    }
  }
  if (!Buffer.isBuffer(input)) throw new TypeError('Input must be a Buffer');
  if (signal?.aborted) throw failure('ABORTED', 'Cancelled before launch');
  return new Promise((resolve, reject) => {
    const started = performance.now();
    const child = spawn(executable, args, { shell: false, windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
    const stdout = [], stderr = [];
    let outputBytes = 0, diagnosticBytes = 0, error, inputError;
    const stop = (code, message) => {
      if (error) return;
      error = failure(code, message, child.pid);
      // The production CLI creates no descendants. Await close before returning;
      // cancellation here is process termination, not .NET cooperative cancellation.
      child.kill('SIGKILL');
    };
    const abort = () => stop('ABORTED', 'Node caller cancelled the CLI process');
    const timer = setTimeout(() => stop('TIMEOUT', 'CLI exceeded its time limit'), timeoutMs);
    signal?.addEventListener('abort', abort, { once: true });
    if (signal?.aborted) abort();
    child.on('error', reason => { error ??= reason; });
    child.stdin.on('error', reason => { inputError = reason; });
    child.stdout.on('data', chunk => {
      outputBytes += chunk.length;
      if (outputBytes > maxStdoutBytes) stop('OUTPUT_LIMIT', 'stdout exceeded its byte limit');
      else if (!error) stdout.push(chunk);
    });
    child.stderr.on('data', chunk => {
      diagnosticBytes += chunk.length;
      if (diagnosticBytes > maxStderrBytes) stop('OUTPUT_LIMIT', 'stderr exceeded its byte limit');
      else if (!error) stderr.push(chunk);
    });
    child.on('close', (exitCode, exitSignal) => {
      clearTimeout(timer);
      signal?.removeEventListener('abort', abort);
      if (error) return reject(error);
      if (inputError && exitCode === 0) return reject(inputError);
      resolve({ stdout: Buffer.concat(stdout), stderr: Buffer.concat(stderr),
        exitCode, exitSignal, pid: child.pid, elapsedMs: performance.now() - started });
    });
    child.stdin.end(input);
  });
}
