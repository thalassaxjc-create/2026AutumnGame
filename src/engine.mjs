import {
  AUDIENCES, CASES, CASE_BY_ID, CATEGORY_BY_ID, DURATIONS, MODES, SCENES, SCENE_BY_ID,
  SOURCES, TEMPLATES, TEMPLATE_BY_ID,
} from './data.mjs';

const sceneMatchers = [
  ['class', /班会|课堂|开学|学生|年级|初中|高中|小学/],
  ['community', /社区|街道|居民|老人|老年|宣传摊位/],
  ['work', /企业|员工|职场|公司|会议室|财务/],
  ['home', /家长会|家庭|亲子|和孩子|家人/],
  ['media', /公众号|线上|传播|短视频/],
];

const audienceMatchers = [
  ['小学生', /小学|少先队/],
  ['中学生', /初中|初[一二三]|高中|高[一二三]|中学生|青少年/],
  ['大学生', /大学|高校|大[一二三四]/],
  ['老年人', /老年|老人|退休|爷爷|奶奶/],
  ['企业员工', /员工|职场|公司|企业/],
  ['家长和孩子', /家长|亲子|和孩子/],
  ['社区居民', /社区|居民/],
];

export function analyzeInput(input, fallbackScene = 'class') {
  const text = String(input ?? '').slice(0, 6000).normalize('NFKC');
  const compact = text.toLowerCase().replace(/\s+/g, '');
  const sceneId = sceneMatchers.find(([, pattern]) => pattern.test(text))?.[0] ?? fallbackScene;
  const scene = SCENE_BY_ID[sceneId] ?? SCENES[0];
  const audience = audienceMatchers.find(([, pattern]) => pattern.test(text))?.[0] ?? scene.audience;
  const durationText = text.match(/(10|[35三五十])\s*(?:分钟|分\b|min)/i)?.[1];
  const duration = Number(({ 三: 3, 五: 5, 十: 10 })[durationText] ?? durationText ?? 5);
  const modeId = /抢答|分组|举手|全班/.test(text) ? 'group' : /投屏|大屏/.test(text) ? 'present' : 'solo';
  const matches = CASES.map(card => {
    const name = card.name.toLowerCase().replace(/\s+/g, '');
    const score = (compact.includes(name) ? 16 : 0) + card.keywords.reduce((sum, word) =>
      sum + (compact.includes(word.toLowerCase()) ? word.length + 1 : 0), 0);
    return { card, score };
  }).filter(item => item.score > 0).sort((a, b) => b.score - a.score ||
    Number(b.card.audiences.includes(audience)) - Number(a.card.audiences.includes(audience)));
  return { sceneId: scene.id, audience, duration, modeId, matches: matches.slice(0, 4).map(item => item.card.id) };
}

export function recommendedTemplates(card, modeId = 'solo') {
  if (!card) return TEMPLATES.slice(0, 3);
  const priority = card.id === 'ai-face' || card.id === 'ai-voice' ? 'ai'
    : card.category === 'shopping' ? 'clues'
    : card.category === 'family' || card.id === 'police' ? 'call'
    : card.category === 'digital' ? 'clues' : 'chat';
  const suitable = TEMPLATES.filter(item => item.suited.includes(card.category)).map(item => item.id);
  const ids = [...new Set([modeId === 'group' ? 'quiz' : priority, priority, ...suitable, 'quiz'])];
  return ids.slice(0, 3).map(id => TEMPLATE_BY_ID[id]);
}

export function validatePublicText(value, max = 90) {
  if (typeof value !== 'string') return '';
  const text = value.normalize('NFKC').replace(/[\u0000-\u001F\u007F]/g, ' ').trim().slice(0, max);
  if (/(?:https?:\/\/|www\.|@[\w.-]+|\b\d{6,}\b|\d{7,}|[A-Za-z0-9-]+\.(?:com|cn|net|org|app|xyz)\b|<[^>]+>|收款码|银行卡号\s*[:：]|身份证号\s*[:：])/i.test(text)) {
    throw new Error('自定义文字请移除链接、号码、账号和个人信息。');
  }
  return text;
}

export function cleanConfig(input) {
  if (!input || typeof input !== 'object' || !Object.hasOwn(CASE_BY_ID, input.caseId)) throw new Error('请选择图鉴里的骗局。');
  if (!Object.hasOwn(TEMPLATE_BY_ID, input.templateId)) throw new Error('请选择可用的玩法。');
  const scene = Object.hasOwn(SCENE_BY_ID, input.sceneId) ? SCENE_BY_ID[input.sceneId] : SCENES[0];
  const title = validatePublicText(input.title, 48);
  const edits = {};
  const variants = {};
  for (let slot = 0; slot < 6; slot++) {
    const edit = validatePublicText(input.edits?.[slot], 100);
    if (edit) edits[slot] = edit;
    const v = input.variants?.[slot];
    if (Number.isInteger(v) && v > 0 && v <= 100) variants[slot] = v;
  }
  return {
    caseId: input.caseId, templateId: input.templateId, sceneId: scene.id,
    audience: AUDIENCES.includes(input.audience) ? input.audience : scene.audience,
    duration: DURATIONS.includes(input.duration) ? input.duration : 5,
    modeId: MODES.some(mode => mode.id === input.modeId) ? input.modeId : 'solo',
    seed: Number.isInteger(input.seed) ? input.seed >>> 0 : 20261128,
    title, edits, variants,
  };
}

