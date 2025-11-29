using System.Collections.Generic;
using HarmonyLib;
using Il2CppGame.Shop;
using Il2CppGame.Customers;
using Il2CppGame.AIBase;
using UnityEngine;
using System.Collections.Concurrent;
using UTSTwitchIntegration.Utils;

namespace UTSTwitchIntegration.Game.Patches
{
    /// <summary>
    /// Harmony patch to intercept customer spawns for Twitch integration
    /// </summary>
    [HarmonyPatch(typeof(TheaterController), nameof(TheaterController.SpawnCustomer))]
    public class SpawnCustomerPatch
    {
        private static readonly HashSet<string> InteractionStateNames = new HashSet<string>
        {
            "ShoppingState",
            "CheckingOutState",
            "BuyingTicketState",
            "MovieState",
            "GamingState",
            "WaitingState",
            "BathroomState"
        };

        /// <summary>
        /// Tracks processed spawns by Customer ID to prevent duplicate processing when the same customer spawns multiple times
        /// </summary>
        private static readonly ConcurrentDictionary<int, byte> ProcessedSpawns = new ConcurrentDictionary<int, byte>();
        private static volatile float lastCleanupTime;
        private static readonly object CleanupLock = new object();

        private const float CLEANUP_INTERVAL = 5f;

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
                    return;
                }

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

                SpawnManager spawnManager = SpawnManager.Instance;
                if (spawnManager != null)
                {
                    if (SpawnManager.HasPendingUsername())
                    {
                        spawnManager.TryAssignUsernameToCustomer(__result, usePendingUsername: true);
                    }
                    else
                    {
                        AIState currentState = __result.CurrentState;
                        if (currentState != null)
                        {
                            string stateTypeName = currentState.GetType().Name;
                            if (InteractionStateNames.Contains(stateTypeName))
                            {
                                spawnManager.TryAssignUsernameToCustomer(__result);
                            }
                        }
                    }
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