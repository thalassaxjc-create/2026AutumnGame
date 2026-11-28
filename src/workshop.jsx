import React, { useEffect, useMemo, useState } from 'react';
import QRCode from 'qrcode';
import {
  AUDIENCES, CASES, CASE_BY_ID, CATEGORY_BY_ID, DURATIONS, EXAMPLE_CONFIG, MODES, SCENES,
  SCENE_BY_ID, TEMPLATES, TEMPLATE_BY_ID,
} from './data.mjs';
import { analyzeInput, buildGame, cleanConfig, recommendedTemplates } from './engine.mjs';
import { CaseArt } from './pages.jsx';
import { api, Button, Chip, copyText, Icon, Modal, PageHeader, toRoute } from './ui.jsx';

const examples = [
  '给初二学生讲游戏充值诈骗，5 分钟班会投屏',
  '社区活动做一次冒充客服退款的 3 分钟演练',
  '企业培训：冒充领导转账，现场抢答',
];
const steps = ['选题', '配置玩法', '预览与发布'];

function CasePicker({ selected, choose, scene }) {
  const [query, setQuery] = useState('');
  const lower = query.trim().toLowerCase();
  const candidates = lower ? CASES.filter(card => [card.name, ...card.keywords].some(text => text.toLowerCase().includes(lower)))
    : [...scene.examples.map(id => CASE_BY_ID[id]), ...CASES.filter(card => !scene.examples.includes(card.id))];
  return <div className="case-picker">
    <label className="searchbar"><Icon name="search" /><input placeholder="在图鉴里找一个主题" aria-label="查找选题案卡" value={query}
      onChange={event => setQuery(event.target.value)} /></label>
    <div className="case-picker-list" role="group" aria-label="选择题材">
      {candidates.map(card => <button key={card.id} className={`case-pick ${card.id === selected ? 'selected' : ''}`}
        aria-pressed={card.id === selected} onClick={() => choose(card.id)}>
        <span className={`pick-icon tone-${CATEGORY_BY_ID[card.category].color}`}><Icon name={CATEGORY_BY_ID[card.category].icon} size={18} /></span>
        <span><strong>{card.name}</strong><small>{card.channel} · {card.difficulty}</small></span>
        <Icon name={card.id === selected ? 'check' : 'next'} size={17} />
      </button>)}
      {!candidates.length && <p className="muted">没有匹配案卡，试试「退款」「充值」或「兼职」。</p>}
    </div>
  </div>;
}

