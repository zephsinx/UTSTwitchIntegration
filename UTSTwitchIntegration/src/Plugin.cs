#nullable disable
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
        public static readonly LogLevel LOG_LEVEL = LogLevel.Info;

        private HarmonyLib.Harmony _harmony;
        private const string HARMONY_ID = "com.uts.twitch-integration";
        private TwitchClientManager _twitchClient;
        private SpawnManager _spawnManager;
        private CooldownManager _cooldownManager;

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
                _harmony = new HarmonyLib.Harmony(HARMONY_ID);
                _harmony.PatchAll();
                Logger.Info("Harmony patches applied successfully");
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Failed to apply Harmony patches: {ex.Message}");
                Logger.Debug($"Stack trace: {ex.StackTrace}");
            }

            try
            {
                _spawnManager = SpawnManager.Instance;
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
                _cooldownManager = new CooldownManager();
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
                    _twitchClient = new TwitchClientManager(config);
                    _twitchClient.OnCommandReceived += OnCommandReceived;
                    _twitchClient.Connect();
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
            if (_twitchClient != null)
            {
                _twitchClient.ProcessMainThreadActions();
                _twitchClient.CheckAndProcessReconnect();
            }

            ModConfiguration config = ConfigManager.GetConfiguration();
            if (config.EnableImmediateSpawn)
            {
                _spawnManager?.TrySpawnNextViewer();
            }
        }

        public override void OnDeinitializeMelon()
        {
            Logger.Info("Starting mod cleanup...");

            if (_spawnManager != null)
            {
                try
                {
                    _spawnManager.Cleanup();
                    Logger.Debug("Spawn manager cleanup completed");
                }
                catch (System.Exception ex)
                {
                    Logger.Error($"Error cleaning up spawn manager: {ex.Message}");
                }
            }

            if (_twitchClient != null)
            {
                try
                {
                    _twitchClient.Cleanup();
                    _twitchClient = null;
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

            if (_harmony != null)
            {
                try
                {
                    _harmony.UnpatchSelf();
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
                    if (_cooldownManager != null && _cooldownManager.IsOnCooldown(command.Username, config.UserCooldownSeconds))
                    {
                        double remainingCooldown = _cooldownManager.GetRemainingCooldown(command.Username, config.UserCooldownSeconds);
                        Logger.Debug($"Viewer '{command.Username}' is on cooldown ({remainingCooldown:F1} seconds remaining)");
                        return;
                    }

                    if (_spawnManager != null)
                    {
                        bool queued = _spawnManager.QueueViewerForSpawn(command.Username, command.UserRole);

                        _cooldownManager?.RecordCommandUsage(command.Username);

                        if (queued)
                        {
                            string spawnMode = config.EnableImmediateSpawn ? "immediate spawn" : "pool";
                            Logger.Info($"Queued viewer '{command.Username}' for {spawnMode} (Pool: {_spawnManager.QueueCount})");
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