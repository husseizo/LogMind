import React, { useEffect, useState } from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter, Routes, Route, NavLink, useLocation } from 'react-router-dom';
import DashboardPage from './pages/DashboardPage';
import LogsPage from './pages/LogsPage';
import KnownIssuesPage from './pages/KnownIssuesPage';
import UploadPage from './pages/UploadPage';
import SettingsPage from './pages/SettingsPage';
import AlertBanner from './components/AlertBanner';
import { securityApi } from './api/client';
import { auth } from './api/auth';
import { useIsMobile } from './hooks/useIsMobile';
import './index.css';

const NAV_ITEMS = [
  { to: '/', label: 'Dashboard', icon: '◈' },
  { to: '/logs', label: 'Logs', icon: '≡' },
  { to: '/issues', label: 'Issues', icon: '⚠' },
  { to: '/upload', label: 'Upload', icon: '↑' },
  { to: '/settings', label: 'Settings', icon: '⚙' },
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
      setError('API key rejected. Please enter the correct key.');
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
    window.location.reload();
  };

  if (!showPrompt) return <>{children}</>;

  return (
    <>
      {children}
      <div style={{
        position: 'fixed', inset: 0, background: 'rgba(15,23,42,.75)',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        zIndex: 9999, padding: 16,
      }}>
        <div style={{
          background: '#fff', borderRadius: 14, padding: '28px 24px',
          width: '100%', maxWidth: 420, boxShadow: '0 20px 60px rgba(0,0,0,.3)',
          boxSizing: 'border-box',
        }}>
          <div style={{ fontSize: 20, fontWeight: 700, color: '#0f172a', marginBottom: 6 }}>
            API Key Required
          </div>
          <p style={{ fontSize: 13, color: '#64748b', marginBottom: 16 }}>
            Enter the key from{' '}
            <code style={{ background: '#f1f5f9', padding: '1px 5px', borderRadius: 3, fontSize: 11 }}>
              appsettings.json → Security.ApiKey
            </code>
          </p>
          {error && (
            <div style={{ background: '#fef2f2', color: '#b91c1c', borderRadius: 6, padding: '8px 12px', fontSize: 13, marginBottom: 12 }}>
              {error}
            </div>
          )}
          <input
            autoFocus
            type="password"
            placeholder="your-api-key"
            value={input}
            onChange={e => setInput(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleSave()}
            style={{
              width: '100%', padding: '10px 14px', borderRadius: 8,
              border: '1px solid #e2e8f0', fontSize: 14, outline: 'none',
              boxSizing: 'border-box', marginBottom: 12, fontFamily: 'monospace',
            }}
          />
          <button onClick={handleSave} style={{
            width: '100%', padding: '10px 0', borderRadius: 8,
            background: '#0f172a', color: '#fff', border: 'none',
            fontSize: 14, fontWeight: 600, cursor: 'pointer',
          }}>
            Authenticate
          </button>
          {keyRequired && auth.get() && (
            <button onClick={() => setShowPrompt(false)} style={{
              width: '100%', marginTop: 8, padding: '8px 0', borderRadius: 8,
              background: 'none', border: '1px solid #e2e8f0',
              fontSize: 13, cursor: 'pointer', color: '#64748b',
            }}>
              Cancel
            </button>
          )}
        </div>
      </div>
    </>
  );
}

// ── Nav link style helper ─────────────────────────────────────────────────────

function SideNavLink({ to, label, onClick }: { to: string; label: string; onClick?: () => void }) {
  return (
    <NavLink
      to={to}
      end={to === '/'}
      onClick={onClick}
      style={({ isActive }) => ({
        display: 'block', padding: '10px 12px', borderRadius: 6, marginBottom: 4,
        color: isActive ? '#38bdf8' : '#94a3b8',
        background: isActive ? '#1e293b' : 'transparent',
        textDecoration: 'none', fontWeight: isActive ? 600 : 400, fontSize: 15,
      })}
    >
      {label}
    </NavLink>
  );
}

// ── Bottom tab bar for mobile ─────────────────────────────────────────────────

