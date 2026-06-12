import React, { useEffect, useState } from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter, Routes, Route, NavLink } from 'react-router-dom';
import DashboardPage from './pages/DashboardPage';
import LogsPage from './pages/LogsPage';
import KnownIssuesPage from './pages/KnownIssuesPage';
import UploadPage from './pages/UploadPage';
import SettingsPage from './pages/SettingsPage';
import AlertBanner from './components/AlertBanner';
import { securityApi } from './api/client';
import { auth } from './api/auth';
import './index.css';

const NAV_ITEMS = [
  { to: '/', label: 'Dashboard' },
  { to: '/logs', label: 'Logs' },
  { to: '/issues', label: 'Known Issues' },
  { to: '/upload', label: 'Upload' },
  { to: '/settings', label: 'Settings' },
];

// ── API Key Gate ─────────────────────────────────────────────────────────────

function ApiKeyGate({ children }: { children: React.ReactNode }) {
  const [keyRequired, setKeyRequired] = useState(false);
  const [showPrompt, setShowPrompt] = useState(false);
  const [input, setInput] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    securityApi.status().then(({ keyRequired: req }) => {
      setKeyRequired(req);
      if (req && !auth.get()) setShowPrompt(true);
    }).catch(() => {});
  }, []);

  useEffect(() => {
    const handler = () => {
      setError('API key rejected by server. Please enter the correct key.');
      setShowPrompt(true);
    };
    window.addEventListener('lm:unauthorized', handler);
    return () => window.removeEventListener('lm:unauthorized', handler);
  }, []);

  const handleSave = () => {
    const trimmed = input.trim();
    if (!trimmed) { setError('Key cannot be empty.'); return; }
    auth.set(trimmed);
    setShowPrompt(false);
    setInput('');
    setError('');
    window.location.reload(); // reload so all pending requests retry with new key
  };

  if (!showPrompt) return <>{children}</>;

  return (
    <>
      {children}
      <div style={{
        position: 'fixed', inset: 0, background: 'rgba(15,23,42,.7)',
        display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 9999,
      }}>
        <div style={{
          background: '#fff', borderRadius: 14, padding: '36px 40px',
          width: 420, boxShadow: '0 20px 60px rgba(0,0,0,.3)',
        }}>
          <div style={{ fontSize: 22, fontWeight: 700, color: '#0f172a', marginBottom: 6 }}>
            API Key Required
          </div>
          <p style={{ fontSize: 13, color: '#64748b', marginBottom: 20 }}>
            This LogMind instance is protected. Enter the API key from{' '}
            <code style={{ background: '#f1f5f9', padding: '1px 5px', borderRadius: 3, fontSize: 12 }}>
              appsettings.json → Security.ApiKey
            </code>
          </p>
          {error && (
            <div style={{ background: '#fef2f2', color: '#b91c1c', borderRadius: 6, padding: '8px 12px', fontSize: 13, marginBottom: 14 }}>
              {error}
            </div>
          )}
          <input
            autoFocus
            type="password"
            placeholder="lm-your-secret-key"
            value={input}
            onChange={e => setInput(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleSave()}
            style={{
              width: '100%', padding: '10px 14px', borderRadius: 8,
              border: '1px solid #e2e8f0', fontSize: 14, outline: 'none',
              boxSizing: 'border-box', marginBottom: 16, fontFamily: 'monospace',
            }}
          />
          <button
            onClick={handleSave}
            style={{
              width: '100%', padding: '10px 0', borderRadius: 8,
              background: '#0f172a', color: '#fff', border: 'none',
              fontSize: 14, fontWeight: 600, cursor: 'pointer',
            }}
          >
            Authenticate
          </button>
          {keyRequired && auth.get() && (
            <button
              onClick={() => setShowPrompt(false)}
              style={{ width: '100%', marginTop: 8, padding: '8px 0', borderRadius: 8, background: 'none', border: '1px solid #e2e8f0', fontSize: 13, cursor: 'pointer', color: '#64748b' }}
            >
              Cancel
            </button>
          )}
        </div>
      </div>
    </>
  );
}

// ── App shell ────────────────────────────────────────────────────────────────

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <BrowserRouter>
      <ApiKeyGate>
        <div style={{ display: 'flex', minHeight: '100vh', fontFamily: 'system-ui, sans-serif' }}>
          <nav style={{ width: 220, background: '#0f172a', color: '#e2e8f0', padding: '24px 16px', flexShrink: 0 }}>
            <div style={{ fontSize: 22, fontWeight: 700, marginBottom: 32, color: '#38bdf8' }}>LogMind</div>
            {NAV_ITEMS.map(({ to, label }) => (
              <NavLink
                key={to}
                to={to}
                end={to === '/'}
                style={({ isActive }) => ({
                  display: 'block', padding: '10px 12px', borderRadius: 6, marginBottom: 4,
                  color: isActive ? '#38bdf8' : '#94a3b8',
                  background: isActive ? '#1e293b' : 'transparent',
                  textDecoration: 'none', fontWeight: isActive ? 600 : 400,
                })}
              >
                {label}
              </NavLink>
            ))}
          </nav>
          <main style={{ flex: 1, padding: 32, background: '#f8fafc', overflowY: 'auto' }}>
            <AlertBanner />
            <Routes>
              <Route path="/" element={<DashboardPage />} />
              <Route path="/logs" element={<LogsPage />} />
              <Route path="/issues" element={<KnownIssuesPage />} />
              <Route path="/upload" element={<UploadPage />} />
              <Route path="/settings" element={<SettingsPage />} />
            </Routes>
          </main>
        </div>
      </ApiKeyGate>
    </BrowserRouter>
  </React.StrictMode>
);
