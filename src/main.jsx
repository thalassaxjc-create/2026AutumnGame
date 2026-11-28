import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { CASE_BY_ID, EXAMPLE_CONFIG, SITE_NAME, TEMPLATE_BY_ID } from './data.mjs';
import { buildGame, recommendedTemplates } from './engine.mjs';
import { Admin, Guide } from './desk.jsx';
import { Home, Library, Templates, Works } from './pages.jsx';
import { GamePlayer } from './player.jsx';
import { ShareDialog, Workshop } from './workshop.jsx';
import { api, Button, Empty, Icon, readLocal, toRoute, writeLocal } from './ui.jsx';
import './style.css';

const nav = [
  ['', '首页'], ['create', '生成工坊'], ['library', '骗局图鉴'], ['templates', '模板库'], ['works', '我的作品'],
];

function currentRoute() {
  const [path = '', query = ''] = (location.hash.replace(/^#\/?/, '') || '').split('?', 2);
  return { path, page: path.split('/')[0], query: new URLSearchParams(query), hash: location.hash };
}

function useRoute() {
  const [route, setRoute] = useState(currentRoute);
  useEffect(() => {
    const changed = () => { setRoute(currentRoute()); window.scrollTo({ top: 0, behavior: 'instant' }); };
    window.addEventListener('hashchange', changed);
    return () => window.removeEventListener('hashchange', changed);
  }, []);
  return route;
}

function Header({ route }) {
  const [open, setOpen] = useState(false);
  useEffect(() => { setOpen(false); }, [route.hash]);
  return <header className="site-header">
    <div className="container header-inner">
      <a href="#/" className="brand" aria-label="反诈游戏工坊首页">
        <span className="brand-icon"><Icon name="shield" size={23} /></span><strong>{SITE_NAME}</strong>
      </a>
      <button className="icon-button nav-toggle" aria-label={open ? '关闭导航' : '打开导航'} aria-expanded={open} onClick={() => setOpen(!open)}>
        <Icon name={open ? 'close' : 'menu'} size={24} /></button>
      <nav className={open ? 'site-nav open' : 'site-nav'} aria-label="主导航">
        {nav.map(([id, label]) => <a key={id} className={route.page === id ? 'active' : ''} aria-current={route.page === id ? 'page' : undefined}
          href={`#/${id}`}>{label}</a>)}
      </nav>
      <Button href="#/create" variant="lime" icon="plus" className="header-action">新建游戏</Button>
    </div>
  </header>;
}

function Footer() {
  return <footer className="site-footer"><div className="container footer-inner">
    <a className="brand" href="#/"><span className="brand-icon"><Icon name="shield" size={20} /></span><strong>{SITE_NAME}</strong></a>
    <div className="footer-links"><a href="#/guide">讲解卡示例</a><a href="#/admin">内容审核与数据</a><a href="#/library">公开来源</a></div>
  </div></footer>;
}

function presetGame(route, draft) {
  if (draft) return buildGame(draft.config);
  const card = CASE_BY_ID[route.query.get('case')];
  if (!card) return buildGame(EXAMPLE_CONFIG);
  const template = TEMPLATE_BY_ID[route.query.get('template')] ?? recommendedTemplates(card)[0];
  return buildGame({ ...EXAMPLE_CONFIG, caseId: card.id, templateId: template.id });
}

function PlayerRoute({ route, draft, notify }) {
  const remoteId = route.path.split('/')[1];
  const [loaded, setLoaded] = useState(null);
  const [error, setError] = useState('');
  useEffect(() => {
    if (!remoteId) return;
    let current = true;
    api(`games/${encodeURIComponent(remoteId)}`).then(data => {
      if (current) setLoaded(buildGame(data.config));
    }).catch(issue => { if (current) setError(issue.message); });
    return () => { current = false; };
  }, [remoteId]);
  const game = remoteId ? loaded : presetGame(route, draft);
  if (route.page === 'preview' && !draft) return <div className="container inner-page"><Empty title="这份草稿不在此设备上"
    description="从这里新建一局，或向制作者索取已发布的试玩链接。" action={<Button href="#/create">新建游戏</Button>} /></div>;
  if (error) return <div className="container inner-page"><Empty icon="link" title="试玩链接无法打开"
    description={error} action={<Button href="#/library">去图鉴找一局</Button>} /></div>;
  if (!game) return <div className="container inner-page"><p role="status">正在打开小游戏…</p></div>;
  const guide = draft ? toRoute('guide', { draft: draft.id })
    : toRoute('guide', { case: game.case.id, template: game.template.id, game: remoteId });
  return <GamePlayer game={game} guideHref={guide} initialPresent={route.query.has('present') || game.config.modeId !== 'solo'}
    backHref={draft ? toRoute('create', { draft: draft.id }) : '#/templates'} notify={notify} />;
}

function GuideRoute({ route, draft, notify }) {
  const remoteId = route.query.get('game');
  const [shared, setShared] = useState(null);
  const [failure, setFailure] = useState('');
  useEffect(() => {
    if (!remoteId) return;
    let current = true;
    api(`games/${encodeURIComponent(remoteId)}`).then(data => {
      if (current) setShared(buildGame(data.config));
    }).catch(error => { if (current) setFailure(error.message); });
    return () => { current = false; };
  }, [remoteId]);
  if (failure) return <div className="container inner-page"><Empty title="无法读取这份讲解卡" description={failure}
    action={<Button href="#/guide">查看讲解卡示例</Button>} /></div>;
  if (remoteId && !shared) return <div className="container inner-page"><p role="status">正在载入讲解卡…</p></div>;
  const game = shared ?? presetGame(route, draft);
  const playHref = draft ? toRoute('preview', { draft: draft.id }) : remoteId ? toRoute(`play/${remoteId}`)
    : toRoute('play', { case: game.case.id, template: game.template.id });
  return <Guide game={game} playHref={playHref} notify={notify} />;
}

function App() {
  const route = useRoute();
  const [drafts, setDrafts] = useState(() => readLocal('fraud.drafts', []));
  const [share, setShare] = useState(null);
  const [toast, setToast] = useState('');
  const [toastSequence, setToastSequence] = useState(0);
  const notify = useCallback(message => { setToast(message); setToastSequence(sequence => sequence + 1); }, []);
  const activeDraft = useMemo(() => drafts.find(item => item.id === route.query.get('draft')), [drafts, route]);
  useEffect(() => { writeLocal('fraud.drafts', drafts); }, [drafts]);
  useEffect(() => {
    if (!toast) return;
    const timer = setTimeout(() => setToast(''), 3300);
    return () => clearTimeout(timer);
  }, [toast, toastSequence]);
  useEffect(() => {
    const name = nav.find(([id]) => route.page === id)?.[1] ?? ({ play: '试玩', preview: '试玩', guide: '讲解卡', admin: '内容审核' }[route.page]);
    document.title = route.page && name ? `${name} · ${SITE_NAME}` : SITE_NAME;
  }, [route.page]);

  function saveDraft(id, config) {
    const draftId = id || (crypto.randomUUID?.() ?? Date.now().toString(36));
    setDrafts(items => {
      const old = items.find(item => item.id === draftId);
      const same = old && JSON.stringify(old.config) === JSON.stringify(config);
      const draft = { id: draftId, config, updatedAt: new Date().toISOString(),
        status: same ? old.status : 'draft', shareId: same ? old.shareId : '' };
      return [draft, ...items.filter(item => item.id !== draftId)].slice(0, 100);
    });
    return draftId;
  }

  const published = useCallback((id, result) => {
    setDrafts(items => items.map(item => item.id === id ? { ...item, status: result.status, shareId: result.id } : item));
  }, []);
  let page;
  if (route.page === 'create') page = <Workshop key={route.hash} route={route.query} draft={activeDraft} saveDraft={saveDraft} onShare={setShare} notify={notify} />;
  else if (route.page === 'library') page = <Library notify={notify} />;
  else if (route.page === 'templates') page = <Templates />;
  else if (route.page === 'works') page = <Works drafts={drafts} onShare={setShare}
    duplicate={draft => { saveDraft('', draft.config); notify('已复制一份草稿。'); }}
    remove={id => { setDrafts(items => items.filter(item => item.id !== id)); notify('已删除本地作品。'); }} />;
  else if (route.page === 'admin') page = <Admin notify={notify} />;
  else if (route.page === 'guide') page = <GuideRoute key={route.hash} route={route} draft={activeDraft} notify={notify} />;
  else if (route.page === 'play' || route.page === 'preview') page = <PlayerRoute key={route.hash} route={route} draft={activeDraft} notify={notify} />;
  else page = <Home notify={notify} />;
  return <>
    <a className="skip-link" href="#content" onClick={event => { event.preventDefault(); document.querySelector('main')?.focus(); }}>跳到主要内容</a>
    <Header route={route} />
    <main id="content" tabIndex="-1">{page}</main>
    <Footer />
    {share && <ShareDialog draft={share} close={() => setShare(null)} onPublished={published} notify={notify} />}
    {!!toast && <div className="toast" key={toastSequence} role="status"><Icon name="badge" size={20} />{toast}</div>}
  </>;
}

createRoot(document.getElementById('root')).render(<App />);
