using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using TMPro;
using CGUI;
using System.Collections.Generic;

namespace FPS.CrosswindCrab
{
    [BepInPlugin("com.fps.mods.crosswindcrab", "FPS Crosswind Crab System", "1.0.0")]
    public class CrosswindCrabPlugin : BaseUnityPlugin
    {
        public static CrosswindCrabPlugin Instance;
        public static ConfigEntry<string> ToggleCrabKey;
        public static ConfigEntry<bool> CrabSystemEnabledConfig;
        public static bool CrabSystemEnabled = true;
        public static float CurrentCrabAngle = 0f;
        
        public static LocTextMeshPro SystemStateText;
        public static Vector3 CustomWorldWind = Vector3.zero;
        public static bool CustomWindEnabled = false;

        private bool _commandRegistered = false;

        private void RegisterConsoleCommand()
        {
            try
            {
                CAppManager.Instance.GetConsole().AddCommandHandler(this, "SetCustomWind", new ConsoleArgumentBase[3]
                {
                    new CAB_Float("X"),
                    new CAB_Float("Y"),
                    new CAB_Float("Z")
                }, OnConsole_SetCustomWind);
                Logger.LogInfo("SetCustomWind console command registered successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to register SetCustomWind console command: {ex}");
            }
        }

        private void OnConsole_SetCustomWind(string[] inArgs, CLogContext inLogContext)
        {
            try
            {
                float x = CGameConsole.GetFloatFromArg(inArgs, 1);
                float y = CGameConsole.GetFloatFromArg(inArgs, 2);
                float z = CGameConsole.GetFloatFromArg(inArgs, 3);
                CustomWorldWind = new Vector3(x, y, z);
                CustomWindEnabled = CustomWorldWind.sqrMagnitude > 0.01f;
                CAppManager.Instance.GetConsole().WriteResult(success: true, $"Custom wind set to {CustomWorldWind}, Enabled: {CustomWindEnabled}");
            }
            catch (Exception ex)
            {
                CAppManager.Instance.GetConsole().WriteResult(success: false, $"Error: {ex.Message}");
            }
        }

        private void Awake()
        {
            try
            {
                Instance = this;
                ToggleCrabKey = Config.Bind("General", "ToggleCrabKey", "Keyboard:0:107", "Keyboard/Controller key to toggle Crosswind Crab Steering (default: K)");
                CrabSystemEnabledConfig = Config.Bind("General", "CrabSystemEnabled", true, "Enable Crosswind Crab Steering System by default");
                CrabSystemEnabled = CrabSystemEnabledConfig.Value;
                
                ModSettingsCore.ModSettingsAPI.AddHeader("GUI_Crab_Header");
                ModSettingsCore.ModSettingsAPI.AddToggle("GUI_ModSettings_DefaultState", () => CrabSystemEnabledConfig.Value, (val) => {
                    CrabSystemEnabledConfig.Value = val;
                    CrabSystemEnabled = val;
                });
                ModSettingsCore.ModSettingsAPI.AddKeybind("GUI_Crab_Key", ToggleCrabKey);

                var harmony = new Harmony("com.fps.mods.crosswindcrab");
                harmony.PatchAll();
                Logger.LogInfo("Crosswind Crab initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Crosswind Crab Init failed: {ex}");
            }
        }

        private void Update()
        {
            if (ToggleCrabKey != null && ModSettingsCore.ModSettingsAPI.GetKeyDown(ToggleCrabKey))
            {
                CrabSystemEnabled = !CrabSystemEnabled;
                if (CrabSystemEnabledConfig != null)
                {
                    CrabSystemEnabledConfig.Value = CrabSystemEnabled;
                }
                
                if (SystemStateText != null)
                {
                    SystemStateText.SetLocKey(CrabSystemEnabled ? "GUI_HUD_Controls_StateOn" : "GUI_HUD_Controls_StateOff");
                }
            }
        }