export function Workshop({ route, draft, saveDraft, onShare, notify }) {
  const selectedScene = SCENE_BY_ID[route.get('scene')] ?? SCENES[0];
  const initialCase = CASE_BY_ID[route.get('case')];
  const presetTemplate = TEMPLATE_BY_ID[route.get('template')];
  const [step, setStep] = useState(draft ? 3 : initialCase ? 2 : 1);
  const [sourceMode, setSourceMode] = useState(initialCase || route.get('scene') ? 'cards' : 'phrase');
  const [prompt, setPrompt] = useState('');
  const [promptError, setPromptError] = useState('');
  const [fileName, setFileName] = useState('');
  const [config, setConfig] = useState(draft?.config ?? {
    ...EXAMPLE_CONFIG, caseId: initialCase?.id ?? '', templateId: presetTemplate?.id ?? recommendedTemplates(initialCase)[0].id,
    sceneId: selectedScene.id, audience: selectedScene.audience, duration: 5, modeId: route.get('scene') === 'class' ? 'present' : 'solo',
    seed: Date.now() >>> 0, title: '', variants: {}, edits: {},
  });
  const [draftId, setDraftId] = useState(draft?.id ?? '');
  const [matchedCases, setMatchedCases] = useState([]);
  const [ready, setReady] = useState(Boolean(draft));
  const [error, setError] = useState('');
  const [showAll, setShowAll] = useState(false);
  const [editing, setEditing] = useState(null);
  const [editText, setEditText] = useState('');
  const selected = CASE_BY_ID[config.caseId];
  const scene = SCENE_BY_ID[config.sceneId];
  const suggestions = useMemo(() => recommendedTemplates(selected, config.modeId), [selected, config.modeId]);
  const game = useMemo(() => ready && step === 3 ? buildGame(config) : null, [ready, step, config]);

  function update(values) {
    setConfig(old => ({ ...old, ...values }));
    setError('');
  }

  function choose(id) {
    const card = CASE_BY_ID[id];
    update({ caseId: id, templateId: presetTemplate?.id ?? recommendedTemplates(card, config.modeId)[0].id, edits: {}, variants: {} });
    setPromptError('');
  }

  function interpret() {
    if (!prompt.trim()) { setPromptError('先描述你想讲的骗局，或者从图鉴选择。'); return; }
    const result = analyzeInput(prompt, config.sceneId);
    setMatchedCases(result.matches);
    const card = CASE_BY_ID[result.matches[0]];
    if (!card) {
      setPromptError('这段描述没有对应到已整理的案卡。可以换个关键词，或者打开「选案卡」。');
      return;
    }
    const { sceneId, audience, duration, modeId } = result;
    update({ sceneId, audience, duration, modeId, caseId: card.id,
      templateId: presetTemplate?.id ?? recommendedTemplates(card, modeId)[0].id, edits: {}, variants: {} });
    setPromptError('');
    setStep(2);
  }

  async function loadText(event) {
    const file = event.target.files?.[0];
    if (!file) return;
    if (!/\.(?:txt|md)$/i.test(file.name) || file.size > 100_000) {
      setPromptError('请选择 100 KB 以内的 TXT 或 Markdown 文件。');
      return;
    }
    try {
      setPrompt((await file.text()).slice(0, 6000));
      setFileName(file.name);
      setPromptError('');
    } catch { setPromptError('读取文件失败，请粘贴案例正文。'); }
  }

  function persist(values = config) {
    const cleaned = cleanConfig(values);
    const id = saveDraft(draftId, cleaned);
    setDraftId(id);
    return { id, config: cleaned };
  }

  function generate() {
    try {
      persist();
      setReady(true); setStep(3);
      notify('游戏已生成，草稿已保存。');
    } catch (issue) { setError(issue.message); }
  }

  function applyEdits(values) {
    try {
      const cleaned = cleanConfig(values);
      setConfig(cleaned);
      persist(cleaned);
      setEditing(null); setError('');
    } catch (issue) { setError(issue.message); }
  }

  function editRound(index) {
    setEditing(index); setEditText(config.edits[index] || game.rounds[index].context); setError('');
  }

  const templatesToShow = showAll ? TEMPLATES
    : [...suggestions, ...(!suggestions.some(template => template.id === config.templateId) ? [TEMPLATE_BY_ID[config.templateId]] : [])];

  return <div className="container inner-page workshop-page">
    <PageHeader title="生成工坊" description="选一个骗局，配一套玩法，几步就能把反诈小游戏带到现场。"
      icon="spark" color="sky" />
    <ol className="steps">
      {steps.map((name, index) => <li key={name} className={`${step === index + 1 ? 'current' : ''} ${step > index + 1 ? 'done' : ''}`}>
        <button disabled={index + 1 > step} onClick={() => setStep(index + 1)} aria-current={step === index + 1 ? 'step' : undefined}>
          <span>{step > index + 1 ? <Icon name="check" size={16} /> : index + 1}</span>{name}
        </button>
      </li>)}
    </ol>
    <div className="workshop-grid">
      <div className="panel workshop-panel">
        {step === 1 && <>
          <h2>这局想讲什么？</h2>
          <div className="tab-row source-tabs" aria-label="选择输入方式">
            {[['phrase', '一句话'], ['cards', '选案卡'], ['import', '导入案例']].map(([id, label]) =>
              <button key={id} className={sourceMode === id ? 'selected' : ''} onClick={() => { setSourceMode(id); setPromptError(''); }}>
                <Icon name={{ phrase: 'message', cards: 'book', import: 'file' }[id]} size={16} />{label}
              </button>)}
          </div>
          {sourceMode === 'cards' ? <CasePicker selected={config.caseId} choose={choose} scene={scene} /> : <>
            <label className="field-label" htmlFor="topic-input">{sourceMode === 'phrase' ? '一句话描述活动' : '粘贴或上传案例'}</label>
            <textarea id="topic-input" className="prompt-box" maxLength={6000} value={prompt}
              onChange={event => { setPrompt(event.target.value); setPromptError(''); }}
              placeholder={sourceMode === 'phrase' ? examples[0] : '粘贴新闻或案例摘要。请去掉真实姓名、联系方式和账号信息。'} />
            {sourceMode === 'phrase' ? <div className="prompt-pills">{examples.map(item =>
              <button key={item} onClick={() => { setPrompt(item); setPromptError(''); }}><Icon name="plus" size={13} />{item}</button>)}</div>
              : <label className="file-drop"><Icon name="file" /><span>{fileName || '选择 TXT 或 Markdown 文件'}</span>
                <input type="file" accept=".txt,.md,text/plain,text/markdown" onChange={loadText} /></label>}
            <p className="field-note">系统从内容库找相似情境，生成结果使用虚构素材。</p>
          </>}
          {promptError && <p className="error-text" role="alert">{promptError}</p>}
          <div className="panel-actions">
            <Button href="#/library" variant="plain" icon="book">先看看图鉴</Button>
            {sourceMode === 'cards' ? <Button disabled={!selected} onClick={() => setStep(2)} end="arrow">下一步</Button>
              : <Button onClick={interpret} icon="spark" end="arrow">识别并继续</Button>}
          </div>
        </>}
        {step === 2 && <>
          <h2>这局怎么玩？</h2>
          {!!matchedCases.length && <div className="identified">
            <Icon name="badge" /><div><strong>匹配到「{selected?.name}」</strong><p>{scene.label} · {config.audience} · {config.duration} 分钟</p></div>
            {matchedCases.length > 1 && <select aria-label="切换匹配案卡" value={config.caseId} onChange={event => choose(event.target.value)}>
              {matchedCases.map(id => <option key={id} value={id}>{CASE_BY_ID[id].name}</option>)}</select>}
          </div>}
          <div className="config-fields">
            <label className="field-label">使用场景<select value={config.sceneId} onChange={event => update({ sceneId: event.target.value, audience: SCENE_BY_ID[event.target.value].audience })}>
              {SCENES.map(item => <option key={item.id} value={item.id}>{item.label}</option>)}</select></label>
            <label className="field-label">参与人群<select value={config.audience} onChange={event => update({ audience: event.target.value })}>
              {AUDIENCES.map(item => <option key={item}>{item}</option>)}</select></label>
            <label className="field-label">活动时长<select value={config.duration} onChange={event => update({ duration: Number(event.target.value) })}>
              {DURATIONS.map(item => <option key={item} value={item}>{item} 分钟</option>)}</select></label>
          </div>
          <h3 className="field-title">互动方式</h3>
          <div className="mode-row" role="group" aria-label="选择互动方式">
            {MODES.map(item => <button key={item.id} className={`mode-button ${config.modeId === item.id ? 'selected' : ''}`}
              aria-pressed={config.modeId === item.id} onClick={() => update({ modeId: item.id })}><Icon name={item.icon} />{item.label}</button>)}
          </div>
          <div className="subheading"><h3>游戏模板</h3><button className="text-link" onClick={() => setShowAll(!showAll)}>
            {showAll ? '只看推荐' : `展开全部 ${TEMPLATES.length} 个`} <Icon name={showAll ? 'back' : 'down'} size={15} /></button></div>
          <div className="template-picker" role="group" aria-label="选择游戏模板">
            {templatesToShow.map(template => <button key={template.id} aria-pressed={config.templateId === template.id}
              onClick={() => update({ templateId: template.id })} className={`template-pick ${config.templateId === template.id ? 'selected' : ''}`}>
              <span className={`pick-icon tone-${template.color}`}><Icon name={template.icon} size={20} /></span>
              <span><strong>{template.name}</strong><small>{template.action}</small></span>
              {config.templateId === template.id && <Icon name="check" size={19} />}
            </button>)}
          </div>
          {error && <p className="error-text" role="alert">{error}</p>}
          <div className="panel-actions"><Button variant="outline" icon="arrow-left" onClick={() => setStep(1)}>上一步</Button>
            <Button icon="spark" end="arrow" onClick={generate}>生成这局游戏</Button></div>
        </>}
        {game && <>
          <div className="preview-title"><div><Chip color="lime">草稿已保存</Chip><h2>{game.name}</h2></div>
            <button className="text-link" onClick={() => { setEditing('title'); setEditText(config.title || game.name); setError(''); }}>修改标题</button></div>
          <div className="preview-actions">
            <Button icon="play" href={toRoute('preview', { draft: draftId })}>试玩游戏</Button>
            <Button variant="outline" icon="monitor" href={toRoute('preview', { draft: draftId, present: 1 })}>投屏</Button>
            <Button variant="outline" icon="clipboard" href={toRoute('guide', { draft: draftId })}>讲解卡</Button>
            <Button variant="lime" icon="qr" onClick={() => onShare({ id: draftId, config })}>发布分享</Button>
          </div>
          <div className="subheading"><h3>关卡预览</h3><span>{game.rounds.length} 道互动</span></div>
          <div className="round-preview-list">{game.rounds.map((round, index) => <details key={round.id} className="round-preview">
            <summary><span className="step-marker">{index + 1}</span><strong>{round.question}</strong><Icon name="down" size={16} /></summary>
            <div className="round-preview-body"><p>{round.context}</p>
              <p className="muted">解析 · {round.explanation}</p>
              <div className="button-row">
                <Button variant="outline" onClick={() => editRound(index)}>修改情境</Button>
                <Button variant="plain" icon="repeat" onClick={() => applyEdits({ ...config,
                  variants: { ...config.variants, [index]: ((config.variants[index] || 0) + 1) % 100 } })}>重生成此题</Button>
              </div>
            </div>
          </details>)}</div>
          <div className="panel-actions"><Button variant="outline" icon="arrow-left" onClick={() => setStep(2)}>调整配置</Button>
            <Button variant="plain" href="#/works" end="arrow">查看我的作品</Button></div>
        </>}
      </div>
      <aside className="workshop-aside">
        <div className="panel selected-card">
          <h3>{selected ? '当前案卡' : '从一个场景开始'}</h3>
          {selected ? <><CaseArt card={selected} /><h4>{selected.name}</h4><p>{selected.hook}</p>
            <ul>{selected.flags.slice(0, 2).map(flag => <li key={flag}>{flag}</li>)}</ul></>
            : <div className="aside-scenes">{SCENES.map(item => <button onClick={() => {
              update({ sceneId: item.id, audience: item.audience }); setSourceMode('cards');
            }} key={item.id}><span className={`pick-icon tone-${item.color}`}><Icon name={item.icon} size={17} /></span>
              {item.label}<Icon name="next" size={16} /></button>)}</div>}
        </div>
        <div className="aside-note"><Icon name="filecheck" size={21} /><div><strong>生成后一起拿到</strong>
          <p>可试玩的小游戏、主持人讲解卡、讨论问题和课后群文案。</p></div></div>
      </aside>
    </div>
    {editing !== null && <Modal title={editing === 'title' ? '编辑作品标题' : '编辑此题情境'} close={() => { setEditing(null); setError(''); }}>
      <label className="field-label" htmlFor="edit-text">{editing === 'title' ? '作品标题' : '情境文字'}</label>
      {editing === 'title' ? <input id="edit-text" value={editText} maxLength={48} onChange={event => setEditText(event.target.value)} />
        : <textarea id="edit-text" value={editText} maxLength={100} onChange={event => setEditText(event.target.value)} />}
      <p className="field-note">添加的文字可以先在本地试玩，发布前进入内容审核。</p>
      {error && <p className="error-text" role="alert">{error}</p>}
      <div className="modal-actions end"><Button variant="outline" onClick={() => setEditing(null)}>取消</Button>
        <Button onClick={() => applyEdits(editing === 'title' ? { ...config, title: editText }
          : { ...config, edits: { ...config.edits, [editing]: editText } })}>保存修改</Button></div>
    </Modal>}
  </div>;
}

