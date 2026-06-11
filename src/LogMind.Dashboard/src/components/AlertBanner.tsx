import { useEffect, useState, useCallback } from 'react';
import { alertsApi } from '../api/client';
import type { Alert } from '../types';

export default function AlertBanner() {
  const [alerts, setAlerts] = useState<Alert[]>([]);

  const load = useCallback(() =>
    alertsApi.getActive().then(setAlerts).catch(() => {}), []);

  useEffect(() => {
    load();
    const id = setInterval(load, 60_000);
    return () => clearInterval(id);
  }, [load]);

  const acknowledge = async (id: number) => {
    await alertsApi.acknowledge(id);
    setAlerts(a => a.filter(x => x.id !== id));
  };

  if (alerts.length === 0) return null;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginBottom: 20 }}>
      {alerts.map(alert => (
        <div key={alert.id} style={{
          background: '#fef2f2', border: '1px solid #fca5a5',
          borderRadius: 8, padding: '10px 16px',
          display: 'flex', alignItems: 'flex-start', gap: 12
        }}>
          <span style={{ fontSize: 18, lineHeight: 1 }}>🚨</span>
          <div style={{ flex: 1, minWidth: 0 }}>
            <span style={{ fontWeight: 700, color: '#991b1b', fontSize: 13 }}>
              {alert.ruleName}
            </span>
            <span style={{ color: '#dc2626', fontSize: 13, marginLeft: 8 }}>
              [{alert.source}] {alert.occurrenceCount} errors in {alert.windowMinutes}min
            </span>
            <div style={{ color: '#7f1d1d', fontSize: 12, marginTop: 2, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {alert.sampleMessage}
            </div>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexShrink: 0 }}>
            <span style={{ color: '#94a3b8', fontSize: 11 }}>
              {new Date(alert.triggeredAt).toLocaleTimeString()}
            </span>
            <button
              onClick={() => acknowledge(alert.id)}
              style={{
                padding: '3px 10px', fontSize: 11, borderRadius: 5,
                border: '1px solid #fca5a5', background: '#fff',
                cursor: 'pointer', color: '#dc2626', fontWeight: 600
              }}
            >
              Dismiss
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}
