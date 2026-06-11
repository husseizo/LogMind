import { useEffect, useState, useCallback } from 'react';
import { logsApi } from '../api/client';
import type { LogEntry } from '../types';

const LEVEL_BADGE: Record<string, string> = {
  ERROR: '#fee2e2|#ef4444',
  FATAL: '#fee2e2|#7f1d1d',
  WARN:  '#fef9c3|#d97706',
  INFO:  '#dbeafe|#2563eb',
  DEBUG: '#f1f5f9|#94a3b8',
};

function LevelBadge({ level }: { level: string }) {
  const [bg, color] = (LEVEL_BADGE[level] ?? '#f1f5f9|#64748b').split('|');
  return (
    <span style={{ background: bg, color, borderRadius: 4, padding: '2px 7px', fontSize: 11, fontWeight: 700 }}>
      {level}
    </span>
  );
}

export default function LogsPage() {
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [query, setQuery] = useState('');
  const [source, setSource] = useState('');
  const [level, setLevel] = useState('');
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [selected, setSelected] = useState<LogEntry | null>(null);
  const [explanation, setExplanation] = useState<string | null>(null);

  const fetch = useCallback(() => {
    setLoading(true);
    const req = query
      ? logsApi.search(query, source || undefined, level || undefined)
      : logsApi.getAll(page);
    req.then(setLogs).finally(() => setLoading(false));
  }, [query, source, level, page]);

  useEffect(() => { fetch(); }, [fetch]);

  const handleExplain = async (entry: LogEntry) => {
    setSelected(entry);
    setExplanation(null);
    const res = await logsApi.explain(entry.id);
    setExplanation(res.explanation);
  };

  return (
    <div>
      <h1 style={{ fontSize: 28, fontWeight: 700, marginBottom: 24 }}>Logs</h1>

      <div style={{ display: 'flex', gap: 12, marginBottom: 20, flexWrap: 'wrap' }}>
        <input
          placeholder="Search messages..."
          value={query}
          onChange={e => { setQuery(e.target.value); setPage(1); }}
          style={inputStyle}
        />
        <select value={source} onChange={e => setSource(e.target.value)} style={inputStyle}>
          <option value="">All Sources</option>
          {['SAP', 'Shopify', 'Finance', 'MotoFleet'].map(s => <option key={s}>{s}</option>)}
        </select>
        <select value={level} onChange={e => setLevel(e.target.value)} style={inputStyle}>
          <option value="">All Levels</option>
          {['DEBUG', 'INFO', 'WARN', 'ERROR', 'FATAL'].map(l => <option key={l}>{l}</option>)}
        </select>
      </div>

      {loading ? (
        <p>Loading...</p>
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
              {logs.map(log => (
                <tr key={log.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
                  <td style={tdStyle}>{new Date(log.timestamp).toLocaleString()}</td>
                  <td style={tdStyle}><LevelBadge level={log.level} /></td>
                  <td style={tdStyle}>{log.source}</td>
                  <td style={{ ...tdStyle, maxWidth: 400, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{log.message}</td>
                  <td style={tdStyle}>
                    <button onClick={() => handleExplain(log)} style={btnStyle}>Explain</button>
                  </td>
                </tr>
              ))}
              {logs.length === 0 && (
                <tr><td colSpan={5} style={{ padding: 32, textAlign: 'center', color: '#94a3b8' }}>No logs found</td></tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {!query && (
        <div style={{ display: 'flex', gap: 8, marginTop: 16, alignItems: 'center' }}>
          <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1} style={btnStyle}>Prev</button>
          <span style={{ color: '#64748b', fontSize: 13 }}>Page {page}</span>
          <button onClick={() => setPage(p => p + 1)} disabled={logs.length < 50} style={btnStyle}>Next</button>
        </div>
      )}

      {selected && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 }}
          onClick={() => setSelected(null)}>
          <div style={{ background: '#fff', borderRadius: 12, padding: 32, maxWidth: 600, width: '90%', maxHeight: '80vh', overflowY: 'auto' }}
            onClick={e => e.stopPropagation()}>
            <h2 style={{ marginBottom: 12 }}>{selected.source} — {selected.level}</h2>
            <p style={{ color: '#64748b', fontSize: 12, marginBottom: 12 }}>{new Date(selected.timestamp).toLocaleString()}</p>
            <p style={{ marginBottom: 12 }}>{selected.message}</p>
            {selected.stackTrace && (
              <pre style={{ background: '#f8fafc', padding: 12, borderRadius: 6, fontSize: 11, overflowX: 'auto', marginBottom: 12 }}>{selected.stackTrace}</pre>
            )}
            <h3 style={{ marginBottom: 8 }}>AI Explanation</h3>
            {explanation ? <p style={{ color: '#334155' }}>{explanation}</p> : <p style={{ color: '#94a3b8' }}>Loading...</p>}
            <button onClick={() => setSelected(null)} style={{ ...btnStyle, marginTop: 16 }}>Close</button>
          </div>
        </div>
      )}
    </div>
  );
}

const inputStyle: React.CSSProperties = {
  padding: '8px 12px', borderRadius: 6, border: '1px solid #e2e8f0', fontSize: 13, minWidth: 180
};
const tdStyle: React.CSSProperties = { padding: '10px 14px', verticalAlign: 'middle' };
const btnStyle: React.CSSProperties = {
  padding: '5px 12px', borderRadius: 6, border: '1px solid #e2e8f0',
  background: '#f8fafc', cursor: 'pointer', fontSize: 12
};
