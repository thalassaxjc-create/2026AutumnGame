import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { Readable } from 'node:stream';
import test from 'node:test';
import { CASES, DURATIONS, EXAMPLE_CONFIG, SOURCES, TEMPLATES } from '../src/data.mjs';
import { analyzeInput, assess, buildGame, cleanConfig, reportFor, teachingPack } from '../src/engine.mjs';
import { createApi, openStore } from '../server/api.mjs';

test('识别主题、人群、时长和现场方式，并为陌生主题留出手动选择', () => {
  const school = analyzeInput('给初二学生讲游戏充值诈骗，5 分钟班会投屏');
  assert.deepEqual([school.matches[0], school.audience, school.sceneId, school.duration, school.modeId],
    ['game-recharge', '中学生', 'class', 5, 'present']);
  const work = analyzeInput('企业员工培训冒充领导加急转账，十分钟抢答');
  assert.deepEqual([work.matches[0], work.sceneId, work.duration, work.modeId], ['boss', 'work', 10, 'group']);
  assert.deepEqual(analyzeInput('做个 5 分钟的班会反诈投屏活动').matches, []);
  assert.equal(analyzeInput('社区活动讲屏幕共享诈骗').matches[0], 'share-screen');
  assert.deepEqual(analyzeInput('这个周末的地质活动').matches, []);
});

test('每张案卡、每种模板、每档时长都能生成可解、可讲且带来源的游戏', () => {
  assert.equal(CASES.length, 36);
  assert.equal(TEMPLATES.length, 10);
  const interactions = new Set();
  for (const card of CASES) for (const template of TEMPLATES) for (const duration of DURATIONS) {
    const game = buildGame({ ...EXAMPLE_CONFIG, caseId: card.id, templateId: template.id, duration });
    assert.equal(game.source, SOURCES[card.source]);
    assert.equal(game.rounds.length, {3: 3, 5: 4, 10: 6}[duration]);
    assert.equal(teachingPack(game).topics.length, 3);
    const results = game.rounds.map(round => {
      interactions.add(round.kind);
      const ids = new Set(round.choices.map(option => option.id));
      assert.equal(ids.size, round.choices.length);
      assert.ok(round.expected.every(id => ids.has(id)));
      assert.ok(round.context && round.explanation);
      assert.equal(assess(round, []).correct, false);
      return assess(round, round.expected);
    });
    assert.equal(reportFor(results).score, 100);
  }
  assert.deepEqual([...interactions].sort(), ['choice', 'clues', 'order']);
});

test('分段重生成只改选定关卡，发布配置排除原文和隐私数据', () => {
  const initial = buildGame(EXAMPLE_CONFIG);
  const edited = buildGame({ ...EXAMPLE_CONFIG, variants: { 1: 1 }, privateSource: '这段原文永远不会发布' });
  assert.deepEqual(initial.rounds[0], edited.rounds[0]);
  assert.notEqual(initial.rounds[1].question, edited.rounds[1].question);
  assert.deepEqual(initial.rounds[2], edited.rounds[2]);
  assert.ok(!JSON.stringify(edited).includes('这段原文永远不会发布'));
  assert.throws(() => cleanConfig({ ...EXAMPLE_CONFIG, title: 'http://example.invalid' }), /移除/);
  assert.throws(() => cleanConfig({ ...EXAMPLE_CONFIG, caseId: '__proto__' }), /骗局/);
});

async function request(api, path, options = {}) {
  const request = Object.assign(Readable.from(options.body ? [Buffer.from(JSON.stringify(options.body))] : []),
    { method: options.body ? 'POST' : 'GET', headers: options.token ? { authorization: `Bearer ${options.token}` } : {},
      socket: { remoteAddress: '203.0.113.1' } });
  const response = {
    status: 0, body: null,
    writeHead(status) { this.status = status; return this; },
    end(body) { this.body = JSON.parse(body); },
  };
  await api(request, response, path);
  return response;
}

test('默认作品直接发布，自定义作品审核后可访问，匿名统计一次计入', async () => {
  const file = join(mkdtempSync(join(tmpdir(), 'fraud-studio-test-')), 'studio.json');
  let api = createApi(openStore(file), { adminToken: 'test-only' });
  const published = await request(api, '/api/games', { body: EXAMPLE_CONFIG });
  assert.equal(published.body.status, 'published');
  assert.equal((await request(api, '/api/games', { body: EXAMPLE_CONFIG })).body.id, published.body.id);
  const custom = await request(api, '/api/games', { body: { ...EXAMPLE_CONFIG, title: '班会练习' } });
  const path = `/api/games/${custom.body.id}`;
  assert.equal(custom.body.status, 'pending');
  assert.equal((await request(api, path)).status, 404);
  assert.equal((await request(api, '/api/admin')).status, 401);
  await request(api, `/api/admin/review/${custom.body.id}`, { body: { action: 'approve' }, token: 'test-only' });
  api = createApi(openStore(file), { adminToken: 'test-only' });
  assert.equal((await request(api, path)).body.name, '班会练习');
  const complete = { type: 'complete', sessionId: randomUUID(), config: EXAMPLE_CONFIG, score: 75, correct: 2 };
  assert.equal((await request(api, '/api/events', { body: { ...complete, type: 'start' } })).status, 200);
  assert.equal((await request(api, '/api/events', { body: complete })).status, 200);
  await request(api, '/api/events', { body: complete });
  const dashboard = (await request(api, '/api/admin', { token: 'test-only' })).body;
  assert.deepEqual([dashboard.stats.started, dashboard.stats.completed, dashboard.stats.average], [1, 1, 75]);
  assert.equal(dashboard.stats.mistakes[0].misses, 1);
  const lanStart = { ...complete, sessionId: 'session-mjjxv7uv-a86k4jd', type: 'start' };
  assert.equal((await request(api, '/api/events', { body: lanStart })).status, 200);
});
