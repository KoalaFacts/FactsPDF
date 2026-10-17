import { withDeadline } from './deadline.mjs';

export async function finishBrowserCleanup(closeBrowser, finalize, report, timeoutMs = 5000) {
  let cleanupFailed = false;
  try { await withDeadline(Promise.resolve().then(closeBrowser), timeoutMs, 'Browser cleanup'); }
  catch (error) {
    cleanupFailed = true;
    report.cleanupError = String(error); report.status = 'failed'; process.exitCode = 1;
  }
  try { await finalize(); }
  finally {
    // A rejected race leaves live transports/processes. Persist evidence first,
    // then exit; Playwright's registered exit handler terminates its browser tree.
    if (cleanupFailed) process.exit(1);
  }
}
