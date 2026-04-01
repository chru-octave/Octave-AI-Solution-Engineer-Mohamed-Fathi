# Insurance Submission Extraction System

## Solution Architecture

This solution is a full-stack web application built around two distinct Claude API usage patterns: **structured extraction** (parsing emails into a database) and **agentic tool use** (letting Claude query that database in natural language). Both patterns share the same ASP.NET Core backend and React frontend.

### High-Level Diagram

```
┌──────────────────────────────────────────────────────────────────┐
│                        React Frontend (Vite + TS)                 │
│                                                                    │
│  Dashboard │ Submissions │ Upload │ AI Insights │ Future Work     │
│  MUI 7 · Recharts · React Router · Axios · jsPDF                 │
└─────────────────────────┬────────────────────────────────────────┘
                          │ HTTP / REST  (CORS-whitelisted)
┌─────────────────────────▼────────────────────────────────────────┐
│                  ASP.NET Core 9 Web API                           │
│                                                                    │
│  POST /api/upload      ← ingest .eml files                       │
│  GET  /api/submissions ← list / search / detail                  │
│  GET  /api/analytics   ← dashboard statistics                     │
│  POST /api/chat        ← AI Insights conversational query         │
│                                                                    │
│  Swagger UI at /swagger                                           │
└──────┬─────────────────────────────────────┬──────────────────────┘
       │                                     │
┌──────▼──────────────────┐   ┌─────────────▼────────────────────┐
│    Application Layer     │   │      Infrastructure Layer         │
│                          │   │                                   │
│  EmailParsingService     │   │  AppDbContext  (EF Core 9)        │
│  ClaudeExtractionService │   │  SQLite  (insurance.db)           │
│  SubmissionProcessing    │   │  EF Migrations  (auto-applied)    │
│  ChatService             │   │                                   │
│  DocumentParsingService  │   └───────────────────────────────────┘
└──────┬───────────────────┘
       │
┌──────▼───────────────────┐
│   External Services       │
│  MimeKit  (email parse)   │
│  PdfPig   (PDF text)      │
│  Anthropic Claude API     │
│    · claude-sonnet-4      │
│    · Extraction calls     │
│    · Tool-use chat calls  │
│  Polly    (retry/backoff) │
│  Serilog  (logging)       │
└──────────────────────────┘
```

### Data Flow — Email Ingestion

```
User uploads .eml  →  UploadController
                            │
                    EmailParsingService          (MimeKit)
                    ├── parse headers, body
                    └── extract PDF attachments  (PdfPig)
                            │
                    ClaudeExtractionService
                    ├── build prompt: headers + body + attachment text
                    ├── POST to Anthropic Messages API
                    └── deserialise structured JSON response
                            │
                    SubmissionProcessingService
                    ├── upsert Insured  (matched by company name)
                    ├── upsert Broker   (matched by broker name)
                    ├── insert CoverageLines, Exposures, LossHistory
                    └── set status: Processed | NeedsReview | Failed
                            │
                        SQLite DB
```

### Data Flow — AI Insights (Conversational Query)

```
User types question  →  ChatController  →  ChatService
                                               │
                                    Claude API  (tool-use mode)
                                    ├── receives full conversation history
                                    ├── decides which SQL tool to call
                                    │     · query_submissions
                                    │     · get_submission_detail
                                    │     · get_analytics
                                    │     · execute_sql  (read-only)
                                    ├── tool results injected back as context
                                    └── produces final natural-language answer
                                               │
                                    ChatController  →  React frontend
                                    (returns answer + tool call records
                                     so the UI can show what was queried)
```

### Backend Project Structure

```
InsuranceExtraction.Domain          ← entities & enums (no dependencies)
InsuranceExtraction.Infrastructure  ← EF Core DbContext + migrations
InsuranceExtraction.Application     ← services + interfaces (business logic)
InsuranceExtraction.API             ← controllers, DI wiring, Program.cs
```

Clean Architecture dependency rule is respected: outer layers depend on inner layers, never the reverse.

## Projects

