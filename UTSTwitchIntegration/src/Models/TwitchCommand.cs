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
        public string[] Arguments { get; set; } = Array.Empty<string>();

        /// <summary>
        /// User's permission level
        /// </summary>
        public PermissionLevel UserRole { get; set; }
    }
}