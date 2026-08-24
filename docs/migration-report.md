# SQL Server → PostgreSQL Migration Report

Run 2026-08-23. Source: SQL Server 2022 (`localhost\DiscordBot`). Target: PostgreSQL 18.6 (`localhost/discordbot`).

## Summary

All 57 tables migrated with schema + data. Row counts verified exact on all 57. 42 identity sequences reset from source `MAX(id)`. 7 audit tables archived (schema preserved, data intentionally left in SQL Server only, per request). One pre-existing, unrelated build issue found (not caused by this migration). Stored procedures (196) were **not** ported — pgloader migrates schema and data only, not T-SQL logic; that remains separate future work.

## 1. What actually happened in Step 4 (read this first)

The initial pgloader run only fully succeeded for 27 of 57 tables. Full detail, including root cause and every corrective action taken, is in [migration-log.txt](../migration-log.txt). Short version:

- **30 tables failed to be created at all** because pgloader carries SQL Server column `DEFAULT` expressions into the generated `CREATE TABLE` verbatim, and doesn't translate `getutcdate()`/`sysutcdatetime()` (confirmed as a known, unresolved pgloader limitation for MSSQL sources — [dimitri/pgloader#1343](https://github.com/dimitri/pgloader/issues/1343)). Fixed by hand-generating correct DDL for those 30 tables (translating the defaults to `now() AT TIME ZONE 'utc'`) and copying their data directly via a small Npgsql/SqlClient console app.
- **11 identity columns** (`AuditLog`, `Birthday`, `BotAIMessage`, `ChatKeyword`, `ChatKeywordMap`, `Music`, `MusicQueue`, `PlayerConnected`, `Pronouns`, `Servers`, `Words`) were created by pgloader with no sequence/default at all, despite genuinely being `IDENTITY` columns in SQL Server (confirmed via `sys.identity_columns`). Backfilled sequences for all 11.
- **1 foreign key** (`PetJournal` → `Pet`) failed to be created (cascading from `PetJournal` not existing yet at that point) and was recreated afterward.

## 2. Row count verification (Step 5)

All 57 tables match exactly — **0 mismatches**. Full detail: [backups/rowcount-verification.csv](../backups/rowcount-verification.csv).

This check ran against the complete, unmodified migration (audit tables included with their real row counts) *before* the archive step below — so it's a clean verification of what pgloader + remediation actually produced, not of a partially-emptied database.

## 3. Audit tables archived (per request)

`AuditButtonExecuted`, `AuditGameTrigger`, `AuditGuildJoined`, `AuditLog`, `AuditReactionAdded`, `AuditUserJoined`, `AuditUserLeft` — moved to a new `archive` schema in `discordbot` and truncated. Their table structure (and now-empty sequences) exist in Postgres; their actual row data remains only in SQL Server (captured in the Step 2 backup regardless). This ran *after* Step 5's verification confirmed their data had migrated correctly, so the emptying is a deliberate, tracked action, not a migration gap.

Stored procedures that write to these tables (`AddAudit`, `AddAuditButtonExecuted`, `AddAuditGameTrigger`, `AddAuditGuildJoined`, `AddAuditReactionAdded`, `AddAuditUserJoined`, `AddAuditUserLeft`) were **not** ported — see the stored-procedure note below.

## 4. Sequences reset (Step 6)

42 identity columns, all set from the SQL Server source `MAX(id)` (not the Postgres target) — this matters for the 7 now-empty archive tables, so a future import of that history won't collide with newly-generated IDs. Full list:

