using Microsoft.EntityFrameworkCore;

namespace TenderScope.Infrastructure.Persistence;

public static class ProductionMigrationRunner
{
    private const long MigrationLockKey = 817_324_119_607_311_027;

    private sealed record Migration(string Version, string Description, string Sql);

    private static readonly Migration[] Migrations =
    [
        new("20260731_001", "Create migration ledger and operational indexes", """
CREATE INDEX IF NOT EXISTS "IX_tenders_DeadlineAt" ON tenders ("DeadlineAt");
CREATE INDEX IF NOT EXISTS "IX_tenders_BuyerName" ON tenders ("BuyerName");
CREATE INDEX IF NOT EXISTS "IX_workspace_items_OrganizationId_InternalDeadline" ON workspace_items ("OrganizationId", "InternalDeadline");
CREATE INDEX IF NOT EXISTS "IX_refresh_tokens_FamilyId" ON refresh_tokens ("FamilyId");
CREATE INDEX IF NOT EXISTS "IX_app_notifications_CreatedAt" ON app_notifications ("CreatedAt");
"""),
        new("20260731_002", "Create operational job history", """
CREATE TABLE IF NOT EXISTS operational_job_runs (
  "Id" uuid PRIMARY KEY,
  "JobName" varchar(160) NOT NULL,
  "InstanceId" varchar(160) NOT NULL,
  "StartedAt" timestamptz NOT NULL,
  "CompletedAt" timestamptz NULL,
  "Succeeded" boolean NULL,
  "Attempt" integer NOT NULL DEFAULT 1,
  "RecordsAffected" integer NOT NULL DEFAULT 0,
  "Error" varchar(4000) NULL
);
CREATE INDEX IF NOT EXISTS "IX_operational_job_runs_JobName_StartedAt" ON operational_job_runs ("JobName", "StartedAt" DESC);
"""),
        new("20260731_003", "Create crawler quality, run history and dead letters", """
ALTER TABLE tender_sources ADD COLUMN IF NOT EXISTS "ParserVersion" varchar(40) NOT NULL DEFAULT '1.0.0';
ALTER TABLE tender_sources ADD COLUMN IF NOT EXISTS "QualityScore" numeric(5,2) NOT NULL DEFAULT 100;
ALTER TABLE tender_sources ADD COLUMN IF NOT EXISTS "SuccessRate" numeric(5,2) NOT NULL DEFAULT 100;
ALTER TABLE tender_sources ADD COLUMN IF NOT EXISTS "TotalRuns" integer NOT NULL DEFAULT 0;
ALTER TABLE tender_sources ADD COLUMN IF NOT EXISTS "SuccessfulRuns" integer NOT NULL DEFAULT 0;
ALTER TABLE tender_sources ADD COLUMN IF NOT EXISTS "LastDataAt" timestamptz NULL;
CREATE TABLE IF NOT EXISTS crawl_runs (
  "Id" uuid PRIMARY KEY,
  "SourceId" uuid NOT NULL REFERENCES tender_sources("Id") ON DELETE CASCADE,
  "SourceKey" varchar(120) NOT NULL,
  "ParserVersion" varchar(40) NOT NULL,
  "Status" integer NOT NULL,
  "Attempt" integer NOT NULL DEFAULT 1,
  "FetchedCount" integer NOT NULL DEFAULT 0,
  "ImportedCount" integer NOT NULL DEFAULT 0,
  "RejectedCount" integer NOT NULL DEFAULT 0,
  "DurationMilliseconds" bigint NOT NULL DEFAULT 0,
  "Error" varchar(4000) NULL,
  "StartedAt" timestamptz NOT NULL,
  "CompletedAt" timestamptz NULL
);
CREATE INDEX IF NOT EXISTS "IX_crawl_runs_SourceId_StartedAt" ON crawl_runs ("SourceId", "StartedAt" DESC);
CREATE TABLE IF NOT EXISTS crawl_dead_letters (
  "Id" uuid PRIMARY KEY,
  "CrawlRunId" uuid NULL REFERENCES crawl_runs("Id") ON DELETE SET NULL,
  "SourceId" uuid NOT NULL REFERENCES tender_sources("Id") ON DELETE CASCADE,
  "SourceKey" varchar(120) NOT NULL,
  "ExternalId" varchar(300) NULL,
  "Error" varchar(4000) NOT NULL,
  "PayloadPreview" varchar(4000) NULL,
  "Attempts" integer NOT NULL DEFAULT 1,
  "CreatedAt" timestamptz NOT NULL,
  "ResolvedAt" timestamptz NULL
);
CREATE INDEX IF NOT EXISTS "IX_crawl_dead_letters_SourceId_ResolvedAt" ON crawl_dead_letters ("SourceId", "ResolvedAt", "CreatedAt" DESC);
""")
    ];

    public static async Task ApplyProductionMigrationsAsync(this TenderScopeDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_lock({MigrationLockKey})", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS schema_migrations (
  "Version" varchar(80) PRIMARY KEY,
  "Description" varchar(500) NOT NULL,
  "AppliedAt" timestamptz NOT NULL DEFAULT now()
);
""", cancellationToken);

            foreach (var migration in Migrations)
            {
                var applied = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM schema_migrations WHERE \"Version\" = {0}", migration.Version).SingleAsync(cancellationToken);
                if (applied > 0) continue;
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                await db.Database.ExecuteSqlRawAsync(migration.Sql, cancellationToken);
                await db.Database.ExecuteSqlRawAsync("INSERT INTO schema_migrations (\"Version\", \"Description\") VALUES ({0}, {1})", [migration.Version, migration.Description], cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            try { await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({MigrationLockKey})", CancellationToken.None); }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }
}
