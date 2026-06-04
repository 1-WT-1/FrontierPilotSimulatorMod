using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using CGUI;

namespace FPS.STOLMode
{
    [BepInPlugin("com.fps.mods.stolmode", "STOL Mode", "1.0.0")]
    public class STOLModePlugin : BaseUnityPlugin
    {
        public static STOLModePlugin Instance;
        public static ConfigEntry<string> ToggleSTOLKey;
        public static ConfigEntry<float> VectorAngleDeg;
        public static ConfigEntry<bool> UseVtolStabilization;
        public static ConfigEntry<bool> STOLEnabledConfig;

        public static bool STOLEnabled = true;
        public static LocTextMeshPro STOLStateText;

        private void Awake()
        {
            try
            {
                Instance = this;
                ToggleSTOLKey = Config.Bind("General", "ToggleSTOLKey", "Keyboard:0:103",
                    "Keyboard/Controller key to toggle STOL Mode (default: G)");
                VectorAngleDeg = Config.Bind("General", "VectorAngleDeg", 15f,
                    "Downward engine vector angle in degrees (clamped 5-25)");
                UseVtolStabilization = Config.Bind("General", "UseVtolStabilization", false,
                    "If true, uses vanilla VTOL auto-leveling/hover stabilization in STOL mode. If false, uses direct control inputs.");
                STOLEnabledConfig = Config.Bind("General", "STOLEnabled", true,
                    "Enable STOL mode by default");
                STOLEnabled = STOLEnabledConfig.Value;

                ModSettingsCore.ModSettingsAPI.AddHeader("GUI_STOLMode_Header");
                ModSettingsCore.ModSettingsAPI.AddToggle("GUI_ModSettings_DefaultState", () => STOLEnabledConfig.Value, (val) => {
                    STOLEnabledConfig.Value = val;
                    STOLEnabled = val;
                });
                ModSettingsCore.ModSettingsAPI.AddKeybind("GUI_STOLMode_Key", ToggleSTOLKey);
                ModSettingsCore.ModSettingsAPI.AddSlider("GUI_STOLMode_Angle", 5f, 25f, 1f, () => VectorAngleDeg.Value, (val) => VectorAngleDeg.Value = val);
                ModSettingsCore.ModSettingsAPI.AddToggle("GUI_STOLMode_VtolStab", () => UseVtolStabilization.Value, (val) => UseVtolStabilization.Value = val);

                var harmony = new Harmony("com.fps.mods.stolmode");
                harmony.PatchAll();
                Logger.LogInfo("STOL Mode initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"STOL Mode Init failed: {ex}");
            }
        }

        private void Update()
        {
            if (ToggleSTOLKey != null && ModSettingsCore.ModSettingsAPI.GetKeyDown(ToggleSTOLKey))
            {
                STOLEnabled = !STOLEnabled;
                if (STOLEnabledConfig != null)
                {
                    STOLEnabledConfig.Value = STOLEnabled;
                }

                if (STOLStateText != null)
                {
                    STOLStateText.SetLocKey(STOLEnabled
                        ? "GUI_HUD_Controls_StateOn"
                        : "GUI_HUD_Controls_StateOff");
                }
            }
        }



        public static float GetClampedAngleDeg()
        {
            return Mathf.Clamp(VectorAngleDeg.Value, 5f, 25f);
        }
    }

