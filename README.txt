================================================================================
                        UTS TWITCH INTEGRATION v1.0.0
================================================================================

DESCRIPTION
-----------
This mod connects Ultimate Theater Simulator to your Twitch chat. Viewers can
join the game as NPCs with their usernames displayed above their heads.

When viewers type `!visit` in chat, they are added to a queue and can spawn as
customers in the theater.


FEATURES
--------
- Twitch chat integration
- Viewers spawn as NPCs using the !visit command (customizable)
- Username display above NPCs
- Two spawn modes: pool-based (regular NPC spawns) or immediate spawn on command
- Queue selection: Random or First-In-First-Out
- Permission system (restrict commands to subs/VIPs/mods/broadcaster)
- User cooldowns to prevent spam
- Predefined names support
- Auto-generated configuration file


REQUIREMENTS
------------
- Ultimate Theater Simulator (tested with v1.3.4, Build ID 20779152)
- MelonLoader 0.7.2-ci.2367 (nightly) or above
- Twitch account for streaming (OAuth token recommended but optional)


INSTALLATION
------------
1. Install MelonLoader 0.7.2-ci.2367 or above
   Download from: https://github.com/LavaGang/MelonLoader/releases

2. Extract the mod files to your game's Mods folder:
   [Game Directory]/Mods/

3. Launch the game once. It will generate a config file and close.

4. (Optional) Get a Twitch OAuth token:
   - Go to: https://twitchtokengenerator.com/
   - Select "chat:read" scope
   - Click "Generate Token" and authorize
   - Copy the "Access Token"
   - Note: Token is recommended but not required. Without it, the mod
     connects anonymously (but cannot use VisitPermission setting).

5. Edit the config file:
Location: [Game Directory]/UserData/UTSTwitchIntegration.cfg

Set these values:
- OAuthToken = "your_token_here"
- ChannelName = "your_channel_name"

6. Launch the game and start streaming.


COMMANDS
--------
!visit  -  Viewer joins as an NPC in the theater

(Command name and prefix can be customized in the config file)


CONFIGURATION
-------------
The config file is at: [Game Directory]/UserData/UTSTwitchIntegration.cfg

Important Settings:
-------------------
OAuthToken           - Your Twitch OAuth token (optional, recommended)
ChannelName          - Your Twitch channel name (REQUIRED)
Enabled              - Set to `false` to disable the mod (default: true)

Spawn Settings:
---------------
EnableImmediateSpawn - Spawn NPCs immediately (default: false)
MaxPoolSize          - Max viewers in pool, 0 = unlimited (default: 300)
SelectionMethod      - 0=Random, 1=FIFO (default: 0)

Command Settings:
-----------------
CommandPrefix        - Command prefix (default: !)
VisitCommandName     - Visit command name (default: visit)
VisitPermission      - Who can use !visit (default: 0)
                       0=Everyone, 1=Subscriber, 2=VIP, 3=Mod, 4=Broadcaster
UserCooldownSeconds  - Cooldown between commands (default: 60)

Predefined Names:
-----------------
EnablePredefinedNames     - Use predefined names when queue empty
PredefinedNamesFilePath   - Path to names file (one name per line)


HOW IT WORKS
------------
Pool Mode (Recommended):
- Viewers type !visit and join the queue
- As the game spawns customers naturally, they're assigned viewer names
- Random or FIFO selection from the queue

Immediate Mode:
- Viewers type !visit and spawn instantly
- Intended mainly for testing; it can spawn many NPCs quickly
- Has a 5-second rate limit between spawns


TROUBLESHOOTING
---------------
Q: The mod doesn't load
A: Check that you have MelonLoader 0.7.2-ci.2367 or above installed.
   Check the MelonLoader log at [Game Directory]/MelonLoader/Latest.log

Q: Can't connect to Twitch
A: - The OAuth token must be valid and have chat:read scope
   - Use your Twitch channel name in lowercase, without the # prefix
   - Confirm that your internet connection is working

Q: No NPCs are spawning
A: - In pool mode, NPCs only spawn when the game naturally spawns customers
   - Try setting EnableImmediateSpawn=true for testing
   - Check that your pool isn't full (MaxPoolSize setting)

To increase the number of viewer NPCs:
- Wait for the game to naturally spawn more customers (pool mode)
- Increase MaxPoolSize if it's full
- Enable predefined names


SUPPORT
-------
If something isn't working, include log excerpts and your config (with the token removed) when you report it.
The MelonLoader log (Latest.log) usually shows useful errors.


CREDITS
-------
- Ultimate Theater Simulator by AlexRak2 (RAKTWO Games)
- Uses TwitchLib for Twitch integration
- Built with MelonLoader


VERSION HISTORY
---------------
v1.0.0 - Initial release
- Basic Twitch integration
- Pool and immediate spawn modes
- Username display
- Permission system
- Cooldown management
- Predefined names support
