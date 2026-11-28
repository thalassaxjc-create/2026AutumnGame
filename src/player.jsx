import React, { useEffect, useRef, useState } from 'react';
import { CASE_BY_ID } from './data.mjs';
import { assess, reportFor } from './engine.mjs';
import { CaseArt } from './pages.jsx';
import { api, Button, Chip, Icon, readLocal, toRoute, writeLocal } from './ui.jsx';

function Rules({ game }) {
  return <div className="play-intro-grid">
    <section className="play-intro-copy">
      <div className="meta-row"><Chip color={game.template.color}>{game.template.name}</Chip><Chip>情境模拟</Chip></div>
      <h1>{game.name}</h1>
      <p>{game.case.hook}</p>
      <div className="rule-box"><Icon name={game.template.icon} size={25} /><div><strong>{game.template.action}</strong>
        <p>{game.rounds.length} 道互动 · 约 {game.config.duration} 分钟</p></div></div>
    </section>
    <div className="play-intro-art"><CaseArt card={game.case} /></div>
  </div>;
}

function EvidenceBoard({ round, style, revealed, reveal, witnesses, toggleWitness, last, turn, speech }) {
  if (style === 'roles') return <div className="context-panel tone-lime">
    <div className="context-heading"><Icon name="users" /><strong>三份角色证词</strong></div>
    <p className="context-intro">逐一打开证词，在圆桌上说说你判断的依据。</p>
    <div className="witness-list">{round.witnesses.map((item, index) => <button className={witnesses.includes(index) ? 'opened' : ''}
      key={item.name} aria-expanded={witnesses.includes(index)} onClick={() => toggleWitness(index)}>
      <strong>{item.name}<Icon name={witnesses.includes(index) ? 'check' : 'down'} size={17} /></strong>
      {witnesses.includes(index) && <span>{item.text}</span>}
    </button>)}</div>
  </div>;
  if (style === 'call') return <div className={`context-panel call-panel ${revealed ? 'connected' : ''}`}>
    <div className="call-avatar"><Icon name="phone" size={31} /></div>
    <h3>{revealed ? '模拟通话已接通' : '一通未知来电'}</h3>
    {revealed ? <><p>{round.context}</p><Button variant="outline" icon="message" onClick={speech}>朗读通话摘要</Button></>
      : <><p>接听后会显示演练字幕。</p><Button variant="lime" icon="phone" onClick={reveal}>接听模拟来电</Button></>}
  </div>;
  if (style === 'blind' && !revealed) return <div className="context-panel blind-panel">
    <div className="blind-gift"><Icon name="gift" size={62} /></div><h3>一封还未打开的案卡</h3>
    <Button variant="lime" icon="gift" onClick={reveal}>翻开盲盒</Button>
  </div>;
  if (style === 'transfer') return <div className="context-panel tone-yellow">
    <div className="context-heading"><Icon name="wallet" /><strong>模拟支付确认</strong></div>
    <div className="mock-transfer"><span>练习状态</span><strong>{revealed ? '已暂停 · 待核验' : '待确认'}</strong>
      <p>来源待确认的付款请求</p></div>
    <p>{round.context}</p>
    {!revealed && <Button variant="primary" icon="shield" onClick={reveal}>先暂停模拟支付</Button>}
  </div>;
  if (style === 'chat') return <div className="context-panel chat-panel">
    <div className="chat-top"><span className="chat-avatar"><Icon name="messages" size={21} /></span>
      <div><strong>{round.channel}</strong><small>来源待确认 · 模拟聊天</small></div></div>
    <div className="conversations">
      {turn > 0 && <div className="msg mine">{last?.correct ? '我先暂停，等核实后再说。' : '我需要重新确认刚才的判断。'}</div>}
      <div className="msg">{round.context}</div>
      <div className="msg small">这条消息让你感到需要马上做决定。</div>
    </div>
  </div>;
  if (style === 'ai') return <div className="context-panel tone-peach">
    <div className="context-heading"><Icon name="scan" /><strong>身份核验板</strong></div>
    <div className="verification-pair"><div><Icon name="smartphone" size={25} /><strong>收到的片段</strong><span>{round.context}</span></div>
      <div><Icon name="shield" size={25} /><strong>独立求证记录</strong><span>还没有通过你原先知道的渠道重新确认。</span></div></div>
  </div>;
  const titles = { clues: '模拟证据板', branch: '情境进展', quiz: '抢答题目', evidence: '案情摘要', blind: round.caseName };
  return <div className={`context-panel tone-${style === 'clues' ? 'sky' : 'paper'}`}>
    <div className="context-heading"><Icon name={style === 'clues' ? 'search' : style === 'blind' ? 'gift' : 'file'} /><strong>{titles[style]}</strong></div>
    {style === 'branch' && turn > 0 && <p className="branch-trail">{last?.correct
      ? '你刚刚停住了危险操作，有了进一步核实的时间。'
      : '对方的说辞又变了。你依然可以从这一步停下来求证。'}</p>}
    <p className="context-copy">{round.context}</p>
    <div className="context-channel"><Icon name="link" size={15} /> {round.channel}</div>
  </div>;
}

