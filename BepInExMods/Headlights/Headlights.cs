using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Ship.Descriptions.EffectScript;
using CGUI;
using System.Linq;

namespace FPS.Headlights
{
    [BepInPlugin("com.fps.mods.headlights", "FPS Headlights", "1.0.0")]
    public class HeadlightsPlugin : BaseUnityPlugin
    {
        public enum EHeadlightsMode
        {
            Auto,
            ManualOn,
            ManualOff
        }

        public static EHeadlightsMode CurrentHeadlightsMode = EHeadlightsMode.Auto;
        private static ConfigEntry<KeyCode> ToggleLightsKey;
        public static LocTextMeshPro HeadlightsText;

        private void Awake()
        {
            try
            {
                ToggleLightsKey = Config.Bind("General", "ToggleLightsKey", KeyCode.L, "Keyboard key to cycle headlights: Auto -> Manual On -> Manual Off -> Auto");
                
                var harmony = new Harmony("com.fps.mods.headlights");
                harmony.PatchAll();
                Logger.LogInfo("FPS Headlights successfully initialized with clean event-driven hooks.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"FPS Headlights Init failed: {ex}");
            }
        }

        private void Update()
        {
            if (ToggleLightsKey != null && Input.GetKeyDown(ToggleLightsKey.Value))
            {
                if (CurrentHeadlightsMode == EHeadlightsMode.Auto)
                    CurrentHeadlightsMode = EHeadlightsMode.ManualOn;
                else if (CurrentHeadlightsMode == EHeadlightsMode.ManualOn)
                    CurrentHeadlightsMode = EHeadlightsMode.ManualOff;
                else
                    CurrentHeadlightsMode = EHeadlightsMode.Auto;

                //Logger.LogInfo($"[FPS Headlights] Mode toggled to: {CurrentHeadlightsMode}");

                if (HeadlightsText != null)
                {
                    if (CurrentHeadlightsMode == EHeadlightsMode.Auto)
                        HeadlightsText.SetLocKey("Headlights_Auto");
                    else if (CurrentHeadlightsMode == EHeadlightsMode.ManualOn)
                        HeadlightsText.SetLocKey("Headlights_On");
                    else
                        HeadlightsText.SetLocKey("Headlights_Off");
                }

                // Apply changes immediately across all supported lighting systems
                SyncLights();
            }
        }

        private void SyncLights()
        {
            var playerShip = CAppManager.Instance?.GameManager?.CurrentWorld?.PlayerShip as MonoBehaviour;
            if (playerShip == null) return;

            // 1. Sync LightEffectScripts (used by some ships)
            var allLightEffects = playerShip.GetComponentsInChildren<LightEffectScript>(true);
            foreach (var effect in allLightEffects)
            {
                var light = effect.GetComponent<Light>();
                if (light != null && light.type == LightType.Spot)
                {
                    Traverse.Create(effect).Method("CheckAndSetPlaying", true).GetValue();
                }
            }

            // 2. Sync EnableByCondition scripts (used by Scarab and newer ships)
            var allConditions = playerShip.GetComponentsInChildren<EnableByCondition>(true);
            foreach (var cond in allConditions)
            {
                bool hasSpotlight = false;
                var lights = cond.GetComponentsInChildren<Light>(true);
                foreach (var l in lights)
                {
                    if (l != null && l.type == LightType.Spot)
                    {
                        hasSpotlight = true;
                        break;
                    }
                }

                if (hasSpotlight)
                {
                    string alias = Traverse.Create(cond).Field<string>("conditionAliasName").Value;
                    if (string.IsNullOrEmpty(alias)) continue;

                    // Skip unrelated conditions (like unpurchased upgrades)
                    if (!alias.Contains("DayTime") && !alias.Contains("Night") && !alias.Contains("Light"))
                        continue;

                    // Prevent stacking ShadowOn and ShadowOff lights at the same time!
                    bool wantShadows = QualitySettings.shadows != ShadowQuality.Disable;
                    if (alias.Contains("ShadowOn") && !wantShadows) continue;
                    if (alias.Contains("ShadowOff") && wantShadows) continue;

                    // Determine the target state based on override or auto
                    bool targetState = false;
                    if (CurrentHeadlightsMode == EHeadlightsMode.ManualOn) 
                        targetState = true;
                    else if (CurrentHeadlightsMode == EHeadlightsMode.ManualOff) 
                        targetState = false;
                    else 
                    {
                        // Auto mode -> read the underlying game condition state
                        targetState = Traverse.Create(cond).Field<bool>("_isEnable").Value;
                    }

                    // Apply to children just like the game does
                    for (int i = 0; i < cond.transform.childCount; i++)
                    {
                        cond.transform.GetChild(i).gameObject.SetActive(targetState);
                    }
                }
            }
        }
    }

    // ==========================================
    // Harmony Patches: Intercept game systems cleanly
    // ==========================================

