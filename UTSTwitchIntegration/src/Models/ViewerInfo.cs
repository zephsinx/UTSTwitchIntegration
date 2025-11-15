#nullable disable
using System;
using UTSTwitchIntegration.Config;

namespace UTSTwitchIntegration.Models
{
    /// <summary>
    /// Viewer information model
    /// </summary>
    public class ViewerInfo
    {
        /// <summary>
        /// Twitch username of the viewer
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Timestamp when viewer was added to queue
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// User's permission level
        /// </summary>
        public PermissionLevel UserRole { get; set; }

        public ViewerInfo()
        {
            Timestamp = DateTime.Now;
        }

        public ViewerInfo(string username, PermissionLevel userRole)
        {
            Username = username ?? throw new ArgumentNullException(nameof(username));
            UserRole = userRole;
            Timestamp = DateTime.Now;
        }
    }
}

