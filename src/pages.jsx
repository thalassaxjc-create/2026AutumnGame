import React, { useMemo, useState } from 'react';
import {
  AUDIENCES, CASES, CASE_BY_ID, CATEGORIES, CATEGORY_BY_ID, FEATURED_CASES, LIBRARY_DATE, SCENES,
  SOURCES, TEMPLATES, TEMPLATE_BY_ID,
} from './data.mjs';
import { buildGame, recommendedTemplates } from './engine.mjs';
import { api, Button, Chip, Empty, Icon, Modal, PageHeader, prettyDate, SectionHeading, toRoute } from './ui.jsx';

export function CaseArt({ card }) {
  const category = CATEGORY_BY_ID[card.category];
  return card.art !== null
    ? <div className={`case-art case-panel case-panel-${card.art}`} role="img" aria-label={`${card.name}插画`} />
    : <div className={`case-art category-art tone-${category.color}`}><span><Icon name={category.icon} size={45} /></span></div>;
}

function CaseCard({ card, choose }) {
  const category = CATEGORY_BY_ID[card.category];
  return <article className="case-card">
    <button className="card-cover-button" onClick={() => choose(card)} aria-label={`查看${card.name}`}><CaseArt card={card} /></button>
    <div className="case-card-body">
      <div className="card-meta"><Chip color={category.color}>{category.label}</Chip><span>{card.difficulty}</span></div>
      <h3><button onClick={() => choose(card)}>{card.name}</button></h3>
      <p>{card.hook}</p>
      <a className="text-link" href={toRoute('create', { case: card.id })}>用这张案卡生成 <Icon name="external" size={15} /></a>
    </div>
  </article>;
}

export function CaseDetails({ card, close, notify }) {
  const [reporting, setReporting] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const source = SOURCES[card.source];
  const firstTemplate = recommendedTemplates(card)[0];
  async function submitReport() {
    setBusy(true); setError('');
    try {
      await api('feedback', { caseId: card.id, message });
      setReporting(false); setMessage(''); notify('纠错反馈已送到内容审核台。');
    } catch (issue) { setError(issue.message); }
    finally { setBusy(false); }
  }
  return <Modal title={card.name} close={close} width="modal-wide">
    <div className="case-detail-banner"><CaseArt card={card} /></div>
    <div className="meta-row"><Chip color={CATEGORY_BY_ID[card.category].color}>{CATEGORY_BY_ID[card.category].label}</Chip>
      <Chip>{card.channel}</Chip><Chip>{card.difficulty}</Chip></div>
    <p className="lead">{card.hook}</p>
    <div className="case-insights">
      <section><h3><Icon name="alert" /> 值得暂停的线索</h3><ul>{card.flags.map(flag => <li key={flag}>{flag}</li>)}</ul></section>
      <section><h3><Icon name="shield" /> 可以做什么</h3><ol>{card.steps.map(step => <li key={step}>{step}</li>)}</ol></section>
    </div>
    <p className="source-line">案卡整理日期 {LIBRARY_DATE} · <a href={source.url} target="_blank" rel="noreferrer">{source.label} <Icon name="external" size={13} /></a></p>
    {reporting && <div className="inline-form">
      <label htmlFor="report-note">需要核对的地方</label>
      <textarea id="report-note" maxLength={260} value={message} onChange={event => setMessage(event.target.value)} placeholder="请说明哪条内容需要复核，隐去个人信息。" />
      {error && <p className="error-text" role="alert">{error}</p>}
      <Button variant="outline" disabled={busy || !message.trim()} onClick={submitReport}>提交反馈</Button>
    </div>}
    <div className="modal-actions">
      <button className="text-link" onClick={() => setReporting(!reporting)}><Icon name="flag" size={15} /> 内容纠错</button>
      <div className="button-row">
        <Button variant="outline" href={toRoute('play', { case: card.id, template: firstTemplate.id })} icon="play">试玩此案</Button>
        <Button href={toRoute('create', { case: card.id })} end="arrow">用作选题</Button>
      </div>
    </div>
  </Modal>;
}

