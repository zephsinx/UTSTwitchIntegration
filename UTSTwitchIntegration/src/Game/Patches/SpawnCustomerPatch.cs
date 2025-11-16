using HarmonyLib;
using Il2CppGame.Shop;
using Il2CppGame.Customers;
using UnityEngine;
using System.Collections.Concurrent;
using System.Threading;
using ModLogger = UTSTwitchIntegration.Utils.Logger;

namespace UTSTwitchIntegration.Game.Patches
{
    /// <summary>
    /// Harmony patch to intercept customer spawns for Twitch integration
    /// </summary>
    [HarmonyPatch(typeof(TheaterController), nameof(TheaterController.SpawnCustomer))]
    public class SpawnCustomerPatch
    {
        private static int spawnCount;
        private static readonly ConcurrentDictionary<int, byte> ProcessedSpawns = new ConcurrentDictionary<int, byte>();
        private static volatile float lastCleanupTime;
        private static readonly object CleanupLock = new object();

        /// <summary>
        /// Cleanup interval in seconds
        /// </summary>
        private const float CLEANUP_INTERVAL = 5f;

        /// <summary>
        /// Postfix patch that runs after SpawnCustomer completes
        /// </summary>
        static void Postfix(TheaterController __instance, Vector3 positionOverride, Vector3 eulerOverride, ref CustomerController __result)
        {
            try
            {
                if (!__result)
                {
                    ModLogger.Warning("SpawnCustomer returned null");
                    return;
                }

                if (!__instance)
                {
                    ModLogger.Warning("TheaterController instance is null in patch");
                    return;
                }

                if (!__result.gameObject || !__result.gameObject.activeInHierarchy)
                {
                    ModLogger.Warning("Customer GameObject is null or inactive");
                    return;
                }

                if (!__result.transform)
                {
                    ModLogger.Warning("Customer transform is null");
                    return;
                }

                int customerId = __result.CustomerId;

                if (!ProcessedSpawns.TryAdd(customerId, 0))
                {
                    ModLogger.Debug($"Skipping duplicate spawn event for Customer ID={customerId}");
                    return;
                }

                Interlocked.Increment(ref spawnCount);

                float currentTime = Time.time;
                float lastCleanup = lastCleanupTime;

                if (currentTime - lastCleanup > CLEANUP_INTERVAL)
                {
                    lock (CleanupLock)
                    {
                        if (currentTime - lastCleanupTime > CLEANUP_INTERVAL)
                        {
                            if (ProcessedSpawns.Count > 100)
                            {
                                ProcessedSpawns.Clear();
                            }

                            lastCleanupTime = currentTime;
                        }
                    }
                }

                Vector3 actualPosition = Vector3.zero;
                Vector3 actualRotation = Vector3.zero;
                if (__result.transform)
                {
                    actualPosition = __result.transform.position;
                    actualRotation = __result.transform.eulerAngles;
                }

                ModLogger.Debug($"Customer spawned (#{spawnCount}): " +
                                $"ID={customerId}, " +
                                $"OverridePos={positionOverride}, " +
                                $"ActualPos={actualPosition}, " +
                                $"OverrideRot={eulerOverride}, " +
                                $"ActualRot={actualRotation}");

                SpawnManager spawnManager = SpawnManager.Instance;
                if (spawnManager != null)
                {
                    string username;

                    string pendingUsername = SpawnManager.GetAndClearPendingUsername();
                    if (!string.IsNullOrEmpty(pendingUsername))
                    {
                        username = pendingUsername;
                        ModLogger.Debug($"Using immediate spawn username '{username}' for Customer ID={customerId}");
                    }
                    else
                    {
                        username = spawnManager.GetNextUsernameFromPool();
                        if (!string.IsNullOrEmpty(username))
                        {
                            ModLogger.Debug($"Using pool username '{username}' for Customer ID={customerId}");
                        }
                    }

                    if (string.IsNullOrEmpty(username))
                        return;

                    spawnManager.StoreViewerUsername(__result, username);
                    ModLogger.Info($"Assigned Twitch username '{username}' to Customer ID={customerId}");

                    UsernameDisplayManager.CreateDisplay(__result, username);
                }
                else
                {
                    ModLogger.Warning($"SpawnManager.Instance is null - cannot assign username for Customer ID={customerId}");
                }
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Error in SpawnCustomer postfix patch: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}