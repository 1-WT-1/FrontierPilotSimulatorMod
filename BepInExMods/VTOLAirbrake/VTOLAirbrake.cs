using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace FPS.VTOLAirbrake
{
    [BepInPlugin("com.fps.mods.vtolairbrake", "FPS VTOL Airbrake", "1.0.0")]
    public class VTOLAirbrakePlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            try
            {
                var harmony = new Harmony("com.fps.mods.vtolairbrake");
                harmony.PatchAll();
                Logger.LogInfo("VTOL Airbrake initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"FPS VTOL Airbrake: Harmony patching failed: {ex}");
            }
        }
    }

    // ==========================================
    // Harmony Patch: Enables the airbrake toggle key to operate in VTOL (JetPack) mode.
    // ==========================================
    [HarmonyPatch(typeof(ShipControllerScript), "GetAirbrakeValue")]
    public class ShipControllerScript_GetAirbrakeValue_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ShipControllerScript __instance, ref float __result, CLogContext inLogContext)
        {
            try
            {
                if (__instance == null) return;
                if (__instance.ShipFlyState == NamedShipState.JetPack)
                {
                    CAirPlane airplane = __instance.InsideAirplane;
                    if (airplane != null)
                    {
                        float airbrakeTrimmer = airplane.GetPlayerTrimmerInput(ETrimmingChannel.AirBrake);
                        if (airbrakeTrimmer > __result)
                        {
                            __result = airbrakeTrimmer;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VTOLAirbrake] Error in GetAirbrakeValue Postfix: {ex}");
            }
        }
    }
}
