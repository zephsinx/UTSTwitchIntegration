using System;
using System.Collections.Concurrent;
using System.Threading;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using UTSTwitchIntegration.Config;
using UTSTwitchIntegration.Models;
using UTSTwitchIntegration.Utils;

namespace UTSTwitchIntegration.Twitch
{
    /// <summary>
    /// Manages Twitch IRC connection, message processing, and automatic reconnection
    /// </summary>
    public class TwitchClientManager
    {
        private TwitchClient client;
        private readonly ModConfiguration config;
        private readonly CommandParser commandParser;
        private readonly ConcurrentQueue<Action> mainThreadActions;
        private volatile bool isConnected;
        private volatile bool isConnecting;
        private bool isAnonymousMode;

        /// <summary>
        /// Reconnection state
        /// </summary>
        private int reconnectAttempts;

        private volatile float reconnectScheduledTime;
        private const int MAX_RECONNECT_ATTEMPTS = 10;
        private volatile bool tokenInvalid;

        public event Action<TwitchCommand> OnCommandReceived;

        public TwitchClientManager(ModConfiguration config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.commandParser = new CommandParser(config);
            this.mainThreadActions = new ConcurrentQueue<Action>();
        }

        public void Connect()
        {
            if (this.isConnecting || this.isConnected)
            {
                ModLogger.Warning("Twitch client is already connecting or connected");
                return;
            }

            if (!this.config.Enabled)
            {
                ModLogger.Debug("Twitch integration is disabled in configuration");
                return;
            }

            string normalizedToken;
            string connectionUsername;

            if (string.IsNullOrWhiteSpace(this.config.OAuthToken))
            {
                ModLogger.Info("OAuth token not configured. Attempting anonymous connection...");
                ModLogger.Info("Anonymous mode: Permission checking unavailable, treating all users as 'Everyone'.");
                this.isAnonymousMode = true;

                Random random = new Random();
                connectionUsername = $"justinfan{random.Next(10000, 99999)}";
                normalizedToken = "";
            }
            else
            {
                this.isAnonymousMode = false;
                connectionUsername = this.config.ChannelName.Trim().ToLowerInvariant();
                if (connectionUsername.StartsWith("#"))
                {
                    connectionUsername = connectionUsername[1..];
                }

                normalizedToken = this.config.OAuthToken.Trim();
                if (!normalizedToken.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase))
                {
                    normalizedToken = "oauth:" + normalizedToken;
                    ModLogger.Debug("Added 'oauth:' prefix to token");
                }
            }

            if (string.IsNullOrWhiteSpace(this.config.ChannelName))
            {
                ModLogger.Error("Channel name is not configured. Please set ChannelName in config file.");
                return;
            }

            string channelName = this.config.ChannelName.Trim();
            if (channelName.StartsWith("#"))
            {
                channelName = channelName[1..];
                ModLogger.Debug("Removed '#' prefix from channel name");
            }

            if (channelName.Contains(" ") || channelName.Contains("/") || channelName.Contains("\\"))
            {
                ModLogger.Error("Channel name contains invalid characters. Please use only alphanumeric characters and underscores.");
                return;
            }

            string normalizedChannelName = channelName.ToLowerInvariant();

            try
            {
                this.isConnecting = true;
                ModLogger.Info("Connecting to Twitch...");

                ConnectionCredentials credentials = new ConnectionCredentials(
                    connectionUsername,
                    normalizedToken);

                this.client = new TwitchClient();
                this.client.Initialize(credentials, normalizedChannelName);

                this.client.OnConnected += OnTwitchConnected;
                this.client.OnDisconnected += OnTwitchDisconnected;
                this.client.OnConnectionError += OnTwitchConnectionError;
                this.client.OnMessageReceived += OnTwitchMessageReceived;
                this.client.OnJoinedChannel += OnTwitchJoinedChannel;
                this.client.OnLeftChannel += OnTwitchLeftChannel;

                this.client.Connect();
            }
            catch (Exception ex)
            {
                this.isConnecting = false;
                ModLogger.Error($"Failed to connect to Twitch: {ex.Message}");

                // Provide help for common errors
                string errorMessage = ex.Message.ToLowerInvariant();
                if (errorMessage.Contains("authentication") || errorMessage.Contains("token") ||
                    errorMessage.Contains("unauthorized") || errorMessage.Contains("invalid"))
                {
                    ModLogger.Debug("Your OAuth token may be invalid or expired.");
                    ModLogger.Debug("Generate a new token at: https://twitchtokengenerator.com/");
                    ModLogger.Debug("Make sure to select 'chat:read' scope when generating the token.");
                }

                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }

