using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using NewCamera;
using CGUI;
using ModSettingsCore;

namespace FPS.VelocityCamera
{
    [BepInPlugin("com.fps.mods.velocitycamera", "FPS Velocity Tracking Camera", "1.0.0")]
    [BepInDependency("com.fps.mods.modsettingscore")]
    public class VelocityCameraPlugin : BaseUnityPlugin
    {
        public static VelocityCameraPlugin Instance;
        public static ConfigEntry<bool> VelocityCameraEnabledConfig;
        public static ConfigEntry<string> ToggleVelocityCameraKey;
        public static ConfigEntry<float> MinSpeed;
        public static ConfigEntry<float> FullSpeed;
        public static ConfigEntry<float> MaxDriftAngle;

        public static bool VelocityCameraEnabled = false; 
        public static LocTextMeshPro SystemStateText;

        private void Awake()
        {
            try
            {
                Instance = this;

                VelocityCameraEnabledConfig = Config.Bind("General", "VelocityCameraEnabled", false, 
                    "Enable Velocity Tracking Camera by default");
                VelocityCameraEnabled = VelocityCameraEnabledConfig.Value;

                ToggleVelocityCameraKey = Config.Bind("General", "ToggleVelocityCameraKey", "None", 
                    "Keyboard/Controller key to toggle Velocity Tracking Camera (default: None)");

                MinSpeed = Config.Bind("General", "MinSpeed", 2.0f, 
                    "Minimum speed (m/s) to start tracking velocity vector");

                FullSpeed = Config.Bind("General", "FullSpeed", 10.0f, 
                    "Speed (m/s) at which camera is fully aligned with velocity vector");

                MaxDriftAngle = Config.Bind("General", "MaxDriftAngle", 60.0f, 
                    "Maximum drift angle (deg) before camera tracking starts to fade out");

                // Register with ModSettingsCore
                ModSettingsCore.ModSettingsAPI.AddHeader("GUI_VelocityCamera_Header");
                ModSettingsCore.ModSettingsAPI.AddToggle("GUI_ModSettings_DefaultState", () => VelocityCameraEnabledConfig.Value, (val) => {
                        VelocityCameraEnabled = val;
                        VelocityCameraEnabledConfig.Value = val;
                        if (SystemStateText != null)
                            SystemStateText.SetLocKey(VelocityCameraEnabled ? "GUI_HUD_Controls_StateOn" : "GUI_HUD_Controls_StateOff");
                    });

                ModSettingsCore.ModSettingsAPI.AddSlider("GUI_VelocityCamera_MinSpeed", 
                    0f, 50f, 1f,
                    () => MinSpeed.Value, 
                    (val) => MinSpeed.Value = val);

                ModSettingsCore.ModSettingsAPI.AddSlider("GUI_VelocityCamera_FullSpeed", 
                    0f, 100f, 1f,
                    () => FullSpeed.Value, 
                    (val) => FullSpeed.Value = val);

                ModSettingsCore.ModSettingsAPI.AddSlider("GUI_VelocityCamera_MaxDriftAngle", 
                    0f, 180f, 5f,
                    () => MaxDriftAngle.Value, 
                    (val) => MaxDriftAngle.Value = val);

                ModSettingsCore.ModSettingsAPI.AddKeybind("GUI_VelocityCamera_ToggleKey", ToggleVelocityCameraKey);

                var harmony = new Harmony("com.fps.mods.velocitycamera");
                harmony.PatchAll();
                Logger.LogInfo("Velocity Tracking Camera initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Velocity Tracking Camera Init failed: {ex}");
            }
        }

        private void Update()
        {
            if (ModSettingsAPI.GetKeyDown(ToggleVelocityCameraKey))
            {
                VelocityCameraEnabled = !VelocityCameraEnabled;
                if (VelocityCameraEnabledConfig != null)
                {
                    VelocityCameraEnabledConfig.Value = VelocityCameraEnabled;
                }
                Logger.LogInfo("Velocity Camera Enabled toggled: " + VelocityCameraEnabled);
                
                if (SystemStateText != null)
                {
                    SystemStateText.SetLocKey(VelocityCameraEnabled ? "GUI_HUD_Controls_StateOn" : "GUI_HUD_Controls_StateOff");
                }
            }
        }
    }