    [HarmonyPatch(typeof(EnableByCondition), "SetChildState")]
    public static class EnableByCondition_SetChildState_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(EnableByCondition __instance, ref bool enable)
        {
            try
            {
                bool hasSpotlight = false;
                var lights = __instance.GetComponentsInChildren<Light>(true);
                foreach (var l in lights)
                {
                    if (l != null && l.type == LightType.Spot)
                    {
                        hasSpotlight = true;
                        break;
                    }
                }

                if (hasSpotlight)
                {
                    string alias = Traverse.Create(__instance).Field<string>("conditionAliasName").Value;
                    if (string.IsNullOrEmpty(alias)) return;

                    // Skip unrelated conditions
                    if (!alias.Contains("DayTime") && !alias.Contains("Night") && !alias.Contains("Light"))
                        return;

                    // Prevent stacking ShadowOn and ShadowOff lights at the same time!
                    bool wantShadows = QualitySettings.shadows != ShadowQuality.Disable;
                    if (alias.Contains("ShadowOn") && !wantShadows) return;
                    if (alias.Contains("ShadowOff") && wantShadows) return;

                    if (HeadlightsPlugin.CurrentHeadlightsMode == HeadlightsPlugin.EHeadlightsMode.ManualOn)
                    {
                        enable = true;
                    }
                    else if (HeadlightsPlugin.CurrentHeadlightsMode == HeadlightsPlugin.EHeadlightsMode.ManualOff)
                    {
                        enable = false;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[FPS Headlights] Error in EnableByCondition patch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(EffectScript), "IsNeedPlaying")]
    public static class EffectScript_IsNeedPlaying_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(EffectScript __instance, ref bool __result, ref string outReason)
        {
            try
            {
                if (__instance is LightEffectScript lightEffect)
                {
                    var light = lightEffect.GetComponent<Light>();
                    if (light != null && light.type == LightType.Spot)
                    {
                        if (HeadlightsPlugin.CurrentHeadlightsMode == HeadlightsPlugin.EHeadlightsMode.ManualOn)
                        {
                            __result = true;
                            outReason = "Headlights Manual On";
                        }
                        else if (HeadlightsPlugin.CurrentHeadlightsMode == HeadlightsPlugin.EHeadlightsMode.ManualOff)
                        {
                            __result = false;
                            outReason = "Headlights Manual Off";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[FPS Headlights] Error in IsNeedPlaying patch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(LightEffectScript), "Apply")]
    public static class LightEffectScript_Apply_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(LightEffectScript __instance)
        {
            try
            {
                var light = __instance.GetComponent<Light>();
                if (light != null && light.type == LightType.Spot)
                {
                    if (HeadlightsPlugin.CurrentHeadlightsMode == HeadlightsPlugin.EHeadlightsMode.ManualOn)
                    {
                        light.enabled = true;
                    }
                    else if (HeadlightsPlugin.CurrentHeadlightsMode == HeadlightsPlugin.EHeadlightsMode.ManualOff)
                    {
                        light.enabled = false;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[FPS Headlights] Error in Apply patch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(CAggregates_AggregateView), MethodType.Constructor, new Type[] { typeof(HUDComponent_Aggregates), typeof(IShipAggregate), typeof(CGUI_HUDDescr.CAggregatesDescr.CAggregateDescr), typeof(RectTransform), typeof(RectTransform) })]
    public static class CAggregates_AggregateView_Constructor_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CAggregates_AggregateView __instance, IShipAggregate aggregate)
        {
            try
            {
                if (aggregate.GetAggregateType() == EShipAggregateType.Chassis)
                {
                    var traverse = Traverse.Create(__instance);
                    var container = traverse.Field<RectTransform>("_warnings_container").Value;
                    var template = traverse.Field<RectTransform>("_warning_template").Value;

                    if (container != null && template != null)
                    {
                        var type = AccessTools.TypeByName("CGUI.CBaseCollapsableView+CTextView");
                        var textObj = Activator.CreateInstance(type, new object[] { container, template });
                        
                        var getMethod = type.GetMethod("GetText", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (getMethod != null)
                        {
                            HeadlightsPlugin.HeadlightsText = getMethod.Invoke(textObj, null) as LocTextMeshPro;
                            if (HeadlightsPlugin.HeadlightsText != null)
                            {
                                HeadlightsPlugin.HeadlightsText.SetLocKey("Headlights_Auto");
                                HeadlightsPlugin.HeadlightsText.gameObject.SetActive(true);
                            }
                        }
                        
                        var setActiveMethod = type.GetMethod("SetActive", new Type[] { typeof(bool) });
                        if (setActiveMethod != null)
                        {
                            setActiveMethod.Invoke(textObj, new object[] { true });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[FPS Headlights] Error in CAggregates_AggregateView patch: {ex}");
            }
        }
    }
}