function BottomNav() {
  const location = useLocation();
  return (
    <nav style={{
      position: 'fixed', bottom: 0, left: 0, right: 0, height: 60,
      background: '#0f172a', display: 'flex', borderTop: '1px solid #1e293b',
      zIndex: 500,
    }}>
      {NAV_ITEMS.map(({ to, label, icon }) => {
        const isActive = to === '/'
          ? location.pathname === '/'
          : location.pathname.startsWith(to);
        return (
          <NavLink
            key={to}
            to={to}
            end={to === '/'}
            style={{
              flex: 1, display: 'flex', flexDirection: 'column',
              alignItems: 'center', justifyContent: 'center', gap: 2,
              textDecoration: 'none',
              color: isActive ? '#38bdf8' : '#64748b',
            }}
          >
            <span style={{ fontSize: 18, lineHeight: 1 }}>{icon}</span>
            <span style={{ fontSize: 10, fontWeight: isActive ? 700 : 400 }}>{label}</span>
          </NavLink>
        );
      })}
    </nav>
  );
}

// ── App shell ─────────────────────────────────────────────────────────────────

function AppShell() {
  const isMobile = useIsMobile();
  const [drawerOpen, setDrawerOpen] = useState(false);

  // Close drawer on navigation
  const closeDrawer = () => setDrawerOpen(false);

  if (isMobile) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', minHeight: '100vh', fontFamily: 'system-ui, sans-serif' }}>
        {/* Mobile top header */}
        <header style={{
          background: '#0f172a', height: 52, flexShrink: 0,
          display: 'flex', alignItems: 'center', padding: '0 16px',
          justifyContent: 'space-between', position: 'sticky', top: 0, zIndex: 400,
        }}>
          <span style={{ fontSize: 20, fontWeight: 700, color: '#38bdf8' }}>LogMind</span>
          <button
            onClick={() => setDrawerOpen(true)}
            style={{
              background: 'none', border: 'none', color: '#94a3b8',
              fontSize: 22, cursor: 'pointer', padding: '4px 8px', lineHeight: 1,
            }}
            aria-label="Open menu"
          >
            ☰
          </button>
        </header>

        {/* Drawer overlay */}
        {drawerOpen && (
          <div
            style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,.5)', zIndex: 600 }}
            onClick={closeDrawer}
          >
            <div
              style={{
                position: 'absolute', left: 0, top: 0, bottom: 0, width: 240,
                background: '#0f172a', padding: '24px 16px',
                boxShadow: '4px 0 20px rgba(0,0,0,.3)',
              }}
              onClick={e => e.stopPropagation()}
            >
              <div style={{ fontSize: 20, fontWeight: 700, color: '#38bdf8', marginBottom: 28 }}>LogMind</div>
              {NAV_ITEMS.map(({ to, label }) => (
                <SideNavLink key={to} to={to} label={label} onClick={closeDrawer} />
              ))}
            </div>
          </div>
        )}

        {/* Page content */}
        <main style={{ flex: 1, padding: '16px 12px', background: '#f8fafc', overflowY: 'auto', paddingBottom: 76 }}>
          <AlertBanner />
          <Routes>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/logs" element={<LogsPage />} />
            <Route path="/issues" element={<KnownIssuesPage />} />
            <Route path="/upload" element={<UploadPage />} />
            <Route path="/settings" element={<SettingsPage />} />
          </Routes>
        </main>

        {/* Bottom tab bar */}
        <BottomNav />
      </div>
    );
  }

  // Desktop layout
  return (
    <div style={{ display: 'flex', minHeight: '100vh', fontFamily: 'system-ui, sans-serif' }}>
      <nav style={{ width: 220, background: '#0f172a', color: '#e2e8f0', padding: '24px 16px', flexShrink: 0 }}>
        <div style={{ fontSize: 22, fontWeight: 700, marginBottom: 32, color: '#38bdf8' }}>LogMind</div>
        {NAV_ITEMS.map(({ to, label }) => (
          <SideNavLink key={to} to={to} label={label} />
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
  );
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <BrowserRouter>
      <ApiKeyGate>
        <AppShell />
      </ApiKeyGate>
    </BrowserRouter>
  </React.StrictMode>
);
