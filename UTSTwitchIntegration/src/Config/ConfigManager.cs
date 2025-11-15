#nullable disable
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
        private static MelonPreferences_Category _category;
        private static ModConfiguration _config;
        private static readonly object _configLock = new object();

        // MelonPreferences entries
        private static MelonPreferences_Entry<string> _oauthToken;
        private static MelonPreferences_Entry<string> _channelName;
        private static MelonPreferences_Entry<string> _commandPrefix;
        private static MelonPreferences_Entry<string> _visitCommandName;
        private static MelonPreferences_Entry<int> _visitPermission;
        private static MelonPreferences_Entry<bool> _enabled;
        private static MelonPreferences_Entry<bool> _usePoolMode;
        private static MelonPreferences_Entry<bool> _enableImmediateSpawn;
        private static MelonPreferences_Entry<int> _maxPoolSize;
        private static MelonPreferences_Entry<int> _poolTimeoutSeconds;
        private static MelonPreferences_Entry<int> _selectionMethod;
        private static MelonPreferences_Entry<int> _userCooldownSeconds;
        private static MelonPreferences_Entry<bool> _enablePredefinedNames;
        private static MelonPreferences_Entry<string> _predefinedNamesFilePath;

        public static void Initialize()
        {
            try
            {
                // Create or get preferences category
                _category = MelonPreferences.CreateCategory("UTSTwitchIntegration");
                _category.SetFilePath("UserData/UTSTwitchIntegration.cfg", false, false);

                // Load existing config file FIRST (if it exists) to preserve user values
                // This prevents MelonLoader from overwriting user edits
                MelonPreferences.Load();

                // Create preferences entries with defaults
                // If config file exists, these defaults will be overridden by loaded values
                _oauthToken = _category.CreateEntry(
                    "OAuthToken",
                    "",
                    "Twitch OAuth Token",
                    "Your Twitch OAuth token for authentication.\n" +
                    "HOW TO GENERATE:\n" +
                    "1. Go to https://twitchtokengenerator.com/\n" +
                    "2. Select 'chat:read' scope\n" +
                    "3. Click 'Generate Token' and authorize with Twitch\n" +
                    "4. Copy the 'Access Token' and paste it here");

                _channelName = _category.CreateEntry(
                    "ChannelName",
                    "",
                    "Channel Name",
                    "Twitch channel to connect to.");

                _commandPrefix = _category.CreateEntry(
                    "CommandPrefix",
                    "!",
                    "Command Prefix",
                    "Prefix for chat commands (default: !)");

                _visitCommandName = _category.CreateEntry(
                    "VisitCommandName",
                    "visit",
                    "Visit Command Name",
                    "Name of the visit command (default: visit)");

                _visitPermission = _category.CreateEntry(
                    "VisitPermission",
                    (int)PermissionLevel.Everyone,
                    "Visit Permission Level",
                    "Minimum permission level for !visit command (0=Everyone, 1=Subscriber, 2=VIP, 3=Moderator, 4=Broadcaster)");

                _enabled = _category.CreateEntry(
                    "Enabled",
                    true,
                    "Enabled",
                    "Enable/disable Twitch integration");

                _usePoolMode = _category.CreateEntry(
                    "UsePoolMode",
                    true,
                    "Use Pool Mode",
                    "Use pool-based spawning (viewers added to pool, assigned to natural spawns). Default: true");

                _enableImmediateSpawn = _category.CreateEntry(
                    "EnableImmediateSpawn",
                    false,
                    "Enable Immediate Spawn",
                    "Enable immediate spawning for testing (spawns NPCs immediately when !visit used). Default: false");

                _maxPoolSize = _category.CreateEntry(
                    "MaxPoolSize",
                    300,
                    "Max Pool Size",
                    "Maximum pool size (0 = unlimited). Default: 300");

                _poolTimeoutSeconds = _category.CreateEntry(
                    "PoolTimeoutSeconds",
                    0,
                    "Pool Timeout Seconds",
                    "Pool entry timeout in seconds (0 = no timeout). Default: 0 (no timeout)");

                _selectionMethod = _category.CreateEntry(
                    "SelectionMethod",
                    (int)QueueSelectionMethod.Random,
                    "Queue Selection Method",
                    "Method for selecting viewers from the pool (0=Random, 1=FIFO). Default: 0 (Random)");

                _userCooldownSeconds = _category.CreateEntry(
                    "UserCooldownSeconds",
                    60,
                    "User Cooldown Seconds",
                    "How long users must wait between !visit commands in seconds (0 = disabled). Default: 60");

                _enablePredefinedNames = _category.CreateEntry(
                    "EnablePredefinedNames",
                    false,
                    "Enable Predefined Names",
                    "Enable predefined names when queue is empty. Default: false");

                _predefinedNamesFilePath = _category.CreateEntry(
                    "PredefinedNamesFilePath",
                    "UserData/predefined_names.txt",
                    "Predefined Names File Path",
                    "Path to predefined names file (one name per line). Default: UserData/predefined_names.txt");

                // Load configuration into model
                LoadConfiguration();

                // Validate configuration
                ConfigValidationResult validationResult = ValidateConfiguration();
                if (!validationResult.IsValid)
                {
                    Logger.Error(validationResult.GetFormattedMessage());
                }
                else if (validationResult.HasWarnings)
                {
                    Logger.Warning(validationResult.GetFormattedMessage());
                }

                // Log configuration summary
                LogConfigurationSummary();

                Logger.Info("Configuration system initialized");
                Logger.Debug("Config file location: UserData/UTSTwitchIntegration.cfg");
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
            lock (_configLock)
            {
                _config = new ModConfiguration
                {
                    // Load raw values from file (don't normalize here to preserve user input)
                    OAuthToken = _oauthToken.Value ?? "",
                    ChannelName = _channelName.Value ?? "",
                    CommandPrefix = _commandPrefix.Value ?? "!",
                    VisitCommandName = _visitCommandName.Value ?? "visit",
                    VisitPermission = (PermissionLevel)_visitPermission.Value,
                    Enabled = _enabled.Value,
                    UsePoolMode = _usePoolMode.Value,
                    EnableImmediateSpawn = _enableImmediateSpawn.Value,
                    MaxPoolSize = _maxPoolSize.Value,
                    PoolTimeoutSeconds = _poolTimeoutSeconds.Value,
                    SelectionMethod = (QueueSelectionMethod)_selectionMethod.Value,
                    UserCooldownSeconds = _userCooldownSeconds.Value,
                    EnablePredefinedNames = _enablePredefinedNames.Value,
                    PredefinedNamesFilePath = _predefinedNamesFilePath.Value ?? "UserData/predefined_names.txt"
                };
            }
        }

        /// <summary>
        /// Get current configuration
        /// </summary>
        public static ModConfiguration GetConfiguration()
        {
            if (_config == null)
            {
                lock (_configLock)
                {
                    if (_config == null)
                    {
                        LoadConfiguration();
                    }
                }
            }

            return _config;
        }

        /// <summary>
        /// Save configuration to file (only if explicitly called - not automatic)
        /// </summary>
        public static void SaveConfiguration()
        {
            try
            {
                ModConfiguration config;
                lock (_configLock)
                {
                    if (_config == null) return;
                    config = _config;
                }

                // Save raw values (normalization happens when values are used, not when saved)
                // This preserves user input format
                _oauthToken.Value = config.OAuthToken;
                _channelName.Value = config.ChannelName;
                _commandPrefix.Value = config.CommandPrefix;
                _visitCommandName.Value = config.VisitCommandName;
                _visitPermission.Value = (int)config.VisitPermission;
                _enabled.Value = config.Enabled;
                _usePoolMode.Value = config.UsePoolMode;
                _enableImmediateSpawn.Value = config.EnableImmediateSpawn;
                _maxPoolSize.Value = config.MaxPoolSize;
                _poolTimeoutSeconds.Value = config.PoolTimeoutSeconds;
                _selectionMethod.Value = (int)config.SelectionMethod;
                _userCooldownSeconds.Value = config.UserCooldownSeconds;
                _enablePredefinedNames.Value = config.EnablePredefinedNames;
                _predefinedNamesFilePath.Value = config.PredefinedNamesFilePath;

                MelonPreferences.Save();
                Logger.Debug("Configuration saved");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Reload configuration from file
        /// </summary>
        public static void ReloadConfiguration()
        {
            try
            {
                MelonPreferences.Load();
                lock (_configLock)
                {
                    LoadConfiguration();
                }

                // Validate reloaded configuration
                ConfigValidationResult validationResult = ValidateConfiguration();
                if (!validationResult.IsValid)
                {
                    Logger.Error(validationResult.GetFormattedMessage());
                }
                else if (validationResult.HasWarnings)
                {
                    Logger.Warning(validationResult.GetFormattedMessage());
                }

                Logger.Info("Configuration reloaded");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to reload configuration: {ex.Message}");
            }
        }

        public static ConfigValidationResult ValidateConfiguration()
        {
            ConfigValidationResult result = new ConfigValidationResult();
            ModConfiguration config = GetConfiguration();

            if (string.IsNullOrWhiteSpace(config.OAuthToken))
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
            else
            {
                string token = config.OAuthToken.Trim();
            }

            if (string.IsNullOrWhiteSpace(config.ChannelName))
            {
                result.AddError(
                    "Channel name is not configured. " +
                    "Please set ChannelName in the config file (e.g., 'your_channel_name').");
            }
            else
            {
                string channelName = config.ChannelName.Trim();

                if (channelName.StartsWith("#"))
                {
                    channelName = channelName[1..];
                }

                if (channelName.Contains(" "))
                {
                    result.AddError(
                        "Channel name contains spaces. " +
                        "Please use only alphanumeric characters and underscores (e.g., 'your_channel_name').");
                }
                else if (channelName.Contains("/") || channelName.Contains("\\"))
                {
                    result.AddError(
                        "Channel name contains invalid characters (/ or \\). " +
                        "Please use only alphanumeric characters and underscores.");
                }
                else if (channelName.Length == 0)
                {
                    result.AddError("Channel name is empty after removing invalid characters.");
                }
            }

            if (string.IsNullOrWhiteSpace(config.CommandPrefix))
            {
                result.AddError(
                    "Command prefix is empty. " +
                    "Please set CommandPrefix in the config file (default: '!').");
            }
            else if (config.CommandPrefix.Length > 1)
            {
                result.AddWarning(
                    $"Command prefix is '{config.CommandPrefix}' (length: {config.CommandPrefix.Length}). " +
                    "Typically, command prefixes are single characters (e.g., '!'). " +
                    "This will still work, but may not match expected behavior.");
            }

            if (string.IsNullOrWhiteSpace(config.VisitCommandName))
            {
                result.AddError(
                    "Visit command name is empty. " +
                    "Please set VisitCommandName in the config file (default: 'visit').");
            }
            else
            {
                string commandName = config.VisitCommandName.Trim();
                if (commandName.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
                {
                    result.AddWarning(
                        $"Visit command name '{commandName}' contains special characters. " +
                        "Command names should typically be alphanumeric with underscores only.");
                }
            }

            int permissionValue = (int)config.VisitPermission;
            if (permissionValue < 0 || permissionValue > 4)
            {
                result.AddError(
                    $"Visit permission level is {permissionValue}, which is out of range. " +
                    "Valid values are: 0=Everyone, 1=Subscriber, 2=VIP, 3=Moderator, 4=Broadcaster. " +
                    "Please set VisitPermission to a value between 0 and 4.");
            }

            if (config.MaxPoolSize < 0)
            {
                result.AddError(
                    $"MaxPoolSize is {config.MaxPoolSize}, which is negative. " +
                    "Please set MaxPoolSize to 0 (unlimited) or a positive number.");
            }

            if (config.PoolTimeoutSeconds < 0)
            {
                result.AddError(
                    $"PoolTimeoutSeconds is {config.PoolTimeoutSeconds}. " +
                    "Please set PoolTimeoutSeconds to 0 (no timeout) or a positive number.");
            }

            int selectionMethodValue = (int)config.SelectionMethod;
            if (selectionMethodValue < 0 || selectionMethodValue > 1)
            {
                result.AddError(
                    $"SelectionMethod is {selectionMethodValue}, which is out of range. " +
                    "Valid values are: 0=Random, 1=FIFO. " +
                    "Please set SelectionMethod to 0 or 1.");
            }

            if (config.UserCooldownSeconds < 0)
            {
                result.AddError(
                    $"UserCooldownSeconds is {config.UserCooldownSeconds}, which is negative. " +
                    "Please set UserCooldownSeconds to 0 (disabled) or a positive number.");
            }

            if (config.EnablePredefinedNames)
            {
                if (string.IsNullOrWhiteSpace(config.PredefinedNamesFilePath))
                {
                    result.AddError(
                        "EnablePredefinedNames is enabled but PredefinedNamesFilePath is not set. " +
                        "Please provide a valid file path.");
                }
                else if (!System.IO.File.Exists(config.PredefinedNamesFilePath))
                {
                    result.AddWarning(
                        $"Predefined names file not found at path: {config.PredefinedNamesFilePath}. " +
                        "The file will be created automatically if it doesn't exist, or you can create it manually with one name per line.");
                }
            }

            return result;
        }

        private static void LogConfigurationSummary()
        {
            ModConfiguration config = GetConfiguration();

            Logger.Info("=== Configuration Summary ===");
            Logger.Info($"Enabled: {config.Enabled}");

            if (config.Enabled)
            {
                string maskedToken = string.IsNullOrWhiteSpace(config.OAuthToken)
                    ? "(not set)"
                    : config.OAuthToken.Length > 8
                        ? config.OAuthToken.Substring(0, 4) + "..." + config.OAuthToken.Substring(config.OAuthToken.Length - 4)
                        : "***";

                Logger.Info($"OAuth Token: {maskedToken}");
                Logger.Info($"Channel Name: {(string.IsNullOrWhiteSpace(config.ChannelName) ? "(not set)" : config.ChannelName)}");
                Logger.Info($"Command Prefix: '{config.CommandPrefix}'");
                Logger.Info($"Visit Command: '{config.VisitCommandName}'");
                Logger.Info($"Permission Level: {config.VisitPermission} ({GetPermissionLevelName(config.VisitPermission)})");

                string spawnMode = config.EnableImmediateSpawn ? "Immediate Spawn" : "Pool Mode";
                Logger.Info($"Spawn Mode: {spawnMode}");

                if (!config.EnableImmediateSpawn)
                {
                    Logger.Info($"Max Pool Size: {(config.MaxPoolSize == 0 ? "Unlimited" : config.MaxPoolSize.ToString())}");
                    Logger.Info($"Pool Timeout: {(config.PoolTimeoutSeconds == 0 ? "None" : $"{config.PoolTimeoutSeconds} seconds")}");
                }

                string selectionMethodName = config.SelectionMethod == QueueSelectionMethod.Random ? "Random" : "FIFO";
                Logger.Info($"Queue Selection Method: {selectionMethodName}");
                Logger.Info($"User Cooldown: {(config.UserCooldownSeconds == 0 ? "Disabled" : $"{config.UserCooldownSeconds} seconds")}");
                Logger.Info($"Predefined Names: {(config.EnablePredefinedNames ? $"Enabled ({config.PredefinedNamesFilePath})" : "Disabled")}");
            }

            Logger.Info("============================");
        }

        private static string GetPermissionLevelName(PermissionLevel level)
        {
            return level switch
            {
                PermissionLevel.Everyone => "Everyone",
                PermissionLevel.Subscriber => "Subscriber",
                PermissionLevel.VIP => "VIP",
                PermissionLevel.Moderator => "Moderator",
                PermissionLevel.Broadcaster => "Broadcaster",
                _ => $"Unknown ({level})"
            };
        }
    }
}

