import { useEffect, useState } from 'react';
import { ollamaApi } from '../api/client';

type OllamaConfig = { baseUrl: string; model: string; embeddingModel: string; timeoutSeconds: number };

function ModelCard({
  label,
  current,
  models,
  onSelect,
  loading,
}: {
  label: string;
  current: string;
  models: string[];
  onSelect: (m: string) => Promise<void>;
  loading: boolean;
}) {
  const [pending, setPending] = useState<string | null>(null);

  const handleSelect = async (m: string) => {
    if (m === current || loading) return;
    setPending(m);
    await onSelect(m);
    setPending(null);
  };

  return (
    <div style={{
      background: '#fff', borderRadius: 10, border: '1px solid #e2e8f0',
      padding: '20px 24px', marginBottom: 24,
    }}>
      <div style={{ fontSize: 13, fontWeight: 600, color: '#64748b', textTransform: 'uppercase', letterSpacing: 1, marginBottom: 6 }}>
        {label}
      </div>
      <div style={{ fontSize: 18, fontWeight: 700, color: '#0f172a', marginBottom: 16, fontFamily: 'monospace' }}>
        {current}
      </div>
      {models.length === 0 ? (
        <div style={{ fontSize: 13, color: '#94a3b8' }}>
          No models found. Make sure Ollama is running and has at least one model pulled.
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          {models.map(m => {
            const isActive = m === current;
            const isLoading = pending === m;
            return (
              <div
                key={m}
                style={{
                  display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                  padding: '10px 14px', borderRadius: 7,
                  background: isActive ? '#f0f9ff' : '#f8fafc',
                  border: `1px solid ${isActive ? '#7dd3fc' : '#e2e8f0'}`,
                }}
              >
                <span style={{ fontFamily: 'monospace', fontSize: 14, color: '#1e293b' }}>{m}</span>
                {isActive ? (
                  <span style={{
                    fontSize: 12, fontWeight: 600, color: '#0284c7',
                    background: '#e0f2fe', padding: '2px 10px', borderRadius: 99,
                  }}>Active</span>
                ) : (
                  <button
                    onClick={() => handleSelect(m)}
                    disabled={!!pending || loading}
                    style={{
                      fontSize: 12, fontWeight: 600, padding: '4px 14px', borderRadius: 6,
                      background: '#0f172a', color: '#fff', border: 'none',
                      cursor: pending || loading ? 'not-allowed' : 'pointer',
                      opacity: pending || loading ? 0.5 : 1,
                    }}
                  >
                    {isLoading ? 'Switching…' : 'Use this model'}
                  </button>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

export default function SettingsPage() {
  const [config, setConfig] = useState<OllamaConfig | null>(null);
  const [models, setModels] = useState<string[]>([]);
  const [ollamaError, setOllamaError] = useState<string | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [switchError, setSwitchError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    Promise.all([ollamaApi.getConfig(), ollamaApi.getModels()])
      .then(([cfg, modData]) => {
        setConfig(cfg);
        setModels(modData.models ?? []);
        if (modData.error) setOllamaError(modData.error);
      })
      .catch(e => setLoadError(e instanceof Error ? e.message : 'Failed to load Ollama settings.'));
  }, []);

  const handleSetModel = async (model: string) => {
    setSwitchError(null);
    setLoading(true);
    try {
      const updated = await ollamaApi.setModel(model);
      setConfig(prev => prev ? { ...prev, model: updated.model } : prev);
    } catch (e) {
      setSwitchError(e instanceof Error ? e.message : 'Failed to switch model.');
    } finally {
      setLoading(false);
    }
  };

  const handleSetEmbeddingModel = async (model: string) => {
    setSwitchError(null);
    setLoading(true);
    try {
      const updated = await ollamaApi.setEmbeddingModel(model);
      setConfig(prev => prev ? { ...prev, embeddingModel: updated.embeddingModel } : prev);
    } catch (e) {
      setSwitchError(e instanceof Error ? e.message : 'Failed to switch embedding model.');
    } finally {
      setLoading(false);
    }
  };

  if (loadError) {
    return (
      <div style={{ maxWidth: 680 }}>
        <h1 style={{ fontSize: 26, fontWeight: 700, color: '#0f172a', marginBottom: 16 }}>Settings</h1>
        <div style={{ padding: 16, borderRadius: 8, background: '#fef2f2', color: '#b91c1c', fontSize: 14 }}>
          {loadError}
        </div>
      </div>
    );
  }

  return (
    <div style={{ maxWidth: 680 }}>
      <h1 style={{ fontSize: 26, fontWeight: 700, color: '#0f172a', marginBottom: 4 }}>Settings</h1>
      <p style={{ color: '#64748b', marginBottom: 28 }}>
        Switch which Ollama model is used for AI explanations or embeddings. Changes take effect immediately — no restart needed.
      </p>

      {ollamaError && (
        <div style={{ padding: '12px 16px', borderRadius: 8, background: '#fff7ed', color: '#c2410c', fontSize: 13, marginBottom: 20 }}>
          Ollama is offline or unreachable: {ollamaError}
        </div>
      )}

      {switchError && (
        <div style={{ padding: '12px 16px', borderRadius: 8, background: '#fef2f2', color: '#b91c1c', fontSize: 13, marginBottom: 20 }}>
          {switchError}
        </div>
      )}

      {!config ? (
        <div style={{ color: '#94a3b8', fontSize: 14 }}>Loading…</div>
      ) : (
        <>
          <ModelCard
            label="AI Explanation Model"
            current={config.model}
            models={models}
            onSelect={handleSetModel}
            loading={loading}
          />
          <ModelCard
            label="Embedding Model"
            current={config.embeddingModel}
            models={models}
            onSelect={handleSetEmbeddingModel}
            loading={loading}
          />

          <div style={{
            background: '#fff', borderRadius: 10, border: '1px solid #e2e8f0',
            padding: '16px 24px',
          }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: '#64748b', textTransform: 'uppercase', letterSpacing: 1, marginBottom: 10 }}>
              Connection
            </div>
            <div style={{ fontSize: 13, color: '#334155', display: 'flex', flexDirection: 'column', gap: 4 }}>
              <span><strong>Base URL:</strong> <code>{config.baseUrl}</code></span>
              <span><strong>Timeout:</strong> {config.timeoutSeconds}s</span>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
