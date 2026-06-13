import { useEffect, useState } from 'react';
import { issuesApi, solutionsApi } from '../api/client';
import type { FeedbackResult, KnownIssue, Solution } from '../types';

// ── Solution form (add / edit) ───────────────────────────────────────────────
function SolutionForm({
  issueId, initial, onSaved, onCancel
}: {
  issueId: number;
  initial?: Solution;
  onSaved: (s: Solution) => void;
  onCancel: () => void;
}) {
  const [title, setTitle]   = useState(initial?.title ?? '');
  const [steps, setSteps]   = useState(initial?.steps ?? '');
  const [refs,  setRefs]    = useState(initial?.references ?? '');
  const [saving, setSaving] = useState(false);

  const save = async () => {
    if (!title.trim() || !steps.trim()) return;
    setSaving(true);
    try {
      const data = { title, steps, references: refs || undefined };
      const result = initial
        ? await solutionsApi.update(issueId, initial.id, data)
        : await solutionsApi.create(issueId, data);
      onSaved(result);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div style={{ background: '#f0fdf4', border: '1px solid #86efac', borderRadius: 8, padding: 16, marginTop: 10 }}>
      <div style={{ marginBottom: 8 }}>
        <label style={labelStyle}>Title</label>
        <input value={title} onChange={e => setTitle(e.target.value)} style={inputStyle} placeholder="Solution title" />
      </div>
      <div style={{ marginBottom: 8 }}>
        <label style={labelStyle}>Steps</label>
        <textarea value={steps} onChange={e => setSteps(e.target.value)}
          rows={5} style={{ ...inputStyle, fontFamily: 'monospace', fontSize: 12, resize: 'vertical' }}
          placeholder="1. Step one&#10;2. Step two" />
      </div>
      <div style={{ marginBottom: 12 }}>
        <label style={labelStyle}>References (optional)</label>
        <input value={refs} onChange={e => setRefs(e.target.value)} style={inputStyle} placeholder="Link, doc name, SAP note…" />
      </div>
      <div style={{ display: 'flex', gap: 8 }}>
        <button onClick={save} disabled={saving} style={primaryBtn}>{saving ? 'Saving…' : initial ? 'Update' : 'Add Solution'}</button>
        <button onClick={onCancel} style={ghostBtn}>Cancel</button>
      </div>
    </div>
  );
}

// ── Single solution card ─────────────────────────────────────────────────────
function SolutionCard({
  issue, solution, onChange
}: {
  issue: KnownIssue;
  solution: Solution;
  onChange: (updated: Solution | null) => void;
}) {
  const [editing, setEditing]           = useState(false);
  const [upvotes, setUpvotes]           = useState(solution.upvotes);
  const [feedbackGiven, setFeedbackGiven] = useState<'worked' | 'not-worked' | null>(null);
  const [feedbackResult, setFeedbackResult] = useState<FeedbackResult | null>(null);
  const [feedbackLoading, setFeedbackLoading] = useState(false);
  const [needsReview, setNeedsReview]   = useState(solution.needsReview ?? false);

  const handleDelete = async () => {
    if (!confirm(`Delete "${solution.title}"?`)) return;
    await solutionsApi.delete(issue.id, solution.id);
    onChange(null);
  };

  const handleUpvote = async () => {
    const res = await solutionsApi.upvote(issue.id, solution.id);
    setUpvotes(res.upvotes);
  };

  const handleFeedback = async (worked: boolean) => {
    if (feedbackGiven || feedbackLoading) return;
    setFeedbackLoading(true);
    try {
      const res = await solutionsApi.feedback(issue.id, solution.id, { worked });
      setFeedbackResult(res);
      setFeedbackGiven(worked ? 'worked' : 'not-worked');
      setUpvotes(res.upvotes);
      setNeedsReview(res.needsReview);
    } finally {
      setFeedbackLoading(false);
    }
  };

  if (editing) return (
    <SolutionForm
      issueId={issue.id}
      initial={solution}
      onSaved={updated => { onChange(updated); setEditing(false); }}
      onCancel={() => setEditing(false)}
    />
  );

  return (
    <div style={{ background: '#f8fafc', borderRadius: 8, padding: 14, marginBottom: 10, border: `1px solid ${needsReview ? '#fca5a5' : '#e2e8f0'}` }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 8 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
          <strong style={{ fontSize: 14 }}>{solution.title}</strong>
          {needsReview && (
            <span style={{ fontSize: 10, fontWeight: 700, background: '#fef2f2', color: '#b91c1c', border: '1px solid #fca5a5', borderRadius: 4, padding: '1px 6px' }}>
              ⚠ Needs Review
            </span>
          )}
        </div>
        <div style={{ display: 'flex', gap: 6, alignItems: 'center', flexShrink: 0, marginLeft: 12 }}>
          <button onClick={handleUpvote} style={{ ...ghostBtn, fontSize: 12 }}>▲ {upvotes}</button>
          <button onClick={() => setEditing(true)} style={{ ...ghostBtn, fontSize: 12 }}>Edit</button>
          <button onClick={handleDelete} style={{ ...dangerBtn, fontSize: 12 }}>Delete</button>
        </div>
      </div>

      {/* Steps */}
      <pre style={{ fontSize: 12, whiteSpace: 'pre-wrap', color: '#334155', lineHeight: 1.6, margin: 0 }}>{solution.steps}</pre>
      {solution.references && (
        <p style={{ fontSize: 11, color: '#94a3b8', marginTop: 6, marginBottom: 0 }}>Ref: {solution.references}</p>
      )}

      {/* Feedback row */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginTop: 12, paddingTop: 10, borderTop: '1px solid #f1f5f9' }}>
        {feedbackGiven ? (
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12 }}>
            <span style={{ color: feedbackGiven === 'worked' ? '#16a34a' : '#b91c1c', fontWeight: 600 }}>
              {feedbackGiven === 'worked' ? '✓ Marked as worked' : '✗ Marked as not worked'}
            </span>
            {feedbackResult && (
              <span style={{ color: '#94a3b8' }}>
                · {feedbackResult.workedCount} worked, {feedbackResult.failedCount} failed
              </span>
            )}
            {needsReview && (
              <span style={{ color: '#b91c1c', fontSize: 11 }}>— flagged for review</span>
            )}
          </div>
        ) : (
          <>
            <span style={{ fontSize: 11, color: '#94a3b8' }}>Did this work?</span>
            <button
              onClick={() => handleFeedback(true)}
              disabled={feedbackLoading}
              style={{ padding: '3px 10px', borderRadius: 5, border: '1px solid #86efac', background: '#f0fdf4', color: '#16a34a', cursor: 'pointer', fontSize: 12, fontWeight: 600, opacity: feedbackLoading ? 0.5 : 1 }}
            >
              ✓ Worked
            </button>
            <button
              onClick={() => handleFeedback(false)}
              disabled={feedbackLoading}
              style={{ padding: '3px 10px', borderRadius: 5, border: '1px solid #fca5a5', background: '#fef2f2', color: '#dc2626', cursor: 'pointer', fontSize: 12, fontWeight: 600, opacity: feedbackLoading ? 0.5 : 1 }}
            >
              ✗ Didn't work
            </button>
          </>
        )}
      </div>
    </div>
  );
}

