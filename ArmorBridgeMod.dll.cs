using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace FPS_DamageSystem_Mod.ArmorBridge
{
    [BepInPlugin("com.fps.mods.armorbridge", "FPS Armor Bridge Mod", "1.0.0")]
    public class ArmorBridgePlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            var harmony = new Harmony("com.fps.mods.armorbridge");
            harmony.PatchAll();
            
            Logger.LogInfo("FPS Armor Bridge Mod (JSON to Combat Layer) successfully initialized!");
        }
    }

    // ==========================================
    // PATCH: Bridges JSON EShipUpgradeParams to active EDamageType values inside CShipUpgrades
    // ==========================================
    [HarmonyPatch(typeof(CShipUpgrades), "GetUpgradedArmor")]
    public class CShipUpgrades_GetUpgradedArmor_Patch
    {
        private static void Postfix(CShipUpgrades __instance, CArmorDescr inBaseArmor, EShipAggregateType inAggregate, ref CArmorDescr __result)
        {
            try
            {
                var armor = new CArmorDescr(inBaseArmor);

                // 1. Bridge ArmorCollision -> Collision (2)
                float collision = CGlobalUpgradeTable.GetUpgradedValue(
                    CGlobalUpgradeTable.GetUpgradeLink(EShipUpgradeParams.ArmorCollision, inAggregate), 
                    0f
                );
                if (collision > 0f)
                {
                    armor.Add(EDamageType.Collision, new CValueModificator(collision / 100f, true));
                }

                // 2. Bridge ArmorKinetic -> Kinetic (4)
                float kinetic = CGlobalUpgradeTable.GetUpgradedValue(
                    CGlobalUpgradeTable.GetUpgradeLink(EShipUpgradeParams.ArmorKinetic, inAggregate), 
                    0f
                );
                if (kinetic > 0f)
                {
                    armor.Add(EDamageType.Kinetic, new CValueModificator(kinetic / 100f, true));
                }

                // 3. Bridge ArmorTemperature -> Temperature (8)
                float temperature = CGlobalUpgradeTable.GetUpgradedValue(
                    CGlobalUpgradeTable.GetUpgradeLink(EShipUpgradeParams.ArmorTemperature, inAggregate), 
                    0f
                );
                if (temperature > 0f)
                {
                    armor.Add(EDamageType.Temperature, new CValueModificator(temperature / 100f, true));
                }

                __result = armor;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ArmorBridgeMod] Fail-safe error bridging aggregate upgrades: {ex.Message}");
            }
        }
    }

    // ==========================================
    // PATCH: Fixes vanilla bug where CDamageModif constructor fails to assign the private _damage_type field!
    // ==========================================
    [HarmonyPatch(typeof(CDamageModif), MethodType.Constructor, new Type[] { typeof(EDamageType), typeof(CValueModificator), typeof(CValueModificator) })]
    public class CDamageModif_Constructor_Patch
    {
        private static void Postfix(CDamageModif __instance, EDamageType inType)
        {
            AccessTools.Field(typeof(CDamageModif), "_damage_type").SetValue(__instance, inType);
        }
    }
}