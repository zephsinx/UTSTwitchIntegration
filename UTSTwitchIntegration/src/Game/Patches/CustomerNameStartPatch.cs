using System;
using HarmonyLib;
using Il2CppGame.Customers;
using Il2CppTMPro;
using UTSTwitchIntegration.Game;
using ModLogger = UTSTwitchIntegration.Utils.Logger;

namespace UTSTwitchIntegration.Game.Patches
{
    /// <summary>
    /// Harmony patch to re-apply Twitch usernames after CustomerName.Start() initializes the component
    /// </summary>
    [HarmonyPatch(typeof(CustomerName), nameof(CustomerName.Start))]
    public class CustomerNameStartPatch
    {
        static void Postfix(CustomerName __instance)
        {
            try
            {
                if (!__instance)
                {
                    return;
                }

                CustomerController customer = __instance.gameObject.GetComponent<CustomerController>();
                if (!customer)
                {
                    return;
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

                // Only re-apply if the name doesn't match (avoids unnecessary work)
                string currentName = __instance._currentName;
                if (currentName != twitchUsername)
                {
                    try
                    {
                        __instance._currentName = twitchUsername;

                        TMP_Text nameText = __instance._customerNameText;
                        if (nameText != null)
                        {
                            nameText.text = twitchUsername;
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Error($"Error re-applying Twitch username in CustomerName.Start postfix: {ex.Message}");
                        ModLogger.Debug($"Stack trace: {ex.StackTrace}");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Error in CustomerName.Start postfix patch: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
