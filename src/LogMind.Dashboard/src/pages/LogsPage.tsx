import { useEffect, useState, useCallback, useRef, KeyboardEvent } from 'react';
import { logsApi } from '../api/client';
import type { LogEntry } from '../types';
import type { LogQuery } from '../api/client';

interface ChatMsg { role: 'user' | 'assistant'; content: string; }

interface Panel {
  key: string;
  entry: LogEntry;
  explanation: string | null;
  chatHistory: ChatMsg[];
  chatInput: string;
  chatLoading: boolean;
  minimized: boolean;
}

const LEVEL_BADGE: Record<string, string> = {
  ERROR: '#fee2e2|#ef4444',
  FATAL: '#fee2e2|#7f1d1d',
  WARN:  '#fef9c3|#d97706',
  INFO:  '#dbeafe|#2563eb',
  DEBUG: '#f1f5f9|#94a3b8',
};

const LEVEL_HEADER_BG: Record<string, string> = {
  ERROR: '#fef2f2',
  FATAL: '#450a0a',
  WARN:  '#fffbeb',
  INFO:  '#eff6ff',
  DEBUG: '#f8fafc',
};

const LEVELS = ['DEBUG', 'INFO', 'WARN', 'ERROR', 'FATAL'];
const PAGE_SIZES = [25, 50, 100, 200];
const PANEL_WIDTH = 340;
const PANEL_GAP = 10;

