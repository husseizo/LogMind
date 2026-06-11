# LogMind — Architecture

## Overview

LogMind is a three-tier application:

```
┌──────────────────────────────────────────────┐
│              React Dashboard (Vite)          │
│  DashboardPage  |  LogsPage  |  IssuesPage   │
└──────────────────────┬───────────────────────┘
                       │ HTTP (Axios → /api proxy)
┌──────────────────────▼───────────────────────┐
│          ASP.NET Core 8 Web API              │
│  LogsController  |  IssuesController         │
└──────┬─────────────────────────┬─────────────┘
       │                         │
┌──────▼──────────┐   ┌──────────▼─────────────┐
│  LogMind.Core   │   │ LogMind.Infrastructure  │
│  Domain models  │   │  EF Core + SQLite       │
│  Interfaces     │   │  LogParserService       │
└─────────────────┘   │  KeywordSearchService   │
                      │  EmbeddingSearchService │
                      │  StubAiExplanationSvc   │
                      └─────────────────────────┘
```

## Projects

### LogMind.Core

Pure domain layer with no external dependencies.

- **Models**: `LogEntry`, `KnownIssue`, `Solution`, `ErrorPattern`
- **Interfaces**: `ILogRepository`, `ISearchService`, `IAiExplanationService`

### LogMind.Infrastructure

- **`LogMindDbContext`** — EF Core DbContext targeting SQLite.  Includes seed data for three known issues (SAP RFC failure, Shopify rate limit, Finance deadlock) and their solutions.
- **`LogRepository`** — implements `ILogRepository`, all DB queries via EF Core LINQ.
- **`LogParserService`** — `BackgroundService` that polls configured log files every 30 s. Uses `FileStream` with `FileShare.ReadWrite` (safe for live files), tracks byte position per file, and supports three common log-line formats via Regex. Multi-line stack traces are captured greedily until the next timestamped line.
- **`KeywordSearchService`** — keyword-based similarity scoring against `KnownIssue.ErrorPattern` pipe-separated patterns.
- **`EmbeddingSearchService`** — stub that delegates to `KeywordSearchService`; replace internals with a vector store query.
- **`StubAiExplanationService`** — returns a formatted plain-text explanation; replace with an LLM call.

### LogMind.API

Thin web host that wires DI and exposes two controllers:

- **`LogsController`** — CRUD + search + stats + per-entry AI explanation.
- **`IssuesController`** — CRUD + similarity search + combined issue/suggestion response.

CORS is pre-configured for `localhost:5173` (Vite dev) and `localhost:3000`.

### LogMind.Dashboard

React 18 SPA, three pages:

| Page | Route | Key Features |
|---|---|---|
| Dashboard | `/` | Bar chart: errors by source; bar chart: volume by level (Recharts) |
| Logs | `/logs` | Paginated table, search/filter by query+source+level, modal with AI explanation |
| Known Issues | `/issues` | Accordion list, semantic search input ("paste an error") |

Vite proxies `/api` to `http://localhost:5000` so no CORS issue during development.

## Database Schema

```
LogEntries       KnownIssues
──────────       ───────────
Id               Id
Timestamp        Title
Level            Description
Source           ErrorPattern
Message          Source
StackTrace       CreatedAt / UpdatedAt
ErrorCode             │
LogFile          Solutions ──── KnownIssueId
IngestedAt             Id
                       Title
ErrorPatterns          Steps
─────────────          References
Id                     Upvotes
Name                   CreatedAt
Pattern
PatternType
Severity
KnownIssueId ──► KnownIssues.Id
```

## Data Flow

```
Log File (disk)
    │
    ▼ every 30s
LogParserService ──► LogEntry rows (SQLite)
                                │
                                ▼
                      LogsController.GetAll / Search
                                │
                                ▼
                      React LogsPage (table)
                                │ click "Explain"
                                ▼
                      StubAiExplanationService
                      (swap → real LLM)
```

## Extension Points

1. **LLM integration** — implement `IAiExplanationService` with Anthropic SDK calls.
2. **Vector search** — implement `EmbeddingSearchService` with a real embedding model + vector DB.
3. **Authentication** — add ASP.NET Core Identity or JWT middleware in `Program.cs`.
4. **Multi-tenant sources** — extend `LogSources` config with credentials and remote file fetching.
