using System;
using System.Linq;
using TwitchLib.Client.Models;
using UTSTwitchIntegration.Config;
using UTSTwitchIntegration.Models;

namespace UTSTwitchIntegration.Twitch
{
    /// <summary>
    /// Parses Twitch chat messages to extract commands
    /// </summary>
    public class CommandParser
    {
        private readonly ModConfiguration config;

        public CommandParser(ModConfiguration config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
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

            if (!messageText.StartsWith(this.config.CommandPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string commandPart = messageText[this.config.CommandPrefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(commandPart))
            {
                return null;
            }

            string[] parts = commandPart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string commandName = parts[0].ToLowerInvariant();

            string[] arguments = parts.Length > 1 ? parts.Skip(1).ToArray() : Array.Empty<string>();

            if (commandName.Equals(this.config.VisitCommandName, StringComparison.OrdinalIgnoreCase))
            {
                return new TwitchCommand
                {
                    CommandName = commandName,
                    Username = message.Username,
                    Arguments = arguments,
                    UserRole = PermissionManager.CheckUserRole(message),
                };
            }

            return null;
        }
    }
}

