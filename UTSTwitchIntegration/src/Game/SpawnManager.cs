using System.Collections.Concurrent;
using System.Collections.Generic;
using Il2CppGame.Shop;
using Il2CppGame.Customers;
using Il2CppGame.AIBase;
using UnityEngine;
using UTSTwitchIntegration.Config;
using UTSTwitchIntegration.Models;
using UTSTwitchIntegration.Queue;
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
        /// Username pending assignment for the next customer spawn
        /// </summary>
        private static string pendingUsername;

        private static readonly object PendingUsernameLock = new object();

        /// <summary>
        /// Spawn rate limiting for immediate spawn mode
        /// </summary>
        private volatile float lastSpawnTime;

        /// <summary>
        /// Minimum time between immediate spawns in seconds
        /// </summary>
        private const float MIN_SPAWN_INTERVAL = 5f;

        private volatile bool hasLoggedTheaterControllerNullWarning;
        private volatile bool hasLoggedTheaterControllerInvalidWarning;

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

        /// <summary>
        /// Periodic cleanup check interval in seconds
        /// </summary>
        private volatile float lastCleanupCheckTime;
        private const float CLEANUP_CHECK_INTERVAL = 2f;

        private SpawnManager()
        {
            this.queue = new ViewerQueue();
            this.spawnedViewers = new ConcurrentDictionary<CustomerController, string>();

            ModConfiguration config = ConfigManager.GetConfiguration();
            this.queue.SetSelectionMethod(config.SelectionMethod);
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
            if (config.MaxPoolSize <= 0 || this.queue.Count < config.MaxPoolSize)
                return this.queue.Enqueue(username);

            ModLogger.Warning($"Pool is full (MaxPoolSize: {config.MaxPoolSize}), cannot add viewer '{username}'");
            return false;

        }

        /// <summary>
        /// Get next username from pool for natural spawn assignment
        /// </summary>
        private string GetNextUsernameFromPool()
        {
            ModConfiguration config = ConfigManager.GetConfiguration();

            this.queue.SetSelectionMethod(config.SelectionMethod);

            ViewerInfo viewer = this.queue.GetNext();

            if (viewer != null && !string.IsNullOrWhiteSpace(viewer.Username))
            {
                this.queue.MarkUsernameInUse(viewer.Username);
                return viewer.Username;
            }

            return null;
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
                if (!this.hasLoggedTheaterControllerNullWarning)
                {
                    ModLogger.Warning("TheaterController.Instance is null - game may not be ready");
                    this.hasLoggedTheaterControllerNullWarning = true;
                }
                return;
            }

            if (this.hasLoggedTheaterControllerNullWarning)
            {
                this.hasLoggedTheaterControllerNullWarning = false;
            }

            try
            {
                Transform _ = theaterController.transform;
            }
            catch
            {
                if (!this.hasLoggedTheaterControllerInvalidWarning)
                {
                    ModLogger.Warning("TheaterController.Instance is destroyed or invalid");
                    this.hasLoggedTheaterControllerInvalidWarning = true;
                }
                return;
            }

            if (this.hasLoggedTheaterControllerInvalidWarning)
            {
                this.hasLoggedTheaterControllerInvalidWarning = false;
            }

            this.queue.SetSelectionMethod(config.SelectionMethod);

            ViewerInfo viewer = this.queue.GetNext();

            if (viewer == null || string.IsNullOrWhiteSpace(viewer.Username))
            {
                return;
            }

            this.queue.MarkUsernameInUse(viewer.Username);

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
                ModLogger.Debug($"Successfully spawned viewer '{viewer.Username}' as Customer ID={customer.CustomerId}");

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

        public static bool HasPendingUsername()
        {
            lock (PendingUsernameLock)
            {
                return !string.IsNullOrEmpty(pendingUsername);
            }
        }

        /// <summary>
        /// Get and clear the pending username for a spawn
        /// </summary>
        /// <returns>Username if spawn is pending, null otherwise</returns>
        private static string GetAndClearPendingUsername()
        {
            lock (PendingUsernameLock)
            {
                string username = pendingUsername;
                pendingUsername = null;
                return username;
            }
        }

        /// <summary>
        /// Check if a customer already has a username assigned
        /// </summary>
        private bool HasUsername(CustomerController customer)
        {
            return customer && this.spawnedViewers.ContainsKey(customer);
        }

        /// <summary>
        /// Get the Twitch username assigned to a customer, if any
        /// </summary>
        /// <param name="customer">Customer to check</param>
        /// <returns>Twitch username if assigned, null otherwise</returns>
        public string GetUsernameForCustomer(CustomerController customer)
        {
            return !customer
                ? null
                : this.spawnedViewers.GetValueOrDefault(customer);
        }

        /// <summary>
        /// Try to overwrite a random existing NPC's name with a Twitch username
        /// Only overwrites NPCs that don't already have Twitch usernames
        /// </summary>
        /// <param name="username">Twitch username to assign</param>
        /// <returns>True if successfully overwrote an NPC, false if no suitable NPC found</returns>
        public bool TryOverwriteRandomNPC(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            TheaterController theaterController = TheaterController.Instance;
            if (theaterController == null)
            {
                return false;
            }

            Il2CppSystem.Collections.Generic.List<CustomerController> customers = TheaterController.Customers;
            if (customers == null || customers.Count == 0)
            {
                return false;
            }

            List<CustomerController> eligibleCustomers = new List<CustomerController>();
            foreach (CustomerController customer in customers)
            {
                if (customer == null || !customer.gameObject || !customer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (this.HasUsername(customer))
                {
                    continue;
                }

                // Skip if in certain invalid states (leaving, knocked out, etc.)
                AIState currentState = customer.CurrentState;
                if (currentState != null)
                {
                    string stateTypeName = currentState.GetType().Name;
                    if (stateTypeName == "LeavingState" ||
                        stateTypeName == "KnockedOutState" ||
                        stateTypeName == "KnockedDownState")
                    {
                        continue;
                    }
                }

                eligibleCustomers.Add(customer);
            }

            if (eligibleCustomers.Count == 0)
            {
                return false;
            }

            System.Random random = new System.Random();
            int randomIndex = random.Next(eligibleCustomers.Count);
            CustomerController targetCustomer = eligibleCustomers[randomIndex];

            if (!this.StoreViewerUsername(targetCustomer, username))
                return false;

            ModLogger.Debug($"Overwrote NPC (Customer ID={targetCustomer.CustomerId}) with Twitch username '{username}'");

            CustomerName customerName = targetCustomer.GetComponent<CustomerName>();
            if (customerName)
            {
                customerName.SetName(true);
            }

            return true;
        }

        /// <summary>
        /// Attempt to assign a username to a customer if they don't already have one
        /// </summary>
        /// <param name="customer"></param>
        /// <param name="usePendingUsername">If true, use pending username for immediate spawns</param>
        public bool TryAssignUsernameToCustomer(CustomerController customer, bool usePendingUsername = false)
        {
            if (!customer)
            {
                return false;
            }

            if (this.HasUsername(customer))
            {
                return false;
            }

            string username = null;

            if (usePendingUsername)
            {
                username = GetAndClearPendingUsername();
            }

            if (string.IsNullOrEmpty(username))
            {
                username = this.GetNextUsernameFromPool();
            }

            if (string.IsNullOrEmpty(username))
            {
                return false;
            }

            return this.StoreViewerUsername(customer, username);
        }

        /// <summary>
        /// Store username mapping for a spawned customer
        /// </summary>
        /// <param name="customer">Spawned customer</param>
        /// <param name="username">Twitch username</param>
        /// <returns>True if this was a new entry, false if customer already had a username</returns>
        private bool StoreViewerUsername(CustomerController customer, string username)
        {
            if (!customer)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            if (this.spawnedViewers.TryAdd(customer, username))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Handle cleanup when a customer is leaving/despawning
        /// </summary>
        /// <param name="customer">Customer that is leaving</param>
        public void OnCustomerLeaving(CustomerController customer)
        {
            if (!customer)
            {
                return;
            }

            if (this.spawnedViewers.TryRemove(customer, out string username))
            {
                if (!string.IsNullOrWhiteSpace(username))
                {
                    this.queue.MarkUsernameAvailable(username);

                    ModLogger.Debug($"Customer '{username}' left - username marked as available");
                }
            }
        }

        /// <summary>
        /// Periodic cleanup check to catch destroyed customers (fallback mechanism)
        /// Should be called periodically (e.g., from Update or timer)
        /// </summary>
        public void PeriodicCleanupCheck()
        {
            float currentTime = Time.time;
            if (currentTime - this.lastCleanupCheckTime < CLEANUP_CHECK_INTERVAL)
            {
                return;
            }

            this.lastCleanupCheckTime = currentTime;

            List<CustomerController> keysToRemove = new List<CustomerController>();

            foreach (KeyValuePair<CustomerController, string> kvp in this.spawnedViewers)
            {
                CustomerController customer = kvp.Key;

                if (customer == null ||
                    !customer.gameObject ||
                    !customer.gameObject.activeInHierarchy ||
                    customer.transform == null)
                {
                    keysToRemove.Add(customer);
                }
            }

            foreach (CustomerController customer in keysToRemove)
            {
                if (this.spawnedViewers.TryRemove(customer, out string username))
                {
                    if (!string.IsNullOrWhiteSpace(username))
                    {
                        this.queue.MarkUsernameAvailable(username);
                        ModLogger.Debug($"Cleaned up destroyed customer '{username}' via periodic check");
                    }
                }
            }
        }

        /// <summary>
        /// Cleanup all resources and reset state
        /// </summary>
        public void Cleanup()
        {
            try
            {
                this.queue.Clear();
                this.spawnedViewers.Clear();

                lock (PendingUsernameLock)
                {
                    pendingUsername = null;
                }

                this.lastSpawnTime = 0f;
                this.lastCleanupCheckTime = 0f;
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Error during spawn manager cleanup: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}