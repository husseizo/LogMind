import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter, Routes, Route, NavLink } from 'react-router-dom';
import DashboardPage from './pages/DashboardPage';
import LogsPage from './pages/LogsPage';
import KnownIssuesPage from './pages/KnownIssuesPage';
import UploadPage from './pages/UploadPage';
import SettingsPage from './pages/SettingsPage';
import AlertBanner from './components/AlertBanner';
import './index.css';

const NAV_ITEMS = [
  { to: '/', label: 'Dashboard' },
  { to: '/logs', label: 'Logs' },
  { to: '/issues', label: 'Known Issues' },
  { to: '/upload', label: 'Upload' },
  { to: '/settings', label: 'Settings' },
];

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <BrowserRouter>
      <div style={{ display: 'flex', minHeight: '100vh', fontFamily: 'system-ui, sans-serif' }}>
        <nav style={{ width: 220, background: '#0f172a', color: '#e2e8f0', padding: '24px 16px', flexShrink: 0 }}>
          <div style={{ fontSize: 22, fontWeight: 700, marginBottom: 32, color: '#38bdf8' }}>LogMind</div>
          {NAV_ITEMS.map(({ to, label }) => (
            <NavLink
              key={to}
              to={to}
              end={to === '/'}
              style={({ isActive }) => ({
                display: 'block',
                padding: '10px 12px',
                borderRadius: 6,
                marginBottom: 4,
                color: isActive ? '#38bdf8' : '#94a3b8',
                background: isActive ? '#1e293b' : 'transparent',
                textDecoration: 'none',
                fontWeight: isActive ? 600 : 400,
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
    </BrowserRouter>
  </React.StrictMode>
);