| Project | Role |
|---------|------|
| `InsuranceExtraction.Domain` | Entities, enums — no external dependencies |
| `InsuranceExtraction.Infrastructure` | EF Core DbContext, SQLite, EF migrations |
| `InsuranceExtraction.Application` | Email parsing, Claude extraction, chat, processing services |
| `InsuranceExtraction.API` | ASP.NET Core controllers, DI wiring, Swagger |
| `frontend/` | React 18 + TypeScript + Vite SPA |

## Key Design Decisions

### Data Extraction Strategy
1. **Email body is the primary data source** — most submission data lives in the email text
2. **PDF attachments are supplementary** — used for loss run history
3. **Graceful degradation** — if PDF parsing fails, the system still processes the email body
4. **Confidence scoring** — Claude returns a 0–1 confidence; submissions below 0.7 are flagged "Needs Review"
5. **Company name fallback** — extracted from the filename if not found in the email

### AI Integration
- Uses `Anthropic.SDK` (v5) with Claude claude-sonnet-4-20250514
- Structured JSON extraction via detailed system prompt
- Polly retry policy (3 retries, exponential backoff) for API resilience
- Prompt includes filename, email headers, full body, and truncated attachment text (8KB limit per attachment)

### Database
- SQLite with EF Core 9 — zero-infrastructure, portable
- 6 related tables: Submission, Insured, Broker, CoverageLine, Exposure, LossHistory
- Insured and Broker records are upserted (reused across submissions from same entity)

## Quick Start

### Prerequisites
- .NET 9 SDK
- Node.js 18+
- Anthropic API key

### 1. Configure API Key

```bash
cd InsuranceExtraction.API
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-api03-YOUR-KEY"
```

Or edit `InsuranceExtraction.API/appsettings.json`:
```json
{
  "Anthropic": {
    "ApiKey": "sk-ant-api03-YOUR-KEY-HERE"
  }
}
```

### 2. Start the Backend

```bash
cd InsuranceExtraction.API
dotnet run
```

API available at: http://localhost:5000
Swagger UI: http://localhost:5000/swagger

### 3. Start the Frontend

```bash
cd frontend
npm install
npm run dev
```

Frontend available at: http://localhost:5173

### 4. Process Test Data

You can upload `.eml` files via the UI (Upload page) or process the provided test data directly:

Using the Swagger UI at http://localhost:5000/swagger:
- `POST /api/upload` with the .eml files

Or use the provided batch script (Windows):
```batch
start-all.bat
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/submissions` | List all (paginated) |
| GET | `/api/submissions/{id}` | Detail with all relations |
| POST | `/api/submissions/search` | Search with filters |
| DELETE | `/api/submissions/{id}` | Delete a submission |
| POST | `/api/upload` | Upload & process .eml file(s) |
| GET | `/api/upload/status/{id}` | Check processing status |
| GET | `/api/analytics/statistics` | Dashboard statistics |
| POST | `/api/chat` | AI Insights — conversational query with Claude tool use |

## Data Extracted

For each submission email:

- **Insured**: company name, address, DOT/MC numbers, years in business, annual revenue
- **Broker**: name, agency, email, phone, license number
- **Coverage Lines**: line of business, limits, target premium, effective/expiration dates
- **Exposures**: trucks, drivers, power units, payroll, miles, etc.
- **Loss History**: from PDF attachments — claim dates, amounts, status, claim numbers

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Backend language | C# / .NET 9 |
| Web framework | ASP.NET Core 9 |
| ORM | Entity Framework Core 9 |
| Database | SQLite |
| Email parsing | MimeKit 4.x |
| PDF extraction | UglyToad.PdfPig |
| AI/LLM | Anthropic Claude API (Anthropic.SDK 5.x) |
| Resilience | Polly 8.x |
| Logging | Serilog |
| Frontend | React 18 + TypeScript + Vite |
| UI components | Material-UI (MUI) 7 |
| Charts | Recharts |
| HTTP client | Axios |

## Cost Estimate

~$0.01–0.02 per submission (depends on email/attachment size).
With $5 in free credits ≈ 250–500 submissions processable.
