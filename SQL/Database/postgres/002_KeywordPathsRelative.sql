-- =============================================================================
-- 002 — Make ChatKeyword local-file entries host-independent.
--
-- Before: local keyword images were stored as absolute Windows paths, e.g.
--   C:\Temp\DiscordBot\cat\social_20260228_x.jpg
-- After:  a host-independent "file:" value resolved at runtime against
--   Constants.keywordDirectory (see Helper/KeywordFiles.cs), e.g.
--   file:cat/social_20260228_x.jpg
--
-- URL entries (http…) and plain-text entries are left untouched.
-- Idempotent: rows already converted start with "file:" and are skipped.
--
-- Run once, after deploying the build that understands the "file:" form:
--   psql -U discordbot -d discordbot -h localhost -f 002_KeywordPathsRelative.sql
-- =============================================================================

BEGIN;

UPDATE "ChatKeyword"
SET "FilePath" = 'file:' || replace(right("FilePath", -length('C:\Temp\DiscordBot\')), '\', '/')
WHERE starts_with("FilePath", 'C:\Temp\DiscordBot\');

-- Any absolute path that somehow points elsewhere: flag rather than guess.
DO $$
DECLARE stray int;
BEGIN
    SELECT count(*) INTO stray FROM "ChatKeyword"
    WHERE "FilePath" ~ '^[A-Za-z]:\\' AND NOT starts_with("FilePath", 'file:');
    IF stray > 0 THEN
        RAISE WARNING '% ChatKeyword row(s) have an absolute path outside C:\Temp\DiscordBot\ and were left as-is', stray;
    END IF;
END $$;

COMMIT;
