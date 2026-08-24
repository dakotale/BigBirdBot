# Stored Procedure Call Site Inventory

Generated 2026-08-23, Stage 1 of the EF Core / PostgreSQL conversion. Read-only discovery — nothing converted yet.

All SQL Server access in the application goes through one wrapper class, `Constants.StoredProcedure` ([Constants/StoredProcedure.cs](../Constants/StoredProcedure.cs)), with exactly two methods:
- `Select(connStr, spName, parameters)` → `DataTable` (procs that return rows)
- `UpdateCreate(connStr, spName, parameters)` → `void` (procs that don't return rows)

No other data-access pattern exists in the app code (no raw `SqlCommand`, no Dapper) — confirmed by an initial file-level scan before the detailed pass.

## Summary

| Area | Files | Call sites |
|---|---:|---:|
| Program.cs (top-level event handlers, scheduler) | 1 | 55 |
| Services/ + Helper/ | 6 | 22 |
| SlashCommands (A–K) | 15 (2 have zero call sites) | 91 |
| SlashCommands (O–W) | 12 | 121 |
| **Total** | **34** | **289** |

## Findings — status

Findings 1 and 2 below were fixed on 2026-08-24, before Stage 2: two missing stored procedures were written and deployed (`GetScheduledEventUsers`, `DeleteUsersScheduledKeyword`), `UpdateUsersScheduledKeywordRequeue` was corrected (it had a real column-order bug in its no-existing-rows branch that would have written garbled data — a datetime into `UserID`, the user's ID into `ChatKeyword`, `0` into `ScheduledDateTime` — now fixed and verified with a live smoke test), and all 5 affected C# call sites (`Program.cs`, `Services/Blackjack.cs` x2, `SlashCommands/Poker.cs`, `SlashCommands/Gambling.cs`) were switched from `.UpdateCreate()` to `.Select()` to match what those procs actually return. Full detail kept below for the record.

## Findings that needed attention before/during conversion

1. **Two call sites reference stored procedures that don't exist in the database.** Verified directly against `sys.procedures` on the live SQL Server instance (zero rows returned), and confirmed there's no `.sql` definition anywhere in the `SQL/` folder either — these were never deployed, not just out of sync:
   - [SlashCommands/OwnerCommands.cs:84](../SlashCommands/OwnerCommands.cs) — `stored.Select(..., "GetScheduledEventUsers", ...)` in `HandleServerList`
   - [SlashCommands/Keyword.cs:594](../SlashCommands/Keyword.cs) — `_sp.UpdateCreate(..., "DeleteUsersScheduledKeyword", ...)` in `ScheduleCommands.HandleRemoveAsync`

   Both would throw `Could not find stored procedure` if ever actually hit at runtime — dead or broken code paths, not something this migration introduced. I'll need you to clarify intent for these two specifically before Stage 4 converts them (see questions at the end).

2. **The same stored procedure is called inconsistently — `Select` in one place, `UpdateCreate` in another:**
   - `UpdateUsersScheduledKeywordRequeue`: `Select` at Program.cs:1715 and Keyword.cs:652, but `UpdateCreate` at Program.cs:1789
   - `IncrementChallengeProgress`: `Select` at Economy.cs:163, but `UpdateCreate` at Blackjack.cs:113, Blackjack.cs:405, Poker.cs:484, Gambling.cs:1676

   Since `Select` returns a `DataTable` and `UpdateCreate` returns nothing, these two call patterns are almost certainly consuming the same proc differently (one path reads a result, the other ignores it) — worth deciding what the EF Core equivalent should return before converting all the call sites for these two procs, so they end up consistent.

3. **Pattern, not a bug:** many procs that are semantically writes (`AddCredits`, `DeductCredits`, `UpdateDailyStreak`, `ClaimChallengeBonus`, `BuyStock`, `SellStock`, `InsertQuote`, `CreatePetEgg`, `HatchEgg`, `AddForgedCosmetic`, `AddPetXP`, `DrawPassiveJackpot`, `ClaimPassiveJackpot`, `AddChatKeywordAlias`, `AddUsersScheduledKeyword`, `LogJournalEntry`, `ToggleAnnouncements`, `UpdateBrokenEmbed`, `CreatePokerGame`) are called via `.Select()` rather than `.UpdateCreate()`. That's consistent within each proc (not the same issue as #2) — these procs do the write *and* return a result row (e.g. the updated balance, a generated ID). The EF Core replacement for each will need to both persist the change and return a value, not just call `SaveChanges()`.

4. `Duel.cs` and `Games.cs` each declare a `private readonly StoredProcedure _sp = new();` field that is never actually called anywhere in the file — dead field, zero call sites. Not something to convert, just noting it exists.

5. `Program.cs:2055` (`AddAttachmentsAsync`) passes a local `connStr` parameter instead of the usual `Constants.discordBotConnStr` used everywhere else — worth confirming it resolves to the same value before assuming this call site converts identically to its siblings.

## Full call site listing, by file

### Program.cs (55: 33 Select, 22 UpdateCreate)

| Line | Method | Call | Proc |
|---:|---|---|---|
| 312 | OnUserLeftAsync | UpdateCreate | DeleteUser |
| 333 | AssignAutoRoleAsync | Select | GetGuildAutoRole |
| 381 | ProcessJoinedGuildAsync | Select | GetServers |
| 388 | ProcessJoinedGuildAsync | UpdateCreate | AddServer |
| 422 | AddUserToDatabase | UpdateCreate | AddUser |
| 444 | OnButtonExecutedAsync | Select | GetPronouns |
| 518 | TryHandleScrambleGuessAsync | Select | GetScrambleByChannel |
| 531 | TryHandleScrambleGuessAsync | UpdateCreate | DeleteScrambleGame |
| 557 | TryHandleWordleGuessAsync | Select | GetWordleByChannel |
| 580 | TryHandleWordleGuessAsync | UpdateCreate | DeleteWordleGame |
| 583 | TryHandleWordleGuessAsync | UpdateCreate | UpdateWordleGame |
| 627 | OnMessageReceivedAsync | UpdateCreate | UpdateUserLastSeen |
| 636 | OnMessageReceivedAsync | Select | GetServerByID |
| 650 | OnMessageReceivedAsync | Select | GetEmbedBroken |
| 672 | OnMessageReceivedAsync | Select | GetActivePet |
| 693 | OnMessageReceivedAsync | Select | AddPetXP |
| 735 | OnMessageReceivedAsync | Select | GetActivePetPuzzle |
| 751 | OnMessageReceivedAsync | UpdateCreate | ClaimPetPuzzle |
| 759 | OnMessageReceivedAsync | Select | GetActivePet |
| 777 | OnMessageReceivedAsync | Select | AddPetXP |
| 842 | OnMessageReceivedAsync | Select | GetChatAction |
| 905 | HandlePrefixCommandAsync | Select | GetChatKeywordMap |
| 966 | StoreChatKeyword | UpdateCreate | AddChatKeyword |
| 1033 | SendChatActionsAsync | UpdateCreate | DeleteChatKeywordURL |
| 1079 | OnUserVoiceStateUpdatedAsync | UpdateCreate | DeletePlayerConnected |
| 1090 | OnUserVoiceStateUpdatedAsync | UpdateCreate | DeletePlayerConnected |
| 1091 | OnUserVoiceStateUpdatedAsync | UpdateCreate | DeleteMusicQueueAll |
| 1161 | TryMarkNsfwAsync | Select | GetKeywordNSFW |
| 1166 | TryMarkNsfwAsync | Select | MarkKeywordNSFW |
| 1196 | HandleTriviaReactionAsync | Select | GetTriviaMessage |
| 1228 | HandleTriviaReactionAsync | UpdateCreate | DeleteTriviaMessage |
| 1279 | RunSchedulerAsync | Select | GetDueReminders |
| 1299 | RunSchedulerAsync | Select | GetDueJournalReminders |
| 1325 | RunSchedulerAsync | Select | GetTodaysBirthdays |
| 1365 | RunSchedulerAsync | Select | DecayPetStats |
| 1407 | RunSchedulerAsync | Select | GetActivePet |
| 1423 | RunSchedulerAsync | Select | AddPetXP |
| 1436 | RunSchedulerAsync | Select | GetRandomWord |
| 1448 | RunSchedulerAsync | UpdateCreate | AddPetWordPuzzle |
| 1480 | RunSchedulerAsync (Task.Run) | Select | GetPetWordPuzzle |
| 1505 | RunSchedulerAsync (Task.Run) | Select | GetPetWordPuzzle |
| 1534 | RunSchedulerAsync (Task.Run) | Select | GetPuzzleClaimedStatus |
| 1564 | RunSchedulerAsync | Select | GetJackpotTotal |
| 1576 | RunSchedulerAsync | Select | GetJackpotEntries |
| 1602 | RunSchedulerAsync | UpdateCreate | ClearJackpot |
| 1631 | RunSchedulerAsync | Select | DrawPassiveJackpot |
| 1678 | RunScheduledKeywordsAsync | Select | GetUsersScheduledKeyword |
| 1715 | RunScheduledKeywordsAsync | Select | UpdateUsersScheduledKeywordRequeue |
| 1768 | RunScheduledKeywordsAsync | UpdateCreate | DeleteChatKeywordURL |
| 1789 | RunScheduledKeywordsAsync | UpdateCreate | UpdateUsersScheduledKeywordRequeue |
| 1842 | TickStockPrices | UpdateCreate | CleanExpiredEffects |
| 1845 | TickStockPrices | Select | GetAllStocks |
| 1858 | TickStockPrices | UpdateCreate | ApplyStockTick |
| 1887 | ResetStockDayRange | UpdateCreate | ResetStockDayRange |
| 2055 | AddAttachmentsAsync | UpdateCreate | AddChatKeyword |

### Services/ + Helper/ (22: 6 Select, 16 UpdateCreate — wait, see table)

| File:Line | Method | Call | Proc |
|---|---|---|---|
| Services/Blackjack.cs:65 | HandleBlackjackAsync | Select | GetBlackjackByUser |
| Services/Blackjack.cs:113 | HandleBlackjackAsync | UpdateCreate | IncrementChallengeProgress |
| Services/Blackjack.cs:128 | HandleBlackjackAsync | UpdateCreate | AddBlackjackGame |
| Services/Blackjack.cs:144 | HandleBlackjackAsync | UpdateCreate | UpdateBlackjackMessageID |
| Services/Blackjack.cs:282 | OnPlayAgainAsync | UpdateCreate | DeleteBlackjackGame |
| Services/Blackjack.cs:316 | OnPlayAgainAsync | UpdateCreate | AddBlackjackGame |
| Services/Blackjack.cs:405 | ResolveStandAsync | UpdateCreate | IncrementChallengeProgress |
| Services/Blackjack.cs:526 | LoadGame | Select | GetBlackjackByUser |
| Services/Blackjack.cs:544 | SaveGame | UpdateCreate | UpdateBlackjackGame |
| Services/Blackjack.cs:556 | EndGame | UpdateCreate | DeleteBlackjackGame |
| Services/InteractionHandlerService.cs:145 | RestorePlayersAsync | Select | GetPlayerConnected |
| Services/InteractionHandlerService.cs:169 | RestorePlayersAsync | Select | GetMusicQueue |
| Services/InteractionHandlerService.cs:205 | RestorePlayersAsync | Select | GetVolume |
| Services/CommandHandlingService.cs:67 | MessageReceivedAsync | Select | GetServerByID |
| Helper/ShopHelper.cs:342 | HasItem | Select | GetInventoryItem |
| Helper/ShopHelper.cs:361 | ConsumeItem | Select | DeductFromInventory |
| Helper/ShopHelper.cs:379 | HasActiveEffect | Select | GetActiveEffect |
| Helper/ShopHelper.cs:398 | ConsumeActiveEffect | Select | ConsumeActiveEffect |
| Helper/ShopHelper.cs:416 | SetActiveEffect | UpdateCreate | AddActiveEffect |
| Helper/ShopHelper.cs:433 | GetEffectStack | Select | GetActiveEffect |
| Helper/ServerHelper.cs:18 | GetServerInfo | Select | GetServerByID |
| Helper/CustomPlayer.cs:69 | NotifyTrackEndedAsync | UpdateCreate | DeleteMusicQueue |

### SlashCommands A–K (91: 58 Select, 33 UpdateCreate)

| File:Line | Method | Call | Proc |
|---|---|---|---|
| AICommands.cs:77 | HandleChatAsync | UpdateCreate | DeleteBotAIMessage |
| AICommands.cs:87 | HandleChatAsync | Select | GetBotAIMessage |
| AICommands.cs:106 | HandleChatAsync | UpdateCreate | AddBotAIMessage |
| AICommands.cs:113 | HandleChatAsync | UpdateCreate | AddBotAIMessage |
| AICommands.cs:198 | HandleAiByAttachmentAsync | Select | GetAIJSONImageReturn |
| AdminCommands.cs:27 | HandlePronounAsync | Select | GetPronouns |
| AdminCommands.cs:109 | HandleAnnouncementsAsync | Select | ToggleAnnouncements |
| Audio.cs:250 | VolumeAsync | UpdateCreate | UpdateVolume |
| Audio.cs:443 | ClearQueueAsync | UpdateCreate | DeleteMusicQueueAll |
| Audio.cs:912 | AdjustVolumeAsync | UpdateCreate | UpdateVolume |
| Audio.cs:1068 | GetVolume | Select | GetVolume |
| Audio.cs:1084 | AddPlayerConnected | UpdateCreate | AddPlayerConnected |
| Audio.cs:1097 | DeletePlayerConnected | UpdateCreate | DeletePlayerConnected |
| Audio.cs:1098 | DeletePlayerConnected | UpdateCreate | DeleteMusicQueueAll |
| Audio.cs:1106 | AddMusicTable | UpdateCreate | AddMusic |
| AutoRoleCommands.cs:28 | HandleSetAsync | UpdateCreate | UpsertGuildAutoRole |
| AutoRoleCommands.cs:46 | HandleClearAsync | UpdateCreate | DeleteGuildAutoRole |
| AutoRoleCommands.cs:63 | HandleStatusAsync | Select | GetGuildAutoRole |
| Breeding.cs:58 | HandleBreedAsync | Select | GetPetByID |
| Breeding.cs:63 | HandleBreedAsync | Select | GetPetByID |
| Breeding.cs:108 | HandleBreedAsync | Select | GetPendingEggs |
| Breeding.cs:144 | HandleBreedAsync | Select | CreatePetEgg |
| Breeding.cs:197 | HandleEggsAsync | Select | GetPendingEggs |
| Breeding.cs:253 | HandleHatchEggAsync | Select | GetEggByID |
| Breeding.cs:284 | HandleHatchEggAsync | Select | GetPetsByUser |
| Breeding.cs:305 | HandleHatchEggAsync | UpdateCreate | AddPet |
| Breeding.cs:316 | HandleHatchEggAsync | Select | GetPetsByUser |
| Breeding.cs:330 | HandleHatchEggAsync | UpdateCreate | ApplyEggStats |
| Breeding.cs:342 | HandleHatchEggAsync | Select | HatchEgg |
| Challenges.cs:44 | HandleChallengesAsync | Select | GetOrAssignDailyChallenges |
| Challenges.cs:96 | HandleChallengesAsync | Select | ClaimChallengeBonus |
| Challenges.cs:159 | HandleStatsAsync | Select | GetGambleStats |
| Challenges.cs:165 | HandleStatsAsync | Select | GetFishStats |
| Challenges.cs:171 | HandleStatsAsync | Select | GetCredits |
| Economy.cs:44 | HandleBalanceAsync | Select | GetCredits |
| Economy.cs:89 | HandleDailyAsync | Select | GetCredits |
| Economy.cs:104 | HandleDailyAsync | Select | GetStreakInfo |
| Economy.cs:127 | HandleDailyAsync | Select | UpdateDailyStreak |
| Economy.cs:163 | HandleDailyAsync | Select | IncrementChallengeProgress |
| Economy.cs:186 | HandleDailyAsync | Select | ClaimChallengeBonus |
| Economy.cs:237 | HandleWorkAsync | Select | GetCredits |
| Economy.cs:363 | HandleDonateAsync | Select | GetCreditLeaderboard |
| Economy.cs:418 | HandleLeaderboardAsync | Select | GetCreditLeaderboard |
| Economy.cs:456 | HandlePrestigeAsync | Select | GetCredits |
| Economy.cs:542 | EnsureAccount | UpdateCreate | EnsureCreditAccount |
| Economy.cs:556 | AddCredits | Select | AddCredits |
| Economy.cs:567 | AddCredits | UpdateCreate | AddLifetimeEarned |
| Economy.cs:586 | DeductCredits | Select | DeductCredits |
| Economy.cs:603 | GetBalance | Select | GetCredits |
| FoodAutocompleteHandler.cs:31 | GenerateSuggestionsAsync | Select | GetActivePet |
| Forge.cs:93 | HandleForgeListAsync | Select | GetActivePet |
| Forge.cs:105 | HandleForgeListAsync | Select | GetForgedCosmetics |
| Forge.cs:216 | HandleForge | Select | GetActivePet |
| Forge.cs:241 | HandleForge | Select | AddForgedCosmetic |
| Gambling.cs:701 | HandleJackpotAsync | Select | GetJackpotTotal |
| Gambling.cs:711 | HandleJackpotAsync | Select | GetPassiveJackpot |
| Gambling.cs:746 | HandleJackpotAsync | UpdateCreate | AddJackpotEntry |
| Gambling.cs:754 | HandleJackpotAsync | Select | GetJackpotTotal |
| Gambling.cs:780 | HandleGambleStatsAsync | Select | GetGambleStats |
| Gambling.cs:1001 | HandleFishAsync | UpdateCreate | AddFishLog |
| Gambling.cs:1185 | HandleInvestAsync | Select | GetPendingInvestment |
| Gambling.cs:1205 | HandleInvestAsync | UpdateCreate | ClaimInvestment |
| Gambling.cs:1266 | HandleInvestAsync | UpdateCreate | AddInvestment |
| Gambling.cs:1320 | ValidateBet | Select | GetDailyLoss |
| Gambling.cs:1441 | ApplyGamble | UpdateCreate | FeedPassiveJackpot |
| Gambling.cs:1480 | TryClaimPassiveJackpotAsync | Select | GetPassiveJackpot |
| Gambling.cs:1490 | TryClaimPassiveJackpotAsync | Select | ClaimPassiveJackpot |
| Gambling.cs:1676 | TrackChallenge | UpdateCreate | IncrementChallengeProgress |
| Gambling.cs:1691 | LogGamble | UpdateCreate | AddGambleLog |
| Interaction.cs:28 | HandleTrivia | Select | GetTriviaToken |
| Interaction.cs:41 | HandleTrivia | Select | GetTrivia |
| Interaction.cs:89 | HandleTrivia | UpdateCreate | AddTriviaMessage |
| JournalCommands.cs:49 | HandleSubscribeAsync | UpdateCreate | UpsertJournalSubscription |
| JournalCommands.cs:73 | HandleUnsubscribeAsync | UpdateCreate | DeleteJournalSubscription |
| JournalCommands.cs:92 | HandleDoneAsync | Select | LogJournalEntry |
| JournalCommands.cs:156 | HandleStatusAsync | Select | GetJournalStatus |
| Keyword.cs:42 | Keyword.HandleAddAsync | UpdateCreate | AddChatKeywordMap |
| Keyword.cs:77 | Keyword.HandleDeleteAsync | UpdateCreate | DeleteChatKeyword |
| Keyword.cs:104 | Keyword.HandleRenameAsync | UpdateCreate | RenameChatKeyword |
| Keyword.cs:161 | AliasCommands.HandleAddAsync | Select | AddChatKeywordAlias |
| Keyword.cs:202 | AliasCommands.HandleDeleteAsync | UpdateCreate | DeleteChatKeywordAlias |
| Keyword.cs:227 | AliasCommands.HandleListAsync | Select | GetChatKeywordAliases |
| Keyword.cs:263 | Keyword.HandleInfoAsync | Select | GetChatKeywordInfo |
| Keyword.cs:278 | Keyword.HandleInfoAsync | Select | GetChatKeywordRecent |
| Keyword.cs:306 | Keyword.HandleListAsync | Select | GetChatKeywordsByServer |
| Keyword.cs:445 | AttachmentCommands.HandleBulkAddAsync | UpdateCreate | AddChatKeyword |
| Keyword.cs:510 | UrlCommands.HandleDeleteAsync | UpdateCreate | DeleteChatKeywordURL |
| Keyword.cs:549 | ScheduleCommands.HandleAddAsync | Select | AddUsersScheduledKeyword |
| Keyword.cs:594 | ScheduleCommands.HandleRemoveAsync | UpdateCreate | **DeleteUsersScheduledKeyword (proc does not exist — see finding #1)** |
| Keyword.cs:615 | ScheduleCommands.HandleListAsync | Select | GetUsersScheduledKeywords |
| Keyword.cs:652 | ScheduleCommands.HandleRequeueAsync | Select | UpdateUsersScheduledKeywordRequeue |

*(Duel.cs and Games.cs: zero call sites — `_sp` field declared but unused in both.)*

### SlashCommands O–W (121: 62 Select, 59 UpdateCreate)

| File:Line | Class.Method | Call | Proc |
|---|---|---|---|
| OwnerCommands.cs:34 | OwnerCommands.HandleAnnouncement | Select | GetServers |
| OwnerCommands.cs:84 | OwnerCommands.HandleServerList | Select | **GetScheduledEventUsers (proc does not exist — see finding #1)** |
| OwnerCommands.cs:103 | OwnerCommands.HandlePlayersConnected | Select | GetPlayerConnected |
| OwnerCommands.cs:141 | OwnerCommands.HandlePopulateAllUserCommand | Select | GetServers |
| OwnerCommands.cs:153 | OwnerCommands.HandlePopulateAllUserCommand | UpdateCreate | AddUser |
| OwnerCommands.cs:188 | OwnerCommands.HandleThirstImageDelete | UpdateCreate | DeleteChatKeywordURL |
| Pet.cs:76 | Pet.HandleAdoptAsync | Select | GetPetsByUser |
| Pet.cs:86 | Pet.HandleAdoptAsync | UpdateCreate | AddPet |
| Pet.cs:117 | Pet.HandlePetsAsync | Select | GetPetsByUser |
| Pet.cs:146 | Pet.HandlePetCardAsync | Select | GetPetJournal |
| Pet.cs:162 | Pet.HandlePetCardAsync | Select | GetPetCosmetics |
| Pet.cs:225 | Pet.HandleFeedAsync | UpdateCreate | UpdatePetStats |
| Pet.cs:244 | Pet.HandleFeedAsync | UpdateCreate | WakePet |
| Pet.cs:249 | Pet.HandleFeedAsync | UpdateCreate | AddPetJournalEntry |
| Pet.cs:301 | Pet.HandlePetPetAsync | UpdateCreate | UpdatePetStats |
| Pet.cs:514 | Pet.HandleGroomAsync | UpdateCreate | UpdatePetStats |
| Pet.cs:551 | Pet.HandleGroomAsync | UpdateCreate | AddPetJournalEntry |
| Pet.cs:606 | Pet.HandlePlayWithAsync | UpdateCreate | UpdatePetStats |
| Pet.cs:781 | Pet.HandlePlayWithAsync | UpdateCreate | AddPetJournalEntry |
| Pet.cs:828 | Pet.HandlePetSleepAsync | UpdateCreate | UpdatePetStats |
| Pet.cs:844 | Pet.HandlePetSleepAsync | UpdateCreate | AddPetJournalEntry |
| Pet.cs:889 | Pet.HandlePetHugAsync | UpdateCreate | UpdatePetStats |
| Pet.cs:906 | Pet.HandlePetHugAsync | UpdateCreate | AddPetJournalEntry |
| Pet.cs:1093 | Pet.HandlePetJournalAsync | Select | GetPetJournal |
| Pet.cs:1180 | Pet.HandleAccessoryAsync | UpdateCreate | UpdatePetAccessory |
| Pet.cs:1202 | Pet.HandleSetActiveAsync | UpdateCreate | SetActivePet |
| Pet.cs:1208 | Pet.HandleSetActiveAsync | Select | GetPetByID |
| Pet.cs:1239 | Pet.HandleRenameAsync | UpdateCreate | RenamePet |
| Pet.cs:1258 | Pet.HandleReleaseAsync | Select | GetPetByID |
| Pet.cs:1293 | Pet.HandleLeaderboardAsync | Select | GetPetLeaderboard |
| Pet.cs:1369 | Pet.HandleExploreAsync | Select | GetPetExplore |
| Pet.cs:1391 | Pet.HandleExploreAsync | UpdateCreate | UpdatePetStats |
| Pet.cs:1407 | Pet.HandleExploreAsync | UpdateCreate | ClearPetExplore |
| Pet.cs:1412 | Pet.HandleExploreAsync | UpdateCreate | AddPetJournalEntry |
| Pet.cs:1469 | Pet.HandleExploreAsync | UpdateCreate | SetPetExplore |
| Pet.cs:1476 | Pet.HandleExploreAsync | UpdateCreate | AddPetJournalEntry |
| Pet.cs:1535 | Pet.HandlePetBattleAsync | Select | GetActivePet |
| Pet.cs:1594 | Pet.HandlePetBattleAsync (local fn ApplyBattleCost) | UpdateCreate | UpdatePetStats |
| Pet.cs:1775 | Pet.HandlePetPictureAsync | UpdateCreate | UpdatePetPicture |
| Pet.cs:1804 | Pet.HandlePetPictureClearAsync | UpdateCreate | UpdatePetPicture |
| Pet.cs:1833 | Pet.HandlePetBioAsync | UpdateCreate | UpdatePetBio |
| Pet.cs:1896 | Pet.GetActivePet | Select | GetActivePet |
| Pet.cs:2081 | PetComponentHandlers.OnReleaseConfirmAsync | Select | GetPetByID |
| Pet.cs:2095 | PetComponentHandlers.OnReleaseConfirmAsync | UpdateCreate | DeletePet |
| Pet.cs:2144 | PetComponentHandlers.OnPetsNavAsync | Select | GetPetsByUser |
| Playlist.cs:114 | Playlist.SaveAsync | UpdateCreate | DeletePlaylist |
| Playlist.cs:125 | Playlist.SaveAsync | UpdateCreate | SavePlaylistTrack |
| Playlist.cs:176 | Playlist.LoadAsync | Select | GetPlaylistTracks |
| Playlist.cs:269 | Playlist.ListAsync | Select | GetUserPlaylists |
| Playlist.cs:323 | Playlist.DeleteAsync | Select | GetUserPlaylists |
| Playlist.cs:341 | Playlist.DeleteAsync | UpdateCreate | DeletePlaylist |
| Poker.cs:51 | Games.HandlePokerAsync | Select | GetPokerGame |
| Poker.cs:75 | Games.HandlePokerAsync | Select | CreatePokerGame |
| Poker.cs:86 | Games.HandlePokerAsync | UpdateCreate | AddPokerPlayer |
| Poker.cs:97 | Games.HandlePokerAsync | UpdateCreate | UpdatePokerDeck |
| Poker.cs:103 | Games.HandlePokerAsync | UpdateCreate | AddPokerPlayer |
| Poker.cs:122 | Games.HandlePokerAsync | UpdateCreate | UpdatePokerMessage |
| Poker.cs:281 | GameComponentHandlers.OnPokerJoinAsync | Select | GetPokerGameById |
| Poker.cs:293 | GameComponentHandlers.OnPokerJoinAsync | Select | GetPokerPlayers |
| Poker.cs:325 | GameComponentHandlers.OnPokerJoinAsync | UpdateCreate | UpdatePokerDeck |
| Poker.cs:331 | GameComponentHandlers.OnPokerJoinAsync | UpdateCreate | AddPokerPlayer |
| Poker.cs:338 | GameComponentHandlers.OnPokerJoinAsync | Select | GetPokerPlayers |
| Poker.cs:371 | GameComponentHandlers.OnPokerStartAsync | Select | GetPokerGameById |
| Poker.cs:382 | GameComponentHandlers.OnPokerStartAsync | Select | GetPokerPlayers |
| Poker.cs:393 | GameComponentHandlers.OnPokerStartAsync | UpdateCreate | UpdatePokerStatus |
| Poker.cs:484 | GameComponentHandlers.OnPokerStartAsync | UpdateCreate | IncrementChallengeProgress |
| Poker.cs:494 | GameComponentHandlers.OnPokerStartAsync | UpdateCreate | UpdatePokerStatus |
| QuoteCommands.cs:40 | QuoteCommands.HandleSaveQuoteAsync | Select | GetGuildQuoteConfig |
| QuoteCommands.cs:84 | QuoteCommands.HandleSaveQuoteAsync | Select | InsertQuote |
| QuoteCommands.cs:130 | QuoteCommands.HandleSaveQuoteAsync | UpdateCreate | UpdateQuoteArchiveUrl |
| QuoteCommands.cs:163 | QuoteSubCommands.HandleSetupAsync | UpdateCreate | UpsertGuildQuoteConfig |
| QuoteCommands.cs:181 | QuoteSubCommands.HandleRandomAsync | Select | GetRandomQuote |
| QuoteCommands.cs:212 | QuoteSubCommands.HandleSearchAsync | Select | SearchQuotes |
| QuoteCommands.cs:237 | QuoteSubCommands.HandleUserAsync | Select | GetQuotesByUser |
| Revolt.cs:168 | Revolt.ExecuteGuillotine | Select | GetPortfolio |
| Revolt.cs:184 | Revolt.ExecuteGuillotine | Select | SellStock |
| Revolt.cs:200 | Revolt.ExecuteGuillotine | Select | GetCreditLeaderboard |
| Revolt.cs:205 | Revolt.ExecuteGuillotine | Select | GetAllServerUsers |
| Scramble.cs:55 | Games.HandleScrambleAsync | Select | GetScrambleByChannel |
| Scramble.cs:89 | Games.HandleScrambleAsync | UpdateCreate | AddScrambleGame |
| Scramble.cs:109 | Games.HandleScrambleAsync (Task.Run) | Select | GetScrambleByChannel |
| Scramble.cs:119 | Games.HandleScrambleAsync (Task.Run) | UpdateCreate | DeleteScrambleGame |
| ServerCommands.cs:129 | ServerCommands.HandleBirthdayAsync | UpdateCreate | AddBirthday |
| ServerCommands.cs:264 | ServerCommands.HandleProfileAsync | Select | GetCredits |
| ServerCommands.cs:279 | ServerCommands.HandleProfileAsync | Select | GetActivePet |
| ServerCommands.cs:301 | ServerCommands.HandleProfileAsync | Select | GetGambleStats |
| Shop.cs:147 | Shop.HandleBuyAsync | UpdateCreate | AddToInventory |
| Shop.cs:175 | Shop.HandleInventoryAsync | Select | GetUserInventory |
| Shop.cs:181 | Shop.HandleInventoryAsync | Select | GetAllActiveEffects |
| Shop.cs:400 | Shop.UsePetStat | UpdateCreate | UpdatePetStats |
| Shop.cs:443 | Shop.UseFullRestore | UpdateCreate | UpdatePetStats |
| Shop.cs:493 | Shop.UseRevive | UpdateCreate | UpdatePetStats |
| Shop.cs:536 | Shop.UseCosmetic | UpdateCreate | SetPetCosmetic |
| Shop.cs:642 | Shop.UseImpregnator | UpdateCreate | CreatePregnancy |
| Shop.cs:697 | Shop.RemoveImpregnator | Select | GetActivePregnancy |
| Shop.cs:709 | Shop.RemoveImpregnator | UpdateCreate | ClearPregnancy |
| Shop.cs:817 | Shop.UseBankHeist | Select | GetCreditLeaderboard |
| Shop.cs:867 | Shop.UseMarketCrash | Select | GetAllStocks |
| Shop.cs:877 | Shop.UseMarketCrash | UpdateCreate | ApplyStockTick |
| Shop.cs:916 | Shop.UseJackpotSeed | UpdateCreate | FeedPassiveJackpot |
| Shop.cs:922 | Shop.UseJackpotSeed | Select | GetPassiveJackpot |
| Shop.cs:945 | Shop.UsePrestigeReset | UpdateCreate | ResetLifetimeEarned |
| Shop.cs:1013 | Shop.UseEconomyNuke | UpdateCreate | HalveAllBalances |
| Shop.cs:1044 | Shop.UseServerReset | UpdateCreate | ZeroAllBalances |
| Shop.cs:1072 | Shop.GetActivePet | Select | GetActivePet |
| Shop.cs:1131 | ShopUseAutocompleteHandler.GenerateSuggestionsAsync | Select | GetUserInventory |
| Stock.cs:40 | Stock.HandleMarketAsync | Select | GetAllStocks |
| Stock.cs:87 | Stock.HandleInfoAsync | Select | GetAllStocks |
| Stock.cs:106 | Stock.HandleInfoAsync | Select | GetStockHistory |
| Stock.cs:152 | Stock.HandleBuyAsync | Select | GetStockDetail |
| Stock.cs:158 | Stock.HandleBuyAsync | Select | GetAllStocks |
| Stock.cs:184 | Stock.HandleBuyAsync | Select | BuyStock |
| Stock.cs:224 | Stock.HandleSellAsync | Select | GetHolding |
| Stock.cs:249 | Stock.HandleSellAsync | Select | GetAllStocks |
| Stock.cs:261 | Stock.HandleSellAsync | Select | SellStock |
| Stock.cs:303 | Stock.HandlePortfolioAsync | Select | GetPortfolio |
| Stock.cs:395 | Stock.HandleHistoryAsync | Select | GetStockTransactions |
| UtilityCommands.cs:199 | UtilityCommands.HandleRemindAsync | UpdateCreate | AddReminder |
| UtilityCommands.cs:317 | UtilityCommands.HandleEmbeds | Select | UpdateBrokenEmbed |
| Wordle.cs:169 | Games.HandleWordleAsync | Select | GetWordleByChannel |
| Wordle.cs:183 | Games.HandleWordleAsync | UpdateCreate | AddWordleGame |
