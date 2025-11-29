using System.Linq;
using TwitchLib.Client.Models;
using UTSTwitchIntegration.Config;
using UTSTwitchIntegration.Utils;

namespace UTSTwitchIntegration.Twitch
{
    /// <summary>
    /// Checks Twitch user permissions based on badges (broadcaster, moderator, VIP, subscriber)
    /// </summary>
    public static class PermissionManager
    {
        /// <summary>
        /// Check if a user has the required permission level
        /// </summary>
        /// <param name="message">Chat message containing user information</param>
        /// <param name="requiredLevel">Minimum permission level required</param>
        public static bool HasPermission(ChatMessage message, PermissionLevel requiredLevel)
        {
            if (message == null)
            {
                ModLogger.Warning("Permission check failed: message is null");
                return false;
            }

            PermissionLevel userLevel = CheckUserRole(message);
            return userLevel >= requiredLevel;
        }

        /// <summary>
        /// Check user's permission level based on badges
        /// </summary>
        /// <param name="message">Chat message containing user badges</param>
        /// <returns>User's permission level</returns>
        public static PermissionLevel CheckUserRole(ChatMessage message)
        {
            if (message == null)
            {
                return PermissionLevel.Everyone;
            }

            if (message.IsBroadcaster)
            {
                return PermissionLevel.Broadcaster;
            }

            if (message.Badges == null || message.Badges.Count <= 0)
                return PermissionLevel.Everyone;

            if (message.Badges.Any(b => b.Key == "moderator") || message.IsModerator)
            {
                return PermissionLevel.Moderator;
            }

            if (message.Badges.Any(b => b.Key == "vip"))
            {
                return PermissionLevel.Vip;
            }

            if (message.Badges.Any(b => b.Key == "subscriber") || message.IsSubscriber)
            {
                return PermissionLevel.Subscriber;
            }

            return PermissionLevel.Everyone;
        }

        /// <summary>
        /// Get a string representation of a permission level
        /// </summary>
        public static string GetPermissionLevelName(PermissionLevel level)
        {
            return level switch
            {
                PermissionLevel.Everyone => "Everyone",
                PermissionLevel.Subscriber => "Subscriber",
                PermissionLevel.Vip => "VIP",
                PermissionLevel.Moderator => "Moderator",
                PermissionLevel.Broadcaster => "Broadcaster",
                _ => "Unknown",
            };
        }
    }
}