| Table.Column | Sequence | Source MAX(id) | Next value |
|---|---|---:|---:|
| archive.AuditButtonExecuted.ID | AuditButtonExecuted_ID_seq | 2 | 3 |
| archive.AuditGameTrigger.ID | AuditGameTrigger_ID_seq | 174 | 175 |
| archive.AuditGuildJoined.ID | AuditGuildJoined_ID_seq | 3 | 4 |
| archive.AuditLog.AuditLogID | AuditLog_AuditLogID_seq | 13153 | 13154 |
| archive.AuditReactionAdded.ID | AuditReactionAdded_ID_seq | 3 | 4 |
| archive.AuditUserJoined.ID | AuditUserJoined_ID_seq | 24 | 25 |
| archive.AuditUserLeft.ID | AuditUserLeft_ID_seq | 20 | 21 |
| public.Birthday.BirthdayID | Birthday_BirthdayID_seq | 108 | 109 |
| public.BotAIMessage.BotAIMessageID | BotAIMessage_BotAIMessageID_seq | 60 | 61 |
| public.ChallengePool.ChallengeID | ChallengePool_ChallengeID_seq | 19 | 20 |
| public.ChatKeyword.ID | ChatKeyword_ID_seq | 4603 | 4604 |
| public.ChatKeywordAlias.ID | ChatKeywordAlias_ID_seq | 2 | 3 |
| public.ChatKeywordMap.ID | ChatKeywordMap_ID_seq | 57 | 58 |
| public.Credits.CreditID | Credits_CreditID_seq | 156 | 157 |
| public.FishLog.LogID | FishLog_LogID_seq | 3 | 4 |
| public.ForgedCosmetics.ForgeID | ForgedCosmetics_ForgeID_seq | (empty) | 1 |
| public.GambleLog.LogID | GambleLog_LogID_seq | 1924 | 1925 |
| public.Investments.InvestmentID | Investments_InvestmentID_seq | 17 | 18 |
| public.JackpotEntries.EntryID | JackpotEntries_EntryID_seq | (empty) | 1 |
| public.JournalEntries.EntryID | JournalEntries_EntryID_seq | 4 | 5 |
| public.Music.MusicID | Music_MusicID_seq | 22860 | 22861 |
| public.MusicQueue.MusicQueueID | MusicQueue_MusicQueueID_seq | (empty) | 1 |
| public.Pet.PetID | Pet_PetID_seq | 126 | 127 |
| public.PetCosmetics.CosmeticID | PetCosmetics_CosmeticID_seq | 3 | 4 |
| public.PetEggs.EggID | PetEggs_EggID_seq | (empty) | 1 |
| public.PetJournal.JournalID | PetJournal_JournalID_seq | 696 | 697 |
| public.PetWordPuzzle.PuzzleID | PetWordPuzzle_PuzzleID_seq | 4056 | 4057 |
| public.PlayerConnected.PlayerID | PlayerConnected_PlayerID_seq | (empty) | 1 |
| public.PokerLobby.GameID | PokerLobby_GameID_seq | 1 | 2 |
| public.PokerPlayer.PlayerID | PokerPlayer_PlayerID_seq | 4 | 5 |
| public.PregnancyEvents.Id | PregnancyEvents_Id_seq | 3 | 4 |
| public.Pronouns.ID | Pronouns_ID_seq | 7 | 8 |
| public.Quotes.QuoteId | Quotes_QuoteId_seq | 1 | 2 |
| public.Reminders.ReminderID | Reminders_ReminderID_seq | (empty) | 1 |
| public.Servers.ServerID | Servers_ServerID_seq | 24 | 25 |
| public.StockHistory.HistoryID | StockHistory_HistoryID_seq | 400374 | 400375 |
| public.StockHoldings.HoldingID | StockHoldings_HoldingID_seq | 57 | 58 |
| public.StockTransactions.TxID | StockTransactions_TxID_seq | 113 | 114 |
| public.UserActiveEffects.EffectID | UserActiveEffects_EffectID_seq | 256 | 257 |
| public.UserDailyChallenges.ID | UserDailyChallenges_ID_seq | 11 | 12 |
| public.UserInventory.InventoryID | UserInventory_InventoryID_seq | 38 | 39 |
| public.Words.ID | Words_ID_seq | 4999 | 5000 |

Note: several sequences (`Birthday`, `Music`, `StockHistory`, `Words`, etc.) have a next-value well above their current row count — that's expected, from historical deletes in the source, not a bug.

## 5. Precision/scale check (Step 7)

27 decimal columns checked. **20 match exactly. 7 differ** — all 7 are in tables pgloader created directly (not ones rebuilt by hand), where it dropped the precision/scale constraint entirely rather than replicating it:

