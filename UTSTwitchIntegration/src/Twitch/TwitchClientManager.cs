#nullable disable
using System;
using System.Collections.Concurrent;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using UTSTwitchIntegration.Config;
using UTSTwitchIntegration.Models;
using UTSTwitchIntegration.Utils;

namespace UTSTwitchIntegration.Twitch
{
    /// <summary>
    /// Twitch client manager for handling Twitch connections and messages
    /// </summary>
    public class TwitchClientManager
    {
        private TwitchClient _client;
        private readonly ModConfiguration _config;
        private readonly CommandParser _commandParser;
        private readonly ConcurrentQueue<Action> _mainThreadActions;
        private volatile bool _isConnected;
        private volatile bool _isConnecting;
        private bool _isAnonymousMode = false;

        /// <summary>
        /// Reconnection state
        /// </summary>
        private int _reconnectAttempts = 0;
        private float _reconnectScheduledTime = 0f;
        private const int MAX_RECONNECT_ATTEMPTS = 10;
        private bool _tokenInvalid = false;

        public event Action<TwitchCommand> OnCommandReceived;

        public bool IsConnected => _isConnected;

        /// <summary>
        /// Get whether connection is in anonymous mode (no OAuth token)
        /// </summary>
        public bool IsAnonymousMode => _isAnonymousMode;

        public TwitchClientManager(ModConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _commandParser = new CommandParser(config);
            _mainThreadActions = new ConcurrentQueue<Action>();
        }

        public void Connect()
        {
            if (_isConnecting || _isConnected)
            {
                Logger.Warning("Twitch client is already connecting or connected");
                return;
            }

            if (!_config.Enabled)
            {
                Logger.Info("Twitch integration is disabled in configuration");
                return;
            }

            string normalizedToken;
            string connectionUsername;

            if (string.IsNullOrWhiteSpace(_config.OAuthToken))
            {
                Logger.Info("OAuth token not configured. Attempting anonymous connection...");
                Logger.Info("Anonymous mode: Permission checking unavailable, treating all users as 'Everyone'.");
                _isAnonymousMode = true;

                Random random = new Random();
                connectionUsername = $"justinfan{random.Next(10000, 99999)}";
                normalizedToken = "";
            }
            else
            {
                _isAnonymousMode = false;
                connectionUsername = _config.ChannelName.Trim().ToLowerInvariant();
                if (connectionUsername.StartsWith("#"))
                {
                    connectionUsername = connectionUsername.Substring(1);
                }

                normalizedToken = _config.OAuthToken.Trim();
                if (!normalizedToken.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase))
                {
                    normalizedToken = "oauth:" + normalizedToken;
                    Logger.Debug("Added 'oauth:' prefix to token");
                }
            }

            if (string.IsNullOrWhiteSpace(_config.ChannelName))
            {
                Logger.Error("Channel name is not configured. Please set ChannelName in config file.");
                return;
            }

            string channelName = _config.ChannelName.Trim();
            if (channelName.StartsWith("#"))
            {
                channelName = channelName.Substring(1);
                Logger.Debug("Removed '#' prefix from channel name");
            }

            if (channelName.Contains(" ") || channelName.Contains("/") || channelName.Contains("\\"))
            {
                Logger.Error("Channel name contains invalid characters. Please use only alphanumeric characters and underscores.");
                return;
            }

            string normalizedChannelName = channelName.ToLowerInvariant();

