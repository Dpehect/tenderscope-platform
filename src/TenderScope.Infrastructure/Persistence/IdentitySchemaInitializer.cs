using Microsoft.EntityFrameworkCore;

namespace TenderScope.Infrastructure.Persistence;

public static class IdentitySchemaInitializer
{
    public static Task EnsureIdentitySchemaAsync(this TenderScopeDbContext db, CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS organizations (
  "Id" uuid PRIMARY KEY,
  "Name" varchar(180) NOT NULL,
  "Slug" varchar(120) NOT NULL,
  "CreatedAt" timestamptz NOT NULL,
  "IsActive" boolean NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_organizations_Slug" ON organizations ("Slug");

CREATE TABLE IF NOT EXISTS app_users (
  "Id" uuid PRIMARY KEY,
  "Email" varchar(320) NOT NULL,
  "DisplayName" varchar(160) NOT NULL,
  "PasswordHash" varchar(1000) NOT NULL,
  "CreatedAt" timestamptz NOT NULL,
  "LastLoginAt" timestamptz NULL,
  "IsActive" boolean NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_app_users_Email" ON app_users ("Email");

CREATE TABLE IF NOT EXISTS organization_memberships (
  "Id" uuid PRIMARY KEY,
  "OrganizationId" uuid NOT NULL REFERENCES organizations("Id") ON DELETE CASCADE,
  "UserId" uuid NOT NULL REFERENCES app_users("Id") ON DELETE CASCADE,
  "Role" integer NOT NULL,
  "JoinedAt" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_organization_memberships_OrganizationId_UserId" ON organization_memberships ("OrganizationId", "UserId");
CREATE INDEX IF NOT EXISTS "IX_organization_memberships_UserId_Role" ON organization_memberships ("UserId", "Role");

CREATE TABLE IF NOT EXISTS refresh_tokens (
  "Id" uuid PRIMARY KEY,
  "UserId" uuid NOT NULL REFERENCES app_users("Id") ON DELETE CASCADE,
  "OrganizationId" uuid NULL REFERENCES organizations("Id") ON DELETE CASCADE,
  "TokenHash" varchar(128) NOT NULL,
  "CreatedAt" timestamptz NOT NULL,
  "ExpiresAt" timestamptz NOT NULL,
  "RevokedAt" timestamptz NULL,
  "ReplacedByTokenHash" varchar(128) NULL
);
ALTER TABLE refresh_tokens ADD COLUMN IF NOT EXISTS "OrganizationId" uuid NULL;
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_refresh_tokens_organizations_OrganizationId') THEN
    ALTER TABLE refresh_tokens ADD CONSTRAINT "FK_refresh_tokens_organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES organizations("Id") ON DELETE CASCADE;
  END IF;
END $$;
CREATE UNIQUE INDEX IF NOT EXISTS "IX_refresh_tokens_TokenHash" ON refresh_tokens ("TokenHash");
CREATE INDEX IF NOT EXISTS "IX_refresh_tokens_UserId_OrganizationId_ExpiresAt" ON refresh_tokens ("UserId", "OrganizationId", "ExpiresAt");

CREATE TABLE IF NOT EXISTS organization_invitations (
  "Id" uuid PRIMARY KEY,
  "OrganizationId" uuid NOT NULL REFERENCES organizations("Id") ON DELETE CASCADE,
  "Email" varchar(320) NOT NULL,
  "Role" integer NOT NULL,
  "TokenHash" varchar(128) NOT NULL,
  "InvitedByUserId" uuid NOT NULL REFERENCES app_users("Id") ON DELETE RESTRICT,
  "CreatedAt" timestamptz NOT NULL,
  "ExpiresAt" timestamptz NOT NULL,
  "AcceptedAt" timestamptz NULL,
  "RevokedAt" timestamptz NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_organization_invitations_TokenHash" ON organization_invitations ("TokenHash");
CREATE INDEX IF NOT EXISTS "IX_organization_invitations_OrganizationId_Email_ExpiresAt" ON organization_invitations ("OrganizationId", "Email", "ExpiresAt");
""", cancellationToken);
}