function shuffle(items, seed) {
  const result = [...items];
  let random = seed >>> 0 || 1;
  for (let i = result.length - 1; i > 0; i--) {
    random = (Math.imul(random, 1664525) + 1013904223) >>> 0;
    const other = random % (i + 1);
    [result[i], result[other]] = [result[other], result[i]];
  }
  return result;
}

function decision(card, slot, seed, variant) {
  const topic = slot % 6;
  const opener = [
    ['你接到这条消息。第一步该怎么做？', '此刻你会先按下哪个决定？', '在回应之前，最稳妥的是哪一步？'],
    ['下面哪条最值得警惕？', '其中哪条能成为你暂停的理由？', '你会把哪一句圈作关键红旗？'],
    ['你已经停下来了。接下来怎么核实？', '核实身份时，哪个入口由你自己掌握？', '下一步该往哪里求证？'],
    ['对方强调事情很急。你如何给自己留一点判断时间？', '当紧迫感上来，你会如何处理？', '遇到连续催促，什么做法能保护你？'],
    ['假如已经照做了一部分，接下来怎么减少损失？', '发现情况不对之后，优先处理什么？', '出现了异常，你准备如何收尾？'],
    ['复盘这一局，哪条提醒最能帮助同伴？', '你会把哪条反诈提醒带走？', '分享给亲友时，哪条行动最有用？'],
  ];
  const solutions = [
    [card.steps[0], '先回应对方，稍后再查', '看对方资料是否像真的，再决定'],
    [card.flags[1], '界面有时间和消息提示', '对方说话有礼貌'],
    [card.steps[1], '使用刚刚收到的陌生入口求证', '用同一条消息里提供的联系方式回问'],
    ['给自己留出核实时间，暂停正在做的操作', '尽快答复，避免让对方再催', '让群里的其他人先判断，自己继续等待'],
    [card.steps[2], '清掉聊天记录，当作没有发生', '继续听对方解释，等事情自己处理完'],
    [`遇到「${card.flags[0]}」先停下来独立核验`, '头像熟悉，身份就可以相信', '只要金额不大，就值得试试'],
  ];
  const choice = solutions[topic].map((label, index) => ({ id: ['safe', 'risk', 'unsure'][index], label }));
  return {
    kind: 'choice', question: opener[topic][variant % 3], choices: shuffle(choice, seed), expected: ['safe'],
    explanation: topic === 1 ? `「${card.flags[1]}」改变了你原本的安全边界。礼貌和界面细节无法证实身份。`
      : topic === 3 ? `这条消息利用了「${card.emotion}」。暂停能给你时间，从自己掌握的入口核对。`
      : [card.steps[0], card.steps[1], card.steps[2]][topic === 2 ? 1 : topic === 4 ? 2 : 0] + '。' + card.exhibit,
    takeaway: topic >= 4 ? card.steps[2] : card.steps[topic === 2 ? 1 : 0],
  };
}

function clueStage(card, slot, seed, variant) {
  const pair = [slot % 3, (slot + 1 + variant % 2) % 3];
  const markers = [
    { id: 'red-a', label: card.flags[pair[0]], note: '这条要求会把你推向一个无法独立核验的操作。' },
    { id: 'red-b', label: card.flags[pair[1]], note: '越权、催促和跳出可信渠道都值得暂停核对。' },
    { id: 'plain-a', label: '通知显示了发送时间', note: '普通时间戳本身无法判断请求真假。' },
    { id: 'plain-b', label: '页面用了熟悉的头像和颜色', note: '外观可以模仿，不能独自作为判断证据。' },
    { id: 'plain-c', label: '消息开头有一条礼貌的招呼', note: '语气友好本身不会带来安全保障。' },
  ];
  return {
    kind: 'clues', question: ['圈出两处值得立刻暂停的破绽', '挑两条真正能说明有风险的证据', '哪些线索会让你停下来核实？'][(slot + variant) % 3],
    choices: shuffle(markers, seed), expected: ['red-a', 'red-b'],
    explanation: `「${card.flags[pair[0]]}」和「${card.flags[pair[1]]}」都在改变原有的安全流程。留意具体要求，熟悉的外观并不能证实身份。`,
    takeaway: card.steps[0],
  };
}

