# TenderScope

TenderScope is an open-data procurement intelligence platform for discovering, normalizing, qualifying and monitoring public tender opportunities. It combines traceable official-source ingestion with a multi-tenant workspace for bid teams.

## Product capabilities

- Search and filter normalized opportunities by market, category, deadline and value
- Inspect source-linked tender details and market analytics
- Create organizations with isolated users, roles and workspaces
- Move opportunities through a qualification pipeline
- Build watchlists and receive matched-opportunity notifications
- Invite team members and manage organization settings
- Review operational metrics, source health, audit history and dead-letter jobs
- Export authorized reports in CSV, Excel and PDF formats

## Public data sources

- European Union Tenders Electronic Daily (TED)
- World Bank Procurement Notices
- Deterministic validation source for local and CI smoke tests

TenderScope stores the original source URL for every record. The official notice remains the final authority for eligibility, deadlines and submission requirements.

## Architecture

| Layer | Technology | Responsibility |
| --- | --- | --- |
| Web | Next.js 16, React 19, TypeScript | Public discovery, analytics and authenticated workspaces |
| API | ASP.NET Core 9 Minimal APIs | Queries, identity, tenancy, administration and operations |
| Worker | .NET Worker Service | Scheduled ingestion and source processing |
| Persistence | PostgreSQL, EF Core | Tender, identity, workspace and operational data |
| Delivery | Docker Compose, GitHub Actions | Reproducible local runtime and CI release gates |

The backend follows a modular-monolith structure with Domain, Application and Infrastructure projects. Source adapters implement a provider-neutral ingestion contract.

## Run locally

Prerequisites: Docker with Compose support.

```bash
cp .env.example .env
docker compose up --build
```

Then open:

- Web: <http://localhost:3000>
- API health: <http://localhost:8080/health>
- Readiness: <http://localhost:8080/health/ready>
- OpenAPI document: <http://localhost:8080/openapi/v1.json>

The defaults in `docker-compose.yml` are intended only for local development. Replace JWT, database and admin credentials before any shared or production deployment.

## Development without Docker

Backend requires the .NET 9 SDK and PostgreSQL:

```bash
dotnet restore TenderScope.sln
dotnet test TenderScope.sln
dotnet run --project apps/api/TenderScope.Api.csproj
```

Frontend requires Node.js 22:

```bash
cd apps/web
npm ci
npm run typecheck
npm run build
npm run dev
```

## Configuration

Copy `.env.example` and supply environment-specific values. Production requires:

- `ConnectionStrings__Postgres`
- `Jwt__Secret` with at least 32 characters
- `Jwt__Issuer` and `Jwt__Audience`
- one or more explicit `Cors__AllowedOrigins__*` values
- `NEXT_PUBLIC_API_URL` and `NEXT_PUBLIC_SITE_URL`
- `Admin__ApiKey` for the separate operational endpoint group

Never commit real credentials. Production must use HTTPS and set `Jwt__AllowHttp=false`.

## Quality and security

- PBKDF2-SHA256 password hashing and rotating refresh-token families
- Role and organization scoped authorization
- PostgreSQL tenant-isolation integration coverage
- Global and authentication-specific rate limiting
- Explicit CORS, security headers and production configuration validation
- Advisory-locked background operations, audit logs and health probes
- Backend tests, vulnerability checks, frontend audit/typecheck/build and API smoke validation in CI

## Repository layout

```text
apps/api                         ASP.NET Core API and endpoint modules
apps/worker                      scheduled crawler worker
apps/web                         Next.js application
src/TenderScope.Domain           entities and domain rules
src/TenderScope.Application      contracts and application models
src/TenderScope.Infrastructure   persistence, normalization and source adapters
tests                            domain, security, crawler and PostgreSQL isolation tests
docs                             architecture and release documentation
```

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Delivery status](docs/PHASES.md)
- [Production release checklist](docs/PRODUCTION_RELEASE_CHECKLIST.md)
- [Crawler policy](src/TenderScope.Infrastructure/Crawling/README.md)

## Responsible use

TenderScope indexes openly accessible procurement information. Source-specific terms, robots directives, crawl intervals and legal constraints must be respected when adding adapters. Do not use the platform to bypass access controls or republish restricted material.

## License

No open-source license has been granted yet. All rights are reserved until a `LICENSE` file is added.
