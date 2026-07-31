# TenderScope Platform

Open-data procurement intelligence platform that discovers, normalizes, deduplicates and analyzes public tenders without paid APIs.

## Stack

- ASP.NET Core 9 Web API
- .NET Worker Service
- PostgreSQL + EF Core
- Next.js 16 + TypeScript
- Docker Compose
- GitHub Actions

## Local development

```bash
docker compose up --build
```

Frontend: http://localhost:3000  
API: http://localhost:8080/health

## Repository layout

- `apps/api` — HTTP API
- `apps/worker` — crawler and normalization worker
- `apps/web` — public dashboard
- `src/TenderScope.Domain` — core domain model
- `src/TenderScope.Application` — application contracts
- `src/TenderScope.Infrastructure` — persistence and crawling

The initial source uses a deterministic demo crawler so the complete system runs without an API key. Real public sources will be added through source adapters.