function AnswerOptions({ round, selection, choose, outcome, disabled }) {
  return <div className={`answer-options ${round.kind === 'clues' ? 'clue-options' : ''}`}>
    {round.choices.map((option, index) => {
      const picked = selection.includes(option.id);
      const gradeStyle = outcome ? (round.expected.includes(option.id) ? ' is-right' : picked ? ' is-wrong' : '') : '';
      return <button type="button" key={option.id} disabled={disabled || Boolean(outcome)} aria-pressed={picked}
        className={`answer-option ${picked ? 'picked' : ''}${gradeStyle}`} onClick={() => choose(option.id)}>
        <span className="answer-key">{outcome && round.expected.includes(option.id) ? <Icon name="check" size={17} /> : index + 1}</span>
        <span>{option.label}</span>
        {picked && round.kind === 'order' && <strong className="picked-index">第 {selection.indexOf(option.id) + 1} 步</strong>}
      </button>;
    })}
  </div>;
}

function Ending({ game, results, restart, guideHref, notify }) {
  const report = reportFor(results);
  const [rating, setRating] = useState(0);
  const [pendingRating, setPendingRating] = useState(false);
  const collection = readLocal('fraud.collection', []);
  return <div className="ending">
    <div className="ending-hero">
      <div><div className="meta-row"><Chip color="lime">结案报告</Chip></div><h1>这局已完成</h1>
        <p>{game.name}。{report.correct === report.count ? '你识别了所有关键风险。' : '把容易犹豫的地方再看一遍，下一次会更从容。'}</p>
        <div className="button-row"><Button variant="lime" icon="repeat" onClick={restart}>再玩一局</Button>
          <Button variant="on-blue" icon="clipboard" href={guideHref}>打开讲解卡</Button></div></div>
      <div className="score-card"><span>反诈得分</span><strong>{report.score}<small>/100</small></strong><span>{report.rank}</span></div>
    </div>
    <div className="ending-grid">
      <section className="panel"><h2>作答回顾</h2><p>答对 {report.correct} / {report.count} 道
        {game.template.id === 'blind' && ` · 图鉴累计收录 ${collection.length} 张`}</p>
        {game.rounds.map((round, index) => <details className="round-preview" key={round.id} open={!results[index]?.correct || undefined}>
          <summary><span className={`step-marker ${results[index]?.correct ? 'good-marker' : 'warn-marker'}`}>
            <Icon name={results[index]?.correct ? 'check' : 'alert'} size={15} /></span>
            <strong>{round.question}</strong><Icon name="down" size={16} />
          </summary><div className="round-preview-body">
            <p>你的选择 · {results[index]?.answer.map(id => round.choices.find(option => option.id === id)?.label).filter(Boolean).join('、') || '未作答'}</p>
            <p>要点 · {round.explanation}</p>
          </div>
        </details>)}
      </section>
      <aside className="panel takeaway"><h2><Icon name="shield" /> 把方法带走</h2>
        <ol>{game.case.steps.map(step => <li key={step}>{step}</li>)}</ol>
        <a className="text-link" href={game.source.url} target="_blank" rel="noreferrer">查看案卡来源 <Icon name="external" size={15} /></a>
        <div className="rating-panel"><h3>这局对你有帮助吗？</h3>
          <div className="rating-row" role="group" aria-label="给试玩评分">{[1, 2, 3, 4, 5].map(value => <button key={value} disabled={pendingRating || rating > 0}
            className={value <= rating ? 'rated' : ''} aria-label={`${value} 星`} onClick={async () => {
              setPendingRating(true);
              try { await api('feedback', { rating: value }); setRating(value); notify('谢谢你的反馈。'); }
              catch (error) { notify(error.message); } finally { setPendingRating(false); }
            }}>★</button>)}</div>
        </div>
      </aside>
    </div>
  </div>;
}

