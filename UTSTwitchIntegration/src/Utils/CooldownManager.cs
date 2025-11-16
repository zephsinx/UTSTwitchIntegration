using System;
using System.Collections.Concurrent;

namespace UTSTwitchIntegration.Utils
{
    /// <summary>
    /// Cooldown manager for tracking per-user command cooldowns
    /// </summary>
    public class CooldownManager
    {
        private readonly ConcurrentDictionary<string, DateTime> lastCommandTime = new ConcurrentDictionary<string, DateTime>();

        /// <summary>
        /// Check if a user is currently on cooldown
        /// </summary>
        /// <param name="username">Twitch username (case-insensitive)</param>
        /// <param name="cooldownSeconds">Cooldown duration in seconds (0 = disabled)</param>
        public bool IsOnCooldown(string username, int cooldownSeconds)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            if (cooldownSeconds <= 0)
            {
                return false;
            }

            string normalizedUsername = username.ToLowerInvariant();

            if (!this.lastCommandTime.TryGetValue(normalizedUsername, out DateTime lastTime))
            {
                return false;
            }

            TimeSpan timeSinceLastCommand = DateTime.Now - lastTime;
            return timeSinceLastCommand.TotalSeconds < cooldownSeconds;
        }

        /// <summary>
        /// Record that a user has used a command (updates their cooldown timer)
        /// </summary>
        /// <param name="username">Twitch username (case-insensitive)</param>
        public void RecordCommandUsage(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            string normalizedUsername = username.ToLowerInvariant();
            this.lastCommandTime.AddOrUpdate(normalizedUsername, DateTime.Now, (key, oldValue) => DateTime.Now);
        }

        /// <summary>
        /// Get the remaining cooldown time for a user in seconds
        /// </summary>
        /// <param name="username">Twitch username (case-insensitive)</param>
        /// <param name="cooldownSeconds">Cooldown duration in seconds</param>
        public double GetRemainingCooldown(string username, int cooldownSeconds)
        {
            if (string.IsNullOrWhiteSpace(username) || cooldownSeconds <= 0)
            {
                return 0;
            }

            string normalizedUsername = username.ToLowerInvariant();

            if (!this.lastCommandTime.TryGetValue(normalizedUsername, out DateTime lastTime))
            {
                return 0;
            }

            TimeSpan timeSinceLastCommand = DateTime.Now - lastTime;
            double remaining = cooldownSeconds - timeSinceLastCommand.TotalSeconds;

            return remaining > 0 ? remaining : 0;
        }
    }
}

