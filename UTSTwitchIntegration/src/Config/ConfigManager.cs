using System;
using System.Linq;
using MelonLoader;
using UTSTwitchIntegration.Utils;

namespace UTSTwitchIntegration.Config
{
    /// <summary>
    /// Manages mod configuration using MelonLoader's MelonPreferences API
    /// </summary>
    public static class ConfigManager
    {
        private static MelonPreferences_Category category;
        private static ModConfiguration config;
        private static readonly object ConfigLock = new object();

        // MelonPreferences entries
        private static MelonPreferences_Entry<string> oauthToken;
        private static MelonPreferences_Entry<string> channelName;
        private static MelonPreferences_Entry<string> commandPrefix;
        private static MelonPreferences_Entry<string> visitCommandName;
        private static MelonPreferences_Entry<int> visitPermission;
        private static MelonPreferences_Entry<bool> enabled;
        private static MelonPreferences_Entry<bool> enableImmediateSpawn;
        private static MelonPreferences_Entry<int> maxPoolSize;
        private static MelonPreferences_Entry<int> poolTimeoutSeconds;
        private static MelonPreferences_Entry<int> selectionMethod;
        private static MelonPreferences_Entry<int> userCooldownSeconds;
        private static MelonPreferences_Entry<int> logLevel;

        public static void Initialize()
        {
            try
            {
                category = MelonPreferences.CreateCategory("UTSTwitchIntegration");
                category.SetFilePath("UserData/UTSTwitchIntegration.cfg", false, false);

                // Load existing config file FIRST (if it exists) to preserve user values
                MelonPreferences.Load();

                oauthToken = category.CreateEntry(
                    "OAuthToken",
                    "",
                    "Twitch OAuth Token",
                    "Your Twitch OAuth token for authentication.\n" +
                    "HOW TO GENERATE:\n" +
                    "1. Go to https://twitchtokengenerator.com/\n" +
                    "2. Select 'chat:read' scope\n" +
                    "3. Click 'Generate Token' and authorize with Twitch\n" +
                    "4. Copy the 'Access Token' and paste it here");

                channelName = category.CreateEntry(
                    "ChannelName",
                    "",
                    "Channel Name",
                    "Twitch channel to connect to.");

                commandPrefix = category.CreateEntry(
                    "CommandPrefix",
                    "!",
                    "Command Prefix",
                    "Prefix for chat commands (default: !)");

                visitCommandName = category.CreateEntry(
                    "VisitCommandName",
                    "visit",
                    "Visit Command Name",
                    "Name of the visit command (default: visit)");

                visitPermission = category.CreateEntry(
                    "VisitPermission",
                    (int)PermissionLevel.Everyone,
                    "Visit Permission Level",
                    "Minimum permission level for !visit command (0=Everyone, 1=Subscriber, 2=VIP, 3=Moderator, 4=Broadcaster)");

                enabled = category.CreateEntry(
                    "Enabled",
                    true,
                    "Enabled",
                    "Enable/disable Twitch integration");

                enableImmediateSpawn = category.CreateEntry(
                    "EnableImmediateSpawn",
                    false,
                    "Enable Immediate Spawn",
                    "Enable immediate spawning for testing (spawns NPCs immediately when !visit used). Default: false");

                maxPoolSize = category.CreateEntry(
                    "MaxPoolSize",
                    300,
                    "Max Pool Size",
                    "Maximum pool size (0 = unlimited). Default: 300");

                poolTimeoutSeconds = category.CreateEntry(
                    "PoolTimeoutSeconds",
                    0,
                    "Pool Timeout Seconds",
                    "Pool entry timeout in seconds (0 = no timeout). Default: 0 (no timeout)");

                selectionMethod = category.CreateEntry(
                    "SelectionMethod",
                    (int)QueueSelectionMethod.Random,
                    "Queue Selection Method",
                    "Method for selecting viewers from the pool (0=Random, 1=FIFO). Default: 0 (Random)");

                userCooldownSeconds = category.CreateEntry(
                    "UserCooldownSeconds",
                    60,
                    "User Cooldown Seconds",
                    "How long users must wait between !visit commands in seconds (0 = disabled). Default: 60");

                logLevel = category.CreateEntry(
                    "LogLevel",
                    (int)LogLevel.Info,
                    "Log Level",
                    "Log verbosity level (0=Error, 1=Warning, 2=Info, 3=Debug). Default: 2 (Info)");

                LoadConfiguration();

                ConfigValidationResult validationResult = ValidateConfiguration();
                if (!validationResult.IsValid)
                {
                    Logger.Error(validationResult.GetFormattedMessage());
                }
                else if (validationResult.HasWarnings)
                {
                    Logger.Warning(validationResult.GetFormattedMessage());
                }

                LogConfigurationSummary();

                Logger.Debug("Configuration system initialized");
                Logger.Debug("Config file location: UserData/UTSTwitchIntegration.cfg");

                MelonPreferences.Save();
                Logger.Debug("Configuration file updated (new settings added with defaults if needed)");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize configuration: {ex.Message}");
                Logger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Load configuration from MelonPreferences into ModConfiguration model
        /// Values loaded raw (not normalized) to preserve user input in config file.
        /// Normalization happens when values are used (e.g., TwitchClientManager.Connect()).
        /// </summary>
        private static void LoadConfiguration()
        {
            lock (ConfigLock)
            {
                config = new ModConfiguration
                {
                    OAuthToken = oauthToken.Value ?? "",
                    ChannelName = channelName.Value ?? "",
                    CommandPrefix = commandPrefix.Value ?? "!",
                    VisitCommandName = visitCommandName.Value ?? "visit",
                    VisitPermission = (PermissionLevel)visitPermission.Value,
                    Enabled = enabled.Value,
                    EnableImmediateSpawn = enableImmediateSpawn.Value,
                    MaxPoolSize = maxPoolSize.Value,
                    PoolTimeoutSeconds = poolTimeoutSeconds.Value,
                    SelectionMethod = (QueueSelectionMethod)selectionMethod.Value,
                    UserCooldownSeconds = userCooldownSeconds.Value,
                    LogLevel = logLevel.Value,
                };
            }
        }

        /// <summary>
        /// Get current configuration
        /// </summary>
        public static ModConfiguration GetConfiguration()
        {
            if (config != null)
                return config;

            lock (ConfigLock)
            {
                if (config == null)
                {
                    LoadConfiguration();
                }
            }

            return config;
        }

        private static ConfigValidationResult ValidateConfiguration()
        {
            ConfigValidationResult result = new ConfigValidationResult();
            ModConfiguration modConfiguration = GetConfiguration();

            if (string.IsNullOrWhiteSpace(modConfiguration.OAuthToken))
            {
                result.AddWarning(
                    "OAuth token is not configured. " +
                    "The mod will connect anonymously using justinfan<number>.\n" +
                    "Anonymous mode limitations: Cannot check user permissions (badges/roles). " +
                    "All users will be treated as 'Everyone' regardless of VisitPermission setting.\n\n" +
                    "To enable permission checking, generate an OAuth token:\n" +
                    "1. Go to https://twitchtokengenerator.com/\n" +
                    "2. Select 'chat:read' scope\n" +
                    "3. Click 'Generate Token' and authorize with Twitch\n" +
                    "4. Copy the 'Access Token' and paste it in the config file");
            }

            if (string.IsNullOrWhiteSpace(modConfiguration.ChannelName))
            {
                result.AddError(
                    "Channel name is not configured. " +
                    "Please set ChannelName in the config file (e.g., 'your_channel_name').");
            }
            else
            {
                string configChannelName = modConfiguration.ChannelName.Trim();

                if (configChannelName.StartsWith("#"))
                {
                    configChannelName = configChannelName[1..];
                }

                if (configChannelName.Contains(" "))
                {
                    result.AddError(
                        "Channel name contains spaces. " +
                        "Please use only alphanumeric characters and underscores (e.g., 'your_channel_name').");
                }
                else if (configChannelName.Contains("/") || configChannelName.Contains("\\"))
                {
                    result.AddError(
                        "Channel name contains invalid characters (/ or \\). " +
                        "Please use only alphanumeric characters and underscores.");
                }
                else if (configChannelName.Length == 0)
                {
                    result.AddError("Channel name is empty after removing invalid characters.");
                }
            }

            if (string.IsNullOrWhiteSpace(modConfiguration.CommandPrefix))
            {
                result.AddError(
                    "Command prefix is empty. " +
                    "Please set CommandPrefix in the config file (default: '!').");
            }
            else if (modConfiguration.CommandPrefix.Length > 1)
            {
                result.AddWarning(
                    $"Command prefix is '{modConfiguration.CommandPrefix}' (length: {modConfiguration.CommandPrefix.Length}). " +
                    "Typically, command prefixes are single characters (e.g., '!'). " +
                    "This will still work, but may not match expected behavior.");
            }

            if (string.IsNullOrWhiteSpace(modConfiguration.VisitCommandName))
            {
                result.AddError(
                    "Visit command name is empty. " +
                    "Please set VisitCommandName in the config file (default: 'visit').");
            }
            else
            {
                string commandName = modConfiguration.VisitCommandName.Trim();
                if (commandName.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
                {
                    result.AddWarning(
                        $"Visit command name '{commandName}' contains special characters. " +
                        "Command names should typically be alphanumeric with underscores only.");
                }
            }

            int permissionValue = (int)modConfiguration.VisitPermission;
            if (permissionValue < 0 || permissionValue > 4)
            {
                result.AddError(
                    $"Visit permission level is {permissionValue}, which is out of range. " +
                    "Valid values are: 0=Everyone, 1=Subscriber, 2=VIP, 3=Moderator, 4=Broadcaster. " +
                    "Please set VisitPermission to a value between 0 and 4.");
            }

            if (modConfiguration.MaxPoolSize < 0)
            {
                result.AddError(
                    $"MaxPoolSize is {modConfiguration.MaxPoolSize}, which is negative. " +
                    "Please set MaxPoolSize to 0 (unlimited) or a positive number.");
            }

            if (modConfiguration.PoolTimeoutSeconds < 0)
            {
                result.AddError(
                    $"PoolTimeoutSeconds is {modConfiguration.PoolTimeoutSeconds}. " +
                    "Please set PoolTimeoutSeconds to 0 (no timeout) or a positive number.");
            }

            int selectionMethodValue = (int)modConfiguration.SelectionMethod;
            if (selectionMethodValue < 0 || selectionMethodValue > 1)
            {
                result.AddError(
                    $"SelectionMethod is {selectionMethodValue}, which is out of range. " +
                    "Valid values are: 0=Random, 1=FIFO. " +
                    "Please set SelectionMethod to 0 or 1.");
            }

            if (modConfiguration.UserCooldownSeconds < 0)
            {
                result.AddError(
                    $"UserCooldownSeconds is {modConfiguration.UserCooldownSeconds}, which is negative. " +
                    "Please set UserCooldownSeconds to 0 (disabled) or a positive number.");
            }

            int logLevelValue = modConfiguration.LogLevel;
            if (logLevelValue < 0 || logLevelValue > 3)
            {
                result.AddWarning(
                    $"LogLevel is {logLevelValue}, which is out of range. " +
                    "Valid values are: 0=Error, 1=Warning, 2=Info, 3=Debug. " +
                    "Defaulting to Info (2).");
                modConfiguration.LogLevel = (int)LogLevel.Info;
                logLevel.Value = (int)LogLevel.Info;
            }

            return result;
        }

        private static void LogConfigurationSummary()
        {
            ModConfiguration modConfiguration = GetConfiguration();

            Logger.Debug("=== Configuration Summary ===");
            Logger.Debug($"Log Level: {GetLogLevelName(modConfiguration.LogLevel)}");
            Logger.Debug($"Enabled: {modConfiguration.Enabled}");

            if (modConfiguration.Enabled)
            {
                string maskedToken = string.IsNullOrWhiteSpace(modConfiguration.OAuthToken)
                    ? "(not set)"
                    : modConfiguration.OAuthToken.Length > 8
                        ? modConfiguration.OAuthToken[..4] + "..." + modConfiguration.OAuthToken[^4..]
                        : "***";

                Logger.Debug($"OAuth Token: {maskedToken}");
                Logger.Debug($"Channel Name: {(string.IsNullOrWhiteSpace(modConfiguration.ChannelName) ? "(not set)" : modConfiguration.ChannelName)}");
                Logger.Debug($"Command Prefix: '{modConfiguration.CommandPrefix}'");
                Logger.Debug($"Visit Command: '{modConfiguration.VisitCommandName}'");
                Logger.Debug($"Permission Level: {modConfiguration.VisitPermission} ({GetPermissionLevelName(modConfiguration.VisitPermission)})");

                string spawnMode = modConfiguration.EnableImmediateSpawn ? "Immediate Spawn" : "Pool Mode";
                Logger.Debug($"Spawn Mode: {spawnMode}");

                if (!modConfiguration.EnableImmediateSpawn)
                {
                    Logger.Debug($"Max Pool Size: {(modConfiguration.MaxPoolSize == 0 ? "Unlimited" : modConfiguration.MaxPoolSize.ToString())}");
                    Logger.Debug($"Pool Timeout: {(modConfiguration.PoolTimeoutSeconds == 0 ? "None" : $"{modConfiguration.PoolTimeoutSeconds} seconds")}");
                }

                string selectionMethodName = modConfiguration.SelectionMethod == QueueSelectionMethod.Random ? "Random" : "FIFO";
                Logger.Debug($"Queue Selection Method: {selectionMethodName}");
                Logger.Debug($"User Cooldown: {(modConfiguration.UserCooldownSeconds == 0 ? "Disabled" : $"{modConfiguration.UserCooldownSeconds} seconds")}");
            }

            Logger.Debug("============================");
        }

        private static string GetPermissionLevelName(PermissionLevel level)
        {
            return level switch
            {
                PermissionLevel.Everyone => "Everyone",
                PermissionLevel.Subscriber => "Subscriber",
                PermissionLevel.Vip => "VIP",
                PermissionLevel.Moderator => "Moderator",
                PermissionLevel.Broadcaster => "Broadcaster",
                _ => $"Unknown ({level})",
            };
        }

        private static string GetLogLevelName(int level)
        {
            return level switch
            {
                0 => "Error",
                1 => "Warning",
                2 => "Info",
                3 => "Debug",
                _ => $"Unknown ({level})",
            };
        }
    }
}