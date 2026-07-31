using Microsoft.EntityFrameworkCore;

namespace TenderScope.Infrastructure.Persistence;

public static class AccountRecoverySchemaInitializer
{
    public static Task EnsureAccountRecoverySchemaAsync(this TenderScopeDbContext db, CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlRawAsync("""
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS "EmailVerifiedAt" timestamptz NULL;

CREATE TABLE IF NOT EXISTS account_action_tokens (
  "Id" uuid PRIMARY KEY,
  "UserId" uuid NOT NULL REFERENCES app_users("Id") ON DELETE CASCADE,
  "Purpose" varchar(40) NOT NULL,
  "TokenHash" varchar(128) NOT NULL,
  "CreatedAt" timestamptz NOT NULL,
  "ExpiresAt" timestamptz NOT NULL,
  "ConsumedAt" timestamptz NULL,
  "RequestedIp" varchar(64) NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_account_action_tokens_TokenHash" ON account_action_tokens ("TokenHash");
CREATE INDEX IF NOT EXISTS "IX_account_action_tokens_UserId_Purpose_ExpiresAt" ON account_action_tokens ("UserId", "Purpose", "ExpiresAt");
""", cancellationToken);
}