    // ================================================================
    // Patch 1: Override engine nacelle X rotation (visual tilt) in Plane mode
    // ================================================================
    [HarmonyPatch(typeof(CShipEngineCalculateRotation), "GetRotationX")]
    public static class EngineCalcRotation_GetRotationX_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CShipEngineCalculateRotation __instance, ref float __result)
        {
            try
            {
                if (!STOLModePlugin.STOLEnabled) return;

                var engine = Traverse.Create(__instance).Field("_owner").GetValue() as CShipEngine;
                if (engine == null) return;

                if (!engine.IsWorking()) return;

                var owner = engine.Ship;
                if (owner == null) return;
                if (owner.ShipFlyState != NamedShipState.Plane) return;

                // Only apply to the player ship
                var playerShip = CAppManager.Instance?.GameManager?.CurrentWorld?.PlayerShip;
                if (playerShip == null || playerShip != owner as IShip) return;

                var place = __instance.Places;
                var jetHookName = engine.Descr.GetJetHookName(place, CLogContext.Current);
                var hook = owner.GetHook(jetHookName, CLogContext.Current);
                if (hook == null) return;

                var localPos = owner.transform.InverseTransformPoint(hook.position) - owner.GetComponent<Rigidbody>().centerOfMass;

                // Read inputs (correct Plane mode mappings)
                float pitchInput = Mathf.Clamp(
                    owner.GetShipInputValue(EInputValueType.Ship_JetpackFB_PlaneUD_WheelsFB, CLogContext.Current) + 
                    owner.GetShipInputValue(EInputValueType.Ship_Pitch, CLogContext.Current), 
                    -1f, 1f);
                float rollInput = -1f * Mathf.Clamp(
                    owner.GetShipInputValue(EInputValueType.Ship_Roll, CLogContext.Current) - 
                    owner.GetShipInputValue(EInputValueType.Ship_YawRoll, CLogContext.Current), 
                    -1f, 1f);

                float theta_base = STOLModePlugin.GetClampedAngleDeg() * Mathf.Deg2Rad;
                float maxPitchRad = STOLModePlugin.GetClampedAngleDeg() * Mathf.Deg2Rad;
                float maxRollRad = Mathf.Max(0f, STOLModePlugin.GetClampedAngleDeg() - 5f) * Mathf.Deg2Rad;

                float local_pitch = -theta_base;
                if (localPos.z < -0.5f) // Tail engines
                {
                    local_pitch += pitchInput * maxPitchRad;
                }
                else // Wing engines / COM engines
                {
                    local_pitch -= pitchInput * maxPitchRad;
                }

                if (localPos.x < -0.1f) // Left engines
                {
                    local_pitch -= rollInput * maxRollRad;
                }
                else if (localPos.x > 0.1f) // Right engines
                {
                    local_pitch += rollInput * maxRollRad;
                }

                // Horizontal is 0.5f, 0 is full down, so a downward angle reduces the value.
                __result = Mathf.Clamp(0.5f + (local_pitch / Mathf.PI), 0f, 1f);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[STOLMode] Error in GetRotationX patch: {ex}");
            }
        }
    }

    // ================================================================
    // Patch 2: Override engine nacelle Z rotation (visual yaw) in Plane mode
    // ================================================================
    [HarmonyPatch(typeof(CShipEngineCalculateRotation), "GetRotationZ")]
    public static class EngineCalcRotation_GetRotationZ_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CShipEngineCalculateRotation __instance, ref float __result)
        {
            try
            {
                if (!STOLModePlugin.STOLEnabled) return;

                var engine = Traverse.Create(__instance).Field("_owner").GetValue() as CShipEngine;
                if (engine == null) return;

                if (!engine.IsWorking()) return;

                var owner = engine.Ship;
                if (owner == null) return;
                if (owner.ShipFlyState != NamedShipState.Plane) return;

                // Only apply to the player ship
                var playerShip = CAppManager.Instance?.GameManager?.CurrentWorld?.PlayerShip;
                if (playerShip == null || playerShip != owner as IShip) return;

                var place = __instance.Places;
                var jetHookName = engine.Descr.GetJetHookName(place, CLogContext.Current);
                var hook = owner.GetHook(jetHookName, CLogContext.Current);
                if (hook == null) return;

                var localPos = owner.transform.InverseTransformPoint(hook.position) - owner.GetComponent<Rigidbody>().centerOfMass;

                // Read inputs
                float yawInput = Mathf.Clamp(
                    owner.GetShipInputValue(EInputValueType.Ship_Yaw, CLogContext.Current) + 
                    owner.GetShipInputValue(EInputValueType.Ship_YawRoll, CLogContext.Current), 
                    -1f, 1f);

                float maxYawRad = STOLModePlugin.GetClampedAngleDeg() * Mathf.Deg2Rad;

                float local_yaw = 0f;
                if (localPos.z < -0.5f) // Tail engines
                {
                    local_yaw -= yawInput * maxYawRad;
                }
                else // Wing engines / COM engines
                {
                    local_yaw += yawInput * maxYawRad;
                }

                __result = Mathf.Clamp(local_yaw / maxYawRad, -1f, 1f);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[STOLMode] Error in GetRotationZ patch: {ex}");
            }
        }
    }

    // ================================================================
    // Patch 3: Dynamic relative torque and downward force application
    // ================================================================
    [HarmonyPatch(typeof(ShipControllerScript), "AddForce")]
    public static class ShipController_AddForce_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(ShipControllerScript __instance, ref Vector3 force, ForceMode mode, EAddForceReason inReason)
        {
            try
            {
                // Only intercept engine forces
                if (inReason != EAddForceReason.Engine) return;
                if (!STOLModePlugin.STOLEnabled) return;
                if (__instance.ShipFlyState != NamedShipState.Plane) return;

                // Only apply to the player ship
                var playerShip = CAppManager.Instance?.GameManager?.CurrentWorld?.PlayerShip;
                if (playerShip == null || playerShip != __instance as IShip) return;

                var engine = __instance.InsideEngine;
                if (engine == null) return;
                if (!engine.IsWorking()) return;

                // Only apply relative torque if VTOL auto-leveling stabilization is disabled
                if (!STOLModePlugin.UseVtolStabilization.Value)
                {
                    var steerer = __instance.GetType().GetField("_steerer", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(__instance) as CShipSteerer;
                    if (steerer != null)
                    {
                        var traverseSteerer = Traverse.Create(steerer);
                        var vtol = traverseSteerer.Field("_vtol").GetValue();
                        if (vtol != null)
                        {
                            var getAccellMethod = vtol.GetType().GetMethod("GetAccell", BindingFlags.Instance | BindingFlags.NonPublic);
                            if (getAccellMethod != null)
                            {
                                Vector3 accellDeg = (Vector3)getAccellMethod.Invoke(vtol, null);
                                Vector3 accellRad = accellDeg * Mathf.Deg2Rad;

                                // Read inputs (matching CVTOLSteerer sign conventions)
                                float pitchInput = Mathf.Clamp(
                                    __instance.GetShipInputValue(EInputValueType.Ship_JetpackFB_PlaneUD_WheelsFB, CLogContext.Current) + 
                                    __instance.GetShipInputValue(EInputValueType.Ship_Pitch, CLogContext.Current), 
                                    -1f, 1f);
                                float yawInput = Mathf.Clamp(
                                    __instance.GetShipInputValue(EInputValueType.Ship_Yaw, CLogContext.Current) + 
                                    __instance.GetShipInputValue(EInputValueType.Ship_YawRoll, CLogContext.Current), 
                                    -1f, 1f);
                                float rollInput = -1f * Mathf.Clamp(
                                    __instance.GetShipInputValue(EInputValueType.Ship_Roll, CLogContext.Current) - 
                                    __instance.GetShipInputValue(EInputValueType.Ship_YawRoll, CLogContext.Current), 
                                    -1f, 1f);

                                float throttle = Mathf.Max(0f, engine.GetThrottle().z);

                                // Apply relative torque directly using ForceMode.Acceleration
                                Vector3 torqueAcc = new Vector3(
                                    pitchInput * accellRad.x,
                                    yawInput * accellRad.y,
                                    rollInput * accellRad.z
                                ) * throttle;

                                __instance.AddRelativeTorque(torqueAcc, ForceMode.Acceleration, EAddForceReason.Engine);
                            }
                        }
                    }
                }

                // Rotate the main force vector downward by the configured STOL angle (always runs when STOL is active)
                float angleDeg = STOLModePlugin.GetClampedAngleDeg();
                Vector3 shipRight = __instance.GetRotation() * Vector3.right;
                Quaternion tilt = Quaternion.AngleAxis(-angleDeg, shipRight);
                force = tilt * force;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[STOLMode] Error in AddForce prefix patch: {ex}");
            }
        }
    }

    // ================================================================
    // Patch 4: CShipSteerer FixedUpdate Prefix to run VTOL stabilization if enabled
    // ================================================================
    [HarmonyPatch(typeof(CShipSteerer), "FixedUpdate")]
    public static class CShipSteerer_FixedUpdate_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(CShipSteerer __instance, float inTime, CLogContext inLogContext)
        {
            try
            {
                if (!STOLModePlugin.STOLEnabled) return;
                if (!STOLModePlugin.UseVtolStabilization.Value) return;
                if (__instance.Ship.ShipFlyState != NamedShipState.Plane) return;

                // Only apply to the player ship
                var playerShip = CAppManager.Instance?.GameManager?.CurrentWorld?.PlayerShip;
                if (playerShip == null || playerShip != __instance.Ship as IShip) return;

                var engine = __instance.Ship.InsideEngine;
                if (engine == null || !engine.IsWorking()) return;

                var vtol = Traverse.Create(__instance).Field("_vtol").GetValue();
                if (vtol == null) return;

                var method = vtol.GetType().GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.Public);
                if (method != null)
                {
                    object[] args = new object[] { inTime, Vector3.zero, inLogContext };
                    method.Invoke(vtol, args);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[STOLMode] Error in CShipSteerer.FixedUpdate prefix patch: {ex}");
            }
        }
    }

    // ================================================================
    // Patch 5: HUD display — add STOL label to Engine aggregate view
    // ================================================================
    [HarmonyPatch(typeof(CAggregates_AggregateView), MethodType.Constructor,
        new Type[] { typeof(HUDComponent_Aggregates), typeof(IShipAggregate),
                     typeof(CGUI_HUDDescr.CAggregatesDescr.CAggregateDescr),
                     typeof(RectTransform), typeof(RectTransform) })]
    public static class CAggregates_AggregateView_Constructor_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CAggregates_AggregateView __instance, IShipAggregate aggregate)
        {
            try
            {
                if (aggregate.GetAggregateType() == EShipAggregateType.Engine)
                {
                    var traverse = Traverse.Create(__instance);
                    var container = traverse.Field<RectTransform>("_warnings_container").Value;
                    var template = traverse.Field<RectTransform>("_warning_template").Value;

                    if (container != null && template != null)
                    {
                        var type = AccessTools.TypeByName("CGUI.CBaseCollapsableView+CTextView");

                        // Label
                        var labelObj = Activator.CreateInstance(type, new object[] { container, template });
                        var getMethod = type.GetMethod("GetText", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (getMethod != null)
                        {
                            var labelText = getMethod.Invoke(labelObj, null) as LocTextMeshPro;
                            if (labelText != null)
                            {
                                labelText.SetText("STOL: ");
                                labelText.gameObject.SetActive(true);
                            }
                        }

                        // State text
                        var stateObj = Activator.CreateInstance(type, new object[] { container, template });
                        var getMethod2 = type.GetMethod("GetText", BindingFlags.Instance | BindingFlags.NonPublic);
                        STOLModePlugin.STOLStateText = getMethod2.Invoke(stateObj, null) as LocTextMeshPro;
                        if (STOLModePlugin.STOLStateText != null)
                        {
                            STOLModePlugin.STOLStateText.SetLocKey(STOLModePlugin.STOLEnabled
                                ? "GUI_HUD_Controls_StateOn"
                                : "GUI_HUD_Controls_StateOff");
                            STOLModePlugin.STOLStateText.gameObject.SetActive(true);
                        }

                        var setActiveMethod = type.GetMethod("SetActive", new Type[] { typeof(bool) });
                        if (setActiveMethod != null)
                        {
                            setActiveMethod.Invoke(labelObj, new object[] { true });
                            setActiveMethod.Invoke(stateObj, new object[] { true });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[STOLMode] Error in HUD patch: {ex}");
            }
        }
    }
}
