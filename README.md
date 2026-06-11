# LogMind

**AI-powered log analysis and solution assistant** — automatically ingests logs from SAP, Shopify, Finance, MotoFleet, Odoo and Python services; detects recurring error bursts; matches errors to known solutions using semantic vector search; and explains errors in plain English via a local Ollama LLM.

---

## Table of Contents

- [Architecture](#architecture)
- [Full Stack](#full-stack)
- [Quick Start](#quick-start)
- [Log Sources](#log-sources)
- [Alert Rules](#alert-rules)
- [Ollama AI Setup](#ollama-ai-setup)
- [API Reference](#api-reference)
- [Dashboard Pages](#dashboard-pages)
- [Data Flow](#data-flow)
- [Extending LogMind](#extending-logmind)
- [Roadmap](#roadmap)

---

## Architecture

```
┌──────────────────────────────────────────────────────┐
│            React 18 + Vite Dashboard                 │
│  Dashboard │ Logs │ Known Issues │ Alert Banner       │
└─────────────────────┬────────────────────────────────┘
                      │ HTTP /api  (Vite proxy → :5000)
┌─────────────────────▼────────────────────────────────┐
│           ASP.NET Core 8 Web API                     │
│  Logs │ Issues │ Solutions │ Alerts │ Ollama          │
└──────┬──────────────────────────────┬────────────────┘
       │                              │
┌──────▼──────────┐      ┌────────────▼───────────────┐
│  LogMind.Core   │      │   LogMind.Infrastructure   │
│  Domain models  │      │   EF Core + SQLite         │
│  Interfaces     │      │   LogParserService         │
└─────────────────┘      │   AlertDetectionService    │
                         │   EmbeddingIndexService    │
                         │   OllamaAiExplanationSvc   │
                         │   OllamaEmbeddingService   │
                         │   KeywordSearchService     │
                         │   EmbeddingSearchService   │
                         └────────────────────────────┘
                                      │
                         ┌────────────▼────────────────┐
                         │   Ollama (local LLM server) │
                         │   llama3 (explanations)     │
                         │   nomic-embed-text (vectors)│
                         └─────────────────────────────┘
```

---

## Full Stack

| Layer | Project / Technology |
|---|---|
| Domain models & interfaces | `LogMind.Core` (.NET 8 class library) |
| Data access & background services | `LogMind.Infrastructure` (EF Core 8 + SQLite) |
| HTTP API | `LogMind.API` (ASP.NET Core 8 Web API) |
| Dashboard | `LogMind.Dashboard` (React 18 + Vite + TypeScript + Recharts) |
| Local AI | Ollama (`llama3` + `nomic-embed-text`) |

### Domain Models

| Model | Purpose |
|---|---|
| `LogEntry` | A single parsed log line with timestamp, level, source, message, stack trace |
| `KnownIssue` | A catalogued error with description and error pattern |
| `Solution` | A fix attached to a `KnownIssue`, with steps, references and upvotes |
| `ErrorPattern` | A regex or keyword pattern linked to a known issue |
| `Alert` | A fired alert when an error pattern exceeds its threshold |
| `KnownIssueEmbedding` | Float vector for a `KnownIssue` used in semantic search |

---

## Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org)
- [Ollama](https://ollama.com) (for AI features)

### 1 — Pull Ollama models (once)

```bash
ollama pull llama3           # error explanation & fix suggestions
ollama pull nomic-embed-text # semantic vector search
```

### 2 — Start the API

```bash
cd src/LogMind.API
dotnet run
```

- API: `http://localhost:5000`
- Swagger: `http://localhost:5000/swagger`
- SQLite DB (`logmind.db`) is created and migrated automatically on first run
- Three background services start immediately: log parser, alert detection, embedding indexer

### 3 — Start the Dashboard

```bash
cd src/LogMind.Dashboard
npm install
npm run dev
```

Dashboard: `http://localhost:5173`

---

## Log Sources

Configure all log sources in `src/LogMind.API/appsettings.json`.

### Single log file

```json
{
  "Name": "SAP",
  "FilePath": "C:\\SAPLogs\\app.log"
}
```

### Directory (recursive, multiple extensions)

```json
{
  "Name": "SapOdoo",
  "DirectoryPath": "C:\\SapOdoo\\Logs",
  "FilePattern": "*.log",
  "Recursive": true
}
```

```json
{
  "Name": "SapScrapOdoo",
  "DirectoryPath": "C:\\Dev\\Sapscrapodoo",
  "FilePattern": "*.log;*.csv;*.txt",
  "Recursive": true
}
```

CSV files from Python services are parsed as `timestamp,level,message` rows automatically.

### SQLite cache database

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

The cache DB reader opens the file read-only, probes its schema with `PRAGMA table_info` (safe against schema mismatches), and uses `rowid` as a cursor so only new rows are ingested each poll.

### Currently configured sources

| Name | Path | Type |
|---|---|---|
| SAP | `C:\SAPLogs\app.log` | Single file |
| Shopify | `C:\Logs\Shopify\shopify.log` | Single file |
| Finance | `C:\Logs\Finance\finance.log` | Single file |
| MotoFleet | `C:\Logs\MotoFleet\motofleet.log` | Single file |
| SapOdoo | `C:\SapOdoo\Logs` | Directory (`*.log`) |
| SapScrapOdoo | `C:\Dev\Sapscrapodoo` | Directory (`*.log;*.csv;*.txt`) |
| SapReplitProductCache | `C:\CacheDbs\SapReplit\productcache.db` | SQLite DB |

All sources are polled every **30 seconds**. Missing files and directories are skipped silently.

### Supported log line formats

```
2024-01-15T10:23:45 ERROR message
[ERROR] 2024-01-15 10:23:45 message
01/15/2024 10:23:45 ERROR message
```

Multi-line stack traces are captured automatically until the next timestamped line.

---

## Alert Rules

`AlertDetectionService` runs every 60 seconds. When an error count exceeds the threshold within the time window an `Alert` row is written and a red banner appears in the dashboard.

Configure rules in `appsettings.json`:

```json
"AlertRules": [
  {
    "Name": "SAP High Error Rate",
    "Source": "SAP",
    "Pattern": "",
    "Threshold": 5,
    "WindowMinutes": 10,
    "CooldownMinutes": 30
  }
]
```

| Field | Description |
|---|---|
| `Source` | Filter to a specific source, or `""` for all sources |
| `Pattern` | Substring match on the error message, or `""` for any error |
| `Threshold` | Number of errors that triggers the alert |
| `WindowMinutes` | Lookback window in minutes |
| `CooldownMinutes` | Minimum gap between repeated alerts for the same rule |

### Default rules

| Rule | Source | Pattern | Threshold | Window |
|---|---|---|---|---|
| SAP High Error Rate | SAP | any | 5 | 10 min |
| Shopify Sync Failures | Shopify | any | 3 | 5 min |
| Finance DB Errors | Finance | `deadlock` | 2 | 5 min |
| Any Source Critical Burst | all | any | 20 | 5 min |

---

## Ollama AI Setup

All AI features use your local Ollama instance — no external API calls, no data leaves your server.

```json
"Ollama": {
  "BaseUrl": "http://localhost:11434",
  "Model": "llama3",
  "EmbeddingModel": "nomic-embed-text",
  "TimeoutSeconds": 120
}
```

| Feature | Model | Trigger |
|---|---|---|
| Error explanation | `llama3` | Click **Explain** on any log entry |
| Fix suggestion | `llama3` | `GET /api/issues/for-log/{id}` |
| Log trend summary | `llama3` | `GET /api/issues/for-log/{id}` batch |
| Semantic issue search | `nomic-embed-text` | Known Issues search box |
| Background indexing | `nomic-embed-text` | Startup + every 1 hour |

**Graceful degradation** — if Ollama is offline or a model is not yet pulled, every endpoint returns a `[Ollama offline]` message instead of an error. The app remains fully functional with keyword-based search as a fallback.

Alternative models to try:

```bash
ollama pull mistral          # lighter, fast on CPU
ollama pull deepseek-coder   # better for stack trace analysis
```

Then update `"Model"` in `appsettings.json`.

---

## API Reference

### Logs

| Method | Path | Description |
|---|---|---|
| GET | `/api/logs` | Paginated log list (`?page=1&pageSize=50`) |
| GET | `/api/logs/search` | Search (`?q=...&source=...&level=...`) |
| GET | `/api/logs/errors` | Recent ERROR/FATAL entries (`?count=100`) |
| GET | `/api/logs/{id}` | Single log entry |
| GET | `/api/logs/{id}/explain` | AI plain-English explanation |
| GET | `/api/logs/stats/by-source` | Error counts grouped by source (last 7 days) |
| GET | `/api/logs/stats/by-level` | Log counts grouped by level (last 7 days) |
| GET | `/api/logs/count` | Total count (`?source=...&level=...`) |

### Known Issues

| Method | Path | Description |
|---|---|---|
| GET | `/api/issues` | All known issues with solutions |
| GET | `/api/issues/{id}` | Single issue |
| GET | `/api/issues/search` | Semantic similarity search (`?q=...&topK=5`) |
| GET | `/api/issues/for-log/{id}` | Similar issues + AI fix suggestion for a log entry |

### Solutions

| Method | Path | Description |
|---|---|---|
| GET | `/api/issues/{issueId}/solutions` | List solutions for an issue |
| POST | `/api/issues/{issueId}/solutions` | Add a new solution |
| PUT | `/api/issues/{issueId}/solutions/{id}` | Edit a solution |
| DELETE | `/api/issues/{issueId}/solutions/{id}` | Delete a solution |
| POST | `/api/issues/{issueId}/solutions/{id}/upvote` | Upvote a solution |

### Alerts

| Method | Path | Description |
|---|---|---|
| GET | `/api/alerts` | All alerts (paginated) |
| GET | `/api/alerts/active` | Unacknowledged alerts only |
| GET | `/api/alerts/count` | Count of unacknowledged alerts |
| POST | `/api/alerts/{id}/acknowledge` | Dismiss an alert |

### Ollama

| Method | Path | Description |
|---|---|---|
| GET | `/api/ollama/models` | Models available in your Ollama instance |
| GET | `/api/ollama/config` | Currently configured model and URL |

---

## Dashboard Pages

### Dashboard `/`
Bar charts showing errors by source and log volume by level for the last 7 days. Refreshes on load.

### Logs `/logs`
Paginated table of all log entries with source/level filters and full-text search. Click **Explain** on any row to open a modal with an AI-generated explanation of the error and its likely root cause.

### Known Issues `/issues`
Accordion list of all known issues. Each issue shows its description, error pattern, and solutions sorted by upvotes.

- **Semantic search** — paste any error message into the search box to find the most similar known issues using vector cosine similarity
- **Add Solution** — inline form to add a new solution with title, steps, and references
- **Edit / Delete** — modify or remove any solution directly from the UI
- **Upvote** — vote up the most useful solutions so they rise to the top

### Alert Banner (all pages)
Red dismissible banners appear at the top of every page when unacknowledged alerts exist. Polls every 60 seconds. Disappears automatically when all alerts are acknowledged.

---

## Data Flow

```
Log file / directory / SQLite cache DB
         │
         │  every 30s — LogParserService (tracks byte position per file)
         ▼
   SQLite: LogEntries
         │
         ├──► AlertDetectionService (every 60s)
         │         Groups by source + pattern in window
         │         Writes Alert row if count ≥ threshold
         │         → Dashboard AlertBanner (polls every 60s)
         │
         ├──► GET /api/logs → Logs page table
         │
         └──► GET /api/logs/{id}/explain
                   → OllamaAiExplanationService (llama3)
                   → Plain-English explanation in modal

Known Issues (seeded + user-added)
         │
         └──► EmbeddingIndexService (startup + every 1h)
                   → OllamaEmbeddingService (nomic-embed-text)
                   → KnownIssueEmbeddings table (float[] as JSON)

GET /api/issues/search?q=error message
         │
         └──► OllamaEmbeddingService embeds query
              → CosineSimilarity against all stored vectors
              → Falls back to KeywordSearchService if Ollama offline
```

---

## Extending LogMind

### Swap the LLM

1. Pull any model: `ollama pull mistral`
2. Update `"Model": "mistral"` in `appsettings.json`
3. No code changes needed

### Add a new log source

Add an entry to `LogSources` or `CacheDbSources` in `appsettings.json`. The parser picks it up on the next poll without a restart.

### Add a new alert rule

Add an entry to `AlertRules` in `appsettings.json` and restart the API.

### Add a new known issue

Either seed it in `LogMindDbContext.SeedData()` or `POST` it directly via Swagger. The `EmbeddingIndexService` will index it within the hour (or on restart).

### Replace SQLite with PostgreSQL

```csharp
// Program.cs
builder.Services.AddDbContext<LogMindDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```

Add `Npgsql.EntityFrameworkCore.PostgreSQL` NuGet package. Migrations are already compatible.

---

## Roadmap

| Phase | Feature | Status |
|---|---|---|
| 1 | Database models | ✅ Done |
| 1 | Log parser (file, directory, CSV, SQLite DB) | ✅ Done |
| 1 | REST API | ✅ Done |
| 1 | Keyword search + similarity | ✅ Done |
| 1 | React dashboard | ✅ Done |
| 2 | Ollama AI explanations (`llama3`) | ✅ Done |
| 2 | Semantic embeddings (`nomic-embed-text`) | ✅ Done |
| 2 | Recurring error alerts | ✅ Done |
| 2 | Solution notes UI (add / edit / upvote) | ✅ Done |
| 3 | Authentication (JWT / Windows Auth) | Planned |
| 3 | Manual log file upload via UI | Planned |
| 3 | Alert notifications (email / Teams) | Planned |
| 3 | Fine-tuned domain model on SAP/Shopify logs | Planned |
