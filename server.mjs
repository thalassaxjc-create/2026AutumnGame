import { createServer } from 'node:http';
import { createReadStream, existsSync, statSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, extname, resolve, sep } from 'node:path';
import { createApi, openStore } from './server/api.mjs';

const root = dirname(fileURLToPath(import.meta.url));
if (existsSync(resolve(root, '.env'))) process.loadEnvFile(resolve(root, '.env'));
const production = process.argv.includes('--production');
const portArg = process.argv.indexOf('--port');
const port = Number(portArg > 0 ? process.argv[portArg + 1] : (process.env.PORT ?? 4173));
const host = process.argv.includes('--lan') ? '0.0.0.0' : process.env.HOST || '127.0.0.1';
const store = openStore(resolve(root, process.env.FRAUD_DATA_FILE || '.data/studio.json'));
const api = createApi(store, { adminToken: process.env.FRAUD_ADMIN_TOKEN || '' });
const dist = resolve(root, 'dist');
const mime = { '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.html': 'text/html; charset=utf-8', '.json': 'application/json', '.png': 'image/png', '.webp': 'image/webp',
  '.ico': 'image/x-icon', '.woff2': 'font/woff2' };

let vite;
const server = createServer((req, res) => {
  res.setHeader('X-Content-Type-Options', 'nosniff');
  res.setHeader('Referrer-Policy', 'same-origin');
  try {
    const route = new URL(req.url, 'http://localhost').pathname;
    if (route.startsWith('/api/')) {
      void api(req, res, route).catch(() => {
        if (!res.headersSent) res.writeHead(500, { 'Content-Type': 'application/json; charset=utf-8' });
        res.end(JSON.stringify({ error: '服务暂时不可用，请稍后再试。' }));
      });
      return;
    }
    if (vite) { vite.middlewares(req, res); return; }
    const requested = resolve(dist, `.${decodeURIComponent(route)}`);
    if (requested !== dist && !requested.startsWith(dist + sep)) {
      res.writeHead(404).end();
      return;
    }
    const file = existsSync(requested) && statSync(requested).isFile() ? requested : resolve(dist, 'index.html');
    if (!existsSync(file)) { res.writeHead(503).end('Run npm run build first.'); return; }
    res.writeHead(200, { 'Content-Type': mime[extname(file)] || 'application/octet-stream',
      'Cache-Control': file.includes(`${sep}assets${sep}`) ? 'public, max-age=31536000, immutable' : 'no-cache' });
    if (req.method === 'HEAD') res.end();
    else createReadStream(file).pipe(res);
  } catch {
    res.writeHead(400).end();
  }
});

if (!production) {
  const { createServer: createViteServer } = await import('vite');
  vite = await createViteServer({ root, server: { middlewareMode: true, hmr: { server } }, appType: 'spa' });
}

server.listen(port, host, () => {
  console.log(`反诈游戏工坊 http://${host === '0.0.0.0' ? '127.0.0.1' : host}:${port}`);
});
