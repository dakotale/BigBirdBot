# BigBirdBot

A feature-rich Discord bot built with [Discord.Net](https://github.com/discord-net/Discord.Net) and C# (.NET 8). BigBirdBot provides a full virtual economy, gambling games, a Tamagotchi-style pet system, a stock market, music playback, mini-games, and general server utilities — all integrated into a single bot with per-server data stored in SQL Server.

---

## Table of Contents

- [Economy](#economy)
- [Shop](#shop)
- [Gambling](#gambling)
- [Pet System](#pet-system)
- [Stock Market](#stock-market)
- [Games](#games)
- [Music & Playlists](#music--playlists)
- [Server & Utility](#server--utility)
- [Admin](#admin)
- [Tech Stack](#tech-stack)

---

## Economy

Credits (⚡) are the server currency. All balances are **per-user per-server**.

| Command | Description |
|---|---|
| `/balance [user]` | View your credit balance, stats, prestige rank, and daily streak. Optionally check another user's balance. |
| `/daily` | Claim your daily ⚡ 100,000 credits. 24-hour cooldown. Streak bonuses apply. |
| `/work` | Earn ⚡ 5,000–75,000 credits. 1-hour cooldown. |
| `/transfer <user> <amount>` | Send credits to another server member. |
| `/donate <amount>` | Split credits equally among all server members who have a balance (excluding yourself). Any remainder stays with you. |
| `/creditleaderboard` | Show the top credit holders in the server. |
| `/prestige [user]` | View prestige rank, lifetime earnings, and progress toward the next rank tier. |

### Daily Streak Multipliers

| Streak | Multiplier |
|---|---|
| 1–2 days | 1× |
| 3–4 days | 1.25× |
| 5–6 days | 1.5× |
| 7–13 days | 2× |
| 14–29 days | 3× |
| 30+ days | 5× |

### Prestige Ranks

Ranks are based on **lifetime earned** credits (never reset when spending).

| Rank | Threshold |
|---|---|
| 🪨 Broke | 0 |
| 🥉 Bronze Roller | 1,000,000 |
| 🥈 Silver Shark | 10,000,000 |
| 🥇 Gold Gambler | 100,000,000 |
| 💎 Diamond Dealer | 1,000,000,000 |
| 👑 Elite Earner | 10,000,000,000 |
| 🌟 Living Legend | 100,000,000,000 |
| 🚀 Mythic Overlord | 1,000,000,000,000 |

---

## Shop

Browse, purchase, and use items with your credits.

| Command | Description |
|---|---|
| `/shop browse [category]` | Browse items by category with paginated embeds. |
| `/shop buy <item> [quantity]` | Purchase one or more of an item. |
| `/shop inventory` | View your owned items and currently active effects. |
| `/shop use <item>` | Consume an item from your inventory to apply its effect. |

### Categories

#### 🐾 Pet Consumables
Restore and boost your active pet's stats.

| Item | Price | Effect |
|---|---|---|
| Premium Kibble 🍖 | ⚡ 20,000 | +30 Hunger |
| Gourmet Feast 🍱 | ⚡ 65,000 | +60 Hunger, +15 Happiness |
| Tasty Treat 🍬 | ⚡ 15,000 | +25 Happiness |
| Luxury Toy 🧸 | ⚡ 45,000 | +40 Happiness |
| Energy Drink ⚡ | ⚡ 30,000 | +50 Energy |
| Grooming Kit 🛁 | ⚡ 20,000 | +50 Hygiene |
| Full Restore 💊 | ⚡ 175,000 | Max all stats |
| Revive Potion 💫 | ⚡ 100,000 | Wake hibernating pet, restore all stats to 50 |

#### ✨ Pet Cosmetics
Titles and auras displayed on `/pet card`.

| Item | Price | Type |
|---|---|---|
| Dragon Tamer 🐉 | ⚡ 250,000 | Title |
| Star Collector ⭐ | ⚡ 250,000 | Title |
| Shadow Walker 🌑 | ⚡ 250,000 | Title |
| Legendary Tamer 🏆 | ⚡ 1,000,000 | Title |
| Sparkle Aura ✨ | ⚡ 200,000 | Aura |
| Golden Aura 🌟 | ⚡ 200,000 | Aura |
| Flame Aura 🔥 | ⚡ 200,000 | Aura |

#### 📈 Boosters
Temporary multipliers for earning and gambling.

| Item | Price | Effect |
|---|---|---|
| Explore Boost 🗺️ | ⚡ 100,000 | Guarantees rare+ reward on next `/pet explore` |
| XP Boost 📈 | ⚡ 150,000 | 2× pet XP for 60 minutes |
| Daily Boost 🎁 | ⚡ 75,000 | 2× next `/daily` payout |
| Work Boost 💼 | ⚡ 60,000 | 2× credits for next 3 `/work` sessions |

#### 🎲 Gambling Perks

| Item | Price | Effect |
|---|---|---|
| Chaos Card 🃏 | ⚡ 120,000 | Randomizes the payout table for next slots/wheel/scratch |
| Comeback Chip 📈 | ⚡ 90,000 | After 3 losses, next bet pays 1.5× guaranteed |
| Hot Streak 🔥 | ⚡ 110,000 | After 3 wins, next bet is free |
| Cooldown Eraser ⏩ | ⚡ 35,000 | Instantly resets all gambling cooldowns |
| Bet Limit Booster 💰 | ⚡ 5,000,000 | Removes max bet cap for 60 minutes |

#### 💎 Luxury
High-ticket items across four tiers.

**Mid-game (500M–5B)**

| Item | Price | Effect |
|---|---|---|
| Interest Boost 📊 | ⚡ 1B | Instantly grants ⚡ 250,000,000 |
| Tax Evasion 🕵️ | ⚡ 2B | Next 10 gambling wins skip the passive jackpot pool |
| Bank Heist 🏦 | ⚡ 3B | Steal 1–5% of another user's balance (30% fail chance, 48h cooldown) |
| Golden Ticket 🎫 | ⚡ 5B | 2× all credit earnings for 2 hours |

**Prestige Cosmetics (10B–50B)**

| Item | Price | Type |
|---|---|---|
| Void Aura 🌌 | ⚡ 10B | Aura (rarest standard aura) |
| Sovereign 👑 | ⚡ 25B | Pet title |
| Celestial Aura 🌠 | ⚡ 50B | Aura (ultra-rare) |

**High-end Economy Tools (100B–500B)**

| Item | Price | Effect |
|---|---|---|
| Market Crash 📉 | ⚡ 100B | Crashes all stock prices 20–40% server-wide |
| Jackpot Seed 💣 | ⚡ 200B | Seeds the passive jackpot pool with ⚡ 100B |
| Prestige Reset 🔄 | ⚡ 200B | Resets your LifetimeEarned to 0, refunds ⚡ 100B |
| Economy Nuke ☢️ | ⚡ 500B | Halves every user's balance in the server. Irreversible. |

**Ultra-Luxury (1T+)**

| Item | Price | Effect |
|---|---|---|
| Eternal ♾️ | ⚡ 1T | Pet title — the most exclusive in existence |
| Wealth Flex 💸 | ⚡ 1T | Burns ⚡ 1T for a server-wide announcement and status badge |
| Golden Ticket II 🏅 | ⚡ 2T | 3× all credit earnings for 6 hours |
| Server Economy Reset 💥 | ⚡ 10T | Resets every user's balance to 0. Irreversible. |

---

## Gambling

All gambling commands require a credit bet. Bet range: ⚡ 10 – ⚡ 100B (Bet Limit Booster removes the cap). An 8-second cooldown applies between games per user. A daily loss limit of ⚡ 100B is enforced.

| Command | Description |
|---|---|
| `/slots <bet>` | Spin a 3-reel slot machine. Three of a kind pays up to 50× bet. |
| `/coinflip <bet> <heads\|tails>` | Flip a coin. Correct guess pays 1.9× bet. |
| `/dice <bet> <over\|under\|seven\|doubles>` | Roll two dice. Over/under pays 1.8×, seven pays 4×, doubles pays 6×. |
| `/roulette <bet> <red\|black\|even\|odd\|low\|high\|0–36>` | Roulette wheel. Color/parity pays 1.9×, exact number pays 35×. |
| `/scratchcard` | Buy a scratch card for ⚡ 2,000. Match three symbols for up to 100× return. |
| `/horses <bet> <horse>` | Bet on a horse race with 8 horses. Odds range from 2× to 50×. |
| `/rps <bet> <rock\|paper\|scissors>` | Play Rock Paper Scissors. Win pays 1.9×. |
| `/highlow <bet>` | Draw a card, then guess if the next is higher or lower. Win pays 1.9×. |
| `/jackpot` | View jackpot pools or contribute to the entry jackpot. |
| `/bigwheel <bet>` | Spin the Big Wheel. Segments range from BANKRUPT (0×) to 100×. |
| `/invest <amount>` | Lock away credits for 24 hours. Returns 0.2×–5× on maturity. |
| `/fish` | Cast your line and catch a fish worth ⚡ 0–250,000. 45-minute cooldown. |
| `/gamblestats` | View your personal gambling and fishing statistics. |

### Passive Jackpot

A percentage of every gambling loss is added to a server-wide passive jackpot pool. Any spin of `/slots` or `/scratchcard` has a small chance to trigger the jackpot — the winner claims the entire pool and a server-wide announcement is posted.

### Slots Payout Table

| Combination | Payout |
|---|---|
| 💎 💎 💎 | 50× bet |
| 7️⃣ 7️⃣ 7️⃣ | 20× bet |
| 🍀 🍀 🍀 | 10× bet |
| ⭐ ⭐ ⭐ | 5× bet |
| 🔔 🔔 🔔 | 3× bet |
| 🍇 🍇 🍇 | 2× bet |
| 🍊 🍊 🍊 | 1.5× bet |
| Any two matching | 0.5× bet |
| Any 🍒 | 0.25× bet |
| No match | 0 |

---

## Pet System

A Tamagotchi-inspired system where you adopt, raise, and battle pets. Pets earn XP through server activity and care actions, and level up to unlock new abilities. Each user can own up to 5 pets with one active at a time.

### Core Commands

| Command | Description |
|---|---|
| `/pet adopt <species> <name>` | Adopt a new pet. Species: Bear, Bird, Cat, Dog, Dragon, Fox, Hamster, Panda, Rabbit, Snake, Tiger. |
| `/pet card` | Show your active pet's full stat card. |
| `/pet list` | List all your pets with stats and status. |
| `/pet setactive <name>` | Switch which pet is currently active. |
| `/pet rename <name>` | Rename your active pet. |
| `/pet release <name>` | Permanently release a pet. |
| `/pet leaderboard` | Show the top pets in the server by level. |

### Care Actions

| Command | Cooldown | Description |
|---|---|---|
| `/pet feed <food>` | — | Feed your pet a food item. See `/pet foodlist`. |
| `/pet pat` | ~30 min | Pet your active pet for a happiness boost. |
| `/pet play` | ~1 hr | Play with your pet for happiness + XP. |
| `/pet groom` | ~2 hr | Groom your pet for a hygiene boost + XP. |
| `/pet sleep` | ~4 hr | Put your pet to sleep to restore energy. |
| `/pet hug` | ~15 min | Quick happiness boost, no XP. |
| `/pet trick` | ~1 hr | Have your pet perform a trick for XP. |

### Exploration & Battle

| Command | Description |
|---|---|
| `/pet explore` | Send your pet on an adventure (returns after a delay) for credits and XP. |
| `/pet battle <user>` | Challenge another user's active pet to a battle. 5-minute cooldown. |

### Customization

| Command | Description |
|---|---|
| `/pet picture` | Upload a photo for your active pet (appears in all embeds). |
| `/pet pictureclear` | Remove your active pet's photo. |
| `/pet bio [text]` | Set a custom bio (up to 1,000 characters). Leave blank to clear. |
| `/pet accessory <item>` | Equip an accessory (unlocks at level 10). |
| `/pet journal` | View the recent activity log for your active pet. |
| `/pet breedlist <species>` | Show all available breeds for a species. |
| `/pet foodlist` | Show all available food items and their stat effects. |

### Breeding

| Command | Description |
|---|---|
| `/breed <pet1> <pet2>` | Breed two of your pets to produce an egg. |
| `/eggs` | View your pending eggs and hatch timers. |
| `/hatchegg <egg>` | Hatch a ready egg into a new pet. |

### Forge (Custom Cosmetics)

Craft custom titles and auras for your active pet by burning credits.

| Command | Description |
|---|---|
| `/forge title <text>` | Forge a custom title for your active pet. |
| `/forge aura <text>` | Forge a custom aura label for your active pet. |
| `/forge list` | View all forged cosmetics on your active pet. |
| `/forge tiers` | View forge tier costs and character limits. |

---

## Stock Market

The Big Bird Stock Exchange — buy and sell shares in in-bot companies whose prices fluctuate over time.

| Command | Description |
|---|---|
| `/stock market` | View all listed stocks and current prices. |
| `/stock info <ticker>` | Detailed info and price history for a stock. |
| `/stock buy <ticker> <shares>` | Buy shares in a company. |
| `/stock sell <ticker> <shares>` | Sell shares you own. |
| `/stock portfolio` | View your holdings and unrealized P&L. |
| `/stock history` | View your recent stock transactions. |

> **Note:** The `Market Crash` luxury shop item causes an immediate 20–40% server-wide price drop.

---

## Games

All games are grouped under the `/game` command.

| Command | Description |
|---|---|
| `/game trivia` | Answer a random trivia question. |
| `/game wordle` | Guess the 5-letter word in 6 attempts (Wordle-style). |
| `/game scramble` | Unscramble the word before time runs out. |
| `/game poker <bet>` | Texas Hold'em — up to 4 players vs the bot. |

### Daily Challenges

| Command | Description |
|---|---|
| `/challenges` | View your three daily challenges and claim the completion bonus when all three are done. |
| `/stats` | View your gambling and fishing stats (win rates, total wagered, fish caught, etc.). |

### Revolt

| Command | Description |
|---|---|
| `/revolt <target>` | Rise up against a wealthy user — requires 3 paupers to agree within 5 minutes to steal a portion of the target's balance. |

---

## Music & Playlists

Music playback is powered by [Lavalink4NET](https://github.com/angelobreuer/Lavalink4NET). Supports YouTube, Spotify, SoundCloud, and more.

### Playback

| Command | Description |
|---|---|
| `/join` | Join your current voice channel. |
| `/leave` | Leave the voice channel and stop playback. |
| `/play <query>` | Play a track or playlist from a URL or search query. |
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

### Playlists

Save and restore named queue snapshots, per-user per-server.

| Command | Description |
|---|---|
| `/playlist save <name>` | Save the current queue as a named playlist. |
| `/playlist load <name>` | Restore a saved playlist into the current queue. |
| `/playlist list` | Show all your saved playlists. |
| `/playlist delete <name>` | Delete one of your saved playlists. |

---

## Server & Utility

### Server Info

| Command | Description |
|---|---|
| `/avatar [user]` | Display a user's avatar in full resolution. |
| `/userinfo [user]` | Show account and server membership info for a user. |
| `/serverinfo` | Show information about the current server. |
| `/addbirthday <user> <date>` | Register a member's birthday for bot announcements. |
| `/setrolecolor <hex>` | Set the colour of your role by hex code (e.g. `#FF5733`). |
| `/profile` | View your full profile — credits, active pet, and stats in one card. |
| `/reportbug <description>` | Submit a bug report to the bot developer. |

### Utility

| Command | Description |
|---|---|
| `/random <max>` | Pick a random number between 1 and the given value. |
| `/etext <message>` | Convert text into regional-indicator emojis. |
| `/poll <question> <options>` | Create a reaction poll with up to 10 choices. |
| `/polldnd` | Reaction availability poll for D&D scheduling (next 7 days). |
| `/8ball <question>` | Ask the magic 8-ball a yes/no question. |
| `/choose <options>` | Let the bot pick from your comma-separated options. |
| `/remind <time> <message>` | Set a DM reminder for yourself. |
| `/daysince <date>` | Calculate how many days since or until a given date. |
| `/colorpreview <hex>` | Preview what a hex colour looks like as an embed. |
| `/dnddice <expression>` | Roll any number of any-sided dice with an optional modifier (e.g. `2d6+3`). |
| `/fixembed <url>` | Fix embeds for Twitter/X, Reddit, TikTok, and Bluesky links. |

---

## Admin

These commands require elevated permissions.

| Command | Description |
|---|---|
| `/pronoun` | Post a pronoun role selection menu for members. |
| `/editbotnickname <name>` | Change the bot's nickname in this server. |
| `/purge <count>` | Bulk-delete up to 100 messages from the current channel. |
| `/lucky` | Toggle "lucky mode" for the server (owner only — modifies gambling odds). |

---

## Tech Stack

| Component | Technology |
|---|---|
| Language | C# (.NET 10) |
| Discord library | [Discord.Net](https://github.com/discord-net/Discord.Net) (Interaction Framework) |
| Music | [Lavalink4NET](https://github.com/angelobreuer/Lavalink4NET) |
| Database | SQL Server (stored procedures via `SqlClient`) |
| Credit type | `decimal` (precision arithmetic throughout) |

### Architecture Notes

- All credit operations use `decimal` for precision — Discord slash command parameters use `long` (INTEGER) for bot-facing input and are explicitly cast to `decimal` inside handlers.
- Shop items, fishing tables, slot symbols, horse odds, and wheel segments are all defined as static arrays in `CreditHelper.cs` and `ShopHelper.cs` — no database lookups needed for game logic.
- Active effects (boosts, timed buffs, stack-count items) are stored in a `UserActiveEffects` table and checked/consumed via stored procedures.
- Per-user gambling cooldowns are tracked in memory (intentionally reset on restart).
- The passive jackpot pool is stored in the database and claimed atomically via a stored procedure with a pre-check pattern to avoid post-reset ambiguity.
- Playlists are stored per-user per-server and reference the original track URI for re-resolution by Lavalink on load.
