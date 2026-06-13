# LogMind

**AI-powered log analysis and operations assistant** — ingests logs from SAP, Odoo, and SQLite cache databases; detects error bursts; and delivers structured 11-section AI diagnoses grounded in operational knowledge, known issue history, and prior explanation cache — all without sending data outside your server.

---

## Table of Contents

- [Architecture](#architecture)
- [AI Explanation Pipeline](#ai-explanation-pipeline)
- [Tech Stack](#tech-stack)
- [Quick Start](#quick-start)
- [Authentication](#authentication)
- [Log Sources](#log-sources)
- [Supported Log Formats](#supported-log-formats)
- [Alert Rules](#alert-rules)
- [Notifications](#notifications)
- [Ollama AI Setup](#ollama-ai-setup)
- [Dashboard Pages](#dashboard-pages)
- [API Reference](#api-reference)
- [Data Flow](#data-flow)
- [Remote Access via ngrok](#remote-access-via-ngrok)
- [Extending LogMind](#extending-logmind)

---

## Architecture

```
┌───────────────────────────────────────────────────────┐
│          React 18 + Vite + TypeScript Dashboard       │
│  Dashboard │ Logs │ Known Issues │ Upload │ Settings  │
│  Mobile responsive — bottom tab bar + hamburger nav   │
└────────────────────────┬──────────────────────────────┘
                         │ HTTP /api  +  X-Api-Key header
                         │ (Vite proxy → :5000 in dev)
┌────────────────────────▼──────────────────────────────┐
│              ASP.NET Core 8 Web API                   │
│  ApiKeyMiddleware → Controllers → Repositories        │
│  Logs │ Issues │ Solutions │ Alerts │ Ollama           │
│  Security │ Upload                                     │
└──────┬────────────────────────────┬────────────────────┘
       │                            │
┌──────▼──────────┐   ┌─────────────▼──────────────────┐
│  LogMind.Core   │   │    LogMind.Infrastructure      │
│  Domain models  │   │  EF Core 8 + SQLite            │
│  Interfaces     │   │  LogParserService (background) │
└─────────────────┘   │  AlertDetectionService         │
                      │  EmbeddingIndexService         │
                      │  ExplanationCacheService       │
                      │  OllamaAiExplanationService    │
                      │  OllamaEmbeddingService        │
                      │  EmailNotificationService      │
                      │  TeamsNotificationService      │
                      └────────────┬───────────────────┘
                                   │
                      ┌────────────▼───────────────────┐
                      │   Ollama  (local LLM server)   │
                      │   llama3          explanations │
                      │   nomic-embed-text    vectors  │
                      └────────────────────────────────┘
```

---

## AI Explanation Pipeline

Every **AI Explain** request passes through a layered pipeline before calling Ollama. The goal: serve cached answers instantly and enrich new calls with the richest possible operational context.

### 3-Tier Explanation Cache

Before calling Ollama, `ExplanationCacheService` checks three tiers in order:

| Tier | Method | Threshold | Speed |
|---|---|---|---|
| **Tier 1** | Exact SHA-256 hash of normalized message | Exact match | ~1 ms |
| **Tier 2** | Token-Jaccard string similarity | ≥ 0.90 | ~5 ms |
| **Tier 3** | Cosine similarity of nomic-embed-text vector | ≥ 0.85 | ~50 ms |

A hit at any tier returns the cached explanation immediately and increments the hit counter. Only a full miss proceeds to the RAG step.

Message normalization strips timestamps, IDs, UUIDs, IP addresses, hex values, and numbers before hashing — so `"Error processing order 12345"` and `"Error processing order 99999"` hash to the same bucket.

### 4-Source RAG Context

On a full cache miss, the following are retrieved **in parallel** and injected into the Ollama prompt:

| Source | What it provides |
|---|---|
| **Similar Known Issues** | Up to 3 matching KnownIssues with ranked solutions |
| **Similar Historical Logs** | Up to 3 past ERROR/FATAL/WARN entries from same source |
| **Previous AI Explanations** | Up to 3 prior cached explanations from same source (pattern recognition) |
| **Operational Knowledge** | Source-scoped business process and integration architecture docs |

### Solution Ranking Formula

Solutions inside KnownIssues are ranked before injection into the prompt:

```
Score = WorkedCount × 10 + Upvotes × 3 − FailedCount × 5
```

Solutions with positive feedback history are always ranked above untested ones. Solutions flagged `NeedsReview` are marked with a warning.

### LogMind AI Persona

The Ollama prompt casts the model as **LogMind AI** — a senior software architect, operations engineer, systems analyst, and troubleshooting specialist who understands this specific environment:

> SAP Business One middleware · Odoo 19 integrations · ASP.NET Core 8 APIs · Python services · SQLite · Windows Server 2022 · background sync workers

Every explanation is returned as exactly **11 structured sections**:

1. **What Happened** — the specific event in one paragraph
2. **Affected Component** — extracted service, class, module, or endpoint
3. **Retrieved Context Used** — which RAG sources were applied
4. **Root Cause** — most probable technical reason with stated assumptions
5. **Recommended Solution** — ranked by feedback history, safety, simplicity
6. **Immediate Fix** — numbered concrete actions
7. **Validation Steps** — how to confirm the fix worked
8. **Prevention** — how to prevent this failure category
9. **Future Improvement** — targeted recommendations for code, monitoring, or alerting
10. **Business / System Impact** — effect on orders, invoices, customers, sync jobs, or reporting
11. **Confidence** — High / Medium / Low with one-sentence reason

### Operational Knowledge Layer

`OperationalKnowledge` documents are source-scoped business process descriptions stored in SQLite. Each document:

- Is embedded via `nomic-embed-text` on first startup (or hourly by `EmbeddingIndexService`)
- Is retrieved by cosine similarity to the current log entry's embedding vector
- Is filtered to only sources listed in its `ApplicableSources` JSON field

**Current seeded document:**

| Title | Systems | Applies to |
|---|---|---|
| SAP, Odoo 19, MolasLubes Cache and Neon Cache Integration Workflow | SAP + Odoo 19 + MolasLubes Cache + Neon Cache | SapOdoo Main, Molaslubes Neon |

The document covers the full integration chain: product sync (`SAP → MolasLubes Cache → Neon Cache → Odoo 19`), customer creation, order creation with UDF/DocEntry writebacks, pick list, invoice, and payment — including common blockers and business impact for each failure mode.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Domain models & interfaces | `LogMind.Core` — .NET 8 class library |
| Data access & background services | `LogMind.Infrastructure` — EF Core 8 + SQLite |
| HTTP API | `LogMind.API` — ASP.NET Core 8 Web API |
| Dashboard | `LogMind.Dashboard` — React 18, Vite, TypeScript, Recharts |
| Local AI | Ollama (`llama3` chat/explanation + `nomic-embed-text` embeddings) |
| Platform | Windows Server 2022 |

### Domain Models

| Model | Purpose |
|---|---|
| `LogEntry` | Single parsed log line — timestamp, level, source, message, stack trace |
| `KnownIssue` | Catalogued error with description and error pattern |
| `Solution` | Fix attached to a `KnownIssue` with steps, references, upvotes, and feedback |
| `SolutionFeedback` | Per-solution worked/failed feedback that influences ranking |
| `Alert` | Fired when an error rule exceeds its threshold |
| `KnownIssueEmbedding` | Float vector for semantic similarity search |
| `AiExplanationCache` | Cached Ollama explanation with hash, embedding, hit count, prompt version |
| `OperationalKnowledge` | Source-scoped business process document with embedding vector |

---

## Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org)
- [Ollama](https://ollama.com)

### 1 — Pull Ollama models (once)

```bash
ollama pull llama3            # AI explanations + chat
ollama pull nomic-embed-text  # semantic vector search + RAG retrieval
```

### 2 — Set your API key

Open `src/LogMind.API/appsettings.json` and change the default key:

```json
"Security": {
  "ApiKey": "your-strong-secret-key"
}
```

### 3 — Start the API

```bash
cd src/LogMind.API
dotnet run
```

- API: `http://localhost:5000`
- Swagger UI: `http://localhost:5000/swagger`
- SQLite database (`logmind.db`) is created and migrated automatically on first run
- OperationalKnowledge seed entries are inserted idempotently on every startup
- Four background services start: log parser, alert detector, embedding indexer, explanation cache

### 4 — Start the Dashboard

```bash
cd src/LogMind.Dashboard
npm install
npm run dev
```

Dashboard: `http://localhost:5173`

On first load, the app will prompt you for your API key. Enter the same key you set in `appsettings.json`. It is stored in `localStorage` and sent as `X-Api-Key` on every request.

---

## Authentication

Every API endpoint (except `GET /api/security/status`) requires the `X-Api-Key` header.

### Backend

```json
// appsettings.json
"Security": {
  "ApiKey": "your-strong-secret-key"
}
```

Set to an empty string `""` to disable authentication entirely.

### Frontend

The dashboard handles auth automatically:

1. On first visit, `GET /api/security/status` is called — if a key is required and none is stored, the API Key prompt modal appears.
2. Enter the key → it is saved to `localStorage` under `lm_api_key`.
3. All subsequent API calls include `X-Api-Key: <your-key>` via an Axios request interceptor.
4. If any call returns `401`, the prompt modal reappears automatically.

You can update or clear the stored key any time from **Settings → API Key**.

---

## Log Sources

All sources are configured in `src/LogMind.API/appsettings.json` and polled every **30 seconds**. Missing files and directories are skipped silently.

### Directory source

```json
"LogSources": [
  {
    "Name": "Sapreplit Autohub",
    "DirectoryPath": "C:\\SAPLogs",
    "FilePattern": "*.log",
    "Recursive": false
  },
  {
    "Name": "SapOdoo Main",
    "DirectoryPath": "C:\\SapOdoo\\Logs",
    "FilePattern": "*.log",
    "Recursive": true
  },
  {
    "Name": "Molaslubes Neon",
    "DirectoryPath": "C:\\Dev\\Sapscrapodoo",
    "FilePattern": "*.log",
    "Recursive": false
  }
]
```

### Single file source

```json
{
  "Name": "MyApp",
  "FilePath": "C:\\Logs\\myapp.log"
}
```

Files locked by another process are opened with `FileShare.ReadWrite` so ingestion continues even while the log writer holds the file open.

CSV files (`*.csv`) are parsed as `timestamp,level,message` rows automatically.

### SQLite cache database source

```json
"CacheDbSources": [
  {
    "Name": "SapReplitProductCache",
    "DbPath": "C:\\CacheDbs\\SapReplit\\productcache.db",
    "Tables": [
      {
        "TableName": "errors",
        "TimestampColumn": "created_at",
        "MessageColumn": "message",
        "LevelColumn": "level"
      }
    ]
  }
]
```

The cache DB reader opens the file read-only, uses `PRAGMA table_info` to verify columns exist, and uses SQLite `rowid` as a cursor so only new rows are ingested — never duplicates.

### Currently configured sources

| Name | Path | Type | Scale |
|---|---|---|---|
| Sapreplit Autohub | `C:\SAPLogs` | Directory (`*.log`) | ~11.4M entries |
| SapOdoo Main | `C:\SapOdoo\Logs` | Directory (`*.log`, recursive) | ~448K entries |
| Molaslubes Neon | `C:\Dev\Sapscrapodoo` | Directory (`*.log`) | ~95K entries |

OperationalKnowledge RAG context is source-scoped: the seeded integration document applies only to **SapOdoo Main** and **Molaslubes Neon** — not Sapreplit Autohub.

---

## Supported Log Formats

The parser auto-detects the format of each line:

| Format | Example |
|---|---|
| Serilog (`+HH:MM` offset) | `2026-06-11 00:01:10.225 +03:00 [INF] MyService Started` |
| ISO 8601 with T | `2026-06-11T10:23:45.123 ERROR Connection refused` |
| Odoo with PID | `2026-06-11 10:23:45,123 12345 ERROR odoo.module: message` |
| Standard | `2026-06-11 10:23:45 ERROR message` |
| Bracket level first | `[ERROR] 2026-06-11 10:23:45 message` |
| US date | `06/11/2026 10:23:45 ERROR message` |
| Bracket timestamp | `[2026-06-11 10:23:45] ERROR message` |
| Pipe / dash delimiter | `2026-06-11 10:23:45 \| ERROR \| message` |
| Level first | `ERROR 2026-06-11 10:23:45 message` |

**Level normalisation** — abbreviated and verbose levels are mapped to the standard set:

| Input | Normalised |
|---|---|
| `INF`, `INFORMATION` | `INFO` |
| `WRN`, `WARNING` | `WARN` |
| `ERR`, `EXCEPTION` | `ERROR` |
| `DBG` | `DEBUG` |
| `FTL`, `FATAL` | `FATAL` |
| `VRB`, `VERBOSE`, `TRACE` | `DEBUG` |
| `CRITICAL` | `FATAL` |

Multi-line stack traces are captured automatically until the next timestamped line.

---

## Alert Rules

`AlertDetectionService` runs every 60 seconds. When error count exceeds the threshold within the time window, an `Alert` row is written and a red dismissible banner appears on every dashboard page.

```json
"AlertRules": [
  {
    "Name": "SAP High Error Rate",
    "Source": "Sapreplit Autohub",
    "Pattern": "",
    "Threshold": 5,
    "WindowMinutes": 10,
    "CooldownMinutes": 30
  },
  {
    "Name": "Any Source Critical Burst",
    "Source": "",
    "Pattern": "",
    "Threshold": 20,
    "WindowMinutes": 5,
    "CooldownMinutes": 60
  }
]
```

| Field | Description |
|---|---|
| `Source` | Restrict to one source, or `""` for all sources |
| `Pattern` | Substring match on the message, or `""` for any error |
| `Threshold` | Number of ERROR/FATAL entries that triggers the alert |
| `WindowMinutes` | Lookback window |
| `CooldownMinutes` | Minimum gap before the same rule fires again |

---

## Notifications

When an alert fires, notifications are sent to all enabled channels.

### Email (SMTP)

```json
"Notifications": {
  "Email": {
    "Enabled": true,
    "SmtpHost": "smtp.example.com",
    "SmtpPort": 587,
    "UseSsl": true,
    "Username": "alerts@example.com",
    "Password": "your-password",
    "From": "logmind@example.com",
    "To": ["team@example.com"]
  }
}
```

### Microsoft Teams (Webhook)

```json
"Notifications": {
  "Teams": {
    "Enabled": true,
    "WebhookUrl": "https://your-tenant.webhook.office.com/webhookb2/..."
  }
}
```

Both channels are independent and can be enabled simultaneously.

---

## Ollama AI Setup

All AI features use your local Ollama — **no data leaves your machine**.

```json
"Ollama": {
  "BaseUrl": "http://localhost:11434",
  "Model": "llama3",
  "EmbeddingModel": "nomic-embed-text",
  "TimeoutSeconds": 120
}
```

| Feature | Model | How it's triggered |
|---|---|---|
| Structured log analysis (11 sections) | `llama3` | Click **AI Explain** on any log entry |
| Multi-turn debugging chat | `llama3` | Type in the chat panel after analysis loads |
| Fix suggestions | `llama3` | `GET /api/issues/for-log/{id}` |
| Log trend summary | `llama3` | `GET /api/issues/for-log/{id}` |
| Semantic issue search | `nomic-embed-text` | Known Issues search box |
| RAG context retrieval | `nomic-embed-text` | On every cache-miss explain call |
| OperationalKnowledge indexing | `nomic-embed-text` | Startup + every 1 hour |
| Background KnownIssue indexing | `nomic-embed-text` | Startup + every 1 hour |

**Graceful degradation** — if Ollama is offline:
- Every endpoint returns a `[Ollama offline]` stub message
- Tier 3 embedding lookup is skipped; Tier 1 and Tier 2 cache still function
- OperationalKnowledge embedding is skipped until Ollama comes back
- Keyword-based search is used as a fallback for Known Issues

You can switch models at runtime from **Settings → AI Explanation Model** without restarting the API.

Alternative models:

```bash
ollama pull mistral          # lighter, fast on CPU
ollama pull deepseek-coder   # better stack trace analysis
ollama pull llama3.1         # improved reasoning
```

---

## Dashboard Pages

### Dashboard `/`

Overview of the last 7 days:
- Bar chart — error count by source
- Bar chart — log volume by level
- Recent alerts summary

### Logs `/logs`

Full log browser with advanced filtering and AI analysis.

**Filter bar:**
- **Time presets** — 1h / 6h / 24h (default) / 7d / 30d / All
- **Quick level buttons** — click ERROR, FATAL, WARN, INFO, or DEBUG to filter
- **Text search** — debounced search across message and stack trace
- **Source dropdown** — filter to a single source
- **Date range pickers** — From / To datetime inputs for precise ranges
- **Page size** — 25 / 50 / 100 / 200 rows per page
- **Active filter pills** — shows what's active with individual × remove buttons

**Pagination:** cursor-based (keyset) — constant speed regardless of depth into 11M+ records. No expensive `COUNT(*)` queries.

**AI Explain panels** (floating, Gmail-style):
- Clicking **AI Explain** opens a floating chat panel at the bottom of the screen
- Multiple panels can be open simultaneously side-by-side (desktop) or as a tab strip (mobile)
- Each panel shows the **11-section LogMind AI analysis**
- **Chat** — type follow-up questions after analysis loads; Ollama holds the full log entry as a system message throughout the conversation
- **Minimize / Restore** — collapse to a 44px tab with message count badge
- Close with ×

### Known Issues `/issues`

Catalogue of recurring problems and their solutions.

- Semantic search — paste any error message to find similar known issues
- Add / edit / delete solutions inline
- Upvote solutions
- Leave worked/failed feedback — influences the ranking formula (`WorkedCount×10 + Upvotes×3 − FailedCount×5`) used in future AI explanations
- Falls back to keyword search if Ollama embeddings are unavailable

### Upload `/upload`

Drag-and-drop or browse to upload a log file manually. Select the source name, click Upload. Entries are parsed and inserted immediately using the same auto-detect format logic as the background poller.

### Settings `/settings`

**API Key** — view and update the key stored in your browser.

**AI Explanation Model** — lists all models available in your local Ollama. Click **Use this model** to switch immediately — no API restart needed.

**Embedding Model** — same live-switch for the vector embedding model used by RAG and search.

**Connection** — shows the configured Ollama base URL and timeout.

### Alert Banner (all pages)

Red dismissible banners at the top of every page when unacknowledged alerts exist. Polls every 60 seconds.

---

## API Reference

All endpoints require the `X-Api-Key` header unless the key is unconfigured.

### Security

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/security/status` | Open | Returns `{ keyRequired: bool }` |

### Logs

| Method | Path | Description |
|---|---|---|
| GET | `/api/logs` | Paginated list (`?page&pageSize`) |
| GET | `/api/logs/query` | Cursor-paginated filtered query — see below |
| GET | `/api/logs/search` | Keyword search (`?q&source&level`) |
| GET | `/api/logs/errors` | Recent ERROR/FATAL (`?count=100`) |
| GET | `/api/logs/{id}` | Single entry |
| GET | `/api/logs/{id}/explain` | 11-section LogMind AI analysis (3-tier cache + 4-source RAG) |
| POST | `/api/logs/{id}/chat` | Multi-turn chat (`{ history, question }`) |
| GET | `/api/logs/stats/by-source` | Error counts by source (last 7 days) |
| GET | `/api/logs/stats/by-level` | Log counts by level (last 7 days) |
| GET | `/api/logs/count` | Total count (`?source&level`) |

#### `GET /api/logs/query` parameters

| Param | Type | Description |
|---|---|---|
| `q` | string | Full-text search on message + stack trace |
| `source` | string | Filter to one source |
| `level` | string | Filter to one level (INFO, WARN, ERROR, FATAL, DEBUG) |
| `from` | datetime | Start of time range (ISO 8601) |
| `to` | datetime | End of time range |
| `pageSize` | int | Rows per page (default 50) |
| `cursorTs` | datetime | Timestamp of last item on previous page |
| `cursorId` | int | ID of last item on previous page |

Response: `{ items, hasMore, nextCursorTs, nextCursorId }`

#### `POST /api/logs/{id}/chat` body

```json
{
  "history": [
    { "role": "user",      "content": "What caused this?" },
    { "role": "assistant", "content": "The root cause is..." }
  ],
  "question": "How do I fix it without touching the sync logic?"
}
```

Response: `{ reply: string }`

### Known Issues

| Method | Path | Description |
|---|---|---|
| GET | `/api/issues` | All known issues with solutions |
| GET | `/api/issues/search` | Semantic search (`?q&topK=5`) |
| GET | `/api/issues/for-log/{id}` | Similar issues + AI fix suggestion |

### Solutions

| Method | Path | Description |
|---|---|---|
| POST | `/api/issues/{issueId}/solutions` | Add solution |
| PUT | `/api/issues/{issueId}/solutions/{id}` | Update solution |
| DELETE | `/api/issues/{issueId}/solutions/{id}` | Delete solution |
| POST | `/api/issues/{issueId}/solutions/{id}/upvote` | Upvote |
| POST | `/api/issues/{issueId}/solutions/{id}/feedback` | Record worked/failed feedback |

### Alerts

| Method | Path | Description |
|---|---|---|
| GET | `/api/alerts` | All alerts (paginated) |
| GET | `/api/alerts/active` | Unacknowledged alerts |
| GET | `/api/alerts/count` | Count of unacknowledged |
| POST | `/api/alerts/{id}/acknowledge` | Dismiss |

### Ollama

| Method | Path | Description |
|---|---|---|
| GET | `/api/ollama/models` | Available models in Ollama |
| GET | `/api/ollama/config` | Current model config |
| PUT | `/api/ollama/model` | Switch chat model (`{ model }`) |
| PUT | `/api/ollama/embedding-model` | Switch embedding model (`{ model }`) |

### Upload

| Method | Path | Description |
|---|---|---|
| POST | `/api/upload` | Upload log file (`multipart/form-data`: `file`, `source`) |

---

## Data Flow

```
Log files / directories (*.log, *.csv, *.txt)
SQLite cache databases
          │
          │  every 30s — LogParserService
          │  tracks byte offset per file, rowid per DB table
          │  opens locked files with FileShare.ReadWrite
          ▼
    SQLite: LogEntries
          │
          ├──► AlertDetectionService (every 60s)
          │         counts ERROR/FATAL per source in window
          │         fires Alert → EmailNotificationService
          │                     → TeamsNotificationService
          │         → Dashboard AlertBanner (polls every 60s)
          │
          ├──► GET /api/logs/query (cursor pagination, no COUNT)
          │         → Logs page filter + card/table view
          │
          └──► GET /api/logs/{id}/explain
                    → ExplanationCacheService
                    │
                    ├── Tier 1: exact hash lookup         → hit: return cached
                    ├── Tier 2: token-Jaccard ≥ 0.90     → hit: return cached
                    ├── Tier 3: cosine embedding ≥ 0.85  → hit: return cached
                    │
                    └── Full miss: build RAG context (parallel)
                        ├── FindSimilarIssuesAsync       → KnownIssues + ranked Solutions
                        ├── SearchLogsAsync              → similar historical logs
                        ├── PreviousExplanations         → prior cache entries (same source)
                        └── FindRelevantAsync            → OperationalKnowledge (source-scoped)
                            │
                            └──► OllamaAiExplanationService (llama3)
                                 LogMind AI persona + retrieved context block
                                 → 11-section structured explanation
                                 → stored in AiExplanationCache

          POST /api/logs/{id}/chat
                    → Full log as system message + conversation history
                    → Ollama multi-turn response
                    → Chat bubble in floating panel

KnownIssues (seeded + user-added)
          │
          └──► EmbeddingIndexService (startup + every 1h)
                    → OllamaEmbeddingService (nomic-embed-text)
                    → KnownIssueEmbeddings table

OperationalKnowledge (seeded at startup, idempotent)
          │
          └──► EmbeddingIndexService (startup + every 1h)
                    → OllamaEmbeddingService (nomic-embed-text)
                    → OperationalKnowledge.EmbeddingVector column
                    → Retrieved by cosine similarity + source filter on explain

GET /api/issues/search?q=message
          │
          └──► embed query → cosine similarity
               fallback → KeywordSearchService

POST /api/issues/{issueId}/solutions/{id}/feedback
          │
          └──► SolutionFeedback.Worked (bool)
               influences Score = WorkedCount×10 + Upvotes×3 − FailedCount×5
               re-ranked on every explain call
```

---

## Remote Access via ngrok

To access the dashboard from a mobile device or share it externally:

```bash
ngrok http 5173
```

The Vite config already allows the `molaslubes.ngrok.app` host. For a different ngrok URL, add it to `src/LogMind.Dashboard/vite.config.ts`:

```typescript
server: {
  allowedHosts: ['your-subdomain.ngrok.app'],
  proxy: { '/api': 'http://localhost:5000' }
}
```

The API CORS policy allows any origin so no backend change is needed.

---

## Extending LogMind

### Add a log source

Add an entry to `LogSources` or `CacheDbSources` in `appsettings.json`. No restart needed — the parser picks it up on the next 30-second poll.

### Add an alert rule

Add an entry to `AlertRules` in `appsettings.json` and restart the API.

### Switch the LLM

```bash
ollama pull mistral
```

Go to **Settings → AI Explanation Model → Use this model**. Takes effect immediately.

### Add a known issue

Either seed it in `LogMindDbContext` or POST via Swagger at `/api/issues`. The `EmbeddingIndexService` indexes it within the hour (or immediately on restart).

### Add an OperationalKnowledge document

Add a new entry to `OperationalKnowledgeSeeder.SeedAsync()` in `src/LogMind.API/OperationalKnowledgeSeeder.cs`. Set `ApplicableSources` to a JSON array of source names (must match the source names in `appsettings.json` exactly). The seeder is idempotent — it checks by title before inserting. The embedding is generated automatically by `EmbeddingIndexService` on next startup or within the hour.

### Add a notification channel

Implement `INotificationService`, register it in `Program.cs` with `AddScoped<INotificationService, YourService>()`. `AlertDetectionService` injects `IEnumerable<INotificationService>` and calls all registered implementations.

### Replace SQLite with PostgreSQL

```csharp
// Program.cs
builder.Services.AddDbContext<LogMindDbContext>(opt =>
    opt.UseNpgsql(connectionString));
```

Add `Npgsql.EntityFrameworkCore.PostgreSQL` NuGet package. EF Core migrations are provider-agnostic.