        private void Disconnect()
        {
            if (!this.isConnected && !this.isConnecting)
            {
                return;
            }

            try
            {
                ModLogger.Debug("Disconnecting from Twitch...");
                this.client?.Disconnect();
                this.isConnected = false;
                this.isConnecting = false;
                ModLogger.Debug("Disconnected from Twitch");
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error disconnecting from Twitch: {ex.Message}");
            }
        }

        private void TryScheduleReconnect()
        {
            if (this.tokenInvalid)
            {
                ModLogger.Debug("Reconnection skipped: token is invalid");
                return;
            }

            if (this.reconnectAttempts >= MAX_RECONNECT_ATTEMPTS)
            {
                ModLogger.Error($"Max reconnection attempts ({MAX_RECONNECT_ATTEMPTS}) reached. Stopping reconnect.");
                return;
            }

            int newAttemptCount = Interlocked.Increment(ref this.reconnectAttempts);
            float backoff = UnityEngine.Mathf.Min(UnityEngine.Mathf.Pow(2, newAttemptCount - 1), 30f);
            this.reconnectScheduledTime = UnityEngine.Time.time + backoff;

            ModLogger.Debug($"Reconnecting in {backoff:F1} seconds (attempt {newAttemptCount}/{MAX_RECONNECT_ATTEMPTS})...");
        }

        public void CheckAndProcessReconnect()
        {
            if (this.reconnectScheduledTime > 0 && UnityEngine.Time.time >= this.reconnectScheduledTime)
            {
                this.reconnectScheduledTime = 0f;
                ModLogger.Debug($"Attempting reconnection (attempt {this.reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS})...");

                try
                {
                    Connect();
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"Reconnection attempt failed: {ex.Message}");
                    ModLogger.Debug($"Stack trace: {ex.StackTrace}");
                }
            }
        }

