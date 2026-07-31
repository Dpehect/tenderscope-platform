using Microsoft.EntityFrameworkCore;

namespace TenderScope.Infrastructure.Persistence;

public static class WorkspaceSchemaInitializer
{
    public static Task EnsureWorkspaceTenantSchemaAsync(this TenderScopeDbContext db, CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlRawAsync("""
ALTER TABLE workspace_items ADD COLUMN IF NOT EXISTS "OrganizationId" uuid NULL;
ALTER TABLE workspace_items ADD COLUMN IF NOT EXISTS "CreatedByUserId" uuid NULL;
ALTER TABLE workspace_items ADD COLUMN IF NOT EXISTS "Position" numeric(18,6) NOT NULL DEFAULT 0;
ALTER TABLE workspace_items ADD COLUMN IF NOT EXISTS "Tags" text[] NOT NULL DEFAULT ARRAY[]::text[];
ALTER TABLE workspace_items ADD COLUMN IF NOT EXISTS "InternalDeadline" timestamptz NULL;
ALTER TABLE workspace_items ADD COLUMN IF NOT EXISTS "AssigneeUserId" uuid NULL;
ALTER TABLE saved_searches ADD COLUMN IF NOT EXISTS "OrganizationId" uuid NULL;
ALTER TABLE saved_searches ADD COLUMN IF NOT EXISTS "CreatedByUserId" uuid NULL;

DO $$ BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'workspace_items' AND column_name = 'UserKey') THEN ALTER TABLE workspace_items ALTER COLUMN "UserKey" DROP NOT NULL; END IF;
  IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'saved_searches' AND column_name = 'UserKey') THEN ALTER TABLE saved_searches ALTER COLUMN "UserKey" DROP NOT NULL; END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_workspace_items_organizations_OrganizationId') THEN ALTER TABLE workspace_items ADD CONSTRAINT "FK_workspace_items_organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES organizations("Id") ON DELETE CASCADE; END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_workspace_items_app_users_CreatedByUserId') THEN ALTER TABLE workspace_items ADD CONSTRAINT "FK_workspace_items_app_users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES app_users("Id") ON DELETE RESTRICT; END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_workspace_items_app_users_AssigneeUserId') THEN ALTER TABLE workspace_items ADD CONSTRAINT "FK_workspace_items_app_users_AssigneeUserId" FOREIGN KEY ("AssigneeUserId") REFERENCES app_users("Id") ON DELETE SET NULL; END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_saved_searches_organizations_OrganizationId') THEN ALTER TABLE saved_searches ADD CONSTRAINT "FK_saved_searches_organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES organizations("Id") ON DELETE CASCADE; END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_saved_searches_app_users_CreatedByUserId') THEN ALTER TABLE saved_searches ADD CONSTRAINT "FK_saved_searches_app_users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES app_users("Id") ON DELETE RESTRICT; END IF;
END $$;

CREATE TABLE IF NOT EXISTS workspace_activities (
  "Id" uuid PRIMARY KEY,
  "OrganizationId" uuid NOT NULL REFERENCES organizations("Id") ON DELETE CASCADE,
  "WorkspaceItemId" uuid NOT NULL REFERENCES workspace_items("Id") ON DELETE CASCADE,
  "ActorUserId" uuid NOT NULL REFERENCES app_users("Id") ON DELETE RESTRICT,
  "Action" varchar(120) NOT NULL,
  "Detail" varchar(2000) NULL,
  "CreatedAt" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_workspace_items_OrganizationId_TenderId" ON workspace_items ("OrganizationId", "TenderId") WHERE "OrganizationId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_workspace_items_OrganizationId_Stage_Position" ON workspace_items ("OrganizationId", "Stage", "Position");
CREATE INDEX IF NOT EXISTS "IX_workspace_activities_OrganizationId_CreatedAt" ON workspace_activities ("OrganizationId", "CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_workspace_activities_WorkspaceItemId_CreatedAt" ON workspace_activities ("WorkspaceItemId", "CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_saved_searches_OrganizationId_CreatedAt" ON saved_searches ("OrganizationId", "CreatedAt");
""", cancellationToken);
}
