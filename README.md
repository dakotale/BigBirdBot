# BigBirdBot

BigBirdBot is a Discord bot built with .NET 10 that provides audio playback, slash commands, and AI-assisted features. This README documents setup, configuration, local development, deployment, and contributing guidelines tailored for this repository.

## Requirements

- .NET 10 SDK
- Visual Studio 2026 (recommended) or other IDE that supports .NET 10
- A Discord bot token and optional Lavalink server for audio

## Quick Start

1. Clone the repository:
  - git clone https://github.com/dakotale/BigBirdBot.git -> cd BigBirdBot

2. Open the solution in Visual Studio 2026 and restore NuGet packages.

3. Configure secrets (see Configuration below).

4. Run the project (set the appropriate startup project) or use `dotnet run` from the project folder.

## Configuration

This project reads configuration from environment variables and an optional `secrets.json` file located in the application base directory. Do NOT commit real secrets to source control.

- Option 1: Environment variables (preferred for CI / production)
  - `discordBotConnStr` — Database connection string (default: `Server=localhost;DataBase=DiscordBot;Integrated Security=true;TrustServerCertificate=True`)
  - `botToken` — Discord bot token
  - `devBotToken` — Optional dev bot token
  - `lavalinkUrl` — Lavalink URL (default: `http://localhost:2333`)
  - `lavaLinkPwd` — Lavalink password
  - `errorImageUrl` — Fallback error image URL
  - `aiApiUserId`, `aiApiSecretId` — AI detector credentials
  - `aiDetectorPath` — Path used by the AI detector (default: `C:\Temp\DiscordBot\AIDetector\`)
  - `avatarTempPath` — Temporary avatar path (default: `C:\Temp\DiscordBot\avatartemp\`)
  - `openAiToken`, `openAiModel` — OpenAI token and model (default model: `gpt-4.1`)

- Option 2: `secrets.json` (local development)
  - Create a `secrets.json` file in the application base directory (same folder as the compiled executable). Example:


````````json
{
  "discordBotConnStr": "Server=localhost;DataBase=DiscordBot;Integrated Security=true;TrustServerCertificate=True",
  "botToken": "YOUR_BOT_TOKEN",
  "devBotToken": "YOUR_DEV_BOT_TOKEN",
  "lavalinkUrl": "http://localhost:2333",
  "lavaLinkPwd": "YOUR_LAVALINK_PASSWORD",
  "errorImageUrl": "FALLBACK_ERROR_IMAGE_URL",
  "aiApiUserId": "YOUR_AI_API_USER_ID",
  "aiApiSecretId": "YOUR_AI_API_SECRET_ID",
  "aiDetectorPath": "C:\\Temp\\DiscordBot\\AIDetector\\",
  "avatarTempPath": "C:\\Temp\\DiscordBot\\avatartemp\\",
  "openAiToken": "YOUR_OPENAI_TOKEN",
  "openAiModel": "gpt-4.1"
}
````````

The code that reads configuration resides in `Constants\Constants.cs` and prefers environment variables over `secrets.json`.

## Running Locally

- Start Lavalink if you use audio features.
- Ensure `botToken` is set in the environment or `secrets.json`.
- Launch via Visual Studio (__Debug > Start Debugging__) or `dotnet run`.

## Logging and Diagnostics

- The project logs to the configured sinks (check project logging configuration).
- For startup issues, check the Output window in Visual Studio (__View > Output__) and select the appropriate pane.

## Tests

- Unit tests (if present) can be run with the Test Explorer in Visual Studio or `dotnet test`.

## Contributing

- Follow the contribution guidelines in `CONTRIBUTING.md` (add or update if missing).
- Respect code formatting and conventions declared by `.editorconfig`.

## Deployment

- Use environment variables for production deployments and CI secrets.
- Do not include `secrets.json` in deployment artifacts.

## Security

- Never commit real tokens or secrets to source control.
- Use GitHub repository secrets for CI and hosting.

