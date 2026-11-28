import React, { useEffect, useRef } from 'react';
import {
  ArrowDown, ArrowLeft, ArrowRight, ArrowUpRight, BadgeCheck, BookOpen, Box, BriefcaseBusiness,
  Check, ChevronDown, ChevronLeft, ChevronRight, CircleHelp, ClipboardList, Copy, Download, ExternalLink,
  FileCheck, FileText, Flag, FolderOpen, Gamepad2, Gift, GitBranch, GraduationCap, Home, Layers3, Link,
  Menu, MessageCircle, MessagesSquare, Monitor, Phone, Play, Plus, QrCode, RotateCcw, ScanLine, Search,
  ShieldCheck, Smartphone, Sparkles, Timer, TriangleAlert, Users, Wallet, X,
} from 'lucide-react';

const icons = {
  arrow: ArrowRight, 'arrow-left': ArrowLeft, 'arrow-down': ArrowDown, external: ArrowUpRight,
  back: ChevronLeft, next: ChevronRight, down: ChevronDown, shield: ShieldCheck, search: Search, bag: FolderOpen,
  badge: BadgeCheck, book: BookOpen, box: Box, briefcase: BriefcaseBusiness, check: Check, help: CircleHelp,
  clipboard: ClipboardList, copy: Copy, download: Download, launch: ExternalLink, filecheck: FileCheck, file: FileText,
  flag: Flag, game: Gamepad2, gift: Gift, branch: GitBranch, graduation: GraduationCap, home: Home, layers: Layers3,
  link: Link, menu: Menu, message: MessageCircle, messages: MessagesSquare, monitor: Monitor, phone: Phone, play: Play,
  plus: Plus, qr: QrCode, repeat: RotateCcw, scan: ScanLine, smartphone: Smartphone, spark: Sparkles,
  timer: Timer, alert: TriangleAlert, users: Users, wallet: Wallet, close: X,
};

export function Icon({ name, size = 18, ...rest }) {
  const Glyph = icons[name] ?? CircleHelp;
  return <Glyph size={size} strokeWidth={2} aria-hidden="true" {...rest} />;
}

export function Button({ children, href, variant = 'primary', icon, end, className = '', ...props }) {
  const Tag = href ? 'a' : 'button';
  return <Tag className={`button button-${variant} ${className}`} {...(href ? { href } : { type: 'button' })} {...props}>
    {icon && <Icon name={icon} />}{children}{end && <Icon name={end} />}
  </Tag>;
}

export function Chip({ children, color = 'paper' }) {
  return <span className={`chip tone-${color}`}>{children}</span>;
}

export function PageHeader({ title, description, icon = 'shield', color = 'sky', actions }) {
  return <section className={`page-header tone-${color}`}>
    <div><h1>{title}</h1><p>{description}</p>{actions}</div>
    <div className="page-emblem"><Icon name={icon} size={66} /></div>
  </section>;
}

export function SectionHeading({ title, description, action }) {
  return <div className="section-heading">
    <div><h2>{title}</h2>{description && <p>{description}</p>}</div>
    {action}
  </div>;
}

export function Empty({ title, description, action, icon = 'bag' }) {
  return <div className="empty-state">
    <div className="blank-icon"><Icon name={icon} size={24} /></div>
    <h3>{title}</h3>{description && <p>{description}</p>}{action}
  </div>;
}

export function Modal({ title, children, close, width = '', label = '对话框' }) {
  const ref = useRef(null);
  useEffect(() => {
    const element = ref.current;
    element.showModal();
    return () => element.close();
  }, []);
  return <dialog className={`modal ${width}`} ref={ref} aria-label={title ?? label}
    onCancel={event => { event.preventDefault(); close(); }}
    onClick={event => { if (event.target === ref.current) close(); }}>
    <div className="modal-content">
      <div className="modal-heading">
        <h2>{title}</h2>
        <button className="icon-button" type="button" aria-label="关闭" onClick={close}><Icon name="close" /></button>
      </div>
      {children}
    </div>
  </dialog>;
}

export async function api(path, body, token) {
  const response = await fetch(`/api/${path}`, {
    method: body === undefined ? 'GET' : 'POST',
    headers: { ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
      ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  const output = await response.json().catch(() => ({ error: '内容服务未启动。' }));
  if (!response.ok) throw Object.assign(new Error(output.error || '暂时无法完成，请重试。'), { status: response.status });
  return output;
}

export function readLocal(key, fallback) {
  try { return JSON.parse(localStorage.getItem(key)) ?? fallback; } catch { return fallback; }
}

export function writeLocal(key, value) {
  try { localStorage.setItem(key, JSON.stringify(value)); } catch { /* Storage can be disabled. */ }
}

export async function copyText(text) {
  if (navigator.clipboard?.writeText && window.isSecureContext) {
    try {
      await navigator.clipboard.writeText(text);
      return;
    } catch { /* Some browsers block clipboard access even on a secure origin. */ }
  }
  const field = Object.assign(document.createElement('textarea'), { value: text });
  const dialog = document.querySelector('dialog[open]');
  (dialog || document.body).append(field);
  field.select();
  const copied = document.execCommand('copy');
  field.remove();
  if (!copied) throw new Error('复制未完成，请选中内容手动复制。');
}

export function downloadText(filename, body, mime = 'text/plain') {
  const link = document.createElement('a');
  link.href = URL.createObjectURL(new Blob([body], { type: `${mime};charset=utf-8` }));
  link.download = filename;
  link.click();
  setTimeout(() => URL.revokeObjectURL(link.href), 1000);
}

export const toRoute = (path, params = {}) => {
  const query = new URLSearchParams(Object.entries(params).filter(([, value]) => value != null && value !== ''));
  return `#/${path}${query.size ? `?${query}` : ''}`;
};

export function prettyDate(value) {
  return new Date(value).toLocaleDateString('zh-CN', { month: 'short', day: 'numeric', year: 'numeric' });
}
