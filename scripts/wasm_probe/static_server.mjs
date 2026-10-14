import { createServer } from 'node:http';

// Every response comes from an explicitly prepared byte map. No converter or filesystem route.
export async function serveStatic(files) {
  const requests = [];
  const server = createServer((req, res) => {
    const path = new URL(req.url, 'http://localhost').pathname;
    const item = files.get(path);
    const status = !['GET', 'HEAD'].includes(req.method) ? 405 : item ? 200 : 404;
    requests.push({ method: req.method, path, status, bytes: status === 200 ? item.bytes.length : 0 });
    res.writeHead(status, { 'Content-Type': item?.type ?? 'text/plain', 'Cache-Control': 'no-store',
      'X-Content-Type-Options': 'nosniff' });
    res.end(status === 200 && req.method !== 'HEAD' ? item.bytes : undefined);
  });
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolve);
  });
  return { origin: `http://127.0.0.1:${server.address().port}`, requests,
    close: () => new Promise(resolve => {
      server.close(resolve);
      server.closeAllConnections();
    }) };
}