export function Home({ notify }) {
  const [activeCase, setActiveCase] = useState(null);
  return <>
    <section className="hero">
      <div className="hero-inner">
        <img className="hero-image" src="/art/hero.webp" fetchPriority="high" alt="漫画调查员拿着手机辨认一条可疑消息" />
        <div className="hero-copy">
          <div className="hero-kicker"><Icon name="shield" size={17} /> 反诈内容创作平台</div>
          <h1>把骗局做成<br /><em>一局游戏。</em></h1>
          <p>从一个情境出发，生成可以试玩、投屏和分享的反诈小游戏。</p>
          <div className="button-row">
            <Button href="#/create" variant="lime" end="external">生成小游戏</Button>
            <Button href="#/play?case=refund&template=clues" variant="on-blue" icon="play">试玩示例</Button>
          </div>
        </div>
      </div>
    </section>
    <div className="home-body container">
      <section className="scene-section" aria-labelledby="scene-heading">
        <div className="scene-intro"><h2 id="scene-heading">你打算在哪里玩？</h2><p>按使用场景推荐选题</p></div>
        <div className="scene-grid">
          {SCENES.map(scene => <a className="scene-card" key={scene.id} href={toRoute('create', { scene: scene.id })}>
            <span className={`scene-icon tone-${scene.color}`}><Icon name={scene.icon} size={22} /></span>
            <strong>{scene.label}</strong><Icon name="external" size={14} />
          </a>)}
        </div>
      </section>
      <section className="home-section">
        <SectionHeading title="热门骗局" description="挑一张案卡，看看生活中的破绽藏在哪里。"
          action={<Button variant="outline" href="#/library" end="arrow">浏览图鉴</Button>} />
        <div className="cards-grid featured-grid">
          {FEATURED_CASES.map(id => <CaseCard key={id} card={CASE_BY_ID[id]} choose={setActiveCase} />)}
        </div>
      </section>
      <section className="home-section">
        <SectionHeading title="选一种玩法" description="同一案卡可以编成多种游戏，适合个人练习和现场互动。"
          action={<Button variant="outline" href="#/templates" end="arrow">全部模板</Button>} />
        <div className="cards-grid">{TEMPLATES.slice(0, 3).map(template => <TemplateCard key={template.id} template={template} />)}</div>
      </section>
      <section className="home-pack">
        <div><Icon name="clipboard" size={29} /><h2>一局游戏，一份讲解包</h2><p>每个作品都附带破绽、应对动作、讨论问题和课后群文案。</p></div>
        <Button href="#/guide" variant="lime" end="arrow">看看讲解卡</Button>
      </section>
    </div>
    {activeCase && <CaseDetails card={activeCase} close={() => setActiveCase(null)} notify={notify} />}
  </>;
}

export function Library({ notify }) {
  const [text, setText] = useState('');
  const [category, setCategory] = useState('');
  const [audience, setAudience] = useState('');
  const [activeCase, setActiveCase] = useState(null);
  const cards = useMemo(() => {
    const query = text.trim().toLowerCase();
    return CASES.filter(card => (!category || card.category === category) &&
      (!audience || card.audiences.includes(audience)) &&
      [card.name, card.hook, ...card.keywords, ...card.flags].some(value => value.toLowerCase().includes(query)));
  }, [text, category, audience]);
  return <div className="container inner-page">
    <PageHeader title="骗局图鉴" description={`${CASES.length} 张情境案卡，带你识别诱因、看清破绽，找到可执行的应对动作。`} icon="book" color="lime" />
    <div className="filter-section">
      <div className="filter-row"><label className="searchbar"><Icon name="search" /><input value={text} onChange={event => setText(event.target.value)} placeholder="搜索骗局、关键词或破绽" aria-label="搜索骗局" /></label>
        <label className="select-label">适合人群<select value={audience} onChange={event => setAudience(event.target.value)}><option value="">全部人群</option>
          {AUDIENCES.map(item => <option key={item}>{item}</option>)}</select></label>
      </div>
      <div className="tab-row" aria-label="骗局分类">
        <button className={!category ? 'selected' : ''} onClick={() => setCategory('')}>全部</button>
        {CATEGORIES.map(item => <button key={item.id} className={category === item.id ? 'selected' : ''} onClick={() => setCategory(item.id)}>
          <Icon name={item.icon} size={15} />{item.label}
        </button>)}
      </div>
    </div>
    <div className="results-count" aria-live="polite">找到 {cards.length} 张案卡</div>
    {cards.length ? <div className="cards-grid">{cards.map(card => <CaseCard key={card.id} card={card} choose={setActiveCase} />)}</div>
      : <Empty icon="search" title="没有找到匹配的案卡" description="试试「退款」「屏幕共享」或清空筛选条件。"
        action={<Button variant="outline" onClick={() => { setText(''); setCategory(''); setAudience(''); }}>清空筛选</Button>} />}
    {activeCase && <CaseDetails card={activeCase} close={() => setActiveCase(null)} notify={notify} />}
  </div>;
}

