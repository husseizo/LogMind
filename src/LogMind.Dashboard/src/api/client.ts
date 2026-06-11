import axios from 'axios';
import type { Alert, KnownIssue, LogEntry, Solution, StatEntry } from '../types';

const api = axios.create({ baseURL: '/api' });

export const logsApi = {
  getAll: (page = 1, pageSize = 50) =>
    api.get<LogEntry[]>(`/logs?page=${page}&pageSize=${pageSize}`).then(r => r.data),
  search: (q: string, source?: string, level?: string) =>
    api.get<LogEntry[]>(`/logs/search`, { params: { q, source, level } }).then(r => r.data),
  getErrors: (count = 100) =>
    api.get<LogEntry[]>(`/logs/errors?count=${count}`).then(r => r.data),
  getById: (id: number) =>
    api.get<LogEntry>(`/logs/${id}`).then(r => r.data),
  explain: (id: number) =>
    api.get<{ explanation: string }>(`/logs/${id}/explain`).then(r => r.data),
  statsBySource: () =>
    api.get<StatEntry>('/logs/stats/by-source').then(r => r.data),
  statsByLevel: () =>
    api.get<StatEntry>('/logs/stats/by-level').then(r => r.data),
  count: (source?: string, level?: string) =>
    api.get<{ count: number }>('/logs/count', { params: { source, level } }).then(r => r.data),
};

export const issuesApi = {
  getAll: () =>
    api.get<KnownIssue[]>('/issues').then(r => r.data),
  search: (q: string, topK = 5) =>
    api.get<KnownIssue[]>('/issues/search', { params: { q, topK } }).then(r => r.data),
  forLog: (logId: number) =>
    api.get<{ logEntry: LogEntry; similarIssues: KnownIssue[]; aiSuggestion: string }>(`/issues/for-log/${logId}`).then(r => r.data),
};

export const solutionsApi = {
  create: (issueId: number, data: { title: string; steps: string; references?: string }) =>
    api.post<Solution>(`/issues/${issueId}/solutions`, data).then(r => r.data),
  update: (issueId: number, id: number, data: { title: string; steps: string; references?: string }) =>
    api.put<Solution>(`/issues/${issueId}/solutions/${id}`, data).then(r => r.data),
  delete: (issueId: number, id: number) =>
    api.delete(`/issues/${issueId}/solutions/${id}`),
  upvote: (issueId: number, id: number) =>
    api.post<{ upvotes: number }>(`/issues/${issueId}/solutions/${id}/upvote`).then(r => r.data),
};

export const alertsApi = {
  getActive: () =>
    api.get<Alert[]>('/alerts/active').then(r => r.data),
  getAll: (page = 1) =>
    api.get<Alert[]>(`/alerts?page=${page}`).then(r => r.data),
  getCount: () =>
    api.get<{ count: number }>('/alerts/count').then(r => r.data),
  acknowledge: (id: number) =>
    api.post(`/alerts/${id}/acknowledge`),
};

export const uploadApi = {
  uploadFile: (file: File, source: string) => {
    const fd = new FormData();
    fd.append('file', file);
    fd.append('source', source);
    return api.post<{ count: number; source: string; fileName: string }>('/upload', fd).then(r => r.data);
  },
};

export const ollamaApi = {
  getModels: () =>
    api.get<{ baseUrl: string; models: string[]; error?: string }>('/ollama/models').then(r => r.data),
  getConfig: () =>
    api.get<{ baseUrl: string; model: string; embeddingModel: string; timeoutSeconds: number }>('/ollama/config').then(r => r.data),
  setModel: (model: string) =>
    api.put<{ model: string }>('/ollama/model', { model }).then(r => r.data),
  setEmbeddingModel: (model: string) =>
    api.put<{ embeddingModel: string }>('/ollama/embedding-model', { model }).then(r => r.data),
};
