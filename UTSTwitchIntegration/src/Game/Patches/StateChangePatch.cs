using System.Collections.Generic;
using HarmonyLib;
using Il2CppGame.AIBase;
using Il2CppGame.Customers;
using Il2CppGame.Shop;
using ModLogger = UTSTwitchIntegration.Utils.Logger;
// ReSharper disable UnusedMember.Local

namespace UTSTwitchIntegration.Game.Patches
{
    [HarmonyPatch(typeof(AIStateMachine), nameof(AIStateMachine.ChangeState))]
    public class StateChangePatch
    {
        private static readonly HashSet<string> InteractionStateTypeNames = new HashSet<string>
        {
            "ShoppingState",
            "CheckingOutState",
            "BuyingTicketState",
            "MovieState",
            "GamingState",
            "WaitingState",
            "BathroomState"
        };

        private static readonly HashSet<string> InteractionStateDisplayNames = new HashSet<string>
        {
            "Shopping",
            "Checking Out",
            "Buying Ticket",
            "Movie",
            "Gaming",
            "Waiting",
            "Bathroom"
        };

        internal static void HandleStateChange(AIStateMachine stateMachine, AIState newState, string patchName, AIBaseController providedController = null)
        {
            try
            {
                if (stateMachine == null)
                {
                    ModLogger.Warning($"AIStateMachine instance is null in {patchName}");
                    return;
                }

                if (newState == null)
                {
                    ModLogger.Warning($"New state is null in {patchName}");
                    return;
                }

                if (!TheaterController.Instance)
                {
                    return;
                }

                AIBaseController controller;

                if (providedController)
                {
                    controller = providedController;
                }
                else
                {
                    controller = stateMachine._controller;
                    if (!controller)
                    {
                        controller = newState.Controller;
                    }
                }

                if (controller)
                {
                    CustomerController customerAttempt = controller.TryCast<CustomerController>();
                    if (customerAttempt)
                    {
                        controller = customerAttempt;
                    }
                }

                if (!controller)
                {
                    ModLogger.Warning($"Controller is null in {patchName} after all retrieval attempts");
                    return;
                }

                if (!(controller is CustomerController customer))
                {
                    return;
                }

                string newStateName = newState.Name ?? "Unknown";
                string newStateType = newState.GetType().Name;

                int customerId = customer.CustomerId;

                bool isInteractionState = InteractionStateTypeNames.Contains(newStateType) ||
                                         InteractionStateDisplayNames.Contains(newStateName);

                if (!isInteractionState)
                    return;

                SpawnManager spawnManager = SpawnManager.Instance;
                if (spawnManager != null)
                {
                    _ = spawnManager.TryAssignUsernameToCustomer(customer);
                }
                else
                {
                    ModLogger.Warning($"SpawnManager.Instance is null, cannot assign username to Customer {customerId}");
                }
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Error in {patchName}: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }

        static void Postfix(AIStateMachine __instance, AIState state)
        {
            HandleStateChange(__instance, state, $"{nameof(StateChangePatch)}.Postfix");
        }
    }

    [HarmonyPatch(typeof(AIStateMachine), nameof(AIStateMachine.Initialize))]
    public class StateInitializePatch
    {
        static void Postfix(AIStateMachine __instance, AIState state, AIBaseController controller)
        {
            StateChangePatch.HandleStateChange(__instance, state, $"{nameof(StateInitializePatch)}.Postfix", controller);
        }
    }

    [HarmonyPatch(typeof(AIStateMachine), nameof(AIStateMachine.ChangeToNextState))]
    public class StateChangeToNextPatch
    {
        static void Postfix(AIStateMachine __instance)
        {
            try
            {
                AIState currentState = __instance?.CurrentState;
                if (currentState != null)
                {
                    StateChangePatch.HandleStateChange(__instance, currentState, $"{nameof(StateChangeToNextPatch)}.Postfix");
                }
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"Error in {nameof(StateChangeToNextPatch)}: {ex.Message}");
                ModLogger.Debug($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}