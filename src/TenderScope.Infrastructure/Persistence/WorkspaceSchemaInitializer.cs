using Microsoft.EntityFrameworkCore;

namespace TenderScope.Infrastructure.Persistence;

public static class WorkspaceSchemaInitializer
{
    public static Task EnsureWorkspaceTenantSchemaAsync(this TenderScopeDbContext db, CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlRawAsync("""
ALTER TABLE workspace_items ADD COLUMN IF NOT EXISTS "OrganizationId" uuid NULL;
ALTER TABLE workspace_items ADD COLUMN IF NOT EXISTS "CreatedByUserId" uuid NULL;
ALTER TABLE workspace_items ALTER COLUMN "UserKey" DROP NOT NULL;
ALTER TABLE saved_searches ADD COLUMN IF NOT EXISTS "OrganizationId" uuid NULL;
ALTER TABLE saved_searches ADD COLUMN IF NOT EXISTS "CreatedByUserId" uuid NULL;
ALTER TABLE saved_searches ALTER COLUMN "UserKey" DROP NOT NULL;

DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_workspace_items_organizations_OrganizationId') THEN
    ALTER TABLE workspace_items ADD CONSTRAINT "FK_workspace_items_organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES organizations("Id") ON DELETE CASCADE;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_workspace_items_app_users_CreatedByUserId') THEN
    ALTER TABLE workspace_items ADD CONSTRAINT "FK_workspace_items_app_users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES app_users("Id") ON DELETE RESTRICT;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_saved_searches_organizations_OrganizationId') THEN
    ALTER TABLE saved_searches ADD CONSTRAINT "FK_saved_searches_organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES organizations("Id") ON DELETE CASCADE;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_saved_searches_app_users_CreatedByUserId') THEN
    ALTER TABLE saved_searches ADD CONSTRAINT "FK_saved_searches_app_users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES app_users("Id") ON DELETE RESTRICT;
  END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_workspace_items_OrganizationId_TenderId" ON workspace_items ("OrganizationId", "TenderId") WHERE "OrganizationId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_workspace_items_OrganizationId_Stage_UpdatedAt" ON workspace_items ("OrganizationId", "Stage", "UpdatedAt");
CREATE INDEX IF NOT EXISTS "IX_saved_searches_OrganizationId_CreatedAt" ON saved_searches ("OrganizationId", "CreatedAt");
""", cancellationToken);
}