        private void FixedUpdate()
        {
            try
            {
                if (!_commandRegistered && CAppManager.Instance?.GetConsole() != null)
                {
                    RegisterConsoleCommand();
                    _commandRegistered = true;
                }

                var playerShip = CAppManager.Instance?.GameManager?.CurrentWorld?.PlayerShip;
                if (playerShip == null) return;

                var shipMono = playerShip as MonoBehaviour;
                if (shipMono == null) return;

                float targetCrabAngle = 0f;

                if (CrabSystemEnabled)
                {
                    Vector3 vel = Vector3.ProjectOnPlane(playerShip.GetVelocity(0.1f), Vector3.up);
                    Vector3 fwd = Vector3.ProjectOnPlane(shipMono.transform.forward, Vector3.up);
                    
                    if (vel.magnitude > 1.0f)
                    {
                        targetCrabAngle = Vector3.SignedAngle(fwd, vel, Vector3.up);
                    }

                    // Calculate compression across all wheels (Weight on Wheels) using IShip interface
                    float avgCompression = playerShip.GetChassisCompression01();

                    // Smoothly reduce crab angle as wheels compress, reaching 0 at 0.2f compression (typical resting load)
                    float compressionFactor = Mathf.Clamp01(1.0f - (avgCompression / 0.2f));
                    targetCrabAngle *= compressionFactor;

                    // Fully disable crab angle when in Wheels/Taxi mode
                    if (playerShip.ShipFlyState == NamedShipState.Wheels)
                    {
                        targetCrabAngle = 0f;
                    }
                }

                // Smoothly interpolate current crab angle to target
                CurrentCrabAngle = Mathf.MoveTowards(CurrentCrabAngle, targetCrabAngle, 60f * Time.fixedDeltaTime);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in CrosswindCrab FixedUpdate: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(ShipWheel.CShipWheel), "GetSteerAngle")]
    public static class CShipWheel_GetSteerAngle_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ShipWheel.CShipWheel __instance, ref float __result)
        {
            try
            {
                float crab = CrosswindCrabPlugin.CurrentCrabAngle;
                if (Mathf.Abs(crab) < 0.01f) return;

                var traverse = Traverse.Create(__instance);
                var owner = traverse.Field<CShipChassis>("_owner").Value;
                
                if (owner != null)
                {
                    var place = __instance.Place;

                    // Do not apply crab steering to natively non-steerable wheels
                    if (EntityPlaceOp.BitCrossing(place, EEntityPlaces.Back) && owner.IsSteerOnlyForwWheels())
                    {
                        return;
                    }

                    if (owner.Descr != null && owner.Descr.SpeedToSteerAngle != null)
                    {
                        float maxSteerAtZeroSpeed = owner.Descr.SpeedToSteerAngle.GetTableValue(0f, CLogContext.Current);
                        if (maxSteerAtZeroSpeed < 1f) return; // Non-steerable bogeys remain straight

                        __result = Mathf.Clamp(__result + crab, -maxSteerAtZeroSpeed, maxSteerAtZeroSpeed);
                    }
                    else
                    {
                        float maxSteer = owner.GetMaxSteerAngle();
                        if (maxSteer < 1f) return;
                        __result = Mathf.Clamp(__result + crab, -maxSteer, maxSteer);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CrosswindCrab] Error in GetSteerAngle patch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(ShipWheel.CShipWheel), "GetSteerAngle01")]
    public static class CShipWheel_GetSteerAngle01_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ShipWheel.CShipWheel __instance, ref float __result)
        {
            try
            {
                float crab = CrosswindCrabPlugin.CurrentCrabAngle;
                if (Mathf.Abs(crab) < 0.01f) return;

                var traverse = Traverse.Create(__instance);
                var owner = traverse.Field<CShipChassis>("_owner").Value;
                
                if (owner != null)
                {
                    var place = __instance.Place;
                    // Do not apply crab steering to natively non-steerable wheels
                    if (EntityPlaceOp.BitCrossing(place, EEntityPlaces.Back) && owner.IsSteerOnlyForwWheels())
                    {
                        return;
                    }

                    if (owner.Descr != null && owner.Descr.SpeedToSteerAngle != null)
                    {
                        float maxSteerAtZeroSpeed = owner.Descr.SpeedToSteerAngle.GetTableValue(0f, CLogContext.Current);
                        if (maxSteerAtZeroSpeed < 1f) return;

                        float crabFraction = crab / maxSteerAtZeroSpeed;
                        __result = Mathf.Clamp(__result + crabFraction, -1f, 1f);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CrosswindCrab] Error in GetSteerAngle01 patch: {ex}");
            }
        }
    }

    // HUD Text integration (Optional, similar to Headlights/Compass)
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
                        
                        var getMethod = type.GetMethod("GetText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        if (getMethod != null)
                        {
                            var textPro = getMethod.Invoke(textObj, null) as LocTextMeshPro;
                            if (textPro != null)
                            {
                                textPro.SetText("CRAB: ");
                                textPro.gameObject.SetActive(true);
                                
                                // To easily make a toggleable text, just append ON/OFF or create a second text object
                                var textObj2 = Activator.CreateInstance(type, new object[] { container, template });
                                var getMethod2 = type.GetMethod("GetText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                                CrosswindCrabPlugin.SystemStateText = getMethod2.Invoke(textObj2, null) as LocTextMeshPro;
                                
                                if (CrosswindCrabPlugin.SystemStateText != null)
                                {
                                    CrosswindCrabPlugin.SystemStateText.SetLocKey(CrosswindCrabPlugin.CrabSystemEnabled
                                        ? "GUI_HUD_Controls_StateOn"
                                        : "GUI_HUD_Controls_StateOff");
                                    CrosswindCrabPlugin.SystemStateText.gameObject.SetActive(true);
                                }
                            }
                        }
                        
                        var setActiveMethod = type.GetMethod("SetActive", new Type[] { typeof(bool) });
                        if (setActiveMethod != null)
                        {
                            setActiveMethod.Invoke(textObj, new object[] { true });
                            // Don't have textObj2 instance here, but it's okay, SetActive true already happens
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CrosswindCrab] Error in HUD patch: {ex}");
            }
        }
    }

    // Patch for CWindEngine (Classic physics)
    [HarmonyPatch(typeof(CWindEngine), "FixedUpdate")]
    public static class CWindEngine_FixedUpdate_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CWindEngine __instance)
        {
            if (CrosswindCrabPlugin.CustomWindEnabled)
            {
                var traverse = Traverse.Create(__instance);
                var owner = traverse.Field<ShipControllerScript>("_owner").Value;
                if (owner != null)
                {
                    var rPosition = owner.GetRPosition();
                    var localWind = rPosition.WorldToLocalDirection_Parent(CrosswindCrabPlugin.CustomWorldWind);
                    
                    var currentLocal = traverse.Field<Vector3>("_wind_local_velocity").Value;
                    traverse.Field<Vector3>("_wind_local_velocity").Value = currentLocal + localWind;
                }
            }
        }
    }

    // Patch for CAWindEngine (Ship2 / Aerodynamics physics)
    [HarmonyPatch(typeof(Ship2.CAWindEngine), "GetAirLocalVelocity")]
    public static class CAWindEngine_GetAirLocalVelocity_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CBaseWindObject inWind, RVector inShipRPos, ref Vector3 __result)
        {
            if (CrosswindCrabPlugin.CustomWindEnabled)
            {
                __result += GameMath.WorldToLocalDirection(CrosswindCrabPlugin.CustomWorldWind, inShipRPos.Rotation);
            }
        }
    }
}