// ── Main page ────────────────────────────────────────────────────────────────
export default function KnownIssuesPage() {
  const [issues, setIssues]           = useState<KnownIssue[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<KnownIssue[] | null>(null);
  const [expanded, setExpanded]       = useState<number | null>(null);
  const [addingTo, setAddingTo]       = useState<number | null>(null);
  const [loading, setLoading]         = useState(true);
  const [searching, setSearching]     = useState(false);

  useEffect(() => {
    issuesApi.getAll().then(setIssues).finally(() => setLoading(false));
  }, []);

  const handleSearch = async () => {
    if (!searchQuery.trim()) { setSearchResults(null); return; }
    setSearching(true);
    try { setSearchResults(await issuesApi.search(searchQuery)); }
    finally { setSearching(false); }
  };

  const updateSolutionInState = (issueId: number, solutionId: number, updated: Solution | null) => {
    const update = (list: KnownIssue[]) => list.map(i => {
      if (i.id !== issueId) return i;
      const solutions = updated
        ? i.solutions.map(s => s.id === solutionId ? updated : s)
        : i.solutions.filter(s => s.id !== solutionId);
      return { ...i, solutions };
    });
    setIssues(update);
    if (searchResults) setSearchResults(update(searchResults));
  };

  const addSolutionInState = (issueId: number, solution: Solution) => {
    const update = (list: KnownIssue[]) => list.map(i =>
      i.id === issueId ? { ...i, solutions: [...i.solutions, solution] } : i
    );
    setIssues(update);
    if (searchResults) setSearchResults(update(searchResults));
    setAddingTo(null);
  };

  const displayed = searchResults ?? issues;

  return (
    <div>
      <h1 style={{ fontSize: 28, fontWeight: 700, marginBottom: 24 }}>Known Issues</h1>

      <div style={{ display: 'flex', gap: 12, marginBottom: 24 }}>
        <input
          placeholder="Paste an error message to find similar issues (semantic search)…"
          value={searchQuery}
          onChange={e => { setSearchQuery(e.target.value); if (!e.target.value) setSearchResults(null); }}
          onKeyDown={e => e.key === 'Enter' && handleSearch()}
          style={{ flex: 1, padding: '10px 14px', borderRadius: 8, border: '1px solid #e2e8f0', fontSize: 14 }}
        />
        <button onClick={handleSearch} disabled={searching} style={primaryBtn}>
          {searching ? 'Searching…' : 'Find Similar'}
        </button>
        {searchResults && (
          <button onClick={() => { setSearchResults(null); setSearchQuery(''); }} style={ghostBtn}>Clear</button>
        )}
      </div>

      {searchResults && (
        <p style={{ marginBottom: 16, color: '#64748b', fontSize: 13 }}>
          {searchResults.length} similar issue{searchResults.length !== 1 ? 's' : ''} found
        </p>
      )}

      {loading ? <p>Loading…</p> : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          {displayed.map(issue => (
            <div key={issue.id} style={{ background: '#fff', borderRadius: 12, boxShadow: '0 1px 4px rgba(0,0,0,.08)', overflow: 'hidden' }}>

              {/* Header row */}
              <div
                onClick={() => { setExpanded(expanded === issue.id ? null : issue.id); setAddingTo(null); }}
                style={{ padding: '16px 20px', cursor: 'pointer', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}
              >
                <div>
                  <span style={{ fontSize: 11, fontWeight: 700, color: '#3b82f6', background: '#dbeafe', padding: '2px 8px', borderRadius: 4, marginRight: 10 }}>
                    {issue.source}
                  </span>
                  <span style={{ fontWeight: 600, fontSize: 15 }}>{issue.title}</span>
                  <span style={{ marginLeft: 12, fontSize: 12, color: '#94a3b8' }}>
                    {issue.solutions.length} solution{issue.solutions.length !== 1 ? 's' : ''}
                  </span>
                </div>
                <span style={{ color: '#94a3b8', fontSize: 18 }}>{expanded === issue.id ? '▲' : '▼'}</span>
              </div>

              {/* Expanded body */}
              {expanded === issue.id && (
                <div style={{ borderTop: '1px solid #f1f5f9', padding: '16px 20px' }}>
                  <p style={{ color: '#475569', marginBottom: 12 }}>{issue.description}</p>
                  <p style={{ fontSize: 12, color: '#94a3b8', marginBottom: 20 }}>
                    Pattern: <code style={{ background: '#f8fafc', padding: '1px 6px', borderRadius: 3 }}>{issue.errorPattern}</code>
                  </p>

                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
                    <h3 style={{ fontWeight: 600, fontSize: 14, margin: 0 }}>Solutions</h3>
                    <button
                      onClick={() => setAddingTo(addingTo === issue.id ? null : issue.id)}
                      style={primaryBtn}
                    >
                      {addingTo === issue.id ? 'Cancel' : '+ Add Solution'}
                    </button>
                  </div>

                  {addingTo === issue.id && (
                    <SolutionForm
                      issueId={issue.id}
                      onSaved={sol => addSolutionInState(issue.id, sol)}
                      onCancel={() => setAddingTo(null)}
                    />
                  )}

                  {issue.solutions
                    .sort((a, b) => b.upvotes - a.upvotes)
                    .map(sol => (
                      <SolutionCard
                        key={sol.id}
                        issue={issue}
                        solution={sol}
                        onChange={updated => updateSolutionInState(issue.id, sol.id, updated)}
                      />
                    ))}

                  {issue.solutions.length === 0 && addingTo !== issue.id && (
                    <p style={{ color: '#94a3b8', fontSize: 13 }}>No solutions yet — add one above.</p>
                  )}
                </div>
              )}
            </div>
          ))}

          {displayed.length === 0 && (
            <p style={{ textAlign: 'center', color: '#94a3b8', padding: 40 }}>No issues found</p>
          )}
        </div>
      )}
    </div>
  );
}

// ── Shared styles ────────────────────────────────────────────────────────────
const inputStyle: React.CSSProperties = {
  width: '100%', padding: '8px 10px', borderRadius: 6,
  border: '1px solid #e2e8f0', fontSize: 13
};
const labelStyle: React.CSSProperties = {
  display: 'block', fontSize: 12, fontWeight: 600, color: '#64748b', marginBottom: 4
};
const primaryBtn: React.CSSProperties = {
  padding: '7px 14px', background: '#0f172a', color: '#fff',
  border: 'none', borderRadius: 6, cursor: 'pointer', fontWeight: 600, fontSize: 13
};
const ghostBtn: React.CSSProperties = {
  padding: '7px 14px', background: '#f8fafc',
  border: '1px solid #e2e8f0', borderRadius: 6, cursor: 'pointer', fontSize: 13
};
const dangerBtn: React.CSSProperties = {
  padding: '7px 14px', background: '#fff', color: '#dc2626',
  border: '1px solid #fca5a5', borderRadius: 6, cursor: 'pointer', fontSize: 13
};
