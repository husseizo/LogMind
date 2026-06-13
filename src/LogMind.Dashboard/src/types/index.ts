export interface LogEntry {
  id: number;
  timestamp: string;
  level: string;
  source: string;
  message: string;
  stackTrace?: string;
  errorCode?: string;
  logFile: string;
  ingestedAt: string;
}

export interface Solution {
  id: number;
  knownIssueId: number;
  title: string;
  steps: string;
  references?: string;
  upvotes: number;
  needsReview: boolean;
  createdAt: string;
}

export interface FeedbackResult {
  workedCount: number;
  failedCount: number;
  needsReview: boolean;
  upvotes: number;
}

export interface KnownIssue {
  id: number;
  title: string;
  description: string;
  errorPattern: string;
  source: string;
  solutions: Solution[];
  createdAt: string;
  updatedAt: string;
}

export interface Alert {
  id: number;
  ruleName: string;
  source: string;
  pattern: string;
  occurrenceCount: number;
  thresholdCount: number;
  windowMinutes: number;
  sampleMessage: string;
  isAcknowledged: boolean;
  triggeredAt: string;
  acknowledgedAt?: string;
}

export interface StatEntry {
  [key: string]: number;
}
