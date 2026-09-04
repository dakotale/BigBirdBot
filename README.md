# BigBirdBot

A Discord bot built with [Discord.Net](https://github.com/discord-net/Discord.Net) (C#, .NET 10). BigBirdBot plays music, auto-responds to configured keywords, talks to Claude, runs a small set of server utilities and admin tools, and posts an hourly bonus word puzzle — with per-server data stored in SQL Server.

---

## Table of Contents

- [Music](#music)
- [Keywords](#keywords)
- [AI](#ai)
- [Bonus Word Puzzle](#bonus-word-puzzle)
- [AutoRole](#autorole)
- [Server & Utility](#server--utility)
- [Admin](#admin)
- [Owner](#owner)
- [Tech Stack](#tech-stack)

---

## Music

Music playback is powered by [Lavalink4NET](https://github.com/angelobreuer/Lavalink4NET). Supports YouTube, Spotify, SoundCloud, and more — including queuing an entire playlist from a source URL.

| Command | Description |
|---|---|
| `/join` | Join your current voice channel. |
| `/leave` | Leave the voice channel and stop playback. |
| `/play <query>` | Play a track, or queue an entire playlist, from a URL or search query. |
| `/playnext <query>` | Same as `/play` but inserts the track next in queue. |
| `/forceskip` | Skip the current track. |
| `/pause` | Pause playback. |
| `/resume` | Resume paused playback. |
| `/stop` | Stop playback, clear the queue, and disconnect. |
| `/nowplaying` | Show the currently playing track with progress bar. |
| `/queue` | Show the upcoming tracks in the queue. |
| `/volume <0–100>` | Set playback volume. |
| `/loop <n>` | Queue the current track N more times. |
| `/repeat` | Queue the current track one more time. |
| `/shuffle` | Randomize the queue order. |
| `/clear` | Remove all tracks from the queue. |
| `/remove <position>` | Remove a specific track from the queue by position. |
| `/swap <pos1> <pos2>` | Swap two tracks in the queue by position. |
| `/seek <timestamp>` | Jump to a timestamp in the current track (e.g. `00:01:30`). |

The bot automatically rejoins each voice channel that still has listeners and resumes its queue if the gateway connection drops and reconnects.

---

## Keywords

Register a trigger word per server; when a member's message contains it, the bot replies with one randomly-picked entry (an image, file, or link) registered under that keyword. Entries can be flagged NSFW, keywords can have aliases, and a user can subscribe to have a keyword's content DM'd to them on a recurring schedule.

All grouped under `/keyword` to keep the command list small.

| Command | Description |
|---|---|
| `/keyword add <keyword> <value>` | Add an entry (image/file/URL/text) under a keyword. |
| `/keyword delete <keyword>` | Permanently remove a keyword and all its entries/mappings. |
| `/keyword rename <old> <new>` | Rename an existing keyword. |
| `/keyword info <keyword>` | Show entry count and stats for a keyword. |
| `/keyword list` | List all keywords registered in this server. |
| `/keyword alias add <keyword> <alias>` | Create a trigger word that serves entries from an existing keyword. |
| `/keyword alias delete <alias>` | Remove a keyword alias. |
| `/keyword alias list <keyword>` | List all aliases pointing to a keyword. |
| `/keyword attachment add <keywords> <files...>` | Attach up to 10 files to one or more keywords at once. |
| `/keyword url delete <keyword> <url>` | Remove a specific URL from a keyword's entries. |
| `/keyword schedule add <user> <keyword>` | Schedule a recurring DM delivery of a keyword for a user. |
| `/keyword schedule remove <user> <keyword>` | Remove a user's scheduled keyword delivery. |
| `/keyword schedule list <user>` | List a user's scheduled keyword deliveries. |
| `/keyword schedule requeue <user>` | Owner only — requeue a user's schedule after a delivery failure. |

Keyword management requires the **Manage Messages** permission. Data access for this area runs on EF Core against SQL Server rather than stored procedures — see [Tech Stack](#tech-stack).

---

## AI

| Command | Description |
|---|---|
| `/chat <message> <new-conversation> <personality>` | Have a multi-turn conversation with Claude. Conversation history persists per user until you start fresh. Choose "None" for a generic assistant, or a named persona (e.g. Cottagecore Witch, Sett, Vi) to change the system prompt. |
| `/detectaibyattachment <attachment>` | Upload an image to check the probability it was AI-generated, via the Sightengine API. |
| `/mood <mood>` | Get a random Spotify track matching a described mood (e.g. melancholy, hype, chill). |

---

## Bonus Word Puzzle

Once an hour, the bot posts a hint (first letter + blanks) for a random word in each server with announcements enabled. The first member to type the secret word in that channel gets a "solved it!" shout-out — there's no reward beyond bragging rights.

---

## AutoRole

Automatically assigns a configured role to every new member who joins.

| Command | Description |
|---|---|
| `/autorole set <role>` | Set the role to assign when a new member joins. |
| `/autorole clear` | Remove the auto-role setting for this server. |
| `/autorole status` | Show the current auto-role configuration. |

Requires the **Manage Roles** permission.

---

## Server & Utility

### Server Info

| Command | Description |
|---|---|
| `/avatar [user]` | Display a user's avatar in full resolution. |
| `/userinfo [user]` | Show account and server membership info for a user. |
| `/serverinfo` | Show information about the current server. |
| `/addbirthday <user> <month> <day> [channel]` | Register a member's birthday for bot announcements. |
| `/setrolecolor <hex> [user]` | Set the colour of your role (or another member's) by hex code (e.g. `#FF5733`). |
| `/polldnd <user>` | Reaction availability poll for D&D scheduling (next 7 days). |
| `/reportbug <description>` | Submit a bug report to the bot developer. |

### Utility

| Command | Description |
|---|---|
| `/random <max>` | Pick a random number between 1 and the given value. |
| `/etext <message>` | Convert text into regional-indicator emojis. |
| `/poll <question> <options>` | Create a reaction poll with up to 10 choices. |
| `/8ball <question>` | Ask the magic 8-ball a yes/no question. |
| `/choose <options>` | Let the bot pick from your comma-separated options. |
| `/remind <message> <when> [utc_offset]` | Set a DM reminder for yourself. |
| `/daysince <date>` | Calculate how many days since or until a given date. |
| `/colorpreview <hex>` | Preview what a hex colour looks like as an embed. |
| `/dnddice <expression>` | Roll any number of any-sided dice with an optional modifier (e.g. `2d6+3`). |
| `/fixembed <url>` | Fix embeds for Twitter/X, Reddit, TikTok, and Bluesky links. |

Birthday reminders and DM reminders are both delivered by the same minute-tick background scheduler that posts the bonus word puzzle.

---

## Admin

These commands require elevated permissions.

| Command | Permission | Description |
|---|---|---|
| `/pronoun` | Manage Messages | Post a pronoun role selection menu for members. |
| `/editbotnickname <name>` | Manage Roles | Change the bot's nickname in this server. |
| `/purge <count>` | Manage Messages | Bulk-delete up to 100 messages from the current channel. |
| `/announcements` | Manage Guild | Toggle timed bot announcements (bonus word puzzle, birthdays) for this server. |

---

## Owner

Bot-owner-only maintenance tooling, visible only in the developer's own server.

| Command | Description |
|---|---|
| `/announcement` | Broadcast a message (with optional attachment) to every server's default channel. |
| `/schedulelist` | List every user's scheduled keyword delivery times. |
| `/connplayers` | List all connected music players across voice channels. |
| `/populateallusers` | Backfill the Users table for a server. |
| `/delmultiimage` | Delete a multi-keyword image by path. |

---

## Tech Stack

| Component | Technology |
|---|---|
| Language | C# (.NET 10) |
| Discord library | [Discord.Net](https://github.com/discord-net/Discord.Net) 3.20 (Interaction Framework — slash commands only) |
| Music | [Lavalink4NET](https://github.com/angelobreuer/Lavalink4NET) |
| Database | SQL Server — mostly ADO.NET stored procedures via `Microsoft.Data.SqlClient`, with the keyword feature area on **EF Core** (`Microsoft.EntityFrameworkCore.SqlServer`) |
| AI chat | Anthropic (Claude) |
| AI image detection | Sightengine |
| Mood tracks | Spotify Web API |
| Image processing | SkiaSharp |

### Architecture Notes

- All tables are kept for archival even after a feature's commands and stored procedures are removed — nothing is dropped from the schema, only unused procs.
- `SQL/Database/dbo/Migrations/` holds hand-written, numbered SQL migrations (schema changes and stored-procedure DROP scripts); they're run manually against the live database, not via EF Core migrations.
- The keyword feature area (`Helper/KeywordService.cs`, `Data/BigBirdContext.cs`) is the only part of the data layer on EF Core so far; everything else still goes through `Constants/StoredProcedure.cs`.
- A single background loop (`BotHost.RunSchedulerAsync` in `Program.cs`) drives every time-based feature: DM reminders and birthday greetings (every minute), the hourly bonus word puzzle, and scheduled keyword deliveries.
