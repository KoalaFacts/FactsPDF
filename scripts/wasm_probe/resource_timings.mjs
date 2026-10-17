import { withDeadline } from './deadline.mjs';

export async function captureResourceTimings(readEntries, timeoutMs = 2000) {
  if (!readEntries)
    return { status: 'unavailable', reason: 'Browser page was not created', entries: null };
  try {
    const entries = await withDeadline(Promise.resolve().then(readEntries), timeoutMs, 'Resource timing capture');
    return { status: 'captured', entries };
  } catch (error) {
    return { status: 'unavailable', reason: String(error), entries: null };
  }
}