        public void Cleanup()
        {
            try
            {
                if (this.client != null)
                {
                    this.client.OnConnected -= OnTwitchConnected;
                    this.client.OnDisconnected -= OnTwitchDisconnected;
                    this.client.OnConnectionError -= OnTwitchConnectionError;
                    this.client.OnMessageReceived -= OnTwitchMessageReceived;
                    this.client.OnJoinedChannel -= OnTwitchJoinedChannel;
                    this.client.OnLeftChannel -= OnTwitchLeftChannel;
                    ModLogger.Debug("Unsubscribed from Twitch events");
                }

                while (this.mainThreadActions.TryDequeue(out _))
                {
                }

                ModLogger.Debug("Cleared main thread action queue");

                Disconnect();
                this.client = null;
                ModLogger.Debug("Twitch client cleanup completed");
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error during Twitch client cleanup: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Process queued actions
        /// </summary>
        public void ProcessMainThreadActions()
        {
            while (this.mainThreadActions.TryDequeue(out Action action))
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"Error executing main thread action: {ex.Message}");
                    ModLogger.Debug($"Stack trace: {ex.StackTrace}");
                }
            }
        }

        #region Twitch Event Handlers

        private void OnTwitchConnected(object sender, OnConnectedArgs e)
        {
            this.isConnected = true;
            this.isConnecting = false;
            this.reconnectAttempts = 0;
            this.reconnectScheduledTime = 0f;
            this.tokenInvalid = false;

            ModLogger.Success(this.isAnonymousMode
                ? $"Connected to Twitch anonymously as {e.BotUsername}"
                : $"Connected to Twitch as {e.BotUsername}");
        }

        private void OnTwitchDisconnected(object sender, EventArgs e)
        {
            this.isConnected = false;
            this.isConnecting = false;
            ModLogger.Warning("Disconnected from Twitch");

            TryScheduleReconnect();
        }

        private void OnTwitchConnectionError(object sender, OnConnectionErrorArgs e)
        {
            this.isConnecting = false;
            ModLogger.Error($"Twitch connection error: {e.Error.Message}");

            if (this.isAnonymousMode)
            {
                ModLogger.Error("Connection to Twitch failed. Connections may be unreliable.");
            }
            else
            {
                string errorMessage = e.Error.Message.ToLowerInvariant();
                if (errorMessage.Contains("authentication") || errorMessage.Contains("token") ||
                    errorMessage.Contains("unauthorized") || errorMessage.Contains("invalid") ||
                    errorMessage.Contains("login") || errorMessage.Contains("password"))
                {
                    this.tokenInvalid = true;
                    ModLogger.Error("OAuth token is invalid or expired.");
                    ModLogger.Error("Generate a new token at: https://twitchtokengenerator.com/");
                    ModLogger.Error("Select 'chat:read' scope, then paste the token in your config file.");
                    ModLogger.Error("Reconnect disabled until valid token provided.");
                    ModLogger.Debug($"Error details: {e.Error}");
                    return;
                }

                if (errorMessage.Contains("network") || errorMessage.Contains("connection") ||
                    errorMessage.Contains("timeout") || errorMessage.Contains("unreachable"))
                {
                    ModLogger.Debug("Check your internet connection and try again.");
                }
                else if (errorMessage.Contains("channel") || errorMessage.Contains("not found"))
                {
                    ModLogger.Debug("Verify your channel name is correct and the channel exists.");
                }
            }

            ModLogger.Debug($"Error details: {e.Error}");
            TryScheduleReconnect();
        }

        private static void OnTwitchJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            ModLogger.Success($"Joined channel: {e.Channel}");
        }

        private static void OnTwitchLeftChannel(object sender, OnLeftChannelArgs e)
        {
            ModLogger.Info($"Left channel: {e.Channel}");
        }

        private void OnTwitchMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            try
            {
                TwitchCommand command = this.commandParser.Parse(e.ChatMessage);

                if (command == null)
                    return;

                PermissionLevel effectivePermission = this.config.VisitPermission;
                if (this.isAnonymousMode)
                {
                    effectivePermission = PermissionLevel.Everyone;

                    if (this.config.VisitPermission != PermissionLevel.Everyone)
                    {
                        ModLogger.Debug(
                            $"Anonymous mode: VisitPermission setting '{PermissionManager.GetPermissionLevelName(this.config.VisitPermission)}' " +
                            "overridden to 'Everyone'. Configure OAuth token for permission checking.");
                    }
                }

                bool hasPermission = PermissionManager.HasPermission(
                    e.ChatMessage,
                    effectivePermission);

                if (!hasPermission)
                {
                    ModLogger.Debug(
                        $"User {command.Username} does not have permission for {command.CommandName} " +
                        $"(required: {PermissionManager.GetPermissionLevelName(effectivePermission)}, " +
                        $"user has: {PermissionManager.GetPermissionLevelName(command.UserRole)})");
                    return;
                }

                this.mainThreadActions.Enqueue(() => ProcessCommand(command));
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error processing Twitch message: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }

        private void ProcessCommand(TwitchCommand command)
        {
            try
            {
                ModLogger.Debug($"User {command.Username} used {command.CommandName} command");
                ModLogger.Debug(
                    $"Command details - User: {command.Username}, " +
                    $"Role: {PermissionManager.GetPermissionLevelName(command.UserRole)}, " +
                    $"Args: [{string.Join(", ", command.Arguments)}]");

                OnCommandReceived?.Invoke(command);
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error processing command: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }

        #endregion
    }
}