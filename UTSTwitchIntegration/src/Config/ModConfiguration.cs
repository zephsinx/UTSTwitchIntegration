namespace UTSTwitchIntegration.Config
{
    /// <summary>
    /// Permission levels for Twitch commands
    /// </summary>
    public enum PermissionLevel
    {
        Everyone = 0,
        Subscriber = 1,
        Vip = 2,
        Moderator = 3,
        Broadcaster = 4,
    }

    /// <summary>
    /// Queue selection method for choosing viewers from the pool
    /// </summary>
    public enum QueueSelectionMethod
    {
        /// <summary>
        /// Random
        /// </summary>
        Random = 0,

        /// <summary>
        /// First-in-first-out
        /// </summary>
        Fifo = 1,
    }

    public class ModConfiguration
    {
        /// <summary>
        /// Twitch OAuth token for authentication
        /// </summary>
        public string OAuthToken { get; set; } = "";

        /// <summary>
        /// Twitch channel name to connect to (lowercase, no #)
        /// </summary>
        public string ChannelName { get; set; } = "";

        /// <summary>
        /// Command prefix (default: !)
        /// </summary>
        public string CommandPrefix { get; set; } = "!";

        /// <summary>
        /// Visit command name (default: visit)
        /// </summary>
        public string VisitCommandName { get; set; } = "visit";

        /// <summary>
        /// Minimum permission level required for !visit command
        /// </summary>
        public PermissionLevel VisitPermission { get; set; } = PermissionLevel.Everyone;

        /// <summary>
        /// Enable/disable Twitch integration
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Enable immediate spawning for testing (spawns NPCs immediately when !visit used)
        /// </summary>
        public bool EnableImmediateSpawn { get; set; }

        /// <summary>
        /// Maximum pool size (0 = unlimited)
        /// </summary>
        public int MaxPoolSize { get; set; } = 300;

        /// <summary>
        /// Pool entry timeout in seconds (0 = no timeout)
        /// </summary>
        public int PoolTimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// Queue selection method (Random or FIFO)
        /// </summary>
        public QueueSelectionMethod SelectionMethod { get; set; } = QueueSelectionMethod.Random;

        /// <summary>
        /// User cooldown in seconds (0 = disabled)
        /// How long users must wait between !visit commands
        /// </summary>
        public int UserCooldownSeconds { get; set; } = 60;

        /// <summary>
        /// Enable predefined names when queue is empty
        /// </summary>
        public bool EnablePredefinedNames { get; set; }

        /// <summary>
        /// Path to predefined names file (one name per line)
        /// </summary>
        public string PredefinedNamesFilePath { get; set; } = "UserData/predefined_names.txt";

        /// <summary>
        /// Log verbosity level (0=Error, 1=Warning, 2=Info, 3=Debug)
        /// </summary>
        public int LogLevel { get; set; } = 2;
    }
}