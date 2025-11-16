using HarmonyLib;
using Il2CppGame.AIBase;
using Il2CppGame.Customers;
using ModLogger = UTSTwitchIntegration.Utils.Logger;

namespace UTSTwitchIntegration.Game.Patches
{
    /// <summary>
    /// Logs NPC state transitions for debugging
    /// </summary>
    [HarmonyPatch(typeof(AIStateMachine), nameof(AIStateMachine.ChangeState))]
    public class StateChangePatch
    {
        static void Postfix(AIStateMachine __instance, AIState state)
        {
            try
            {
                if (__instance == null)
                {
                    ModLogger.Warning("AIStateMachine instance is null in state change patch");
                    return;
                }

                if (state == null)
                {
                    ModLogger.Warning("New state is null in state change patch");
                    return;
                }

                AIBaseController controller = __instance._controller;
                if (!controller)
                {
                    ModLogger.Warning("Controller is null in state change patch");
                    return;
                }

                string newStateName = state.Name ?? "Unknown";
                string newStateType = state.GetType().Name;

                // Postfix runs after state change completes, so CurrentState may already point to the new state
                AIState oldState = __instance.CurrentState;
                string oldStateName = oldState != null ? (oldState.Name ?? "Unknown") : "None";

                if (controller is CustomerController customer)
                {
                    int customerId = customer.CustomerId;
                    string logMessage = $"Customer {customerId} state change: {oldStateName} -> {newStateName} ({newStateType})";

                    // If states match, CurrentState was already updated before postfix, so we can't show the transition
                    if (oldState == state || oldStateName == newStateName)
                    {
                        logMessage = $"Customer {customerId} state change: -> {newStateName} ({newStateType})";
                    }

                    ModLogger.Debug(logMessage);
                }
                else
                {
                    string controllerType = controller.GetType().Name;
                    string logMessage = $"{controllerType} state change: {oldStateName} -> {newStateName} ({newStateType})";

                    // If states match, CurrentState was already updated before postfix, so we can't show the transition
                    if (oldState == state || oldStateName == newStateName)
                    {
                        logMessage = $"{controllerType} state change: -> {newStateName} ({newStateType})";
                    }

                    ModLogger.Debug(logMessage);
                }
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Error in StateChange postfix patch: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}