function roundContext(card, index) {
  return [
    card.hook,
    card.exhibit,
    '你已经暂停操作。现在你可以离开这条消息，自己找可信渠道求证。',
    `你还在核对，对方又催了一次。你感到「${card.emotion}」，还没做决定。`,
    '假设你已经回应了对方，甚至做了一部分操作。你现在有机会停下并求助。',
    `事后，有人也收到了类似的${card.channel}，来问你该留意什么。`,
  ][index];
}

function evidenceStage(card, seed) {
  const choice = card.steps.map((label, index) => ({ id: String(index), label }));
  const order = shuffle(choice, seed);
  return {
    kind: 'order', question: '按先后顺序拼出正确的应对流程',
    choices: order.map(item => item.id).join('') === '012' ? [order[1], order[0], order[2]] : order,
    expected: ['0', '1', '2'], explanation: '先停下危险操作，再从独立渠道核实，最后保存记录和寻求帮助。疑似已经发生损失时，尽快联系平台、银行和警方。',
    takeaway: card.steps[0],
  };
}

export function buildGame(input) {
  const config = cleanConfig(input);
  const template = TEMPLATE_BY_ID[config.templateId];
  const main = CASE_BY_ID[config.caseId];
  const total = { 3: 3, 5: 4, 10: 6 }[config.duration];
  const related = shuffle(CASES.filter(card => card.id !== main.id && card.category === main.category), config.seed);
  const rounds = Array.from({ length: total }, (_, index) => {
    const variant = config.variants[index] ?? 0;
    const seed = (config.seed + index * 809 + variant * 977) >>> 0;
    const card = template.id === 'blind' && index > 0 ? related[index - 1] ?? main : main;
    let interaction = decision(card, index, seed, variant);
    if (template.id === 'clues' && index < 2) interaction = clueStage(card, index, seed, variant);
    if (template.id === 'evidence' && index % 2 === 0) interaction = evidenceStage(card, seed);
    if (template.id === 'ai' && index === 1) interaction = clueStage(card, index, seed, variant);
    return {
      ...interaction, id: `r${index + 1}`, caseId: card.id, caseName: card.name, channel: card.channel,
      title: ['收到请求', '拆开细节', '独立核实', '应对催促', '及时补救', '分享提醒'][index],
      context: config.edits[index] || (template.id === 'blind' ? card.hook : roundContext(card, index)),
      alert: card.flags[index % 3], timeLimit: template.id === 'quiz' ? (config.duration === 3 ? 14 : 20) : 0,
      witnesses: [
        { name: '当事人', text: card.hook },
        { name: '现场记录', text: card.exhibit },
        { name: '求证线索', text: `可以从自己掌握的入口查证：${card.steps[1]}。` },
      ],
    };
  });
  return {
    config, case: main, template, rounds,
    name: config.title || `${main.name} · ${template.name}`,
    category: CATEGORY_BY_ID[main.category].label,
    source: SOURCES[main.source],
    requiresReview: Boolean(config.title || Object.keys(config.edits).length),
  };
}

export function assess(round, answer) {
  const accepted = Array.isArray(answer) ? answer : answer ? [answer] : [];
  const sameLength = accepted.length === round.expected.length;
  const correct = sameLength && round.expected.every((item, index) =>
    round.kind === 'order' ? item === accepted[index] : accepted.includes(item));
  const found = round.expected.filter(item => accepted.includes(item)).length;
  const partial = !correct && round.kind === 'clues' && found > 0;
  return { correct, partial, answer: accepted, points: correct ? 100 : partial ? 40 : 0, feedback: round.explanation };
}

export function reportFor(results) {
  const count = results.length;
  const correct = results.filter(result => result.correct).length;
  const score = count ? Math.round(results.reduce((sum, result) => sum + result.points, 0) / count) : 0;
  return {
    count, correct, score,
    rank: score === 100 ? '机警观察员' : score >= 65 ? '冷静核实员' : '继续练习的侦查员',
    missing: results.flatMap((result, index) => result.correct ? [] : [index]),
  };
}

export function teachingPack(game) {
  const card = game.case;
  const audience = game.config.audience;
  return {
    name: game.name,
    lesson: `面向${audience}的 ${game.config.duration} 分钟互动练习`,
    opening: `先读这个情境：${card.hook} 请大家先想十秒，自己会停在哪一步？`,
    flow: [
      '播放情境，给参与者留出几秒安静观察。',
      '每次作答先问理由，再公布解析。投屏时可以用举手投票。',
      `最后请每人记住一条能立刻做的动作：${card.steps[0]}。`,
    ],
    topics: [
      `如果消息让你感到「${card.emotion}」，你会怎么给自己争取核实时间？`,
      `在这个情境里，为什么「${card.flags[0]}」值得警惕？`,
      '如果身边的人已经点击、授权或付款，怎样能帮他保留记录、及时求助？',
    ],
    copy: `今天我们一起练习了「${card.name}」。这里有一条可以记下来的应对方法：${card.steps[0]}，然后${card.steps[1]}。遇到催促先停一下，和信任的人一起核对。`,
  };
}
