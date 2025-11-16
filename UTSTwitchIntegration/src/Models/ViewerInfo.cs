using System;

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
        public string Username { get; }

        public ViewerInfo(string username)
        {
            Username = username ?? throw new ArgumentNullException(nameof(username));
        }
    }
}