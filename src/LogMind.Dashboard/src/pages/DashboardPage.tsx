import { useEffect, useState } from 'react';
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend
} from 'recharts';
import { logsApi } from '../api/client';

const LEVEL_COLORS: Record<string, string> = {
  ERROR: '#ef4444',
  FATAL: '#7f1d1d',
  WARN: '#f59e0b',
  INFO: '#3b82f6',
  DEBUG: '#94a3b8',
};

export default function DashboardPage() {
  const [sourceData, setSourceData] = useState<{ name: string; errors: number }[]>([]);
  const [levelData, setLevelData] = useState<{ name: string; count: number }[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([logsApi.statsBySource(), logsApi.statsByLevel()])
      .then(([src, lvl]) => {
        setSourceData(Object.entries(src).map(([name, errors]) => ({ name, errors: errors as number })));
        setLevelData(Object.entries(lvl).map(([name, count]) => ({ name, count: count as number })));
      })
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <p>Loading dashboard...</p>;

  return (
    <div>
      <h1 style={{ fontSize: 28, fontWeight: 700, marginBottom: 8 }}>Dashboard</h1>
      <p style={{ color: '#64748b', marginBottom: 32 }}>Error summary for the last 7 days</p>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 24 }}>
        <Card title="Errors by Source">
          <ResponsiveContainer width="100%" height={280}>
            <BarChart data={sourceData} margin={{ top: 8, right: 16, left: 0, bottom: 8 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
              <XAxis dataKey="name" tick={{ fontSize: 12 }} />
              <YAxis tick={{ fontSize: 12 }} />
              <Tooltip />
              <Bar dataKey="errors" fill="#ef4444" radius={[4, 4, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </Card>

        <Card title="Log Volume by Level">
          <ResponsiveContainer width="100%" height={280}>
            <BarChart data={levelData} margin={{ top: 8, right: 16, left: 0, bottom: 8 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
              <XAxis dataKey="name" tick={{ fontSize: 12 }} />
              <YAxis tick={{ fontSize: 12 }} />
              <Tooltip />
              <Legend />
              {levelData.map(d => (
                <Bar key={d.name} dataKey="count" name={d.name} fill={LEVEL_COLORS[d.name] ?? '#94a3b8'} radius={[4, 4, 0, 0]} />
              ))}
            </BarChart>
          </ResponsiveContainer>
        </Card>
      </div>

      {sourceData.length === 0 && levelData.length === 0 && (
        <div style={{ marginTop: 48, textAlign: 'center', color: '#94a3b8' }}>
          No log data yet — configure log sources and wait for the parser to ingest files.
        </div>
      )}
    </div>
  );
}

function Card({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div style={{ background: '#fff', borderRadius: 12, padding: 24, boxShadow: '0 1px 4px rgba(0,0,0,.08)' }}>
      <h2 style={{ fontSize: 16, fontWeight: 600, marginBottom: 16, color: '#334155' }}>{title}</h2>
      {children}
    </div>
  );
}
