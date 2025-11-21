using MelonLoader;
using Il2CppGame.Shop;
using UTSTwitchIntegration.Config;
using UTSTwitchIntegration.Game;
using UTSTwitchIntegration.Twitch;
using UTSTwitchIntegration.Utils;

[assembly: MelonInfo(typeof(UTSTwitchIntegration.Plugin), "UTSTwitchIntegration", "1.1.0", "zephsinx")]
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
            Logger.Initialize(LoggerInstance);
            Logger.SetLogLevel(LogLevel.Info);

            Logger.Info("UTSTwitchIntegration mod loaded successfully!");

            try
            {
                ConfigManager.Initialize();
                ModConfiguration config = ConfigManager.GetConfiguration();
                Logger.SetLogLevel((LogLevel)config.LogLevel);
                Logger.Debug("Configuration loaded");
                Logger.Debug($"Twitch integration enabled: {config.Enabled}");
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Failed to initialize configuration: {ex.Message}");
                Logger.Debug($"Stack trace: {ex.StackTrace}");
            }

            try
            {
                this.harmony = new HarmonyLib.Harmony(HARMONY_ID);
                this.harmony.PatchAll();
                Logger.Debug("Harmony patches applied successfully");
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Failed to apply Harmony patches: {ex.Message}");
                Logger.Debug($"Stack trace: {ex.StackTrace}");
            }

            try
            {
                this.spawnManager = SpawnManager.Instance;
                ModConfiguration config = ConfigManager.GetConfiguration();
                string spawnMode = config.EnableImmediateSpawn ? "immediate spawn mode" : "pool mode";
                Logger.Info($"Spawn manager initialized ({spawnMode})");
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Failed to initialize spawn manager: {ex.Message}");
                Logger.Debug($"Stack trace: {ex.StackTrace}");
            }

            try
            {
                this.cooldownManager = new CooldownManager();
                Logger.Debug("Cooldown manager initialized");
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Failed to initialize cooldown manager: {ex.Message}");
                Logger.Debug($"Stack trace: {ex.StackTrace}");
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
                    Logger.Info("Twitch integration is disabled in configuration");
                }
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Failed to initialize Twitch client: {ex.Message}");
                Logger.Debug($"Stack trace: {ex.StackTrace}");
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
            Logger.Info("Starting mod cleanup...");

            if (this.spawnManager != null)
            {
                try
                {
                    this.spawnManager.Cleanup();
                    Logger.Debug("Spawn manager cleanup completed");
                }
                catch (System.Exception ex)
                {
                    Logger.Error($"Error cleaning up spawn manager: {ex.Message}");
                }
            }

            if (this.twitchClient != null)
            {
                try
                {
                    this.twitchClient.Cleanup();
                    this.twitchClient = null;
                    Logger.Debug("Twitch client cleanup completed");
                }
                catch (System.Exception ex)
                {
                    Logger.Error($"Error cleaning up Twitch client: {ex.Message}");
                }
            }

            if (this.harmony != null)
            {
                try
                {
                    this.harmony.UnpatchSelf();
                    Logger.Debug("Harmony patches removed");
                }
                catch (System.Exception ex)
                {
                    Logger.Error($"Error removing Harmony patches: {ex.Message}");
                }
            }

            Logger.Info("Mod cleanup completed");
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
                        Logger.Debug($"Viewer '{command.Username}' is on cooldown ({remainingCooldown:F1} seconds remaining)");
                        return;
                    }

                    if (this.spawnManager != null)
                    {
                        // Only try to overwrite if queue is empty and setting is enabled
                        bool attemptedOverwrite = false;
                        if (config.OverwriteRandomNPCOnVisit && this.spawnManager.QueueCount == 0)
                        {
                            attemptedOverwrite = this.spawnManager.TryOverwriteRandomNPC(command.Username);

                            if (attemptedOverwrite)
                            {
                                Logger.Info($"Overwrote random NPC with username '{command.Username}'");
                                this.cooldownManager?.RecordCommandUsage(command.Username);
                                return; // Success, don't add to queue
                            }
                        }

                        // If overwrite didn't happen or failed, use normal queue behavior
                        bool queued = this.spawnManager.QueueViewerForSpawn(command.Username);

                        this.cooldownManager?.RecordCommandUsage(command.Username);

                        if (queued)
                        {
                            string spawnMode = config.EnableImmediateSpawn ? "immediate spawn" : "pool";
                            Logger.Debug($"Queued viewer '{command.Username}' for {spawnMode} (Pool: {this.spawnManager.QueueCount})");
                        }
                        else
                        {
                            Logger.Debug($"Viewer '{command.Username}' is already in pool");
                        }
                    }
                    else
                    {
                        Logger.Warning("SpawnManager is null - cannot queue viewer");
                    }
                }
                else
                {
                    Logger.Debug($"Received unknown command: {command.CommandName} from {command.Username}");
                }
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error handling command: {ex.Message}");
                Logger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}