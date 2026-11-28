import React, { useEffect, useState } from 'react';
import { CASE_BY_ID, LIBRARY_DATE, SOURCES } from './data.mjs';
import { buildGame, teachingPack } from './engine.mjs';
import { api, Button, Chip, copyText, downloadText, Empty, Icon, Modal, PageHeader, prettyDate, toRoute } from './ui.jsx';

export function Guide({ game, playHref, notify }) {
  const pack = teachingPack(game);
  const [copying, setCopying] = useState(false);
  async function copy() {
    setCopying(true);
    try { await copyText(pack.copy); notify('课后群文案已复制。'); }
    catch (issue) { notify(issue.message); }
    finally { setCopying(false); }
  }
  function download() {
    const list = items => items.map((item, index) => `${index + 1}. ${item}`).join('\n');
    const text = [
      `# ${pack.name} — 讲解卡`, pack.lesson, '## 开场提问', pack.opening,
      '## 带领步骤', list(pack.flow), '## 关键破绽', list(game.case.flags), '## 安全动作', list(game.case.steps),
      '## 课后讨论', list(pack.topics), '## 群文案', pack.copy,
      '## 来源', `[${game.source.label}](${game.source.url})`, `案卡整理日期：${LIBRARY_DATE}`,
    ].join('\n\n');
    downloadText('反诈讲解卡.md', text, 'text/markdown');
  }
  return <div className="container inner-page print-guide">
    <PageHeader title="主持人讲解卡" description={`${game.name} · ${pack.lesson}`} icon="clipboard" color="yellow"
      actions={<div className="button-row no-print">
        <Button href={playHref} icon="play">进入游戏</Button>
        <Button variant="outline" icon="download" onClick={download}>下载讲解卡</Button>
        <Button variant="outline" onClick={() => window.print()}>打印</Button>
      </div>} />
    <div className="guide-grid">
      <article className="panel guide-card"><h2><Icon name="message" /> 怎么开场</h2><p className="guide-quote">{pack.opening}</p>
        <h3>带领步骤</h3><ol>{pack.flow.map(item => <li key={item}>{item}</li>)}</ol></article>
      <article className="panel guide-card"><h2><Icon name="search" /> 拆解这局</h2><h3>关键破绽</h3>
        <ul>{game.case.flags.map(item => <li key={item}>{item}</li>)}</ul><h3>安全动作</h3>
        <ol>{game.case.steps.map(item => <li key={item}>{item}</li>)}</ol>
        <p className="source-line"><a href={game.source.url} target="_blank" rel="noreferrer">{game.source.label} <Icon name="external" size={14} /></a></p>
      </article>
      <article className="panel guide-card"><h2><Icon name="users" /> 课后讨论</h2><ol>{pack.topics.map(item => <li key={item}>{item}</li>)}</ol></article>
      <article className="panel guide-card"><h2><Icon name="copy" /> 群文案</h2><div className="copy-preview">{pack.copy}</div>
        <Button variant="outline" className="no-print" icon="copy" disabled={copying} onClick={copy}>复制群文案</Button></article>
    </div>
  </div>;
}

