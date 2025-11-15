#nullable disable
using System;
using UTSTwitchIntegration.Config;

namespace UTSTwitchIntegration.Models
{
    /// <summary>
    /// Twitch chat command model
    /// </summary>
    public class TwitchCommand
    {
        /// <summary>
        /// Name of the command (e.g., "visit")
        /// </summary>
        public string CommandName { get; set; }

        /// <summary>
        /// Username of the user who sent the command
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Command arguments (if any)
        /// </summary>
        public string[] Arguments { get; set; }

        /// <summary>
        /// User's permission level
        /// </summary>
        public PermissionLevel UserRole { get; set; }

        /// <summary>
        /// Timestamp when command was received
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Original chat message
        /// </summary>
        public string OriginalMessage { get; set; }

        public TwitchCommand()
        {
            Arguments = new string[0];
            Timestamp = DateTime.Now;
        }
    }
}

