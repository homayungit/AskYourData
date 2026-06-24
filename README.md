# AskYourData

> **Chat with your SQL Server database using natural language — 100% on-premise, no cloud, no data leaks.**

AskYourData is an open-source AI chatbot that lets anyone in your organization query business databases by simply typing a question. No SQL knowledge required. The entire AI stack runs locally via [Ollama](https://ollama.com) — your data never leaves your server.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)
![Vue](https://img.shields.io/badge/Vue-3-42b883.svg)
![Ollama](https://img.shields.io/badge/LLM-Ollama-black.svg)
![Qdrant](https://img.shields.io/badge/Vector_DB-Qdrant-red.svg)

---

## What it does

| You type | AskYourData does |
|---|---|
| *"Which products had the highest sales last month?"* | Generates SQL → runs it → returns a human-readable answer |
| *"How many employees are in the HR department?"* | Queries the right table → answers with the exact count |
| *"Show me all vendors with pending purchase orders"* | Routes to the correct database → executes → formats results |

---

## Architecture

```
┌──────────────────────┐     HTTP      ┌──────────────────────────────┐
│  Vue 3 + Tailwind    │ ────────────► │  ASP.NET Core 9 Web API      │
│  (port 4500)         │              │  (port 5000)                  │
└──────────────────────┘              │                               │
                                      │  ┌──────────────────────────┐ │
                                      │  │   Semantic Kernel         │ │
                                      │  │   ┌────────────────────┐  │ │
                                      │  │   │  DatabaseRouter    │  │ │
                                      │  │   │  SqlGenerator      │  │ │
                                      │  │   │  VectorIngestion   │  │ │
                                      │  │   │  SafeQueryExecutor │  │ │
                                      │  │   └────────────────────┘  │ │
                                      │  └──────────────────────────┘ │
                                      └───────────┬──────────┬─────────┘
                                                  │          │
                                      ┌───────────┘  ┌───────┘
                                      ▼              ▼
                               ┌─────────────┐  ┌──────────────┐
                               │   Qdrant    │  │    Ollama    │
                               │  Vector DB  │  │  (local LLM) │
                               │ (6333/6334) │  │   (11434)    │
                               └─────────────┘  └──────────────┘
                                                       │
                                         ┌─────────────┘
                                         ▼
                                ┌─────────────────┐
                                │  SQL Server DBs │
                                │  (read-only)    │
                                └─────────────────┘
```

### How a question flows through the system

1. **User types a question** in the Vue 3 chat UI
2. **DatabaseRouter** identifies which configured database the question is about
3. **VectorIngestion** retrieves the relevant schema context from Qdrant
4. **SqlGeneratorService** uses Ollama (Qwen2.5:32B) to generate a safe SQL query
5. **SafeQueryExecutor** validates and runs the query (read-only, blocked keywords enforced)
6. **ChatbotService** passes the live SQL results back to the LLM for a natural language answer
7. The answer is streamed back to the UI

---

## Features

- **Natural language to SQL** — Ask questions in plain English (or Bengali); the AI generates and runs the correct query
- **Schema-aware RAG** — Qdrant vector database stores your schema so the AI understands your database structure before answering
- **100% on-premise** — Ollama runs the LLM locally; no data ever reaches an external server
- **Read-only by design** — Dangerous SQL keywords are hard-blocked at the API level (INSERT, UPDATE, DELETE, DROP, TRUNCATE, ALTER, CREATE, EXEC, XP_, SP_)
- **Multi-database support** — Configure multiple SQL Server databases; questions are automatically routed to the right one
- **OpenAI fallback** — Optionally enable GPT-4o-mini as a fallback when Ollama is unavailable
- **Smart table routing** — TableGroups with trigger keywords route questions to the correct subset of tables, reducing token usage and improving accuracy
- **Schema explorer API** — Protected endpoints to inspect database structure without writing SQL
- **Structured logging** — Serilog with daily rolling log files
- **Docker Compose** — Single command to deploy the entire stack

---

## Tech Stack

| Layer | Technology |
|---|---|
| **Backend** | ASP.NET Core 9, C# |
| **AI Orchestration** | Microsoft Semantic Kernel |
| **LLM** | Ollama — `qwen2.5:32b-instruct-q4_K_M` |
| **Embeddings** | Ollama — `bge-m3` |
| **Vector Database** | Qdrant |
| **Relational Database** | Microsoft SQL Server (read-only) |
| **ORM / Query** | Dapper |
| **Frontend** | Vue 3 + TypeScript + Tailwind CSS + Vite |
| **Logging** | Serilog |
| **Containerization** | Docker + Docker Compose |

---

## Prerequisites

| Dependency | Version |
|---|---|
| .NET SDK | 9.0+ |
| Node.js | 22+ |
| Docker + Docker Compose | Latest |
| Ollama | 0.20+ |
| SQL Server | 2017+ |
| GPU (recommended) | RTX 3090+ (16GB+ VRAM) for qwen2.5:32b |

> **No GPU?** You can use a smaller Ollama model like `qwen2.5:7b` or enable the OpenAI fallback in `appsettings.json`.

---

## Quick Start (Docker)

### 1. Pull Ollama models

```bash
ollama pull qwen2.5:32b-instruct-q4_K_M
ollama pull bge-m3
```

### 2. Configure your database

Copy the example config and fill in your values:

```bash
cp src/AskYourData.API/appsettings.example.json src/AskYourData.API/appsettings.json
```

Edit `src/AskYourData.API/appsettings.json` and set your SQL Server connection string:

```json
"ChatDatabases": [
  {
    "Name": "MyDatabase",
    "DisplayName": "My Business DB",
    "ConnectionString": "Server=YOUR_SERVER;Database=YOUR_DB;User Id=sa;Password=YOUR_PASS;TrustServerCertificate=true;ApplicationIntent=ReadOnly;",
    "IsReadOnly": true,
    "Topics": ["sales", "order", "product", "customer"],
    "TableGroups": []
  }
]
```

Also set `ChatbotSettings.IngestApiKey` to a secure random string (used to protect the `/ingest` endpoint).

### 3. Start with Docker Compose

```bash
docker compose up -d
```

| Service | URL |
|---|---|
| Frontend | http://localhost:4500 |
| API | http://localhost:5000 |
| Qdrant UI | http://localhost:6333/dashboard |

### 4. Ingest your schema (one-time setup)

This step reads your database schema and stores it as vectors in Qdrant so the AI can understand your tables.

```bash
curl -X POST http://localhost:5000/api/chatbot/ingest \
     -H "X-Api-Key: YOUR_INGEST_KEY"
```

The API responds immediately with `202 Accepted` — ingestion runs in the background. Check the logs to confirm completion.

### 5. Start asking questions

Open http://localhost:4500 and type your first question.

---

## Development Setup (without Docker)

```bash
# Terminal 1 — API
cd src/AskYourData.API
dotnet run
# API available at http://localhost:5000

# Terminal 2 — Frontend
cd src/frontend
npm install
npm run dev
# Frontend available at http://localhost:3000
```

Make sure Qdrant is running locally:

```bash
docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant
```

---

## Configuration Reference

### Ollama Settings

| Key | Description | Default |
|---|---|---|
| `OllamaSettings:BaseUrl` | Ollama server URL | `http://localhost:11434` |
| `OllamaSettings:ChatModel` | LLM model for SQL generation and answering | `qwen2.5:32b-instruct-q4_K_M` |
| `OllamaSettings:EmbeddingModel` | Model for vector embeddings | `bge-m3` |
| `OllamaSettings:NumCtx` | Context window size in tokens | `16384` |
| `OllamaSettings:Temperature` | LLM temperature (lower = more deterministic) | `0.1` |
| `OllamaSettings:RequestTimeoutSeconds` | HTTP timeout for Ollama requests | `120` |

### Qdrant Settings

| Key | Description | Default |
|---|---|---|
| `QdrantSettings:Host` | Qdrant server host | `localhost` |
| `QdrantSettings:Port` | Qdrant gRPC port | `6334` |
| `QdrantSettings:CollectionName` | Vector collection name | `adventureworks_chunks` |

### Chatbot Settings

| Key | Description | Default |
|---|---|---|
| `ChatbotSettings:IngestApiKey` | API key for `/ingest` and `/schema` endpoints | *(must set)* |
| `ChatbotSettings:MaxContextRows` | Max rows returned per SQL query | `200` |
| `ChatbotSettings:TopKVectorResults` | Number of schema chunks retrieved from Qdrant | `5` |
| `ChatbotSettings:SqlTimeoutSeconds` | SQL query execution timeout | `30` |
| `ChatbotSettings:EnableDebugInfo` | Include generated SQL in the API response | `true` |
| `ChatbotSettings:SystemPrompt` | System prompt sent to the LLM | *(see appsettings.json)* |

### OpenAI Fallback (optional)

```json
"OpenAIFallback": {
  "Enabled": true,
  "ApiKey": "sk-...",
  "ChatModel": "gpt-4o-mini",
  "EmbeddingModel": "text-embedding-3-small"
}
```

---

## API Endpoints

### Public

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/chatbot/ask` | Send a question, receive an AI-generated answer |
| `GET` | `/api/chatbot/status` | Health check + list configured databases |
| `GET` | `/api/chatbot/databases` | List databases with their indexed tables |

### Protected (requires `X-Api-Key` header)

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/chatbot/ingest` | Trigger schema ingestion into Qdrant |
| `GET` | `/api/chatbot/schema` | List all configured databases |
| `GET` | `/api/chatbot/schema/{dbName}` | List all tables in a database |
| `GET` | `/api/chatbot/schema/{dbName}/{tableName}` | Get all columns for a table |

### Example: Ask a question

```bash
curl -X POST http://localhost:5000/api/chatbot/ask \
     -H "Content-Type: application/json" \
     -d '{"question": "Which product category had the most sales last year?", "databaseName": "AdventureWorks"}'
```

---

## TableGroups — Smart Routing

TableGroups let you define named subsets of tables with trigger keywords. When a question matches the keywords, only those tables' schemas are sent to the LLM — reducing token usage and improving answer accuracy.

```json
"TableGroups": [
  {
    "Name": "SalesOverview",
    "Description": "Sales orders, customers, salesperson performance, and territory data",
    "EntityLabel": "customer or product",
    "TriggerKeywords": ["sales order", "revenue", "salesperson", "territory"],
    "Tables": [
      "Sales.SalesOrderHeader",
      "Sales.SalesOrderDetail",
      "Sales.Customer",
      "Sales.SalesPerson",
      "Sales.SalesTerritory"
    ]
  }
]
```

---

## Running Tests

```bash
# Unit tests (no external dependencies required)
dotnet test tests/AskYourData.Tests.Unit

# Integration tests (requires running API + SQL Server + Qdrant)
INTEGRATION_TEST_LIVE=true dotnet test tests/AskYourData.Tests.Integration
```

---

## Security

- **Read-only SQL only** — Blocked keywords at the executor level: `INSERT`, `UPDATE`, `DELETE`, `DROP`, `TRUNCATE`, `ALTER`, `CREATE`, `EXEC`, `XP_`, `SP_`, `--`, `/*`
- **Connection string isolation** — Each database connection uses `ApplicationIntent=ReadOnly`
- **API key protection** — The `/ingest` and `/schema` endpoints require an `X-Api-Key` header
- **No credentials in code** — Use environment variables or a secrets manager for production keys
- **On-premise LLM** — All AI inference runs locally via Ollama; your business data never leaves your network

---

## Project Structure

```
AskYourData/
├── src/
│   ├── AskYourData.Core/              # Domain models, interfaces, options
│   │   ├── Interfaces/                # IChatbotService, IDatabaseRouter, ISqlGeneratorService, ...
│   │   ├── Models/                    # ChatRequest, ChatResponse, QueryResult, VectorRecord, ...
│   │   └── Options/                   # OllamaOptions, QdrantOptions, ChatbotOptions
│   │
│   ├── AskYourData.Infrastructure/    # AI, database, and vector implementations
│   │   ├── AI/                        # Ollama LLM + embedding service
│   │   ├── Chatbot/                   # ChatbotService, DatabaseRouter, SqlGeneratorService
│   │   └── Database/                  # SafeQueryExecutor, SchemaInspector, DatabaseRegistry
│   │
│   ├── AskYourData.API/               # Web API — controllers, middleware, DI wiring
│   │   ├── Controllers/               # ChatbotController
│   │   ├── Middleware/                # Request logging, error handling
│   │   └── Program.cs
│   │
│   └── frontend/                      # Vue 3 + TypeScript + Tailwind CSS
│       └── src/
│           ├── components/            # ChatWindow, MessageBubble, DatabaseStatusBar
│           ├── composables/           # useChat, useDatabase
│           ├── api/                   # API client
│           └── types/                 # TypeScript type definitions
│
├── tests/
│   ├── AskYourData.Tests.Unit/        # Unit tests (no external dependencies)
│   └── AskYourData.Tests.Integration/ # Integration tests (requires live services)
│
├── docker-compose.yml
├── docker-compose.override.yml
└── AskYourData.slnx
```

---

## Roadmap

- [ ] Streaming responses (Server-Sent Events)
- [ ] Conversation history / multi-turn chat
- [ ] Export results to CSV / Excel
- [ ] Support for PostgreSQL and MySQL
- [ ] Authentication (LDAP / Active Directory integration)
- [ ] Dark mode UI

---

## Contributing

Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/your-feature`)
3. Commit your changes
4. Push to the branch and open a Pull Request

---

## License

[MIT](LICENSE)

---

> Built with the goal of bringing AI-powered data access to on-premise enterprise environments — without sacrificing privacy or data sovereignty.
