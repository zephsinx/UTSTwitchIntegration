using MelonLoader;
using Il2CppInterop.Runtime.Injection;
using UTSTwitchIntegration.Config;
using UTSTwitchIntegration.Game;
using UTSTwitchIntegration.Twitch;
using UTSTwitchIntegration.Utils;

[assembly: MelonInfo(typeof(UTSTwitchIntegration.Plugin), "UTSTwitchIntegration", "1.0.0", "zephsinx")]
[assembly: MelonGame("RakTwo_SteelBox", "UltimateTheaterSimulator")]

namespace UTSTwitchIntegration
{
    public class Plugin : MelonMod
    {
        private const LogLevel LOG_LEVEL = LogLevel.Info;

        private HarmonyLib.Harmony harmony;
        private const string HARMONY_ID = "com.uts.twitch-integration";
        private TwitchClientManager twitchClient;
        private SpawnManager spawnManager;
        private CooldownManager cooldownManager;

        public override void OnInitializeMelon()
        {
            Logger.Initialize(LoggerInstance);
            Logger.SetLogLevel(LOG_LEVEL);

            Logger.Info("UTSTwitchIntegration mod loaded successfully!");

            try
            {
                ClassInjector.RegisterTypeInIl2Cpp<UsernameDisplayUpdater>();
                Logger.Debug("Registered UsernameDisplayUpdater with Il2CppInterop");
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Failed to register UsernameDisplayUpdater with Il2CppInterop: {ex.Message}");
                Logger.Debug($"Stack trace: {ex.StackTrace}");
            }

            try
            {
                ConfigManager.Initialize();
                ModConfiguration config = ConfigManager.GetConfiguration();
                Logger.Info("Configuration loaded");
                Logger.Debug($"Twitch integration enabled: {config.Enabled}");

                // Load predefined names if enabled
                if (config.EnablePredefinedNames)
                {
                    bool loaded = PredefinedNamesManager.Instance.LoadNamesFromFile(config.PredefinedNamesFilePath);
                    if (loaded)
                    {
                        Logger.Info($"Predefined names loaded: {PredefinedNamesManager.Instance.Count} names available");
                    }
                    else
                    {
                        Logger.Warning("Failed to load predefined names - feature disabled");
                    }
                }
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
                Logger.Info("Harmony patches applied successfully");
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

            ModConfiguration config = ConfigManager.GetConfiguration();
            if (config.EnableImmediateSpawn)
            {
                this.spawnManager?.TrySpawnNextViewer();
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

            try
            {
                UsernameDisplayManager.CleanupAllDisplays();
                Logger.Debug("Username displays cleanup completed");
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error cleaning up username displays: {ex.Message}");
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
                    // Check cooldown before processing command
                    if (this.cooldownManager != null && this.cooldownManager.IsOnCooldown(command.Username, config.UserCooldownSeconds))
                    {
                        double remainingCooldown = this.cooldownManager.GetRemainingCooldown(command.Username, config.UserCooldownSeconds);
                        Logger.Debug($"Viewer '{command.Username}' is on cooldown ({remainingCooldown:F1} seconds remaining)");
                        return;
                    }

                    if (this.spawnManager != null)
                    {
                        bool queued = this.spawnManager.QueueViewerForSpawn(command.Username);

                        this.cooldownManager?.RecordCommandUsage(command.Username);

                        if (queued)
                        {
                            string spawnMode = config.EnableImmediateSpawn ? "immediate spawn" : "pool";
                            Logger.Info($"Queued viewer '{command.Username}' for {spawnMode} (Pool: {this.spawnManager.QueueCount})");
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