export function ShareDialog({ draft, close, onPublished, notify }) {
  const [state, setState] = useState({ loading: true });
  useEffect(() => {
    let cancelled = false;
    async function publish() {
      try {
        const result = await api('games', draft.config);
        const link = `${location.origin}${location.pathname}#/play/${result.id}`;
        const qr = result.status === 'published' ? await QRCode.toDataURL(link, { width: 256, margin: 2, errorCorrectionLevel: 'M' }) : '';
        if (!cancelled) {
          onPublished(draft.id, result);
          setState({ ...result, link, qr });
        }
      } catch (issue) {
        if (!cancelled) setState({ error: issue.message });
      }
    }
    void publish();
    return () => { cancelled = true; };
  }, [draft]);
  return <Modal title="发布与分享" close={close}>
    {state.loading ? <p role="status">正在生成试玩链接和二维码…</p>
      : state.error ? <p className="error-text" role="alert">{state.error}</p>
      : state.status !== 'published' ? <>
        <div className="aside-note"><Icon name="filecheck" /><div><strong>{state.status === 'declined' ? '自定义内容需调整' : '等待内容审核'}</strong>
          <p>自定义标题或情境需要复核。你可以继续本地试玩，也可以从内容审核台查看进度。</p></div></div>
        <div className="modal-actions end"><Button href="#/admin" onClick={close} end="arrow">打开审核台</Button></div>
      </> : <>
        <div className="share-preview"><img src={state.qr} width="190" height="190" alt="打开已发布游戏的二维码" />
          <div><Chip color="lime">已发布</Chip><h3>{buildGame(draft.config).name}</h3>
            <p>扫码即可打开试玩。分享链接会保留当前的案卡、玩法和关卡配置。</p>
            {/^(localhost|127\.0\.0\.1)$/.test(location.hostname) && <small>本机地址适合当前设备试玩。手机扫码请从局域网或部署地址发布。</small>}
            <a className="text-link" href={state.qr} download="反诈小游戏二维码.png">下载二维码 <Icon name="download" size={15} /></a>
          </div>
        </div>
        <label className="field-label">试玩链接<input readOnly value={state.link} onFocus={event => event.target.select()} /></label>
        <div className="modal-actions end"><Button variant="outline" href={`#/play/${state.id}`} onClick={close} icon="play">打开链接</Button>
          <Button icon="copy" onClick={async () => { try { await copyText(state.link); notify('已复制试玩链接。'); } catch (issue) { notify(issue.message); } }}>复制链接</Button></div>
      </>}
  </Modal>;
}