export function Admin({ notify }) {
  const [token, setToken] = useState(() => sessionStorage.getItem('fraud.admin') ?? '');
  const [tokenInput, setTokenInput] = useState('');
  const [tab, setTab] = useState('queue');
  const [state, setState] = useState({ loading: true });
  const [checking, setChecking] = useState(null);
  const [busy, setBusy] = useState(false);

  async function refresh(currentToken = token) {
    try {
      const data = await api('admin', undefined, currentToken);
      setState({ data });
    } catch (error) { setState({ error: error.message, denied: error.status === 401 }); }
  }
  useEffect(() => { void refresh(token); }, [token]);

  async function review(id, action) {
    setBusy(true);
    try {
      await api(`admin/review/${id}`, { action }, token);
      notify(action === 'approve' ? '审核通过，试玩链接已生效。' : '作品已退回调整。');
      setChecking(null);
      await refresh();
    } catch (issue) { notify(issue.message); }
    finally { setBusy(false); }
  }

  const data = state.data;
  return <div className="container inner-page">
    <PageHeader title="内容审核台" description="复核自定义情境、处理纠错，并查看匿名试玩数据。" color="lilac" icon="filecheck" />
    {state.loading && <p role="status">正在载入审核台…</p>}
    {state.denied ? <form className="panel admin-login" onSubmit={event => {
      event.preventDefault();
      const value = tokenInput.trim();
      sessionStorage.setItem('fraud.admin', value);
      if (value === token) void refresh(value);
      else setToken(value);
    }}>
      <h2>打开审核台</h2><label className="field-label">管理口令<input value={tokenInput} onChange={event => setTokenInput(event.target.value)} type="password" autoComplete="current-password" /></label>
      <p className="field-note">口令由部署时设置。它只保存在当前会话。</p>
      <Button type="submit">进入审核台</Button>
    </form> : state.error && <Empty title={state.error} action={<Button onClick={() => refresh()}>重试</Button>} />}
    {data && <>
      <div className="stat-grid">
        {[
          ['已发布作品', data.stats.published, 'filecheck'], ['开始试玩', data.stats.started, 'play'],
          ['完成率', data.stats.started ? `${Math.round(data.stats.completed / data.stats.started * 100)}%` : '—', 'check'],
          ['平均得分', data.stats.average == null ? '—' : `${data.stats.average}/100`, 'clipboard'],
        ].map(([label, value, icon]) => <div className="panel stat-tile" key={label}><Icon name={icon} /><span>{label}</span><strong>{value}</strong></div>)}
      </div>
      <div className="tab-row desk-tabs">
        {['queue', 'reports', 'stats', 'live'].map(id => <button key={id} className={tab === id ? 'selected' : ''} onClick={() => setTab(id)}>
          {{ queue: '作品审核', reports: '内容纠错', stats: '试玩数据', live: '已发布' }[id]}</button>)}
      </div>
      {tab === 'queue' && (data.queue.length ? <div className="work-list">{data.queue.map(game => <article className="work-card" key={game.id}>
        <div className="work-icon tone-yellow"><Icon name="filecheck" size={25} /></div>
        <div className="work-info"><Chip color="yellow">{game.status === 'declined' ? '已退回' : '待复核'}</Chip><h3>{game.name}</h3><p>{prettyDate(game.createdAt)}</p></div>
        <Button variant="outline" end="arrow" onClick={() => setChecking(game)}>查看内容</Button>
      </article>)}</div> : <Empty title="没有待复核的作品" description="使用内置案卡生成的作品可以直接发布。自定义情境和标题会在这里等待审核。" icon="filecheck" />)}
      {tab === 'reports' && (data.reports.length ? <div className="work-list">{data.reports.map(item => <article className="work-card report-card" key={item.id}>
        <div className="work-info"><div className="meta-row"><Chip color={item.status === 'open' ? 'peach' : 'lime'}>{item.status === 'open' ? '待核对' : '已处理'}</Chip>
          <span>{item.kind} · {prettyDate(item.createdAt)}</span></div><h3>{CASE_BY_ID[item.caseId].name}</h3><p>{item.message}</p>
          <a className="text-link" target="_blank" rel="noreferrer" href={SOURCES[CASE_BY_ID[item.caseId].source].url}>打开案卡来源 <Icon name="external" size={14} /></a></div>
        {item.status === 'open' && <Button variant="outline" icon="check" onClick={async () => {
          try { await api(`admin/reports/${item.id}`, {}, token); await refresh(); notify('已标为处理。'); }
          catch (issue) { notify(issue.message); }
        }}>标为已处理</Button>}
      </article>)}</div> : <Empty title="还没有收到内容纠错" description="试玩者可以在案卡详情中提交意见。" icon="flag" />)}
      {tab === 'stats' && <div className="guide-grid">
        <section className="panel guide-card"><h2>玩法参与</h2><p className="muted">统计匿名开始与完成次数。</p>
          <div className="data-table"><div className="data-table-row header"><span>玩法</span><span>开始</span><span>完成</span><span>均分</span></div>
            {data.stats.byTemplate.map(item => <div className="data-table-row" key={item.id}><strong>{item.name}</strong>
              <span>{item.started}</span><span>{item.completed}</span><span>{item.average ?? '—'}</span></div>)}</div></section>
        <section className="panel guide-card"><h2>易错案卡</h2>
          <p className="muted">试玩满意度 · {data.stats.satisfaction == null ? '暂无反馈' : `${data.stats.satisfaction} / 5`}</p>
          {data.stats.mistakes.length ? <ul>{data.stats.mistakes.map(item => <li key={item.name}>{item.name} · {item.misses} / {item.rounds} 次答错</li>)}</ul>
            : <p>出现错题后，相关案卡会显示在这里。</p>}</section>
      </div>}
      {tab === 'live' && (data.published.length ? <div className="work-list">{data.published.map(game => <article className="work-card" key={game.id}>
        <div className="work-info"><Chip color="lime">已发布</Chip><h3>{game.name}</h3></div>
        <Button variant="outline" href={toRoute(`play/${game.id}`)} icon="play">打开试玩</Button>
      </article>)}</div> : <Empty title="还没有已发布作品" icon="link" />)}
    </>}
    {checking && <Modal title="复核自定义内容" close={() => setChecking(null)} width="modal-wide">
      <h3>{checking.name}</h3>
      <p>核对是否包含个人信息、可执行的诈骗指令或误导性建议。</p>
      {buildGame(checking.config).rounds.map(round => <div className="review-block" key={round.id}><strong>{round.question}</strong>
        <p>{round.context}</p></div>)}
      <div className="modal-actions end"><Button variant="outline" disabled={busy} onClick={() => review(checking.id, 'decline')}>退回调整</Button>
        <Button disabled={busy} icon="check" onClick={() => review(checking.id, 'approve')}>审核通过</Button></div>
    </Modal>}
  </div>;
}