            try
            {
                _isConnecting = true;
                Logger.Info("Connecting to Twitch IRC...");

                ConnectionCredentials credentials = new ConnectionCredentials(
                    connectionUsername,
                    normalizedToken);

                _client = new TwitchClient();
                _client.Initialize(credentials, normalizedChannelName);

                _client.OnConnected += OnTwitchConnected;
                _client.OnDisconnected += OnTwitchDisconnected;
                _client.OnConnectionError += OnTwitchConnectionError;
                _client.OnMessageReceived += OnTwitchMessageReceived;
                _client.OnJoinedChannel += OnTwitchJoinedChannel;
                _client.OnLeftChannel += OnTwitchLeftChannel;

                _client.Connect();
            }
            catch (Exception ex)
            {
                _isConnecting = false;
                Logger.Error($"Failed to connect to Twitch: {ex.Message}");

                // Provide help for common errors
                string errorMessage = ex.Message.ToLowerInvariant();
                if (errorMessage.Contains("authentication") || errorMessage.Contains("token") ||
                    errorMessage.Contains("unauthorized") || errorMessage.Contains("invalid"))
                {
                    Logger.Info("Your OAuth token may be invalid or expired.");
                    Logger.Info("Generate a new token at: https://twitchtokengenerator.com/");
                    Logger.Info("Make sure to select 'chat:read' scope when generating the token.");
                }

                Logger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }

        public void Disconnect()
        {
            if (!_isConnected && !_isConnecting)
            {
                return;
            }

            try
            {
                Logger.Info("Disconnecting from Twitch...");
                _client?.Disconnect();
                _isConnected = false;
                _isConnecting = false;
                Logger.Info("Disconnected from Twitch");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error disconnecting from Twitch: {ex.Message}");
            }
        }

        public void Reconnect()
        {
            Logger.Info("Attempting to reconnect to Twitch...");
            Disconnect();
            System.Threading.Thread.Sleep(2000);
            Connect();
        }

        private void TryScheduleReconnect()
        {
            if (_tokenInvalid)
            {
                Logger.Debug("Reconnection skipped: token is invalid");
                return;
            }

            if (_reconnectAttempts >= MAX_RECONNECT_ATTEMPTS)
            {
                Logger.Error($"Max reconnection attempts ({MAX_RECONNECT_ATTEMPTS}) reached. Stopping reconnect.");
                return;
            }

            _reconnectAttempts++;
            float backoff = UnityEngine.Mathf.Min(UnityEngine.Mathf.Pow(2, _reconnectAttempts - 1), 30f);
            _reconnectScheduledTime = UnityEngine.Time.time + backoff;

            Logger.Info($"Reconnecting in {backoff:F1} seconds (attempt {_reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS})...");
        }

        public void CheckAndProcessReconnect()
        {
            if (_reconnectScheduledTime > 0 && UnityEngine.Time.time >= _reconnectScheduledTime)
            {
                _reconnectScheduledTime = 0f;
                Logger.Info($"Attempting reconnection (attempt {_reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS})...");

                try
                {
                    Connect();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Reconnection attempt failed: {ex.Message}");
                    Logger.Debug($"Stack trace: {ex.StackTrace}");
                }
            }
        }

        public void Cleanup()
        {
            try
            {
                if (_client != null)
                {
                    _client.OnConnected -= OnTwitchConnected;
                    _client.OnDisconnected -= OnTwitchDisconnected;
                    _client.OnConnectionError -= OnTwitchConnectionError;
                    _client.OnMessageReceived -= OnTwitchMessageReceived;
                    _client.OnJoinedChannel -= OnTwitchJoinedChannel;
                    _client.OnLeftChannel -= OnTwitchLeftChannel;
                    Logger.Debug("Unsubscribed from Twitch events");
                }

                while (_mainThreadActions.TryDequeue(out _)) { }
                Logger.Debug("Cleared main thread action queue");

                Disconnect();
                _client = null;
                Logger.Info("Twitch client cleanup completed");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during Twitch client cleanup: {ex.Message}");
                Logger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Process queued actions
        /// </summary>
        public void ProcessMainThreadActions()
        {
            while (_mainThreadActions.TryDequeue(out Action action))
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error executing main thread action: {ex.Message}");
                    Logger.Debug($"Stack trace: {ex.StackTrace}");
                }
            }
        }

        #region Twitch Event Handlers

        private void OnTwitchConnected(object sender, OnConnectedArgs e)
        {
            _isConnected = true;
            _isConnecting = false;
            _reconnectAttempts = 0;
            _reconnectScheduledTime = 0f;
            _tokenInvalid = false;

            if (_isAnonymousMode)
            {
                Logger.Success($"Connected to Twitch IRC anonymously as {e.BotUsername}");
            }
            else
            {
                Logger.Success($"Connected to Twitch IRC as {e.BotUsername}");
            }
        }

