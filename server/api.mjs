import { createHash, randomBytes, timingSafeEqual } from 'node:crypto';
import { mkdirSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { analyzeInput, buildGame, cleanConfig, validatePublicText } from '../src/engine.mjs';
import { CASE_BY_ID, TEMPLATES } from '../src/data.mjs';

export function openStore(path) {
  let state = { games: {}, byHash: {}, sessions: {}, reports: [], feedback: [] };
  try {
    state = { ...state, ...JSON.parse(readFileSync(path, 'utf8')) };
  } catch (error) {
    if (error.code !== 'ENOENT') throw error;
  }
  mkdirSync(dirname(path), { recursive: true });
  return {
    state,
    save() {
      writeFileSync(`${path}.new`, JSON.stringify(state, null, 2), 'utf8');
      renameSync(`${path}.new`, path);
    },
  };
}

function send(res, status, content) {
  res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' });
  res.end(JSON.stringify(content));
}

async function readBody(req) {
  let length = 0;
  const chunks = [];
  for await (const chunk of req) {
    length += chunk.byteLength;
    if (length > 20_000) throw new Error('提交内容太长。');
    chunks.push(chunk);
  }
  return JSON.parse(Buffer.concat(chunks).toString('utf8') || '{}');
}

function localhost(address) {
  return address === '127.0.0.1' || address === '::1' || address === '::ffff:127.0.0.1';
}

function adminAllowed(req, secret) {
  if (!secret) return localhost(req.socket.remoteAddress);
  const token = Buffer.from(String(req.headers.authorization ?? '').replace(/^Bearer /, ''));
  const expected = Buffer.from(secret);
  return token.length === expected.length && timingSafeEqual(token, expected);
}

function stats(state) {
  const sessions = Object.values(state.sessions);
  const completed = sessions.filter(session => session.complete);
  const playedByCase = new Map();
  const byTemplate = TEMPLATES.map(template => {
    const played = sessions.filter(session => session.templateId === template.id);
    const finished = played.filter(session => session.complete);
    return { id: template.id, name: template.name, started: played.length, completed: finished.length,
      average: finished.length ? Math.round(finished.reduce((sum, s) => sum + s.score, 0) / finished.length) : null };
  });
  for (const session of completed) {
    const entry = playedByCase.get(session.caseId) ?? { name: CASE_BY_ID[session.caseId].name, misses: 0, rounds: 0 };
    entry.misses += session.rounds - session.correct;
    entry.rounds += session.rounds;
    playedByCase.set(session.caseId, entry);
  }
  return {
    published: Object.values(state.games).filter(game => game.status === 'published').length,
    started: sessions.length, completed: completed.length,
    average: completed.length ? Math.round(completed.reduce((sum, s) => sum + s.score, 0) / completed.length) : null,
    satisfaction: state.feedback.length ? Math.round(10 * state.feedback.reduce((sum, f) => sum + f, 0) / state.feedback.length) / 10 : null,
    byTemplate,
    mistakes: [...playedByCase.values()].filter(entry => entry.misses).sort((a, b) => b.misses - a.misses).slice(0, 8),
  };
}

const validSessionId = /^(?:[\da-f]{8}(?:-[\da-f]{4}){3}-[\da-f]{12}|session-[\da-z]{1,14}-[\da-z]{1,7})$/i;
const date = () => new Date().toISOString();
const makeId = () => randomBytes(8).toString('base64url');

export function createApi(store, { adminToken = '' } = {}) {
  const { state, save } = store;
  return async function handle(req, res, path) {
    if (req.method === 'POST' && req.headers.origin) {
      const origin = new URL(req.headers.origin);
      if (origin.host !== req.headers.host) return send(res, 403, { error: '请从工坊页面提交。' });
    }
    if (path.startsWith('/api/admin') && !adminAllowed(req, adminToken)) {
      return send(res, 401, { error: '请输入管理口令。' });
    }
    try {
      if (req.method === 'GET' && path === '/api/status') return send(res, 200, { online: true });
      if (req.method === 'POST' && path === '/api/analyze') {
        const { text, sceneId } = await readBody(req);
        return send(res, 200, analyzeInput(text, sceneId));
      }
      if (req.method === 'POST' && path === '/api/games') {
        const config = cleanConfig(await readBody(req));
        const digest = createHash('sha256').update(JSON.stringify(config)).digest('hex');
        if (state.byHash[digest]) {
          const existing = state.games[state.byHash[digest]];
          return send(res, 200, { id: existing.id, status: existing.status });
        }
        const game = buildGame(config);
        const id = makeId();
        const status = game.requiresReview ? 'pending' : 'published';
        state.games[id] = { id, config, name: game.name, status, createdAt: date() };
        state.byHash[digest] = id;
        save();
        return send(res, 201, { id, status });
      }
      const requestedGame = path.match(/^\/api\/games\/([\w-]{8,32})$/);
      if (req.method === 'GET' && requestedGame) {
        const game = state.games[requestedGame[1]];
        if (!game || game.status !== 'published') return send(res, 404, { error: '这份游戏尚未发布或已移除。' });
        return send(res, 200, { id: game.id, config: game.config, name: game.name });
      }
      if (req.method === 'POST' && path === '/api/events') {
        const { sessionId, config, type, score, correct } = await readBody(req);
        if (typeof sessionId !== 'string' || !validSessionId.test(sessionId) || !['start', 'complete'].includes(type)) {
          return send(res, 400, { error: '无效的匿名互动记录。' });
        }
        const game = buildGame(config);
        const active = state.sessions[sessionId];
        if (active?.complete) return send(res, 200, { ok: true });
        if (!active && Object.keys(state.sessions).length >= 25_000) return send(res, 200, { ok: true });
        const safeScore = Math.max(0, Math.min(100, Math.round(Number(score) || 0)));
        const safeCorrect = Math.max(0, Math.min(game.rounds.length, Math.floor(Number(correct) || 0)));
        state.sessions[sessionId] = { caseId: game.case.id, templateId: game.template.id,
          rounds: game.rounds.length, complete: type === 'complete', score: safeScore, correct: safeCorrect };
        save();
        return send(res, 200, { ok: true });
      }
      if (req.method === 'POST' && path === '/api/feedback') {
        const { caseId, kind, message, rating } = await readBody(req);
        if (Number.isInteger(rating) && rating >= 1 && rating <= 5) {
          state.feedback.push(rating);
        } else {
          if (!Object.hasOwn(CASE_BY_ID, caseId)) return send(res, 400, { error: '请选择要反馈的案卡。' });
          const note = validatePublicText(message, 260);
          if (!note) return send(res, 400, { error: '请写明需要核对的地方。' });
          state.reports.unshift({ id: makeId(), caseId, kind: kind === 'sample' ? '试玩内容' : '案卡内容',
            message: note, createdAt: date(), status: 'open' });
        }
        save();
        return send(res, 200, { ok: true });
      }
      if (req.method === 'GET' && path === '/api/admin') {
        return send(res, 200, { stats: stats(state), reports: state.reports,
          queue: Object.values(state.games).filter(game => game.status !== 'published').reverse(),
          published: Object.values(state.games).filter(game => game.status === 'published').reverse().slice(0, 40) });
      }
      const requestedReview = path.match(/^\/api\/admin\/review\/([\w-]{8,32})$/);
      if (req.method === 'POST' && requestedReview) {
        const game = state.games[requestedReview[1]];
        if (!game) return send(res, 404, { error: '没有找到这份游戏。' });
        const { action } = await readBody(req);
        if (!['approve', 'decline'].includes(action)) return send(res, 400, { error: '请选择审核操作。' });
        game.status = action === 'approve' ? 'published' : 'declined';
        save();
        return send(res, 200, { id: game.id, status: game.status });
      }
      const requestedReport = path.match(/^\/api\/admin\/reports\/([\w-]{8,32})$/);
      if (req.method === 'POST' && requestedReport) {
        const report = state.reports.find(item => item.id === requestedReport[1]);
        if (!report) return send(res, 404, { error: '没有找到该反馈。' });
        report.status = 'resolved';
        save();
        return send(res, 200, { ok: true });
      }
      return send(res, 404, { error: '没有找到这个服务。' });
    } catch (error) {
      return send(res, 400, { error: error instanceof SyntaxError ? '数据格式有误。' : error.message });
    }
  };
}