function TemplateCard({ template }) {
  const demos = { chat: 'game-recharge', clues: 'refund', quiz: 'game-skin', branch: 'campus-loan',
    call: 'police', blind: 'package', evidence: 'invest', roles: 'family-emergency', ai: 'ai-face', transfer: 'boss' };
  return <article className="template-card">
    <div className={`template-art tone-${template.color}`}><Icon name={template.icon} size={46} /><span>{template.action}</span></div>
    <div className="template-card-body"><h3>{template.name}</h3><p>{template.detail}</p>
      <div className="button-row">
        <Button variant="outline" href={toRoute('play', { case: demos[template.id], template: template.id })} icon="play">试玩</Button>
        <Button variant="plain" href={toRoute('create', { template: template.id })} end="arrow">用此玩法</Button>
      </div>
    </div>
  </article>;
}

export function Templates() {
  return <div className="container inner-page">
    <PageHeader title="游戏模板" description="从聊天拆弹到证据拼图，选符合现场节奏的互动方式。" icon="game" color="peach" />
    <div className="cards-grid">{TEMPLATES.map(template => <TemplateCard key={template.id} template={template} />)}</div>
  </div>;
}

export function Works({ drafts, remove, duplicate, onShare }) {
  const [tab, setTab] = useState('all');
  const [search, setSearch] = useState('');
  const [deleting, setDeleting] = useState(null);
  const items = drafts.filter(draft => (tab === 'all' || (tab === 'published' ? draft.status === 'published' : draft.status !== 'published')) &&
    buildGame(draft.config).name.includes(search.trim()));
  return <div className="container inner-page">
    <PageHeader title="我的作品" description="继续修改草稿，复用上次的班会作品，或再次分享试玩。"
      icon="bag" color="lilac" actions={<Button href="#/create" icon="plus">新建小游戏</Button>} />
    <div className="filter-row">
      <div className="tab-row" aria-label="作品状态">{[['all', '全部作品'], ['draft', '草稿与待审'], ['published', '已发布']].map(([id, name]) =>
        <button key={id} className={tab === id ? 'selected' : ''} onClick={() => setTab(id)}>{name}</button>)}</div>
      <label className="searchbar"><Icon name="search" /><input value={search} onChange={event => setSearch(event.target.value)} placeholder="按作品名称查找" aria-label="搜索作品" /></label>
    </div>
    {items.length ? <div className="work-list">{items.map(draft => {
      const game = buildGame(draft.config);
      return <article className="work-card" key={draft.id}>
        <div className={`work-icon tone-${TEMPLATE_BY_ID[draft.config.templateId].color}`}><Icon name={game.template.icon} size={27} /></div>
        <div className="work-info"><div className="meta-row"><Chip color={draft.status === 'published' ? 'lime' : 'paper'}>
          {draft.status === 'published' ? '已发布' : draft.status === 'pending' ? '待审核' : '草稿'}</Chip><span>{prettyDate(draft.updatedAt)}</span></div>
          <h3>{game.name}</h3><p>{draft.config.audience} · {draft.config.duration} 分钟 · {game.rounds.length} 道互动</p></div>
        <div className="work-actions">
          <Button variant="outline" href={toRoute('preview', { draft: draft.id })} icon="play">试玩</Button>
          <Button variant="plain" href={toRoute('create', { draft: draft.id })}>编辑</Button>
          <button title="分享" className="icon-button" onClick={() => onShare(draft)} aria-label="分享作品"><Icon name="qr" /></button>
          <button title="复制草稿" className="icon-button" onClick={() => duplicate(draft)} aria-label="复制草稿"><Icon name="copy" /></button>
          <button title="删除本地作品" className="icon-button" onClick={() => setDeleting(draft)} aria-label="删除本地作品"><Icon name="close" /></button>
        </div>
      </article>;
    })}</div> : <Empty title={drafts.length ? '没有符合条件的作品' : '你的第一局游戏等着开场'}
      description="生成的作品会保存在此浏览器，方便回来编辑和复用。"
      action={<Button href="#/create" end="arrow">去生成工坊</Button>} />}
    {deleting && <Modal title="删除这份本地作品？" close={() => setDeleting(null)}>
      <p>已发布的链接可以继续使用。此浏览器里的草稿、编辑入口和本地副本会移除。</p>
      <div className="modal-actions end"><Button variant="outline" onClick={() => setDeleting(null)}>取消</Button>
        <Button onClick={() => { remove(deleting.id); setDeleting(null); }}>删除本地作品</Button></div>
    </Modal>}
  </div>;
}
