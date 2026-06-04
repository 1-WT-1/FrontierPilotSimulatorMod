using System;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace FPS.KeepCruise
{
    [BepInPlugin("com.fps.mods.keepcruise", "FPS Keep Cruise", "1.0.0")]
    public class KeepCruisePlugin : BaseUnityPlugin
    {
        public static bool SavedCruiseState = false;

        private void Awake()
        {
            try
            {
                var harmony = new Harmony("com.fps.mods.keepcruise");
                harmony.PatchAll();
                Logger.LogInfo("Keep Cruise initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Keep Cruise Init failed: {ex}");
            }
        }
    }

    // Patch for standard CInputThrottleValue
    [HarmonyPatch(typeof(CInputThrottleValue))]
    public static class CInputThrottleValue_Patches
    {
        [HarmonyPatch(MethodType.Constructor, new Type[] { typeof(CShipEngine), typeof(ShipControllerScript), typeof(CLogContext) })]
        [HarmonyPostfix]
        public static void Constructor_Postfix(CInputThrottleValue __instance)
        {
            try
            {
                if (KeepCruisePlugin.SavedCruiseState)
                {
                    __instance.SetCruise(true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FPS Keep Cruise] Error in CInputThrottleValue Constructor Postfix: {ex}");
            }
        }

        [HarmonyPatch("SetCruise", new Type[] { typeof(bool) })]
        [HarmonyPostfix]
        public static void SetCruise_Postfix(bool value)
        {
            try
            {
                KeepCruisePlugin.SavedCruiseState = value;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FPS Keep Cruise] Error in CInputThrottleValue.SetCruise Postfix: {ex}");
            }
        }
    }

    // Patch for Ship2.CInputThrottleValue Constructor
    [HarmonyPatch]
    public static class Ship2_CInputThrottleValue_Constructor_Patch
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Ship2.CInputThrottleValue");
            var paramProviderType = AccessTools.TypeByName("Ship2.IParamValueProvider");
            return AccessTools.Constructor(type, new Type[] { paramProviderType, typeof(float) });
        }

        [HarmonyPostfix]
        public static void Postfix(object __instance)
        {
            try
            {
                if (KeepCruisePlugin.SavedCruiseState)
                {
                    var traverse = Traverse.Create(__instance);
                    traverse.Field("_cruise").SetValue(true);
                    
                    var inputJetpackUD = traverse.Field("_input_JetpackUD").GetValue();
                    if (inputJetpackUD != null)
                    {
                        var toFloatMethod = inputJetpackUD.GetType().GetMethod("ToFloat");
                        if (toFloatMethod != null)
                        {
                            float val = (float)toFloatMethod.Invoke(inputJetpackUD, null);
                            traverse.Field("_handlevalue").SetValue(val);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FPS Keep Cruise] Error in Ship2.CInputThrottleValue Constructor Postfix: {ex}");
            }
        }
    }

    // Patch for Ship2.CInputThrottleValue.OnCruiseSwitch
    [HarmonyPatch]
    public static class Ship2_CInputThrottleValue_OnCruiseSwitch_Patch
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Ship2.CInputThrottleValue");
            var argsType = AccessTools.TypeByName("CInputFireEventArgs");
            return AccessTools.Method(type, "OnCruiseSwitch", new Type[] { typeof(object), argsType });
        }

        [HarmonyPostfix]
        public static void Postfix(object __instance)
        {
            try
            {
                var traverse = Traverse.Create(__instance);
                KeepCruisePlugin.SavedCruiseState = traverse.Field<bool>("_cruise").Value;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FPS Keep Cruise] Error in Ship2.CInputThrottleValue.OnCruiseSwitch Postfix: {ex}");
            }
        }
    }

    // Patch for Ship2.CInputThrottleValue.ResetCruise
    [HarmonyPatch]
    public static class Ship2_CInputThrottleValue_ResetCruise_Patch
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Ship2.CInputThrottleValue");
            return AccessTools.Method(type, "ResetCruise");
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                KeepCruisePlugin.SavedCruiseState = false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FPS Keep Cruise] Error in Ship2.CInputThrottleValue.ResetCruise Postfix: {ex}");
            }
        }
    }
}
