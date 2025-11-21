using HarmonyLib;
using Il2CppGame.Customers;
using ModLogger = UTSTwitchIntegration.Utils.Logger;
// ReSharper disable UnusedMember.Local

namespace UTSTwitchIntegration.Game.Patches
{
    /// <summary>
    /// Harmony patch to intercept customer leaving/despawning
    /// </summary>
    [HarmonyPatch(typeof(CustomerController), nameof(CustomerController.Leave))]
    public class CustomerLeavePatch
    {
        static void Prefix(CustomerController __instance, bool changeState)
        {
            try
            {
                _ = changeState;

                if (!__instance)
                {
                    return;
                }

                SpawnManager spawnManager = SpawnManager.Instance;
                spawnManager?.OnCustomerLeaving(__instance);
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Error in CustomerLeave prefix patch: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}

