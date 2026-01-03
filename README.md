# BigBirdBot

Small Discord bot written in .NET 10.

## Overview

`BigBirdBot` is a simple Discord bot implemented in .NET 10. The project contains a `DiscordBot` application that connects to the Discord gateway using a bot token and responds to server events or commands (see project code for implemented features).

This repository is intended as a lightweight bot starter and can be extended with commands, services, and persistence as needed.

## Requirements

- .NET 10 SDK or later
- A Discord application with a bot account and token (https://discord.com/developers)
- Optional: an IDE such as Visual Studio or VS Code

## Configuration

The bot reads its configuration from environment variables or an `appsettings.json` file depending on how the application is implemented. At minimum you must provide a Discord bot token.

Recommended environment variable:

- `DISCORD_TOKEN` - the bot token from the Discord Developer Portal

Example `appsettings.json` (optional, place in the `DiscordBot` project directory):

```json
{
  "Discord": {
    "Token": "YOUR_TOKEN_HERE"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

Note: Do not commit secrets to source control. Use environment variables or a secret manager in production.

## Development

Clone the repository and open it in your preferred editor. From the repository root you can build and run the bot using the .NET SDK.

### Build

```bash
dotnet build
```

### Run

Run the bot with the SDK (makes sure `DISCORD_TOKEN` is set in your environment):

```bash
dotnet run --project DiscordBot
```

Set the token in the environment before running. PowerShell example:

```powershell
$env:DISCORD_TOKEN = "your_token_here"
dotnet run --project DiscordBot
```

Bash example:

```bash
export DISCORD_TOKEN="your_token_here"
dotnet run --project DiscordBot
```

### Publish

To publish a release build:

```bash
dotnet publish -c Release -o ./publish
```

## Configuration and Gateway Intents

Depending on the bot's features you may need to enable specific gateway intents in the Discord Developer Portal and request privileged intents (such as Presence or Members) if required by the code.

Review the `DiscordBot` project code to see which intents and services are configured.

## Contribution

Contributions are welcome. Typical workflow:

1. Fork the repository
2. Create a feature branch
3. Make changes and add tests where appropriate
4. Open a pull request with a clear description of changes

When opening issues, include reproduction steps, expected behavior, and logs if applicable.

## Troubleshooting

- If the bot cannot connect, verify the token and that the bot is invited to your server with the correct permissions.
- Check gateway intents in the Developer Portal if the bot is missing events or member info.
- Inspect console logs for stack traces and error messages.

## License

Check the repository for a license file. If none is present, contact the repository owner for licensing details.

## Contact

For questions about this codebase open an issue in the repository or contact the project owner.

