# BigBirdBot

A Discord bot built with [Discord.Net](https://github.com/discord-net/Discord.Net) (C#, .NET 10). BigBirdBot plays music, auto-responds to configured keywords, talks to Claude, runs a small set of server utilities and admin tools, and posts an hourly bonus word puzzle — with per-server data stored in PostgreSQL.

---

## Table of Contents

- [Music](#music)
- [Keywords](#keywords)
- [AI](#ai)
- [Bonus Word Puzzle](#bonus-word-puzzle)
- [AutoRole](#autorole)
- [Server & Utility](#server--utility)
- [Admin](#admin)
- [Moderation](#moderation)
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
| `/loop <mode>` | Set the repeat mode: `off`, `track` (repeat the current track), or `queue` (repeat the whole queue). |
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

Keyword management requires the **Manage Messages** permission.

---

## AI

| Command | Description |
|---|---|
| `/chat <message> <new-conversation> [personality]` | Multi-turn conversation with Claude using a light character persona (Cottagecore Witch, Meisho Doto, Sett, T. M. Opera O, Vi) or "None" for a plain assistant. History persists per user/channel until you start fresh. |
| `/support <message> <new-conversation> <topic>` | Multi-turn conversation with a mental-health or identity support guide (ADHD, Anxiety, Bipolar, Bisexual, BPD, Depression, Eating Disorder Recovery, Gay, OCD, PTSD & Trauma, Queer, Schizophrenia, Transfirmation). Every reply carries a peer-support / crisis-resource disclaimer; the personas are framed as psychoeducation and peer support, not therapy. Same history model as `/chat`. |
| `/detectai <attachment>` | Upload an image to check the probability it was AI-generated, via the Sightengine API. |
| `/mood <mood>` | Get a random Spotify track matching a described mood (e.g. melancholy, hype, chill). |

---

## Bonus Word Puzzle

Once an hour, the bot posts a hint (first letter + blanks) for a random word in each server with announcements enabled. The first member to type the secret word in that channel gets a "solved it!" shout-out — there's no reward beyond bragging rights.

---

## AutoRole

Automatically assigns a configured role to every new member who joins.

| Command | Description |
|---|---|
| `/autorole set <role> [backfill]` | Set the role to assign when a new member joins. Warns if the role sits above the bot's own role (assignment would fail); `backfill: true` also grants it to every current member. |
| `/autorole clear` | Remove the auto-role setting for this server. |
| `/autorole status` | Show the current auto-role configuration. |

Requires the **Manage Roles** permission (bot and user).

---

## Server & Utility

### Server Info

| Command | Description |
|---|---|
| `/avatar [user]` | Display a user's avatar in full resolution (works in DMs too). |
| `/serverinfo` | Show information about the current server. |
| `/setrolecolor <hex> [member]` | Set the colour of your personal name-role by hex code (e.g. `#FF5733`). Setting another member's needs **Manage Roles**; the bot needs **Manage Roles**. Abandoned personal roles are cleaned up when the member leaves. |
| `/polldnd <user> [duration_hours]` | Native multi-select poll of the next 7 days for scheduling a member's D&D session. |
| `/reportbug <description>` | Submit a bug report to the bot developer (rate-limited to one per minute). |

### Birthdays

The bot posts a greeting in the server's announcement channel on each registered birthday. You may always manage your own; managing someone else's needs **Manage Server**.

| Command | Description |
|---|---|
| `/addbirthday <user> <month> <day> [channel]` | Register (or re-register) a birthday. Re-running replaces the previous entry — no duplicate greetings. Rejects impossible dates (e.g. Apr 31). |
| `/birthdays` | List the birthdays registered in this server, each as its next upcoming date. |
| `/birthdayremove <user>` | Remove a member's registered birthday. |

### Utility

| Command | Description |
|---|---|
| `/random <max>` | Pick a random number between 1 and the given value. |
| `/poll <question> <answer1> <answer2> … [duration_hours] [allow_multiple]` | Create a native Discord poll with 2–10 answers. |
| `/timezone [zone]` | Show, set, or clear your saved time zone — an offset (`-5`, `+5:30`), an IANA name (`Europe/London`), or `clear`. Used by `/remind`. |
| `/remind <message> <when> [timezone]` | Set a DM reminder. `when` accepts `in 90m`, `in 3 days`, `tomorrow 9am`, or `2026-03-25 15:30` (month/day order), interpreted in your saved `/timezone` unless overridden. |
| `/reminders` | List your pending reminders with their cancel numbers. |
| `/reminddelete <id>` | Cancel one of your pending reminders. |
| `/colorpreview <hex>` | Preview what a hex colour looks like as an embed. |
| `/dnddice <number_of_dice> <sides_on_dice> [modifier]` | Roll up to 100 dice of up to 1000 sides (e.g. `2` × `d6` `+3`), flagging natural 1s and max rolls. |
| `/fixembed` | **(Manage Server)** Toggle whether the bot fixes Twitter/X, Reddit, TikTok, and Bluesky embeds in this server. |

Birthday greetings and DM reminders are both delivered by the same minute-tick background scheduler that posts the bonus word puzzle.

---

## Admin

These commands require elevated permissions.

| Command | Permission | Description |
|---|---|---|
| `/pronoun` | Manage Messages | Post a pronoun role selection menu for members. |
| `/botnick <name>` | Manage Roles | Change the bot's nickname in this server. |
| `/purge <count>` | Manage Messages | Bulk-delete up to 100 messages from the current channel (messages older than 14 days can't be bulk-deleted and are skipped). |
| `/announcements` | Manage Guild | Toggle timed bot announcements (bonus word puzzle, birthdays) for this server. |

---

## Moderation

Each command requires the matching Discord permission from **both** the invoking user and the bot, and refuses targets that neither the caller nor the bot outranks by role hierarchy.

| Command | Permission | Description |
|---|---|---|
| `/mod kick <member> [reason]` | Kick Members | Kick a member. |
| `/mod ban <member> [delete_message_days] [reason]` | Ban Members | Ban a member, optionally pruning 0–7 days of their messages. |
| `/mod unban <user_id>` | Ban Members | Lift a ban by user ID. |
| `/mod timeout <member> <minutes> [reason]` | Moderate Members | Time a member out (1 min – 28 days). |
| `/mod untimeout <member>` | Moderate Members | Clear a member's timeout. |
| `/mod slowmode <seconds>` | Manage Channels | Set the current channel's slowmode (0–21600s; 0 disables). |
| `/role add <member> <role>` | Manage Roles | Give a member a role (below the bot's and your own top role). |
| `/role remove <member> <role>` | Manage Roles | Remove a role from a member. |

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
| `/keywordreconcile [purge_orphan_files]` | Sync the keyword image folder with the database — drop rows whose file is gone, and report (or purge) files no row points at. |

---

## Tech Stack

| Component | Technology |
|---|---|
| Language | C# (.NET 10) |
| Discord library | [Discord.Net](https://github.com/discord-net/Discord.Net) 3.20 (Interaction Framework — slash commands only) |
| Music | [Lavalink4NET](https://github.com/angelobreuer/Lavalink4NET) |
| Database | PostgreSQL via **EF Core** (`Npgsql.EntityFrameworkCore.PostgreSQL`) |
| AI chat | Anthropic (Claude) |
| AI image detection | Sightengine |
| Mood tracks | Spotify Web API |

### Architecture Notes

- All tables are kept for archival even after a feature's commands and stored procedures are removed — nothing is dropped from the schema, only unused procs.
- `SQL/Database/postgres/` holds hand-written, numbered migration scripts (`001_InitialSchema.sql`, `002_…`, `003_UserTimezone.sql`, …), run manually against the live database — there are no EF Core migrations. **After deploying a build that adds one, run the new script:** `psql -U discordbot -d discordbot -h localhost -f SQL/Database/postgres/00N_*.sql`.
- Every feature area's data access goes through EF Core (`Data/*Entities.cs` + `Helper/*Service.cs`, mapped explicitly onto the existing schema in `Data/BigBirdContext.cs`) — there is no remaining ADO.NET stored-procedure access anywhere in the app.
- A single background loop (`BotHost.RunSchedulerAsync` in `Program.cs`) drives every time-based feature: DM reminders and birthday greetings (every minute), the hourly bonus word puzzle, and scheduled keyword deliveries.
- No Windows-only dependencies: keyword images use OS-relative paths (`Constants.keywordDirectory`, resolved via `Helper/KeywordFiles.cs`), hex colours are parsed by `Helper/HexColor.cs`, and there is no `System.Drawing` / `SkiaSharp`.

## Deployment

The bot runs on Windows and macOS (Apple Silicon). Lavalink is a separate Java process — install a JRE (`brew install --cask temurin`) and run `Lavalink.jar` alongside its `application.yml` + `plugins/`.

**Publish for the Mac Mini (osx-arm64), cross-compiles fine from Windows:**

```bash
dotnet publish DiscordBot.csproj -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -o publish/osx
```

Produces a single ~100 MB `DiscordBot` executable with the .NET runtime bundled (nothing to install on the Mac). Put `secrets.json` next to it (or set the keys as env vars), then:

```bash
xattr -dr com.apple.quarantine publish/osx   # clear Gatekeeper once (unsigned binary)
chmod +x publish/osx/DiscordBot
./publish/osx/DiscordBot
```

Swap `-r osx-arm64` for `win-x64` (or `osx-x64` for an Intel Mac). Do **not** add `PublishTrimmed`/AOT — Discord.Net's interaction framework and EF Core rely on reflection.
