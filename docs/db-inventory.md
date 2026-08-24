# SQL Server Database Inventory — DiscordBot

Generated 2026-08-23 by querying the live database directly (read-only — `sys.schemas`, `sys.tables`, `sys.partitions`, `sys.columns`, `sys.procedures`). No data was queried or modified.

- **Server:** `localhost` (SQL Server 2022, build `16.00.4175`)
- **Database:** `DiscordBot`
- **Connection string source:** `Secrets/secrets.json` (`discordBotConnStr`)

Row counts come from `sys.partitions` (`rows` where `index_id IN (0,1)`), which reflects SQL Server's maintained row-count metadata rather than a live `COUNT(*)` per table — standard practice for a fast, non-locking inventory. Schemas with no tables or procedures (the built-in `guest`, `INFORMATION_SCHEMA`, `sys`, and fixed database-role schemas) are omitted below since they carry no objects.

## 1. Summary

| Metric | Count |
|---|---|
| Schemas (in use) | 1 |
| Tables | 57 |
| Stored procedures | 196 |
| Total rows across all tables | 168,240 |

## 2. Schema: `dbo`

57 tables.

| Table | Row Count | Column Count |
|---|---:|---:|
| AuditButtonExecuted | 2 | 5 |
| AuditGameTrigger | 174 | 5 |
| AuditGuildJoined | 3 | 4 |
| AuditLog | 9,973 | 5 |
| AuditReactionAdded | 3 | 6 |
| AuditUserJoined | 24 | 4 |
| AuditUserLeft | 20 | 4 |
| Birthday | 27 | 6 |
| BlackjackGame | 0 | 8 |
| BotAIMessage | 10 | 7 |
| ChallengePool | 19 | 7 |
| ChatKeyword | 4,602 | 5 |
| ChatKeywordAlias | 2 | 6 |
| ChatKeywordMap | 57 | 6 |
| Credits | 148 | 11 |
| FishLog | 3 | 7 |
| ForgedCosmetics | 0 | 10 |
| GambleLog | 1,924 | 8 |
| GuildAutoRole | 1 | 3 |
| GuildQuoteConfig | 1 | 2 |
| Investments | 17 | 8 |
| JackpotEntries | 0 | 5 |
| JournalEntries | 4 | 4 |
| JournalSubscriptions | 1 | 5 |
| Music | 19,385 | 8 |
| MusicQueue | 0 | 8 |
| NamesReference | 90,538 | 1 |
| NamesStaging | 31,904 | 3 |
| PassiveJackpot | 2 | 2 |
| PassiveJackpotContributors | 0 | 2 |
| Pet | 125 | 26 |
| PetCosmetics | 3 | 5 |
| PetEggs | 0 | 16 |
| PetJournal | 559 | 5 |
| PetWordPuzzle | 4,056 | 5 |
| PlayerConnected | 0 | 6 |
| PokerLobby | 1 | 9 |
| PokerPlayer | 4 | 4 |
| PregnancyEvents | 3 | 8 |
| Pronouns | 7 | 2 |
| Quotes | 1 | 11 |
| Reminders | 0 | 5 |
| ScrambleGame | 0 | 6 |
| ServerPassiveJackpot | 1 | 3 |
| Servers | 24 | 10 |
| StockHistory | 250 | 4 |
| StockHoldings | 17 | 6 |
| Stocks | 25 | 10 |
| StockTransactions | 113 | 9 |
| TriviaMessage | 3 | 3 |
| UserActiveEffects | 8 | 7 |
| UserDailyChallenges | 11 | 11 |
| UserInventory | 30 | 6 |
| Users | 218 | 9 |
| UsersScheduledKeyword | 36 | 3 |
| WordleGame | 0 | 6 |
| Words | 3,901 | 2 |

## 3. Stored Procedures

196 procedures, all in schema `dbo`.

