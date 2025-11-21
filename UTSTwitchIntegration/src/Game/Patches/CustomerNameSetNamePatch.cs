using System;
using HarmonyLib;
using Il2CppGame.Customers;
using Il2CppTMPro;
using UTSTwitchIntegration.Game;
using ModLogger = UTSTwitchIntegration.Utils.Logger;

namespace UTSTwitchIntegration.Game.Patches
{
    /// <summary>
    /// Harmony patch to override native CustomerName with Twitch usernames
    /// Intercepts CustomerName.SetName() to replace procedural names with Twitch usernames
    /// </summary>
    [HarmonyPatch(typeof(CustomerName), nameof(CustomerName.SetName))]
    public class CustomerNameSetNamePatch
    {
        /// <summary>
        /// Counter for throttling debug logs (only log every Nth call)
        /// </summary>
        private static int callCount = 0;
        private static int missingCustomerControllerCount = 0;
        private const int LOG_THROTTLE_INTERVAL = 10; // Log every 10th occurrence

        static CustomerNameSetNamePatch()
        {
            ModLogger.Info("[CustomerNameSetNamePatch] Patch class loaded and registered");
        }

        static void Postfix(CustomerName __instance, bool isMale)
        {
            if (callCount == 0)
            {
                ModLogger.Info("[CustomerNameSetNamePatch] Postfix called for the first time - patch is active!");
            }

            try
            {
                callCount++;
                bool shouldLog = (callCount % LOG_THROTTLE_INTERVAL == 0);

                if (callCount <= 3)
                {
                    ModLogger.Info($"[CustomerNameSetNamePatch] Postfix called (call #{callCount}), __instance is null: {__instance == null}");
                }

                if (!__instance)
                {
                    if (shouldLog)
                    {
                        ModLogger.Debug($"[CustomerNameSetNamePatch] Postfix called but __instance is null (call #{callCount})");
                    }
                    return;
                }

                if (shouldLog)
                {
                    ModLogger.Debug($"[CustomerNameSetNamePatch] Postfix called for CustomerName instance (call #{callCount})");
                }

                CustomerController customer = __instance.gameObject.GetComponent<CustomerController>();
                if (!customer)
                {
                    missingCustomerControllerCount++;
                    if (missingCustomerControllerCount == 1 || missingCustomerControllerCount % LOG_THROTTLE_INTERVAL == 0)
                    {
                        ModLogger.Warning($"[CustomerNameSetNamePatch] CustomerController component not found on GameObject (occurrence #{missingCustomerControllerCount}). " +
                                         "This may indicate CustomerName and CustomerController are on different GameObjects.");
                    }
                    return;
                }

                if (shouldLog)
                {
                    ModLogger.Debug($"[CustomerNameSetNamePatch] CustomerController found: CustomerId={customer.CustomerId}");
                }

                SpawnManager spawnManager = SpawnManager.Instance;
                if (spawnManager == null)
                {
                    return;
                }
                string twitchUsername = spawnManager.GetUsernameForCustomer(customer);
                if (string.IsNullOrEmpty(twitchUsername))
                {
                    return;
                }

                try
                {
                    ModLogger.Info($"[CustomerNameSetNamePatch] Setting Twitch username '{twitchUsername}' for Customer ID={customer.CustomerId}");

                    __instance._currentName = twitchUsername;
                    ModLogger.Debug($"[CustomerNameSetNamePatch] Set _currentName property successfully");

                    TMP_Text nameText = __instance._customerNameText;
                    if (nameText != null)
                    {
                        nameText.text = twitchUsername;
                        ModLogger.Info($"[CustomerNameSetNamePatch] Successfully set name text to '{twitchUsername}' for Customer ID={customer.CustomerId}");
                    }
                    else
                    {
                        ModLogger.Warning($"[CustomerNameSetNamePatch] CustomerName._customerNameText is null for customer ID={customer.CustomerId} - name property was set but text component is missing");
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"[CustomerNameSetNamePatch] Error setting Twitch username in CustomerName: {ex.Message}");
                    ModLogger.Debug($"[CustomerNameSetNamePatch] Stack trace: {ex.StackTrace}");
                }

                _ = isMale;
            }
            catch (Exception ex)
            {
                ModLogger.Error($"[CustomerNameSetNamePatch] Error in CustomerName.SetName patch: {ex.Message}");
                ModLogger.Debug($"[CustomerNameSetNamePatch] Stack trace: {ex.StackTrace}");
            }
        }
    }
}

