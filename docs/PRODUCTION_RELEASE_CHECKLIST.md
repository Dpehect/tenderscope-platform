# TenderScope Production Release Checklist

## Required configuration
- `ConnectionStrings__Default`
- `Jwt__Secret` (minimum 32 characters)
- `Jwt__Issuer`
- `Jwt__Audience`
- `Cors__AllowedOrigins__0`
- `NEXT_PUBLIC_API_URL`
- `NEXT_PUBLIC_SITE_URL`

## Release gates
1. Backend restore, vulnerability scan, Release build and tests pass.
2. Frontend clean install, npm audit, typecheck and production build pass.
3. API Docker image builds successfully.
4. Production migrations complete once and are recorded in the migration ledger.
5. `/health/live` and `/health/ready` return success.
6. Register, login, refresh, logout and password-reset smoke tests pass.
7. Tenant A cannot read or mutate Tenant B workspace records.
8. Ingestion job acquires its advisory lock and writes a job-history record.
9. Admin metrics and audit endpoints require Admin or Owner authorization.
10. Rollback image and database backup are available before promotion.

## Production smoke test
- Open public opportunities and an opportunity detail page.
- Create an account and organization.
- Add a tender to workspace and move it between stages.
- Create a watchlist and run matching.
- Verify notification badge and notification center.
- Export CSV, Excel and PDF reports.
- Run one source synchronization and inspect source health.
