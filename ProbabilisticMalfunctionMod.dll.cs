using System;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace FPS_DamageSystem_Mod.ProbabilisticMalfunctions
{
    [BepInPlugin("com.fps.mods.probabilisticmalfunctions", "FPS Probabilistic Malfunctions Mod", "1.0.0")]
    public class ProbabilisticMalfunctionsPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            var harmony = new Harmony("com.fps.mods.probabilisticmalfunctions");
            harmony.PatchAll();
            
            Logger.LogInfo("FPS Probabilistic Malfunctions Mod (Chance Saving Throws) successfully initialized!");
        }
    }

    // ==========================================
    // PATCH: Intercepts CMalfunction.Start to execute a chance-based saving throw
    // ==========================================
    [HarmonyPatch(typeof(CMalfunction), "Start")]
    public class CMalfunction_Start_Patch
    {
        private static bool Prefix(CMalfunction __instance, CLogContext inLogContext)
        {
            try
            {
                var descr = __instance.Descr;
                if (descr == null) return true;

                string malfunctionName = descr.Name.name;
                float timeBeforeStartMin = descr.TimeBeforeStartMin;

                // 1. Map the malfunction name to its corresponding Resistance parameter
                string resistanceEnumName = "None";
                if (malfunctionName.Contains("Radiation")) resistanceEnumName = "SkyRadiationResistance";
                else if (malfunctionName.Contains("Water")) resistanceEnumName = "WaterResistance";
                else if (malfunctionName.Contains("Geyser") || malfunctionName.Contains("Dust")) resistanceEnumName = "DustParticleResistance";

                // Non-environmental malfunctions trigger automatically as intended
                if (resistanceEnumName == "None") return true;

                // 2. Retrieve active component resistance score from ship aggregates
                var ownerField = AccessTools.Field(typeof(CMalfunction), "_owner");
                if (ownerField == null) return true;

                var owner = ownerField.GetValue(__instance) as CMalfunctions;
                if (owner == null) return true;

                var ship = owner.Ship;
                if (ship == null) return true;

                var hull = ship.Hull;
                if (hull == null) return true;

                // Retrieve aggregate resistance using actual enum
                float resistanceRating = 0f;
                if (Enum.TryParse<EShipAggregateResistance>(resistanceEnumName, out var resEnum))
                {
                    resistanceRating = hull.GetResistanceValue(resEnum, inLogContext);
                }

                var toStartField = AccessTools.Field(typeof(CMalfunction), "_to_start");
                if (toStartField == null) return true;

                // 3. Complete immunity cutoff (100% or higher resistance)
                if (resistanceRating >= 100f)
                {
                    ResetWarningTimer(__instance, toStartField, timeBeforeStartMin);
                    return false; // Skip execution, blocking the breakdown
                }

                // 4. Roll the saving throw: failure chance is 100 minus current resistance
                float failureChance = 100f - resistanceRating;
                float roll = UnityEngine.Random.Range(0f, 100f);

                if (roll > failureChance)
                {
                    // SUCCESS: The ship resisted! Reset the warning countdown phase
                    ResetWarningTimer(__instance, toStartField, timeBeforeStartMin);
                    return false; // Interrupt breakdown sequence, keeping systems active
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProbabilisticMalfunctionsMod] Fail-safe error during saving throw check: {ex.Message}");
            }

            // FAILURE (or error fallback): Execute default malfunction sequence
            return true;
        }

        private static void ResetWarningTimer(CMalfunction instance, FieldInfo toStartField, float timeMin)
        {
            // Reset the countdown timer back to a safe range, letting the warning alarm loop run again
            float safetyWindow = Mathf.Max(timeMin, 5f);
            toStartField.SetValue(instance, safetyWindow);
        }
    }
}