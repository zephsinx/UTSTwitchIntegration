#nullable disable
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
        private static SpawnManager _instance;
        private static readonly object _instanceLock = new object();
        private readonly ViewerQueue _queue;
        private readonly ConcurrentDictionary<CustomerController, string> _spawnedViewers;

        /// <summary>
        /// Flag to indicate a spawn is from SpawnManager
        /// </summary>
        private static string _pendingUsername = null;
        private static readonly object _pendingUsernameLock = new object();

        /// <summary>
        /// Spawn rate limiting
        /// </summary>
        private volatile float _lastSpawnTime = 0f;
        private const float MIN_SPAWN_INTERVAL = 5f;

        /// <summary>
        /// Singleton instance of SpawnManager
        /// </summary>
        public static SpawnManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SpawnManager();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Get the number of viewers in pool
        /// </summary>
        public int QueueCount => _queue.Count;

        /// <summary>
        /// Get the number of spawned viewers
        /// </summary>
        public int SpawnedCount => _spawnedViewers.Count;

        private SpawnManager()
        {
            _queue = new ViewerQueue();
            _spawnedViewers = new ConcurrentDictionary<CustomerController, string>();
        }

        /// <summary>
        /// Add a viewer to the spawn queue
        /// </summary>
        /// <param name="username">Twitch username</param>
        /// <param name="role">User's permission level</param>
        public bool QueueViewerForSpawn(string username, PermissionLevel role)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                ModLogger.Warning("Attempted to queue viewer with null or empty username");
                return false;
            }

            ModConfiguration config = ConfigManager.GetConfiguration();
            if (config.MaxPoolSize > 0 && _queue.Count >= config.MaxPoolSize)
            {
                ModLogger.Warning($"Pool is full (MaxPoolSize: {config.MaxPoolSize}), cannot add viewer '{username}'");
                return false;
            }

            bool added = _queue.Enqueue(username, role);

            if (added)
            {
                ModLogger.Info($"Viewer '{username}' added to spawn pool (Pool size: {_queue.Count})");
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
                viewer = _queue.DequeueRandom();
                ModLogger.Debug($"Retrieved username '{viewer?.Username}' from pool using Random selection (Pool size: {_queue.Count})");
            }
            else
            {
                viewer = _queue.Dequeue();
                ModLogger.Debug($"Retrieved username '{viewer?.Username}' from pool using FIFO selection (Pool size: {_queue.Count})");
            }

            if (viewer != null && !string.IsNullOrWhiteSpace(viewer.Username))
            {
                return viewer.Username;
            }

            if (config.EnablePredefinedNames)
            {
                string predefinedName = PredefinedNamesManager.Instance.GetRandomName();
                if (!string.IsNullOrWhiteSpace(predefinedName))
                {
                    ModLogger.Debug($"Queue empty, using predefined name: '{predefinedName}'");
                    return predefinedName;
                }
            }

            return null;
        }

        /// <summary>
        /// Attempt to spawn the next viewer from queue (immediate spawn mode)
        /// </summary>
        public bool TrySpawnNextViewer()
        {
            ModConfiguration config = ConfigManager.GetConfiguration();
            if (!config.EnableImmediateSpawn)
            {
                return false;
            }

            float currentTime = Time.time;
            if (currentTime - _lastSpawnTime < MIN_SPAWN_INTERVAL)
            {
                return false;
            }

            if (_queue.Count == 0)
            {
                return false;
            }

            TheaterController theaterController = TheaterController.Instance;
            if (theaterController == null)
            {
                ModLogger.Warning("TheaterController.Instance is null - game may not be ready");
                return false;
            }

            try
            {
                Transform _ = theaterController.transform;
            }
            catch
            {
                ModLogger.Warning("TheaterController.Instance is destroyed or invalid");
                return false;
            }

            ViewerInfo viewer;

            if (config.SelectionMethod == QueueSelectionMethod.Random)
            {
                viewer = _queue.DequeueRandom();
            }
            else
            {
                viewer = _queue.Dequeue();
            }

            if (viewer == null || string.IsNullOrWhiteSpace(viewer.Username))
            {
                return false;
            }

            try
            {
                lock (_pendingUsernameLock)
                {
                    _pendingUsername = viewer.Username;
                }

                CustomerController customer = theaterController.SpawnCustomer();

                lock (_pendingUsernameLock)
                {
                    if (_pendingUsername == viewer.Username)
                    {
                        _pendingUsername = null;
                    }
                }

                if (customer == null)
                {
                    ModLogger.Error($"Failed to spawn customer for viewer '{viewer.Username}' - SpawnCustomer returned null");
                    return false;
                }

                _spawnedViewers.TryAdd(customer, viewer.Username);
                ModLogger.Info($"Successfully spawned viewer '{viewer.Username}' as Customer ID={customer.CustomerId}");

                _lastSpawnTime = currentTime;
                return true;
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Exception while spawning viewer '{viewer.Username}': {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");

                lock (_pendingUsernameLock)
                {
                    _pendingUsername = null;
                }

                return false;
            }
        }

        /// <summary>
        /// Get and clear the pending username for a spawn
        /// </summary>
        /// <returns>Username if spawn is pending, null otherwise</returns>
        public static string GetAndClearPendingUsername()
        {
            lock (_pendingUsernameLock)
            {
                string username = _pendingUsername;
                _pendingUsername = null;
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
            if (customer == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            if (_spawnedViewers.TryAdd(customer, username))
            {
                ModLogger.Debug($"Stored username '{username}' for Customer ID={customer.CustomerId}");
            }
            else
            {
                _spawnedViewers[customer] = username;
                ModLogger.Debug($"Username already stored for Customer ID={customer.CustomerId}, updating to '{username}'");
            }
        }

        /// <summary>
        /// Get username for a spawned customer
        /// </summary>
        /// <param name="customer">Customer to look up</param>
        /// <returns>Username if found, null otherwise</returns>
        public string GetViewerUsername(CustomerController customer)
        {
            if (customer == null)
            {
                return null;
            }

            _spawnedViewers.TryGetValue(customer, out string username);
            return username;
        }

        /// <summary>
        /// Remove a customer from tracking
        /// </summary>
        /// <param name="customer">Customer to remove</param>
        public void RemoveViewer(CustomerController customer)
        {
            if (customer == null)
            {
                return;
            }

            if (_spawnedViewers.TryRemove(customer, out _))
            {
                ModLogger.Debug($"Removed viewer tracking for Customer ID={customer.CustomerId}");
            }
        }

        /// <summary>
        /// Clear all viewers from pool
        /// </summary>
        public void ClearQueue()
        {
            int count = _queue.Count;
            _queue.Clear();
            ModLogger.Info($"Cleared spawn pool ({count} viewers removed)");
        }

        /// <summary>
        /// Cleanup all resources and reset state
        /// </summary>
        public void Cleanup()
        {
            try
            {
                int queueCount = _queue.Count;
                int spawnedCount = _spawnedViewers.Count;

                _queue.Clear();
                ModLogger.Debug($"Cleared viewer queue ({queueCount} viewers removed)");

                _spawnedViewers.Clear();
                ModLogger.Debug($"Cleared spawned viewer tracking ({spawnedCount} tracked viewers removed)");

                lock (_pendingUsernameLock)
                {
                    _pendingUsername = null;
                }
                ModLogger.Debug("Cleared pending username");

                _lastSpawnTime = 0f;
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

