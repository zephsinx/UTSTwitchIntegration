using MelonLoader;
using Il2CppGame.Shop;
using UTSTwitchIntegration.Config;
using UTSTwitchIntegration.Game;
using UTSTwitchIntegration.Twitch;
using UTSTwitchIntegration.Utils;

[assembly: MelonInfo(typeof(UTSTwitchIntegration.Plugin), "UTSTwitchIntegration", "1.1.3", "zephsinx")]
[assembly: MelonGame("RakTwo_SteelBox", "UltimateTheaterSimulator")]

namespace UTSTwitchIntegration
{
    public class Plugin : MelonMod
    {
        private HarmonyLib.Harmony harmony;
        private const string HARMONY_ID = "com.uts.twitch-integration";
        private TwitchClientManager twitchClient;
        private SpawnManager spawnManager;
        private CooldownManager cooldownManager;

        public override void OnInitializeMelon()
        {
            ModLogger.Initialize(LoggerInstance);
            ModLogger.SetLogLevel(LogLevel.Info);

            ModLogger.Info("UTSTwitchIntegration mod loaded successfully!");

            try
            {
                ConfigManager.Initialize();
                ModConfiguration config = ConfigManager.GetConfiguration();
                ModLogger.SetLogLevel((LogLevel)config.LogLevel);
                ModLogger.Debug("Configuration loaded");
                ModLogger.Debug($"Twitch integration enabled: {config.Enabled}");
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Failed to initialize configuration: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }

            try
            {
                this.harmony = new HarmonyLib.Harmony(HARMONY_ID);
                this.harmony.PatchAll();
                ModLogger.Debug("Harmony patches applied successfully");
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Failed to apply Harmony patches: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }

            try
            {
                this.spawnManager = SpawnManager.Instance;
                ModConfiguration config = ConfigManager.GetConfiguration();
                string spawnMode = config.EnableImmediateSpawn ? "immediate spawn mode" : "pool mode";
                ModLogger.Info($"Spawn manager initialized ({spawnMode})");
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Failed to initialize spawn manager: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }

            try
            {
                this.cooldownManager = new CooldownManager();
                ModLogger.Debug("Cooldown manager initialized");
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Failed to initialize cooldown manager: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }

            try
            {
                ModConfiguration config = ConfigManager.GetConfiguration();
                if (config.Enabled)
                {
                    this.twitchClient = new TwitchClientManager(config);
                    this.twitchClient.OnCommandReceived += OnCommandReceived;
                    this.twitchClient.Connect();
                }
                else
                {
                    ModLogger.Info("Twitch integration is disabled in configuration");
                }
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Failed to initialize Twitch client: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }

        public override void OnUpdate()
        {
            if (this.twitchClient != null)
            {
                this.twitchClient.ProcessMainThreadActions();
                this.twitchClient.CheckAndProcessReconnect();
            }

            // Periodic cleanup check for destroyed customers (fallback mechanism)
            this.spawnManager?.PeriodicCleanupCheck();

            ModConfiguration config = ConfigManager.GetConfiguration();
            if (config.EnableImmediateSpawn)
            {
                if (TheaterController.Instance != null)
                {
                    this.spawnManager?.TrySpawnNextViewer();
                }
            }
        }

        public override void OnDeinitializeMelon()
        {
            ModLogger.Info("Starting mod cleanup...");

            if (this.spawnManager != null)
            {
                try
                {
                    this.spawnManager.Cleanup();
                    ModLogger.Debug("Spawn manager cleanup completed");
                }
                catch (System.Exception ex)
                {
                    ModLogger.Error($"Error cleaning up spawn manager: {ex.Message}");
                }
            }

            if (this.twitchClient != null)
            {
                try
                {
                    this.twitchClient.Cleanup();
                    this.twitchClient = null;
                    ModLogger.Debug("Twitch client cleanup completed");
                }
                catch (System.Exception ex)
                {
                    ModLogger.Error($"Error cleaning up Twitch client: {ex.Message}");
                }
            }

            if (this.harmony != null)
            {
                try
                {
                    this.harmony.UnpatchSelf();
                    ModLogger.Debug("Harmony patches removed");
                }
                catch (System.Exception ex)
                {
                    ModLogger.Error($"Error removing Harmony patches: {ex.Message}");
                }
            }

            ModLogger.Info("Mod cleanup completed");
        }

        private void OnCommandReceived(Models.TwitchCommand command)
        {
            try
            {
                ModConfiguration config = ConfigManager.GetConfiguration();
                if (command.CommandName.Equals(config.VisitCommandName, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (this.cooldownManager != null && this.cooldownManager.IsOnCooldown(command.Username, config.UserCooldownSeconds))
                    {
                        double remainingCooldown = this.cooldownManager.GetRemainingCooldown(command.Username, config.UserCooldownSeconds);
                        ModLogger.Debug($"Viewer '{command.Username}' is on cooldown ({remainingCooldown:F1} seconds remaining)");
                        return;
                    }

                    if (this.spawnManager != null)
                    {
                        // Only try to overwrite if queue is empty and setting is enabled
                        if (config.OverwriteRandomNPCOnVisit && this.spawnManager.QueueCount == 0)
                        {
                            if (this.spawnManager.TryOverwriteRandomNPC(command.Username))
                            {
                                ModLogger.Debug($"Overwrote random NPC with username '{command.Username}'");
                                this.cooldownManager?.RecordCommandUsage(command.Username);
                                return;
                            }
                        }

                        // If overwrite didn't happen or failed, use normal queue behavior
                        bool queued = this.spawnManager.QueueViewerForSpawn(command.Username);

                        this.cooldownManager?.RecordCommandUsage(command.Username);

                        if (queued)
                        {
                            string spawnMode = config.EnableImmediateSpawn ? "immediate spawn" : "pool";
                            ModLogger.Debug($"Queued viewer '{command.Username}' for {spawnMode} (Pool: {this.spawnManager.QueueCount})");
                        }
                        else
                        {
                            ModLogger.Debug($"Viewer '{command.Username}' is already in pool");
                        }
                    }
                    else
                    {
                        ModLogger.Warning("SpawnManager is null - cannot queue viewer");
                    }
                }
                else
                {
                    ModLogger.Debug($"Received unknown command: {command.CommandName} from {command.Username}");
                }
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Error handling command: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}