| Table.Column | Source | Target |
|---|---|---|
| ChallengePool.RewardAmount | decimal(38,0) | numeric (unconstrained) |
| Credits.Balance | decimal(38,0) | numeric (unconstrained) |
| Credits.LifetimeEarned | decimal(38,0) | numeric (unconstrained) |
| Credits.TotalEarned | decimal(38,0) | numeric (unconstrained) |
| Credits.TotalSpent | decimal(38,0) | numeric (unconstrained) |
| PassiveJackpot.Pool | decimal(20,0) | numeric (unconstrained) |
| StockHoldings.AvgBuyPrice | decimal(18,2) | numeric (unconstrained) |

**No existing data was truncated or rounded** — unconstrained `numeric` can hold at least as much precision as the source allowed, in either direction. But it's a loosened constraint: a future insert with fractional cents would now silently succeed in these 7 columns where SQL Server would have rejected it. Recommend tightening these to match source precision/scale before relying on this schema — I left this for your review rather than changing it unilaterally, since Step 7 was specified as a report, not a fix.

All other 20 decimal columns (including every column pgloader had to route through my hand-written DDL) match source precision/scale exactly.

## 6. Stored procedures — not migrated (scope note)

196 stored procedures exist in the source and were **not** translated. pgloader migrates schema and data, not procedural logic, and T-SQL → PL/pgSQL has no automatic path. This was flagged before Step 1 started and stayed out of scope for this pass, consistent with Step 8's instruction to leave all stored-procedure-calling code untouched. Porting them (or replacing their call sites with EF Core/LINQ) is separate future work.

## 7. Unrelated finding: pre-existing build issue

`dotnet build` on the full solution fails with `NU1605` — `DiscordBot.Tests.csproj` pins `Microsoft.Data.SqlClient` to `6.0.2` directly while `DiscordBot.csproj` requires `>= 7.0.2`. Confirmed via `git diff`/`git log` that this predates this session entirely (last touched 2026-04-29) and was not introduced by anything here. `DiscordBot.csproj` alone builds cleanly (0 errors). Left unfixed since it's outside this migration's scope — flagging for your awareness.

## 8. Artifacts produced

- [tools/pgloader.jar](../tools/pgloader.jar) — pgloader v4.0.0
- [backups/DiscordBot_pre-migration_2026-08-23.bak](../backups/DiscordBot_pre-migration_2026-08-23.bak) — full source backup (verified restorable)
- [backups/source-schema-columns.csv](../backups/source-schema-columns.csv) — full source column metadata
- [backups/create-missing-tables.sql](../backups/create-missing-tables.sql), [backups/fix-missing-sequences.sql](../backups/fix-missing-sequences.sql), [backups/fix-chatkeywordmap-seq.sql](../backups/fix-chatkeywordmap-seq.sql), [backups/fix-fk.sql](../backups/fix-fk.sql), [backups/archive-audit-tables.sql](../backups/archive-audit-tables.sql), [backups/reset-sequences.sql](../backups/reset-sequences.sql) — every corrective/setup SQL script run
- [backups/rowcount-verification.csv](../backups/rowcount-verification.csv) — Step 5 detail
- [migration-log.txt](../migration-log.txt) — full pgloader output + corrective-action narrative
- [Models/Generated/](../Models/Generated/) — 57 scaffolded EF Core entity classes
- [Data/DiscordbotContext.cs](../Data/DiscordbotContext.cs) — scaffolded DbContext

## 9. Needs your attention

1. Postgres superuser password is still only in `%TEMP%\pg_super_pw.txt` — move it somewhere durable.
2. Decide whether to tighten the 7 unconstrained `numeric` columns (§5) to match source precision/scale.
3. Decide whether/when to port the 196 stored procedures, and what happens to the 7 audit-related ones given the archived tables are now empty in Postgres.
4. Pre-existing `Microsoft.Data.SqlClient` version conflict in `DiscordBot.Tests.csproj` (§7) — unrelated to this migration but blocks a full-solution build.
5. Nothing in the running application has been changed — `Models/Generated` and `Data/DiscordbotContext.cs` are new, unwired files; the bot still runs entirely against SQL Server.

Stopping here per your instructions — no further changes made.