    [HarmonyPatch(typeof(CCameraTarget_ShiftedPos), "CalcNeedPosition")]
    public static class CCameraTarget_ShiftedPos_CalcNeedPosition_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(CCameraTarget_ShiftedPos __instance, float inTime, ref RVector inTargetPos)
        {
            try
            {
                if (!VelocityCameraPlugin.VelocityCameraEnabled) return;

                var playerShip = CAppManager.Instance?.GameManager?.CurrentWorld?.PlayerShip;
                if (playerShip == null) return;

                var targetEntity = Traverse.Create(__instance).Field("_target").GetValue() as CTargetRef;
                if (targetEntity == null || targetEntity.IsEmpty || targetEntity.GetEntity() != playerShip as IWorldEntity) return;

                // Obtain 0.3s averaged physical velocity
                Vector3 velocity = playerShip.GetVelocity(0.3f);
                float speed = velocity.magnitude;

                float minSpeed = VelocityCameraPlugin.MinSpeed.Value;
                float fullSpeed = VelocityCameraPlugin.FullSpeed.Value;

                if (speed >= minSpeed)
                {
                    float t = Mathf.Clamp01((speed - minSpeed) / (fullSpeed - minSpeed));

                    Vector3 velocityDir = velocity.normalized;
                    Quaternion targetRot = inTargetPos.Rotation;

                    float angle = Vector3.Angle(velocityDir, targetRot * Vector3.forward);
                    float maxAngle = VelocityCameraPlugin.MaxDriftAngle.Value;

                    if (angle > maxAngle)
                    {
                        if (angle < maxAngle + 30f)
                        {
                            float fade = 1f - ((angle - maxAngle) / 30f);
                            t *= fade;
                        }
                        else
                        {
                            t = 0f;
                        }
                    }

                    if (t > 0.001f)
                    {
                        Vector3 up = targetRot * Vector3.up;
                        Quaternion velocityRot = Quaternion.LookRotation(velocityDir, up);
                        Quaternion blendedRot = Quaternion.Slerp(targetRot, velocityRot, t);

                        inTargetPos = new RVector(inTargetPos.Position, blendedRot);
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }

    // HUD Text integration — attached to Engine aggregate
    [HarmonyPatch(typeof(CAggregates_AggregateView), MethodType.Constructor, new Type[] { 
        typeof(HUDComponent_Aggregates), typeof(IShipAggregate), 
        typeof(CGUI_HUDDescr.CAggregatesDescr.CAggregateDescr), typeof(RectTransform), typeof(RectTransform) 
    })]
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
                        var textObj = Activator.CreateInstance(type, new object[] { container, template });
                        
                        var getMethod = type.GetMethod("GetText", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (getMethod != null)
                        {
                            var textPro = getMethod.Invoke(textObj, null) as LocTextMeshPro;
                            if (textPro != null)
                            {
                                textPro.SetText("VELCAM: ");
                                textPro.gameObject.SetActive(true);
                                
                                var textObj2 = Activator.CreateInstance(type, new object[] { container, template });
                                var getMethod2 = type.GetMethod("GetText", BindingFlags.Instance | BindingFlags.NonPublic);
                                VelocityCameraPlugin.SystemStateText = getMethod2.Invoke(textObj2, null) as LocTextMeshPro;
                                
                                if (VelocityCameraPlugin.SystemStateText != null)
                                {
                                    VelocityCameraPlugin.SystemStateText.SetLocKey(VelocityCameraPlugin.VelocityCameraEnabled
                                        ? "GUI_HUD_Controls_StateOn"
                                        : "GUI_HUD_Controls_StateOff");
                                    VelocityCameraPlugin.SystemStateText.gameObject.SetActive(true);
                                }
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
                Debug.LogError($"[VelocityCamera] Error in HUD patch: {ex}");
            }
        }
    }
}