function LevelBadge({ level }: { level: string }) {
  const [bg, color] = (LEVEL_BADGE[level] ?? '#f1f5f9|#64748b').split('|');
  return (
    <span style={{ background: bg, color, borderRadius: 4, padding: '2px 7px', fontSize: 11, fontWeight: 700, flexShrink: 0 }}>
      {level}
    </span>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function LogsPage() {
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [hasMore, setHasMore] = useState(false);
  const [sources, setSources] = useState<string[]>([]);
  const [query, setQuery] = useState('');
  const [source, setSource] = useState('');
  const [level, setLevel] = useState('');
  const [pageSize, setPageSize] = useState(50);
  const [expandedRow, setExpandedRow] = useState<number | null>(null);
  const [panels, setPanels] = useState<Panel[]>([]);
  const [loading, setLoading] = useState(false);
  const [activePreset, setActivePreset] = useState<string>('24h');

  // Cursor stack: null = first page, each entry is cursor for that page
  const [cursorStack, setCursorStack] = useState<Array<{ ts: string; id: number } | null>>([null]);
  const [cursorIdx, setCursorIdx] = useState(0);

  // Controlled date range (set by presets or manual input)
  const [from, setFrom] = useState(() => toLocalInput(new Date(Date.now() - 24 * 3600_000)));
  const [to, setTo] = useState('');

  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  function toLocalInput(d: Date) {
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  const PRESETS = [
    { label: '1h',  ms: 3600_000 },
    { label: '6h',  ms: 6 * 3600_000 },
    { label: '24h', ms: 24 * 3600_000 },
    { label: '7d',  ms: 7 * 86400_000 },
    { label: '30d', ms: 30 * 86400_000 },
    { label: 'All', ms: 0 },
  ] as const;

  const applyPreset = (label: string, ms: number) => {
    setActivePreset(label);
    setFrom(ms > 0 ? toLocalInput(new Date(Date.now() - ms)) : '');
    setTo('');
    resetCursor();
  };

  const resetCursor = () => { setCursorStack([null]); setCursorIdx(0); };

  useEffect(() => {
    logsApi.statsBySource().then(stats => setSources(Object.keys(stats))).catch(() => {});
  }, []);

  const runQuery = useCallback((params: LogQuery) => {
    setLoading(true);
    logsApi.query(params)
      .then(res => {
        setLogs(res.items);
        setHasMore(res.hasMore);
      })
      .catch(() => { setLogs([]); setHasMore(false); })
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    const cursor = cursorStack[cursorIdx];
    const params: LogQuery = {
      q: query || undefined,
      source: source || undefined,
      level: level || undefined,
      from: from || undefined,
      to: to || undefined,
      pageSize,
      cursorTs: cursor?.ts,
      cursorId: cursor?.id,
    };
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => runQuery(params), query ? 350 : 0);
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
  }, [query, source, level, from, to, pageSize, cursorStack, cursorIdx, runQuery]);

  const goNext = () => {
    const last = logs[logs.length - 1];
    if (!last || !hasMore) return;
    const newCursor = { ts: last.timestamp, id: last.id };
    const newStack = [...cursorStack.slice(0, cursorIdx + 1), newCursor];
    setCursorStack(newStack);
    setCursorIdx(cursorIdx + 1);
  };

  const goPrev = () => {
    if (cursorIdx === 0) return;
    setCursorIdx(cursorIdx - 1);
  };

  const goFirst = () => { setCursorIdx(0); };

  const clearFilters = () => {
    setQuery(''); setSource(''); setLevel('');
    setActivePreset('24h');
    setFrom(toLocalInput(new Date(Date.now() - 24 * 3600_000)));
    setTo('');
    resetCursor();
  };

  const hasFilters = query || source || level;
  const currentPage = cursorIdx + 1;

  // ── Panel management ───────────────────────────────────────────────────────

  const updatePanel = useCallback((key: string, patch: Partial<Panel>) => {
    setPanels(prev => prev.map(p => p.key === key ? { ...p, ...patch } : p));
  }, []);

  const handleExplain = (entry: LogEntry) => {
    // If a panel for this entry already exists, just restore it
    const existing = panels.find(p => p.entry.id === entry.id);
    if (existing) {
      updatePanel(existing.key, { minimized: false });
      return;
    }
    const key = `${entry.id}-${Date.now()}`;
    setPanels(prev => [...prev, {
      key, entry, explanation: null,
      chatHistory: [], chatInput: '', chatLoading: false, minimized: false,
    }]);
    logsApi.explain(entry.id)
      .then(res => updatePanel(key, { explanation: res.explanation }))
      .catch(() => updatePanel(key, { explanation: '[Error fetching explanation]' }));
  };

  const closePanel = (key: string) => setPanels(prev => prev.filter(p => p.key !== key));
  const toggleMinimize = (key: string) =>
    setPanels(prev => prev.map(p => p.key === key ? { ...p, minimized: !p.minimized } : p));

  const sendChat = async (panel: Panel) => {
    if (!panel.explanation || !panel.chatInput.trim() || panel.chatLoading) return;
    const question = panel.chatInput.trim();
    const newHistory: ChatMsg[] = [...panel.chatHistory, { role: 'user', content: question }];
    updatePanel(panel.key, { chatInput: '', chatHistory: newHistory, chatLoading: true });
    try {
      const res = await logsApi.chat(
        panel.entry.id,
        panel.chatHistory.map(m => ({ role: m.role, content: m.content })),
        question
      );
      updatePanel(panel.key, {
        chatHistory: [...newHistory, { role: 'assistant', content: res.reply }],
        chatLoading: false,
      });
    } catch {
      updatePanel(panel.key, { chatLoading: false });
    }
  };

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <div style={{ paddingBottom: panels.length > 0 ? 60 : 0 }}>
      {/* Page header */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
        <h1 style={{ fontSize: 28, fontWeight: 700, margin: 0 }}>Logs</h1>
        {panels.length > 0 && (
          <span style={{ fontSize: 12, color: '#64748b', background: '#f1f5f9', borderRadius: 20, padding: '3px 10px' }}>
            {panels.length} panel{panels.length > 1 ? 's' : ''} open
          </span>
        )}
      </div>

      {/* Filter bar */}
      <div style={{ background: '#fff', borderRadius: 10, padding: 16, marginBottom: 12, boxShadow: '0 1px 4px rgba(0,0,0,.06)' }}>

        {/* Row 1: time presets + quick level buttons */}
        <div style={{ display: 'flex', gap: 8, marginBottom: 12, alignItems: 'center', flexWrap: 'wrap' }}>
          <span style={labelStyle}>Time:</span>
          {PRESETS.map(p => (
            <button
              key={p.label}
              onClick={() => applyPreset(p.label, p.ms)}
              style={{
                ...btnStyle,
                background: activePreset === p.label ? '#0f172a' : '#f8fafc',
                color: activePreset === p.label ? '#fff' : '#334155',
                borderColor: activePreset === p.label ? '#0f172a' : '#e2e8f0',
                fontWeight: activePreset === p.label ? 600 : 400,
              }}
            >
              {p.label}
            </button>
          ))}
          <span style={{ ...labelStyle, marginLeft: 12 }}>Level:</span>
          {(['ERROR', 'FATAL', 'WARN', 'INFO', 'DEBUG'] as const).map(l => {
            const [bg, color] = (LEVEL_BADGE[l] ?? '#f1f5f9|#64748b').split('|');
            const active = level === l;
            return (
              <button
                key={l}
                onClick={() => { setLevel(active ? '' : l); resetCursor(); }}
                style={{
                  ...btnStyle,
                  background: active ? bg : '#f8fafc',
                  color: active ? color : '#64748b',
                  borderColor: active ? color : '#e2e8f0',
                  fontWeight: active ? 700 : 400,
                  fontSize: 11,
                }}
              >
                {l}
              </button>
            );
          })}
        </div>

        {/* Row 2: text search + source + date range + page size */}
        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <div style={fieldWrap}>
            <label style={labelStyle}>Search</label>
            <input placeholder="Search messages & stack traces..." value={query}
              onChange={e => { setQuery(e.target.value); resetCursor(); }}
              style={{ ...inputStyle, minWidth: 220 }} />
          </div>
          <div style={fieldWrap}>
            <label style={labelStyle}>Source</label>
            <select value={source} onChange={e => { setSource(e.target.value); resetCursor(); }} style={inputStyle}>
              <option value="">All Sources</option>
              {sources.map(s => <option key={s}>{s}</option>)}
            </select>
          </div>
          <div style={fieldWrap}>
            <label style={labelStyle}>From</label>
            <input type="datetime-local" value={from}
              onChange={e => { setFrom(e.target.value); setActivePreset(''); resetCursor(); }} style={inputStyle} />
          </div>
          <div style={fieldWrap}>
            <label style={labelStyle}>To</label>
            <input type="datetime-local" value={to}
              onChange={e => { setTo(e.target.value); setActivePreset(''); resetCursor(); }} style={inputStyle} />
          </div>
          <div style={fieldWrap}>
            <label style={labelStyle}>Per page</label>
            <select value={pageSize} onChange={e => { setPageSize(Number(e.target.value)); resetCursor(); }} style={{ ...inputStyle, minWidth: 80 }}>
              {PAGE_SIZES.map(n => <option key={n} value={n}>{n}</option>)}
            </select>
          </div>
          {(hasFilters || activePreset === 'All') && (
            <button onClick={clearFilters} style={{ ...btnStyle, alignSelf: 'flex-end', borderColor: '#fca5a5', color: '#ef4444' }}>
              Reset
            </button>
          )}
        </div>

        {/* Active filter pills */}
        {(query || source) && (
          <div style={{ display: 'flex', gap: 6, marginTop: 10, flexWrap: 'wrap' }}>
            {query  && <Pill label={`"${query}"`}        onRemove={() => { setQuery('');  resetCursor(); }} />}
            {source && <Pill label={`Source: ${source}`} onRemove={() => { setSource(''); resetCursor(); }} />}
          </div>
        )}
      </div>

      {/* Log table */}
      {loading ? (
        <div style={{ padding: 40, textAlign: 'center', color: '#94a3b8' }}>Loading...</div>
      ) : (
        <div style={{ background: '#fff', borderRadius: 12, overflow: 'hidden', boxShadow: '0 1px 4px rgba(0,0,0,.08)' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
            <thead style={{ background: '#f8fafc', borderBottom: '1px solid #e2e8f0' }}>
              <tr>
                {['Timestamp', 'Level', 'Source', 'Message', ''].map(h => (
                  <th key={h} style={{ textAlign: 'left', padding: '10px 14px', fontWeight: 600, color: '#64748b' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {logs.map(log => {
                const isOpen = panels.some(p => p.entry.id === log.id);
                return (
                  <>
                    <tr
                      key={log.id}
                      style={{ borderBottom: expandedRow === log.id ? 'none' : '1px solid #f1f5f9', cursor: 'pointer', background: isOpen ? '#f0f9ff' : undefined }}
                      onClick={() => setExpandedRow(expandedRow === log.id ? null : log.id)}
                    >
                      <td style={tdStyle}>{new Date(log.timestamp).toLocaleString()}</td>
                      <td style={tdStyle}><LevelBadge level={log.level} /></td>
                      <td style={tdStyle}>{log.source}</td>
                      <td style={{ ...tdStyle, maxWidth: 480, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{log.message}</td>
                      <td style={{ ...tdStyle, whiteSpace: 'nowrap' }}>
                        <button
                          onClick={e => { e.stopPropagation(); handleExplain(log); }}
                          style={{ ...btnStyle, ...(isOpen ? { borderColor: '#3b82f6', color: '#2563eb', background: '#eff6ff' } : {}) }}
                        >
                          {isOpen ? '↗ Analysing' : 'AI Explain'}
                        </button>
                      </td>
                    </tr>
                    {expandedRow === log.id && (
                      <tr key={`${log.id}-exp`} style={{ borderBottom: '1px solid #f1f5f9', background: '#f8fafc' }}>
                        <td colSpan={5} style={{ padding: '12px 14px' }}>
                          <div style={{ fontSize: 12, color: '#334155', whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
                            <strong>Message:</strong> {log.message}
                            {log.stackTrace && (
                              <>
                                <br /><br />
                                <strong>Stack Trace:</strong>
                                <pre style={{ margin: '6px 0 0', background: '#fff', padding: 10, borderRadius: 6, fontSize: 11, overflowX: 'auto', border: '1px solid #e2e8f0' }}>
                                  {log.stackTrace}
                                </pre>
                              </>
                            )}
                          </div>
                        </td>
                      </tr>
                    )}
                  </>
                );
              })}
              {logs.length === 0 && (
                <tr><td colSpan={5} style={{ padding: 40, textAlign: 'center', color: '#94a3b8' }}>No logs found</td></tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Pagination — cursor-based, no expensive COUNT(*) */}
      <div style={{ display: 'flex', gap: 8, marginTop: 16, alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
          <button onClick={goFirst} disabled={cursorIdx === 0} style={btnStyle}>« First</button>
          <button onClick={goPrev}  disabled={cursorIdx === 0} style={btnStyle}>‹ Prev</button>
          <span style={{ color: '#64748b', fontSize: 13, padding: '0 4px' }}>Page {currentPage}</span>
          <button onClick={goNext}  disabled={!hasMore} style={btnStyle}>Next ›</button>
        </div>
        <span style={{ color: '#94a3b8', fontSize: 12 }}>
          {logs.length === 0 ? 'No results' : `${logs.length} rows${hasMore ? ' (more available)' : ''}`}
        </span>
      </div>

      {/* Floating chat panels */}
      <div style={{
        position: 'fixed', bottom: 0, right: 16, zIndex: 200,
        display: 'flex', flexDirection: 'row-reverse', gap: PANEL_GAP, alignItems: 'flex-end',
        pointerEvents: 'none',
      }}>
        {panels.map(panel => (
          <ChatPanel
            key={panel.key}
            panel={panel}
            onMinimize={() => toggleMinimize(panel.key)}
            onClose={() => closePanel(panel.key)}
            onInputChange={v => updatePanel(panel.key, { chatInput: v })}
            onSend={() => sendChat(panel)}
          />
        ))}
      </div>

      <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
    </div>
  );
}

// ── Floating chat panel ───────────────────────────────────────────────────────

interface ChatPanelProps {
  panel: Panel;
  onMinimize: () => void;
  onClose: () => void;
  onInputChange: (v: string) => void;
  onSend: () => void;
}

function ChatPanel({ panel, onMinimize, onClose, onInputChange, onSend }: ChatPanelProps) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const { entry, explanation, chatHistory, chatInput, chatLoading, minimized } = panel;

  useEffect(() => {
    if (!minimized) scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' });
  }, [chatHistory.length, minimized]);

  const handleKey = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); onSend(); }
  };

  const headerBg = LEVEL_HEADER_BG[entry.level] ?? '#f8fafc';
  const [, levelColor] = (LEVEL_BADGE[entry.level] ?? '#f1f5f9|#64748b').split('|');

  return (
    <div style={{
      width: PANEL_WIDTH,
      background: '#fff',
      borderRadius: minimized ? '10px 10px 0 0' : '12px 12px 0 0',
      boxShadow: '0 -2px 20px rgba(0,0,0,.15)',
      display: 'flex', flexDirection: 'column',
      overflow: 'hidden',
      pointerEvents: 'all',
      transition: 'height 0.2s ease',
      height: minimized ? 44 : 520,
    }}>
      {/* Panel header — always visible, click to toggle minimize */}
      <div
        onClick={onMinimize}
        style={{
          background: headerBg,
          borderBottom: minimized ? 'none' : '1px solid #e2e8f0',
          padding: '0 10px',
          height: 44, flexShrink: 0,
          display: 'flex', alignItems: 'center', gap: 8,
          cursor: 'pointer', userSelect: 'none',
        }}
      >
        <LevelBadge level={entry.level} />
        <span style={{ fontWeight: 600, fontSize: 12, color: '#334155', flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
          {entry.source}
        </span>
        {chatHistory.length > 0 && (
          <span style={{ fontSize: 10, background: levelColor, color: '#fff', borderRadius: 10, padding: '1px 6px', flexShrink: 0 }}>
            {Math.ceil(chatHistory.length / 2)}
          </span>
        )}
        {explanation === null && !minimized && (
          <span style={{ display: 'inline-block', width: 12, height: 12, border: '2px solid #cbd5e1', borderTopColor: '#3b82f6', borderRadius: '50%', animation: 'spin 0.8s linear infinite', flexShrink: 0 }} />
        )}
        <button
          onClick={e => { e.stopPropagation(); onMinimize(); }}
          title={minimized ? 'Restore' : 'Minimize'}
          style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#94a3b8', fontSize: 16, lineHeight: 1, padding: '0 2px', flexShrink: 0 }}
        >
          {minimized ? '▲' : '▼'}
        </button>
        <button
          onClick={e => { e.stopPropagation(); onClose(); }}
          title="Close"
          style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#94a3b8', fontSize: 18, lineHeight: 1, padding: '0 2px', flexShrink: 0 }}
        >
          ×
        </button>
      </div>

      {/* Body — hidden when minimized */}
      {!minimized && (
        <>
          <div ref={scrollRef} style={{ flex: 1, overflowY: 'auto', padding: '12px 14px', display: 'flex', flexDirection: 'column', gap: 10 }}>
            {/* Log message */}
            <div style={{ background: '#f8fafc', borderRadius: 8, padding: '8px 10px', border: '1px solid #e2e8f0' }}>
              <div style={{ fontSize: 10, fontWeight: 700, color: '#94a3b8', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: 4 }}>Log</div>
              <p style={{ fontSize: 12, color: '#334155', margin: 0, wordBreak: 'break-word', lineHeight: 1.5 }}>{entry.message}</p>
              {entry.stackTrace && (
                <pre style={{ marginTop: 6, fontSize: 10, color: '#64748b', whiteSpace: 'pre-wrap', wordBreak: 'break-word', margin: '6px 0 0' }}>
                  {entry.stackTrace.slice(0, 300)}{entry.stackTrace.length > 300 ? '…' : ''}
                </pre>
              )}
            </div>

            {/* AI Analysis */}
            {explanation === null ? (
              <div style={{ fontSize: 12, color: '#94a3b8', textAlign: 'center', padding: 12 }}>Analysing...</div>
            ) : (
              <AnalysisCard text={explanation} compact />
            )}

            {/* Chat messages */}
            {chatHistory.map((msg, i) => (
              <div key={i} style={{
                alignSelf: msg.role === 'user' ? 'flex-end' : 'flex-start',
                maxWidth: '90%',
                background: msg.role === 'user' ? '#2563eb' : '#f1f5f9',
                color: msg.role === 'user' ? '#fff' : '#334155',
                borderRadius: msg.role === 'user' ? '10px 10px 2px 10px' : '10px 10px 10px 2px',
                padding: '8px 12px',
                fontSize: 12,
                lineHeight: 1.5,
                whiteSpace: 'pre-wrap',
                wordBreak: 'break-word',
              }}>
                {msg.content}
              </div>
            ))}
            {chatLoading && (
              <div style={{ alignSelf: 'flex-start', background: '#f1f5f9', borderRadius: '10px 10px 10px 2px', padding: '8px 12px', color: '#94a3b8', fontSize: 12 }}>
                Thinking...
              </div>
            )}
          </div>

          {/* Chat input */}
          <div style={{ borderTop: '1px solid #e2e8f0', padding: '8px 10px', flexShrink: 0, display: 'flex', gap: 8, alignItems: 'flex-end', background: '#fafafa' }}>
            <textarea
              value={chatInput}
              onChange={e => onInputChange(e.target.value)}
              onKeyDown={handleKey}
              placeholder={explanation ? 'Ask a follow-up... (Enter to send)' : 'Waiting for analysis...'}
              disabled={explanation === null || chatLoading}
              rows={2}
              style={{ flex: 1, resize: 'none', padding: '6px 10px', borderRadius: 6, border: '1px solid #e2e8f0', fontSize: 12, fontFamily: 'inherit', outline: 'none', background: explanation ? '#fff' : '#f1f5f9' }}
            />
            <button
              onClick={onSend}
              disabled={explanation === null || !chatInput.trim() || chatLoading}
              style={{ padding: '7px 14px', borderRadius: 6, background: '#2563eb', color: '#fff', border: 'none', cursor: 'pointer', fontSize: 12, fontWeight: 600, opacity: explanation && chatInput.trim() ? 1 : 0.4, flexShrink: 0 }}
            >
              Send
            </button>
          </div>
        </>
      )}
    </div>
  );
}

// ── Analysis card ─────────────────────────────────────────────────────────────

function AnalysisCard({ text, compact = false }: { text: string; compact?: boolean }) {
  const sections = text.split(/\n(?=\*\*)/);
  if (sections.length <= 1) {
    return <p style={{ fontSize: compact ? 12 : 13, color: '#334155', lineHeight: 1.6, whiteSpace: 'pre-wrap', margin: 0 }}>{text}</p>;
  }

  const sectionColors: Record<string, string> = {
    'What happened':      '#eff6ff',
    'Component affected': '#f0fdf4',
    'Root cause':         '#fff7ed',
    'Fix steps':          '#fdf4ff',
    'What NOT to change': '#fef2f2',
  };
  const sectionBorders: Record<string, string> = {
    'What happened':      '#bfdbfe',
    'Component affected': '#bbf7d0',
    'Root cause':         '#fed7aa',
    'Fix steps':          '#e9d5ff',
    'What NOT to change': '#fecaca',
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: compact ? 6 : 10 }}>
      {sections.map((section, i) => {
        const match = section.match(/^\*\*(.+?):\*\*\s*([\s\S]*)/);
        if (!match) return <p key={i} style={{ fontSize: compact ? 12 : 13, color: '#334155', margin: 0 }}>{section}</p>;
        const [, title, body] = match;
        return (
          <div key={i} style={{
            background: sectionColors[title] ?? '#f8fafc',
            border: `1px solid ${sectionBorders[title] ?? '#e2e8f0'}`,
            borderRadius: compact ? 6 : 8,
            padding: compact ? '7px 10px' : '10px 14px',
          }}>
            <div style={{ fontSize: compact ? 9 : 11, fontWeight: 700, color: '#64748b', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: compact ? 2 : 4 }}>{title}</div>
            <div style={{ fontSize: compact ? 11 : 13, color: '#334155', lineHeight: 1.5, whiteSpace: 'pre-wrap' }}>{body.trim()}</div>
          </div>
        );
      })}
    </div>
  );
}

// ── Small components ──────────────────────────────────────────────────────────

function Pill({ label, onRemove }: { label: string; onRemove: () => void }) {
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, background: '#eff6ff', color: '#2563eb', borderRadius: 20, padding: '2px 10px', fontSize: 11, fontWeight: 600 }}>
      {label}
      <button onClick={onRemove} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#60a5fa', fontWeight: 700, fontSize: 13, lineHeight: 1, padding: 0 }}>×</button>
    </span>
  );
}

const fieldWrap: React.CSSProperties = { display: 'flex', flexDirection: 'column', gap: 4 };
const labelStyle: React.CSSProperties = { fontSize: 11, fontWeight: 600, color: '#64748b', textTransform: 'uppercase', letterSpacing: '0.04em' };
const inputStyle: React.CSSProperties = { padding: '8px 12px', borderRadius: 6, border: '1px solid #e2e8f0', fontSize: 13, minWidth: 140 };
const tdStyle: React.CSSProperties = { padding: '10px 14px', verticalAlign: 'middle' };
const btnStyle: React.CSSProperties = { padding: '5px 12px', borderRadius: 6, border: '1px solid #e2e8f0', background: '#f8fafc', cursor: 'pointer', fontSize: 12 };
