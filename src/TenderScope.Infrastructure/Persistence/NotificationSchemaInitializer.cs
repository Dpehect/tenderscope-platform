using Microsoft.EntityFrameworkCore;

namespace TenderScope.Infrastructure.Persistence;

public static class NotificationSchemaInitializer
{
    public static Task EnsureNotificationSchemaAsync(this TenderScopeDbContext db, CancellationToken ct = default) =>
        db.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS watchlist_matches (
  "Id" uuid PRIMARY KEY,
  "OrganizationId" uuid NOT NULL REFERENCES organizations("Id") ON DELETE CASCADE,
  "SavedSearchId" uuid NOT NULL REFERENCES saved_searches("Id") ON DELETE CASCADE,
  "TenderId" uuid NOT NULL REFERENCES tenders("Id") ON DELETE CASCADE,
  "Score" integer NOT NULL,
  "Reason" varchar(1000) NOT NULL,
  "CreatedAt" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_watchlist_matches_SavedSearchId_TenderId" ON watchlist_matches ("SavedSearchId", "TenderId");
CREATE INDEX IF NOT EXISTS "IX_watchlist_matches_OrganizationId_CreatedAt" ON watchlist_matches ("OrganizationId", "CreatedAt");

CREATE TABLE IF NOT EXISTS app_notifications (
  "Id" uuid PRIMARY KEY,
  "OrganizationId" uuid NOT NULL REFERENCES organizations("Id") ON DELETE CASCADE,
  "UserId" uuid NOT NULL REFERENCES app_users("Id") ON DELETE CASCADE,
  "Type" varchar(80) NOT NULL,
  "Title" varchar(240) NOT NULL,
  "Message" varchar(1000) NOT NULL,
  "ResourceUrl" varchar(1000) NULL,
  "CreatedAt" timestamptz NOT NULL,
  "ReadAt" timestamptz NULL
);
CREATE INDEX IF NOT EXISTS "IX_app_notifications_OrganizationId_UserId_ReadAt_CreatedAt" ON app_notifications ("OrganizationId", "UserId", "ReadAt", "CreatedAt");

CREATE TABLE IF NOT EXISTS notification_preferences (
  "Id" uuid PRIMARY KEY,
  "OrganizationId" uuid NOT NULL REFERENCES organizations("Id") ON DELETE CASCADE,
  "UserId" uuid NOT NULL REFERENCES app_users("Id") ON DELETE CASCADE,
  "InAppEnabled" boolean NOT NULL,
  "WatchlistMatchesEnabled" boolean NOT NULL,
  "DeadlineRemindersEnabled" boolean NOT NULL,
  "UpdatedAt" timestamptz NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_notification_preferences_OrganizationId_UserId" ON notification_preferences ("OrganizationId", "UserId");
""", ct);
}
