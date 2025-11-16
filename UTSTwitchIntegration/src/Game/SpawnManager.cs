using System.Collections.Concurrent;
using Il2CppGame.Shop;
using Il2CppGame.Customers;
using UnityEngine;
using UTSTwitchIntegration.Config;
using UTSTwitchIntegration.Models;
using UTSTwitchIntegration.Queue;
using UTSTwitchIntegration.Utils;
using ModLogger = UTSTwitchIntegration.Utils.Logger;

namespace UTSTwitchIntegration.Game
{
    /// <summary>
    /// Manages viewer pool and NPC spawning
    /// </summary>
    public class SpawnManager
    {
        private static SpawnManager instance;
        private static readonly object InstanceLock = new object();
        private readonly ViewerQueue queue;
        private readonly ConcurrentDictionary<CustomerController, string> spawnedViewers;

        /// <summary>
        /// Flag to indicate a spawn is from SpawnManager
        /// </summary>
        private static string pendingUsername;

        private static readonly object PendingUsernameLock = new object();

        /// <summary>
        /// Spawn rate limiting
        /// </summary>
        private volatile float lastSpawnTime;

        private const float MIN_SPAWN_INTERVAL = 5f;

        /// <summary>
        /// Singleton instance of SpawnManager
        /// </summary>
        public static SpawnManager Instance
        {
            get
            {
                if (instance != null)
                    return instance;

                lock (InstanceLock)
                {
                    instance ??= new SpawnManager();
                }

                return instance;
            }
        }

        /// <summary>
        /// Get the number of viewers in pool
        /// </summary>
        public int QueueCount => this.queue.Count;

        private SpawnManager()
        {
            this.queue = new ViewerQueue();
            this.spawnedViewers = new ConcurrentDictionary<CustomerController, string>();
        }

        /// <summary>
        /// Add a viewer to the spawn queue
        /// </summary>
        /// <param name="username">Twitch username</param>
        public bool QueueViewerForSpawn(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                ModLogger.Warning("Attempted to queue viewer with null or empty username");
                return false;
            }

            ModConfiguration config = ConfigManager.GetConfiguration();
            if (config.MaxPoolSize > 0 && this.queue.Count >= config.MaxPoolSize)
            {
                ModLogger.Warning($"Pool is full (MaxPoolSize: {config.MaxPoolSize}), cannot add viewer '{username}'");
                return false;
            }

            bool added = this.queue.Enqueue(username);

            if (added)
            {
                ModLogger.Info($"Viewer '{username}' added to spawn pool (Pool size: {this.queue.Count})");
            }
            else
            {
                ModLogger.Debug($"Viewer '{username}' is already in pool, skipping duplicate");
            }

            return added;
        }

        /// <summary>
        /// Get next username from pool for natural spawn assignment
        /// </summary>
        public string GetNextUsernameFromPool()
        {
            ModConfiguration config = ConfigManager.GetConfiguration();
            ViewerInfo viewer;

            if (config.SelectionMethod == QueueSelectionMethod.Random)
            {
                viewer = this.queue.DequeueRandom();
                ModLogger.Debug($"Retrieved username '{viewer?.Username}' from pool using Random selection (Pool size: {this.queue.Count})");
            }
            else
            {
                viewer = this.queue.Dequeue();
                ModLogger.Debug($"Retrieved username '{viewer?.Username}' from pool using FIFO selection (Pool size: {this.queue.Count})");
            }

            if (viewer != null && !string.IsNullOrWhiteSpace(viewer.Username))
            {
                return viewer.Username;
            }

            if (!config.EnablePredefinedNames)
                return null;

            string predefinedName = PredefinedNamesManager.Instance.GetRandomName();
            if (string.IsNullOrWhiteSpace(predefinedName))
                return null;

            ModLogger.Debug($"Queue empty, using predefined name: '{predefinedName}'");
            return predefinedName;
        }

        /// <summary>
        /// Attempt to spawn the next viewer from queue (immediate spawn mode)
        /// </summary>
        public void TrySpawnNextViewer()
        {
            ModConfiguration config = ConfigManager.GetConfiguration();
            if (!config.EnableImmediateSpawn)
            {
                return;
            }

            float currentTime = Time.time;
            if (currentTime - this.lastSpawnTime < MIN_SPAWN_INTERVAL)
            {
                return;
            }

            if (this.queue.Count == 0)
            {
                return;
            }

            TheaterController theaterController = TheaterController.Instance;
            if (!theaterController)
            {
                ModLogger.Warning("TheaterController.Instance is null - game may not be ready");
                return;
            }

            try
            {
                Transform _ = theaterController.transform;
            }
            catch
            {
                ModLogger.Warning("TheaterController.Instance is destroyed or invalid");
                return;
            }

            ViewerInfo viewer = config.SelectionMethod == QueueSelectionMethod.Random
                ? this.queue.DequeueRandom()
                : this.queue.Dequeue();

            if (viewer == null || string.IsNullOrWhiteSpace(viewer.Username))
            {
                return;
            }

            try
            {
                lock (PendingUsernameLock)
                {
                    pendingUsername = viewer.Username;
                }

                CustomerController customer = theaterController.SpawnCustomer();

                lock (PendingUsernameLock)
                {
                    if (pendingUsername == viewer.Username)
                    {
                        pendingUsername = null;
                    }
                }

                if (!customer)
                {
                    ModLogger.Error($"Failed to spawn customer for viewer '{viewer.Username}' - SpawnCustomer returned null");
                    return;
                }

                this.spawnedViewers.TryAdd(customer, viewer.Username);
                ModLogger.Info($"Successfully spawned viewer '{viewer.Username}' as Customer ID={customer.CustomerId}");

                this.lastSpawnTime = currentTime;
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Exception while spawning viewer '{viewer.Username}': {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");

                lock (PendingUsernameLock)
                {
                    pendingUsername = null;
                }
            }
        }

        /// <summary>
        /// Get and clear the pending username for a spawn
        /// </summary>
        /// <returns>Username if spawn is pending, null otherwise</returns>
        public static string GetAndClearPendingUsername()
        {
            lock (PendingUsernameLock)
            {
                string username = pendingUsername;
                pendingUsername = null;
                return username;
            }
        }

        /// <summary>
        /// Store username mapping for a spawned customer
        /// </summary>
        /// <param name="customer">Spawned customer</param>
        /// <param name="username">Twitch username</param>
        public void StoreViewerUsername(CustomerController customer, string username)
        {
            if (!customer)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            if (this.spawnedViewers.TryAdd(customer, username))
            {
                ModLogger.Debug($"Stored username '{username}' for Customer ID={customer.CustomerId}");
            }
            else
            {
                this.spawnedViewers[customer] = username;
                ModLogger.Debug($"Username already stored for Customer ID={customer.CustomerId}, updating to '{username}'");
            }
        }

        /// <summary>
        /// Cleanup all resources and reset state
        /// </summary>
        public void Cleanup()
        {
            try
            {
                int queueCount = this.queue.Count;
                int spawnedCount = this.spawnedViewers.Count;

                this.queue.Clear();
                ModLogger.Debug($"Cleared viewer queue ({queueCount} viewers removed)");

                this.spawnedViewers.Clear();
                ModLogger.Debug($"Cleared spawned viewer tracking ({spawnedCount} tracked viewers removed)");

                lock (PendingUsernameLock)
                {
                    pendingUsername = null;
                }

                ModLogger.Debug("Cleared pending username");

                this.lastSpawnTime = 0f;
                ModLogger.Info("Spawn manager cleanup completed");
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Error during spawn manager cleanup: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}