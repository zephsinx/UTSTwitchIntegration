# UTS Twitch Integration

A mod for Ultimate Theater Simulator that connects Twitch chat to the game and spawns viewers as NPCs with their
usernames shown above them.

## Features

- **Twitch Chat Integration** - Connect to your Twitch channel and listen for chat commands
- **Viewer Spawning** - Viewers join your game as NPCs using the `!cinema` command
- **Two Spawn Modes**:
  - Pool mode: Viewers are added to a pool and assigned to natural game spawns
  - Immediate spawn: NPCs spawn instantly when viewers use commands (for testing)
- **Queue Selection** - Choose between Random or FIFO (First-In-First-Out) selection from the pool
- **Permission System** - Restrict commands by permission level (Everyone, Subscriber, VIP, Moderator, Broadcaster)
- **User Cooldowns** - Configurable per-user command cooldowns
- **Username Display** - Twitch usernames appear above NPCs in-game
- **Auto-Configuration** - Config file generates automatically on first launch

## Tech Stack

- **Framework**: MelonLoader 0.7.2-ci.2367 (nightly) or above
- **Language**: C# (.NET Standard 2.1)
- **Game Engine**: Unity (IL2CPP)
- **Twitch Library**: TwitchLib.Client 3.4.0
- **Patching**: Harmony (0Harmony)

## Requirements

- [Ultimate Theater Simulator](https://store.steampowered.com/app/1541370/Ultimate_Theater_Simulator/) (tested with
  v1.3.5, Build ID 20923223)
- MelonLoader 0.7.2-ci.2367 or above

## Installation

1. Install MelonLoader 0.7.2-ci.2367 or above for Ultimate Theater Simulator
2. Download the mod package from releases
3. Extract `UTSTwitchIntegration.dll` to `[Game Directory]/Mods/`
4. Launch the game once to generate the config file
5. Edit `[Game Directory]/UserData/UTSTwitchIntegration.cfg` with your preferred settings
6. Restart the game

See `INSTALLATION.txt` for detailed setup instructions.

## Configuration

The mod generates a config file at `[Game Directory]/UserData/UTSTwitchIntegration.cfg` on first launch.

### Key Settings

| Setting                | Type   | Default  | Description                                                                            |
| ---------------------- | ------ | -------- | -------------------------------------------------------------------------------------- |
| `OAuthToken`           | string | ""       | Your Twitch OAuth token for authentication (optional)                                  |
| `ChannelName`          | string | ""       | Twitch channel to connect to                                                           |
| `CommandPrefix`        | string | "!"      | Prefix for chat commands                                                               |
| `VisitCommandName`     | string | "cinema" | Name of the visit command                                                              |
| `VisitPermission`      | int    | 0        | Minimum permission level (0=Everyone, 1=Subscriber, 2=VIP, 3=Moderator, 4=Broadcaster) |
| `Enabled`              | bool   | true     | Enable/disable Twitch integration                                                      |
| `EnableImmediateSpawn` | bool   | false    | Enable immediate spawning (for testing)                                                |
| `MaxPoolSize`          | int    | 300      | Maximum pool size (0 = unlimited)                                                      |
| `PoolTimeoutSeconds`   | int    | 0        | Pool entry timeout in seconds (0 = no timeout)                                         |
| `SelectionMethod`      | int    | 0        | Selection method (0=Random, 1=FIFO)                                                    |
| `UserCooldownSeconds`  | int    | 60       | Cooldown between commands per user (0 = disabled)                                      |
| `LogLevel`             | int    | 2        | Log verbosity level (0=Error, 1=Warning, 2=Info, 3=Debug)                              |

### Getting a Twitch OAuth Token

1. Go to <https://twitchtokengenerator.com/>
2. Select the `chat:read` scope
3. Click "Generate Token" and authorize with Twitch
4. Copy the "Access Token" and paste it into the config file

## Commands

- `!cinema` - Join the theater as an NPC (default command, configurable)

## Development Setup

### Prerequisites

- .NET SDK (for .NET Standard 2.1 support)
- Ultimate Theater Simulator installed
- MelonLoader 0.7.2-ci.2367 or above installed

### Building from Source

1. Clone the repository
2. Update the reference paths in `UTSTwitchIntegration.csproj` to point to your game installation
3. Open `UTSTwitchIntegration.sln` in your IDE
4. Build the solution (Debug or Release)

The post-build event will automatically copy the DLL to your game's `Mods` folder if the path exists. Modify it as
needed.

## How It Works

1. **Connection**: The mod connects to Twitch IRC using your OAuth token
2. **Command Parsing**: When viewers type `!cinema` in chat, the mod parses the command
3. **Queue Management**: Viewers are added to a queue (with duplicate checking)
4. **Spawning**:
   - In pool mode, viewers are assigned to NPCs as they naturally spawn in the game
   - In immediate mode, NPCs spawn instantly (for testing)
5. **Display**: A TextMeshPro element displays the viewer's username above their NPC

## Troubleshooting

### Mod doesn't load

- Check MelonLoader version (requires 0.7.2-ci.2367 or above)
- Check `[Game Directory]/MelonLoader/Latest.log` for errors

### Can't connect to Twitch

- The OAuth token must be valid and have the `chat:read` scope
- Use your Twitch channel name in lowercase, without the `#` prefix
- Confirm that your internet connection is working

### Usernames don't appear

- This is a known limitation with some game states
- Usernames should appear on most NPCs

### NPCs don't spawn

- In pool mode, NPCs only spawn when the game naturally spawns customers
- Try enabling `EnableImmediateSpawn` for testing
- Check that your pool isn't full (`MaxPoolSize`)

## Credits

- Ultimate Theater Simulator by AlexRak2 ([RAKTWO Games](https://www.raktwogames.com/))
- Uses TwitchLib for Twitch integration
- Built with MelonLoader