        private void OnTwitchDisconnected(object sender, EventArgs e)
        {
            _isConnected = false;
            _isConnecting = false;
            Logger.Warning("Disconnected from Twitch IRC");

            TryScheduleReconnect();
        }

        private void OnTwitchConnectionError(object sender, OnConnectionErrorArgs e)
        {
            _isConnecting = false;
            Logger.Error($"Twitch connection error: {e.Error.Message}");

            if (_isAnonymousMode)
            {
                Logger.Error("Connection to Twitch failed. Connections may be unreliable.");
            }
            else
            {
                string errorMessage = e.Error.Message.ToLowerInvariant();
                if (errorMessage.Contains("authentication") || errorMessage.Contains("token") ||
                    errorMessage.Contains("unauthorized") || errorMessage.Contains("invalid") ||
                    errorMessage.Contains("login") || errorMessage.Contains("password"))
                {
                    _tokenInvalid = true;
                    Logger.Error("OAuth token is invalid or expired.");
                    Logger.Error("Generate a new token at: https://twitchtokengenerator.com/");
                    Logger.Error("Select 'chat:read' scope, then paste the token in your config file.");
                    Logger.Error("Reconnect disabled until valid token provided.");
                    Logger.Debug($"Error details: {e.Error}");
                    return;
                }
                else if (errorMessage.Contains("network") || errorMessage.Contains("connection") ||
                         errorMessage.Contains("timeout") || errorMessage.Contains("unreachable"))
                {
                    Logger.Info("Check your internet connection and try again.");
                }
                else if (errorMessage.Contains("channel") || errorMessage.Contains("not found"))
                {
                    Logger.Info("Verify your channel name is correct and the channel exists.");
                }
            }

            Logger.Debug($"Error details: {e.Error}");
            TryScheduleReconnect();
        }

        private void OnTwitchJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            Logger.Success($"Joined channel: {e.Channel}");
        }

        private void OnTwitchLeftChannel(object sender, OnLeftChannelArgs e)
        {
            Logger.Info($"Left channel: {e.Channel}");
        }

        private void OnTwitchMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            try
            {
                TwitchCommand command = _commandParser.Parse(e.ChatMessage);

                if (command != null)
                {
                    PermissionLevel effectivePermission = _config.VisitPermission;
                    if (_isAnonymousMode)
                    {
                        effectivePermission = PermissionLevel.Everyone;

                        if (_config.VisitPermission != PermissionLevel.Everyone)
                        {
                            Logger.Debug(
                                $"Anonymous mode: VisitPermission setting '{PermissionManager.GetPermissionLevelName(_config.VisitPermission)}' " +
                                $"overridden to 'Everyone'. Configure OAuth token for permission checking.");
                        }
                    }

                    bool hasPermission = PermissionManager.HasPermission(
                        e.ChatMessage,
                        effectivePermission);

                    if (!hasPermission)
                    {
                        Logger.Debug(
                            $"User {command.Username} does not have permission for {command.CommandName} " +
                            $"(required: {PermissionManager.GetPermissionLevelName(effectivePermission)}, " +
                            $"user has: {PermissionManager.GetPermissionLevelName(command.UserRole)})");
                        return;
                    }

                    _mainThreadActions.Enqueue(() => ProcessCommand(command));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing Twitch message: {ex.Message}");
                Logger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }

        private void ProcessCommand(TwitchCommand command)
        {
            try
            {
                Logger.Info($"User {command.Username} used {command.CommandName} command");
                Logger.Debug(
                    $"Command details - User: {command.Username}, " +
                    $"Role: {PermissionManager.GetPermissionLevelName(command.UserRole)}, " +
                    $"Args: [{string.Join(", ", command.Arguments)}]");

                OnCommandReceived?.Invoke(command);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing command: {ex.Message}");
                Logger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }

        #endregion
    }
}