export function GamePlayer({ game, initialPresent, guideHref, backHref, notify }) {
  const [phase, setPhase] = useState('intro');
  const [turn, setTurn] = useState(0);
  const [selection, setSelection] = useState([]);
  const [results, setResults] = useState([]);
  const [outcome, setOutcome] = useState(null);
  const [revealed, setRevealed] = useState(false);
  const [witnesses, setWitnesses] = useState([]);
  const [time, setTime] = useState(0);
  const [present, setPresent] = useState(initialPresent);
  const session = useRef('');
  const settled = useRef(false);
  const turnStart = useRef(null);
  const round = game.rounds[turn];
  const template = game.template.id;
  const lockUntilReveal = ['call', 'blind', 'transfer'].includes(template);
  const interactionLocked = (lockUntilReveal && !revealed) || (template === 'roles' && witnesses.length < 3);
  const progress = phase === 'ending' ? 100 : Math.round(turn / game.rounds.length * 100);

  function record(type, report) {
    const { caseId, templateId, duration } = game.config;
    void api('events', { type, sessionId: session.current, config: { caseId, templateId, duration }, score: report?.score, correct: report?.correct })
      .catch(() => { /* The game is playable when telemetry is temporarily unavailable. */ });
  }

  function start() {
    session.current = crypto.randomUUID?.() ?? `session-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 9).padEnd(7, '0')}`;
    record('start');
    setResults([]); setTurn(0); setPhase('play'); resetTurn();
  }

  function resetTurn() {
    settled.current = false;
    setSelection([]); setOutcome(null); setRevealed(false); setWitnesses([]);
  }

  function submit(answer) {
    if (settled.current || phase !== 'play' || interactionLocked) return;
    settled.current = true;
    const evaluated = assess(round, answer);
    setOutcome(evaluated);
    setResults(old => [...old, evaluated]);
    if (template === 'blind' && evaluated.correct) {
      writeLocal('fraud.collection', [...new Set([...readLocal('fraud.collection', []), round.caseId])]);
    }
  }

  function choose(id) {
    if (outcome || interactionLocked) return;
    if (round.kind === 'choice') { setSelection([id]); submit(id); return; }
    if (selection.includes(id)) setSelection(selection.filter(item => item !== id));
    else if (selection.length < round.expected.length) setSelection([...selection, id]);
  }

  function next() {
    if (!outcome) return;
    window.speechSynthesis?.cancel();
    if (turn + 1 === game.rounds.length) {
      setPhase('ending');
      record('complete', reportFor(results));
    } else {
      setTurn(turn + 1);
      resetTurn();
    }
  }

  function togglePresent() {
    setPresent(!present);
    if (!present && document.documentElement.requestFullscreen) void document.documentElement.requestFullscreen().catch(() => {});
    else if (present && document.fullscreenElement) void document.exitFullscreen().catch(() => {});
  }

  useEffect(() => {
    const reset = () => { if (!document.fullscreenElement) setPresent(false); };
    document.addEventListener('fullscreenchange', reset);
    return () => { document.removeEventListener('fullscreenchange', reset); window.speechSynthesis?.cancel(); };
  }, []);

  useEffect(() => {
    if (phase === 'play') turnStart.current?.focus({ preventScroll: true });
    if (phase !== 'intro') window.scrollTo({ top: 0, behavior: 'instant' });
  }, [phase, turn]);

  useEffect(() => {
    if (phase !== 'play' || outcome || interactionLocked || !round.timeLimit) return;
    const end = performance.now() + round.timeLimit * 1000;
    setTime(round.timeLimit);
    const interval = setInterval(() => {
      const left = Math.max(0, Math.ceil((end - performance.now()) / 1000));
      setTime(left);
      if (!left) { clearInterval(interval); submit([]); }
    }, 150);
    return () => clearInterval(interval);
  }, [phase, turn, outcome, interactionLocked]);

  useEffect(() => {
    function keyboard(event) {
      if (event.repeat || event.ctrlKey || event.metaKey || event.altKey ||
          event.target?.closest?.('input, textarea, select, [contenteditable]')) return;
      if (event.key === 'Enter') {
        if (event.target?.closest?.('a, summary, button:not(.answer-option)')) return;
        if (phase === 'intro') { event.preventDefault(); start(); }
        else if (phase === 'play' && outcome) { event.preventDefault(); next(); }
        else if (phase === 'play' && !interactionLocked && round.kind !== 'choice' && selection.length === round.expected.length) {
          event.preventDefault(); submit(selection);
        }
      } else if (phase === 'play' && !outcome && /^[1-5]$/.test(event.key)) {
        const option = round.choices[Number(event.key) - 1];
        if (option) { event.preventDefault(); choose(option.id); }
      } else if (event.key === 'Escape' && present && !document.fullscreenElement) setPresent(false);
    }
    window.addEventListener('keydown', keyboard);
    return () => window.removeEventListener('keydown', keyboard);
  });

  function speak() {
    if (!window.speechSynthesis || !window.SpeechSynthesisUtterance) { notify('当前设备可直接阅读通话字幕。'); return; }
    window.speechSynthesis.cancel();
    const utterance = new SpeechSynthesisUtterance(round.context);
    utterance.lang = 'zh-CN';
    window.speechSynthesis.speak(utterance);
  }

  return <div className={`play-page ${present ? 'projection' : ''}`}>
    <div className="container">
      <div className="game-topbar"><Button variant="plain" icon="arrow-left" href={backHref}>返回工坊</Button>
        <div className="button-row"><Chip color={game.template.color}>{game.template.name}</Chip>
          <Button variant="outline" icon="monitor" onClick={togglePresent}>{present ? '退出投屏' : '投屏模式'}</Button></div>
      </div>
      {phase === 'intro' && <section className="panel play-intro">
        <Rules game={game} />
        <div className="play-intro-actions"><Button variant="lime" icon="play" end="arrow" onClick={start}>开始挑战</Button>
          <span>投屏时可用数字键作答，回车继续。</span></div>
      </section>}
      {phase === 'play' && <>
        <div className="game-progress-row" ref={turnStart} tabIndex={-1} aria-label={`第 ${turn + 1} 关 ${round.title}`}>
          <div><strong>第 {turn + 1} / {game.rounds.length} 关</strong><span>{round.title}</span></div>
          <div className="progress-track" role="progressbar" aria-label="游戏进度" aria-valuenow={progress}><div style={{ width: `${progress}%` }} /></div>
          {template === 'quiz' ? <strong className={`game-timer ${time < 5 ? 'urgent' : ''}`}><Icon name="timer" size={20} />{outcome ? '已作答' : `${time} 秒`}</strong>
            : <strong className="game-counter"><Icon name="shield" size={18} />答对 {results.filter(result => result.correct).length}</strong>}
        </div>
        <div className="game-grid">
          <EvidenceBoard round={round} style={template} revealed={revealed} reveal={() => setRevealed(true)}
            witnesses={witnesses} toggleWitness={index => setWitnesses(old => old.includes(index) ? old : [...old, index])}
            last={results[turn - 1]} turn={turn} speech={speak} />
          <section className="panel answer-panel" aria-labelledby="game-question">
            <h1 id="game-question">{round.question}</h1>
            {interactionLocked ? <div className="interaction-lock" role="status"><Icon name={game.template.icon} size={27} />
              <p>{template === 'roles' ? '打开左侧三份证词后作答。' : template === 'call' ? '接听来电后选择应对。'
                : template === 'blind' ? '翻开封套后作答。' : '先暂停模拟支付，再决定如何核实。'}</p></div>
              : <>
                {round.kind !== 'choice' && <p className="field-note">{round.kind === 'order' ? '按顺序选 3 条，可再次点击取消。'
                  : '选两条。选中后可再次点击取消。'}</p>}
                <AnswerOptions round={round} selection={selection} choose={choose} outcome={outcome} disabled={false} />
              </>}
            {!outcome && !interactionLocked && round.kind !== 'choice' && <div className="answer-actions">
              <span>已选 {selection.length}/{round.expected.length}</span>
              <Button disabled={selection.length !== round.expected.length} onClick={() => submit(selection)} end="arrow">确认答案</Button></div>}
            {outcome && <div className={`answer-feedback ${outcome.correct ? 'success' : 'warning'}`} role="status">
              <div><Icon name={outcome.correct ? 'check' : 'help'} size={23} /><strong>{outcome.correct ? '判断正确' : outcome.partial ? '你找到了一处关键线索' : '这里值得再想一步'}</strong></div>
              <p>{outcome.feedback}</p>
              <Button variant={outcome.correct ? 'primary' : 'outline'} end="arrow" onClick={next}>
                {turn + 1 === game.rounds.length ? '查看结案报告' : '下一关'}</Button>
            </div>}
          </section>
        </div>
      </>}
      {phase === 'ending' && <Ending game={game} results={results} restart={start} guideHref={guideHref} notify={notify} />}
    </div>
  </div>;
}
