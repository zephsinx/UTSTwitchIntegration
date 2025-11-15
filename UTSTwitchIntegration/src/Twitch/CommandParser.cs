#nullable disable
using System;
using System.Linq;
using TwitchLib.Client.Models;
using UTSTwitchIntegration.Config;
using UTSTwitchIntegration.Models;
using UTSTwitchIntegration.Utils;

namespace UTSTwitchIntegration.Twitch
{
    /// <summary>
    /// Parses Twitch chat messages to extract commands
    /// </summary>
    public class CommandParser
    {
        private readonly ModConfiguration _config;

        public CommandParser(ModConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Parse a chat message to extract command information
        /// </summary>
        /// <param name="message">Chat message from Twitch</param>
        /// <returns>Parsed command or null if message is not a valid command</returns>
        public TwitchCommand Parse(ChatMessage message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.Message))
            {
                return null;
            }

            string messageText = message.Message.Trim();

            if (!messageText.StartsWith(_config.CommandPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string commandPart = messageText.Substring(_config.CommandPrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(commandPart))
            {
                return null;
            }

            string[] parts = commandPart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string commandName = parts[0].ToLowerInvariant();

            string[] arguments = parts.Length > 1 ? parts.Skip(1).ToArray() : new string[0];

            if (commandName.Equals(_config.VisitCommandName, StringComparison.OrdinalIgnoreCase))
            {
                return new TwitchCommand
                {
                    CommandName = commandName,
                    Username = message.Username,
                    Arguments = arguments,
                    UserRole = PermissionManager.CheckUserRole(message),
                    Timestamp = DateTime.Now,
                    OriginalMessage = messageText
                };
            }

            Logger.Debug($"Unknown command: {commandName} from user {message.Username}");
            return null;
        }
    }
}

