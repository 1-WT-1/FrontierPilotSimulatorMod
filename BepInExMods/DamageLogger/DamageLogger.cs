using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace FPS.DamageLogger
{
    [BepInPlugin("com.fps.mods.damagelogger", "FPS Damage Logger", "1.0.0")]
    public class DamageLoggerPlugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo("FPS Damage Logger: Awake started.");

            // 1. Reflection analysis to confirm types and methods exist exactly as expected
            AnalyzeType(typeof(CShipUpgrades), "CShipUpgrades");
            AnalyzeType(typeof(CDamageDescr), "CDamageDescr");
            AnalyzeType(typeof(ShipControllerScript), "ShipControllerScript");

            // 2. Harmony patching
            try
            {
                var harmony = new Harmony("com.fps.mods.damagelogger");
                harmony.PatchAll();
                Log.LogInfo("FPS Damage Logger: Harmony PatchAll succeeded.");
            }
            catch (Exception ex)
            {
                Log.LogError($"FPS Damage Logger: Harmony PatchAll failed: {ex}");
            }
        }

        private void AnalyzeType(Type t, string name)
        {
            Log.LogInfo($"=== Reflection Analysis for {name} ===");
            try
            {
                var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                Log.LogInfo($"Found {methods.Length} methods in {name}:");
                foreach (var m in methods)
                {
                    if (m.Name.Contains("GetUpgradedArmor") || m.Name.Contains("GetValue") || m.Name.Contains("Damage"))
                    {
                        string pStr = "";
                        foreach (var p in m.GetParameters())
                        {
                            pStr += $"{p.ParameterType.Name} {p.Name}, ";
                        }
                        Log.LogInfo($"  - {m.ReturnType.Name} {m.Name}({pStr.TrimEnd(' ', ',')})");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Error analyzing {name}: {ex.Message}");
            }
            Log.LogInfo($"=====================================");
        }
    }

    // ==========================================
    // PATCH 1: Log when upgrades armor gets requested
    // ==========================================
    [HarmonyPatch(typeof(CShipUpgrades), "GetUpgradedArmor")]
    public class CShipUpgrades_GetUpgradedArmor_Logger
    {
        private static void Postfix(CShipUpgrades __instance, CArmorDescr inBaseArmor, EShipAggregateType inAggregate, ref CArmorDescr __result)
        {
            try
            {
                string baseArmorStr = "";
                if (inBaseArmor != null)
                {
                    inBaseArmor.ForEach(m => baseArmorStr += $"[{m.DamageType}: Val={m.ValueModif}, Crit={m.CritChanceModif}] ");
                }
                
                string resArmorStr = "";
                if (__result != null)
                {
                    __result.ForEach(m => resArmorStr += $"[{m.DamageType}: Val={m.ValueModif}, Crit={m.CritChanceModif}] ");
                }

                DamageLoggerPlugin.Log.LogInfo($"[DamageLogger] CShipUpgrades.GetUpgradedArmor called for {inAggregate}:\n" +
                                              $"  - Base Armor: {baseArmorStr}\n" +
                                              $"  - Patched Armor: {resArmorStr}");
            }
            catch (Exception ex)
            {
                DamageLoggerPlugin.Log.LogError($"[DamageLogger] Error logging GetUpgradedArmor: {ex}");
            }
        }
    }

    // ==========================================
    // PATCH 2: Log every damage calculation and armor reduction
    // ==========================================
    [HarmonyPatch(typeof(CDamageDescr), "GetValue")]
    public class CDamageDescr_GetValue_Logger
    {
        private static void Postfix(CDamageDescr __instance, CArmorDescr armors, float distance, ref Tuple<int, bool> __result)
        {
            try
            {
                string armorStr = "";
                if (armors != null)
                {
                    armors.ForEach(m => armorStr += $"[{m.DamageType}: Val={m.ValueModif}, Crit={m.CritChanceModif}] ");
                }
                else
                {
                    armorStr = "None";
                }

                DamageLoggerPlugin.Log.LogInfo($"[DamageLogger] CDamageDescr.GetValue called:\n" +
                                              $"  - Damage Descr: {__instance}\n" +
                                              $"  - Distance: {distance}\n" +
                                              $"  - Armor applied: {armorStr}\n" +
                                              $"  - Final Damage: {__result.Item1} (Crit: {__result.Item2})");
            }
            catch (Exception ex)
            {
                DamageLoggerPlugin.Log.LogError($"[DamageLogger] Error logging GetValue: {ex}");
            }
        }
    }

    // ==========================================
    // PATCH 3: Log when any damage is applied to the ship controller
    // ==========================================
    [HarmonyPatch(typeof(ShipControllerScript), "Damage")]
    public class ShipControllerScript_Damage_Logger
    {
        private static void Prefix(ShipControllerScript __instance, CDamagePack inDamages, CLogContext inLogContext)
        {
            try
            {
                DamageLoggerPlugin.Log.LogInfo($"[DamageLogger] ShipControllerScript.Damage: Incoming DamagePack = {inDamages}");
            }
            catch (Exception ex)
            {
                DamageLoggerPlugin.Log.LogError($"[DamageLogger] Error in ShipControllerScript.Damage prefix: {ex}");
            }
        }
    }
}
