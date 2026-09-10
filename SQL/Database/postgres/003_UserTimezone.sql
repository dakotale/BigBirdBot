-- =============================================================================
-- 003 — Per-user time zone, backing the /timezone command.
--
-- One row per user (not per-server): /remind reads it so members don't re-enter
-- a UTC offset every time. "TimeZone" holds an IANA id (e.g. America/New_York)
-- or a fixed-offset token (e.g. UTC-05:00) — see Helper/TimeZoneResolver.cs.
--
-- Run once, after deploying the build that adds /timezone:
--   psql -U discordbot -d discordbot -h localhost -f 003_UserTimezone.sql
-- =============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS "UserTimezone"
(
    "UserID"    varchar(50) NOT NULL PRIMARY KEY,
    "TimeZone"  varchar(64) NOT NULL,
    "UpdatedOn" timestamp   NOT NULL
);

COMMIT;
