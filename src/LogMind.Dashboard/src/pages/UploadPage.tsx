import { useCallback, useRef, useState } from 'react';
import { uploadApi } from '../api/client';

type UploadResult = { count: number; source: string; fileName: string };

export default function UploadPage() {
  const [source, setSource] = useState('Upload');
  const [dragging, setDragging] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [result, setResult] = useState<UploadResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const reset = () => { setResult(null); setError(null); };

  const handleFile = useCallback(async (file: File) => {
    const ext = file.name.split('.').pop()?.toLowerCase() ?? '';
    if (!['log', 'txt', 'csv'].includes(ext)) {
      setError('Only .log, .txt, and .csv files are supported.');
      return;
    }
    reset();
    setUploading(true);
    try {
      const data = await uploadApi.uploadFile(file, source || 'Upload');
      setResult(data);
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : 'Upload failed.';
      setError(msg);
    } finally {
      setUploading(false);
    }
  }, [source]);

  const onDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragging(false);
    const file = e.dataTransfer.files[0];
    if (file) handleFile(file);
  }, [handleFile]);

  const onInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) handleFile(file);
    e.target.value = '';
  };

  return (
    <div style={{ maxWidth: 640 }}>
      <h1 style={{ fontSize: 26, fontWeight: 700, marginBottom: 8, color: '#0f172a' }}>Upload Logs</h1>
      <p style={{ color: '#64748b', marginBottom: 28 }}>
        Drop a <code>.log</code>, <code>.txt</code>, or <code>.csv</code> file to ingest it immediately.
        Entries are parsed in-memory using the same logic as the background poller.
      </p>

      {/* Source label */}
      <label style={{ display: 'block', marginBottom: 16 }}>
        <span style={{ fontSize: 13, fontWeight: 600, color: '#374151', display: 'block', marginBottom: 6 }}>
          Source label
        </span>
        <input
          value={source}
          onChange={e => setSource(e.target.value)}
          placeholder="e.g. SAP, Shopify, Manual"
          style={{
            width: '100%', padding: '8px 12px', borderRadius: 6,
            border: '1px solid #d1d5db', fontSize: 14, boxSizing: 'border-box',
          }}
        />
      </label>

      {/* Drop zone */}
      <div
        onDragOver={e => { e.preventDefault(); setDragging(true); }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
        onClick={() => inputRef.current?.click()}
        style={{
          border: `2px dashed ${dragging ? '#38bdf8' : '#cbd5e1'}`,
          borderRadius: 12,
          padding: '48px 32px',
          textAlign: 'center',
          cursor: 'pointer',
          background: dragging ? '#f0f9ff' : '#f8fafc',
          transition: 'border-color 0.15s, background 0.15s',
          marginBottom: 20,
        }}
      >
        <div style={{ fontSize: 40, marginBottom: 12 }}>📂</div>
        <div style={{ fontSize: 16, fontWeight: 600, color: '#334155', marginBottom: 6 }}>
          {dragging ? 'Drop it!' : 'Drag & drop a log file here'}
        </div>
        <div style={{ fontSize: 13, color: '#94a3b8' }}>or click to browse</div>
        <input
          ref={inputRef}
          type="file"
          accept=".log,.txt,.csv"
          style={{ display: 'none' }}
          onChange={onInputChange}
        />
      </div>

      {/* Status */}
      {uploading && (
        <div style={{ padding: '14px 18px', borderRadius: 8, background: '#eff6ff', color: '#1d4ed8', fontSize: 14 }}>
          Uploading and parsing…
        </div>
      )}

      {error && (
        <div style={{ padding: '14px 18px', borderRadius: 8, background: '#fef2f2', color: '#b91c1c', fontSize: 14 }}>
          {error}
        </div>
      )}

      {result && (
        <div style={{ padding: '18px 20px', borderRadius: 8, background: '#f0fdf4', border: '1px solid #bbf7d0' }}>
          <div style={{ fontSize: 15, fontWeight: 700, color: '#166534', marginBottom: 6 }}>
            Ingested {result.count} {result.count === 1 ? 'entry' : 'entries'}
          </div>
          <div style={{ fontSize: 13, color: '#4b7c5e' }}>
            File: <strong>{result.fileName}</strong> &nbsp;·&nbsp; Source: <strong>{result.source}</strong>
          </div>
          {result.count === 0 && (
            <div style={{ fontSize: 13, color: '#6b7280', marginTop: 8 }}>
              No parseable log lines found. Check the file format matches the expected pattern
              (e.g. <code>[YYYY-MM-DD HH:MM:SS] [LEVEL] message</code>).
            </div>
          )}
        </div>
      )}
    </div>
  );
}