| Schema | Procedure |
|---|---|
| dbo | AddActiveEffect |
| dbo | AddAudit |
| dbo | AddAuditButtonExecuted |
| dbo | AddAuditGameTrigger |
| dbo | AddAuditGuildJoined |
| dbo | AddAuditReactionAdded |
| dbo | AddAuditUserJoined |
| dbo | AddAuditUserLeft |
| dbo | AddBirthday |
| dbo | AddBlackjackGame |
| dbo | AddBotAIMessage |
| dbo | AddChatKeyword |
| dbo | AddChatKeywordAlias |
| dbo | AddChatKeywordMap |
| dbo | AddCredits |
| dbo | AddFishLog |
| dbo | AddForgedCosmetic |
| dbo | AddGambleLog |
| dbo | AddInvestment |
| dbo | AddJackpotEntry |
| dbo | AddLifetimeEarned |
| dbo | AddMusic |
| dbo | AddMusicQueue |
| dbo | AddPet |
| dbo | AddPetJournalEntry |
| dbo | AddPetWordPuzzle |
| dbo | AddPetXP |
| dbo | AddPlayerConnected |
| dbo | AddPokerPlayer |
| dbo | AddReminder |
| dbo | AddScrambleGame |
| dbo | AddServer |
| dbo | AddToInventory |
| dbo | AddTriviaMessage |
| dbo | AddUser |
| dbo | AddUsersScheduledKeyword |
| dbo | AddWordleGame |
| dbo | ApplyEggStats |
| dbo | ApplyStockTick |
| dbo | BuyStock |
| dbo | ClaimChallengeBonus |
| dbo | ClaimInvestment |
| dbo | ClaimPassiveJackpot |
| dbo | ClaimPetPuzzle |
| dbo | CleanExpiredEffects |
| dbo | ClearJackpot |
| dbo | ClearPetExplore |
| dbo | ClearPregnancy |
| dbo | ConsumeActiveEffect |
| dbo | CreatePetEgg |
| dbo | CreatePokerGame |
| dbo | CreatePregnancy |
| dbo | DeactiveServer |
| dbo | DecayPetStats |
| dbo | DeductCredits |
| dbo | DeductFromInventory |
| dbo | DeleteBlackjackGame |
| dbo | DeleteBotAIMessage |
| dbo | DeleteChatKeyword |
| dbo | DeleteChatKeywordAlias |
| dbo | DeleteChatKeywordURL |
| dbo | DeleteGuildAutoRole |
| dbo | DeleteJournalSubscription |
| dbo | DeleteMusicQueue |
| dbo | DeleteMusicQueueAll |
| dbo | DeletePet |
| dbo | DeletePlayerConnected |
| dbo | DeletePokerGame |
| dbo | DeleteScrambleGame |
| dbo | DeleteTriviaMessage |
| dbo | DeleteUser |
| dbo | DeleteWordleGame |
| dbo | DrawPassiveJackpot |
| dbo | EnsureCreditAccount |
| dbo | FeedPassiveJackpot |
| dbo | GetActiveEffect |
| dbo | GetActivePet |
| dbo | GetActivePetPuzzle |
| dbo | GetActivePregnancy |
| dbo | GetAIJSONImageReturn |
| dbo | GetAllActiveEffects |
| dbo | GetAllActivePets |
| dbo | GetAllServerUsers |
| dbo | GetAllStocks |
| dbo | GetBlackjackByUser |
| dbo | GetBotAIMessage |
| dbo | GetChatAction |
| dbo | GetChatKeywordAliases |
| dbo | GetChatKeywordAll |
| dbo | GetChatKeywordInfo |
| dbo | GetChatKeywordMap |
| dbo | GetChatKeywordRecent |
| dbo | GetChatKeywordsByServer |
| dbo | GetCreditLeaderboard |
| dbo | GetCredits |
| dbo | GetDailyLoss |
| dbo | GetDueChildSupport |
| dbo | GetDueJournalReminders |
| dbo | GetDueReminders |
| dbo | GetEggByID |
| dbo | GetEmbedBroken |
| dbo | GetFishStats |
| dbo | GetForgedCosmetics |
| dbo | GetGambleStats |
| dbo | GetGuildAutoRole |
| dbo | GetGuildQuoteConfig |
| dbo | GetHolding |
| dbo | GetInventoryItem |
| dbo | GetJackpotEntries |
| dbo | GetJackpotTotal |
| dbo | GetJournalStatus |
| dbo | GetKeywordNSFW |
| dbo | GetMaturePregnancies |
| dbo | GetMusicQueue |
| dbo | GetMusicQueueByTrack |
| dbo | GetOrAssignDailyChallenges |
| dbo | GetPassiveJackpot |
| dbo | GetPendingEggs |
| dbo | GetPendingInvestment |
| dbo | GetPetByID |
| dbo | GetPetCosmetics |
| dbo | GetPetExplore |
| dbo | GetPetJournal |
| dbo | GetPetLeaderboard |
| dbo | GetPetsByUser |
| dbo | GetPetWordPuzzle |
| dbo | GetPlayerConnected |
| dbo | GetPokerGame |
| dbo | GetPokerGameById |
| dbo | GetPokerPlayers |
| dbo | GetPortfolio |
| dbo | GetPronouns |
| dbo | GetPuzzleClaimedStatus |
| dbo | GetQuotesByUser |
| dbo | GetRandomQuote |
| dbo | GetRandomWord |
| dbo | GetScrambleByChannel |
| dbo | GetServerByID |
| dbo | GetServers |
| dbo | GetStockDetail |
| dbo | GetStockHistory |
| dbo | GetStockTransactions |
| dbo | GetStreakInfo |
| dbo | GetTodaysBirthdays |
| dbo | GetTotalForged |
| dbo | GetTrivia |
| dbo | GetTriviaMessage |
| dbo | GetTriviaToken |
| dbo | GetUserBornChildCount |
| dbo | GetUserInventory |
| dbo | GetUsersScheduledKeyword |
| dbo | GetUsersScheduledKeywords |
| dbo | GetUserStats |
| dbo | GetVolume |
| dbo | GetWordleByChannel |
| dbo | HalveAllBalances |
| dbo | HatchEgg |
| dbo | IncrementChallengeProgress |
| dbo | InsertQuote |
| dbo | LogJournalEntry |
| dbo | MarkKeywordNSFW |
| dbo | MarkPregnancyBorn |
| dbo | RemovePetCosmetic |
| dbo | RenameChatKeyword |
| dbo | RenamePet |
| dbo | ResetLifetimeEarned |
| dbo | ResetStockDayRange |
| dbo | SearchQuotes |
| dbo | SellStock |
| dbo | SetActivePet |
| dbo | SetPetCosmetic |
| dbo | SetPetExplore |
| dbo | TickStockPrices |
| dbo | ToggleAnnouncements |
| dbo | UpdateBlackjackGame |
| dbo | UpdateBlackjackMessageID |
| dbo | UpdateBrokenEmbed |
| dbo | UpdateChildSupportDate |
| dbo | UpdateDailyStreak |
| dbo | UpdatePetAccessory |
| dbo | UpdatePetBio |
| dbo | UpdatePetPicture |
| dbo | UpdatePetStats |
| dbo | UpdatePokerDeck |
| dbo | UpdatePokerMessage |
| dbo | UpdatePokerStatus |
| dbo | UpdateQuoteArchiveUrl |
| dbo | UpdateUserLastSeen |
| dbo | UpdateUsersScheduledKeywordRequeue |
| dbo | UpdateVolume |
| dbo | UpdateWordleGame |
| dbo | UpsertGuildAutoRole |
| dbo | UpsertGuildQuoteConfig |
| dbo | UpsertJournalSubscription |
| dbo | WakePet |
| dbo | ZeroAllBalances |
