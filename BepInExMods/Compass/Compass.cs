using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Ship.Descriptions.EffectScript;
using CGUI;
using System.Linq;
namespace FPS.Compass
{
    [BepInPlugin("com.fps.mods.compass", "FPS Compass Enhancements", "1.0.0")]
    public class CompassPlugin : BaseUnityPlugin
    {
        public static ConfigEntry<bool> ShowShipHeadingNumber;
        public static ConfigEntry<bool> ShowVelocityHeadingNumber;
        public static ConfigEntry<bool> ShowIntermediateCompassMarks;

        public static System.Runtime.CompilerServices.ConditionalWeakTable<object, object> CustomMarks = new System.Runtime.CompilerServices.ConditionalWeakTable<object, object>();

        private void Awake()
        {
            try
            {
                ShowShipHeadingNumber = Config.Bind("Compass", "ShowShipHeadingNumber", true, "Show numeric heading below the ship's forward caret");
                ShowVelocityHeadingNumber = Config.Bind("Compass", "ShowVelocityHeadingNumber", true, "Show numeric heading below the ship's velocity caret");
                ShowIntermediateCompassMarks = Config.Bind("Compass", "ShowIntermediateCompassMarks", true, "Show 45, 135, 225, 315 marks on the compass tape");

                var harmony = new Harmony("com.fps.mods.compass");
                harmony.PatchAll();
                Logger.LogInfo("FPS Compass Enhancements successfully initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"FPS Compass Enhancements Init failed: {ex}");
            }
        }
    }



    // ==========================================
    // Harmony Patch: Implements numerical heading HUD element
    // ==========================================
    [HarmonyPatch(typeof(HUDComponent_Compass), "Init")]
    public static class HUDComponent_Compass_Init_Patch
    {
        public static TextMeshProUGUI ShipHeadingText;
        public static TextMeshProUGUI VelocityHeadingText;

        [HarmonyPostfix]
        public static void Postfix(HUDComponent_Compass __instance)
        {
            try
            {
                Debug.Log("[FPS Compass] Compass Init Postfix started.");

                // Find the Rose transform and carets
                var rose = __instance.transform.FindChildByName<RectTransform>("$Rose");
                if (rose == null)
                {
                    Debug.LogError("[FPS Compass] Could not find '$Rose' under HUDComponent_Compass!");
                    return;
                }

                var shipForward = rose.FindChildByName<RectTransform>("$ShipForward");
                var shipVelocity = rose.FindChildByName<RectTransform>("$ShipVelocity");
                var cameraFov = rose.FindChildByName<RectTransform>("$CameraFOV");

                Debug.Log($"[FPS Compass] Found carets: ShipForward={shipForward != null}, ShipVelocity={shipVelocity != null}, CameraFOV={cameraFov != null}");

                // Find an existing TextMeshProUGUI to copy font and material from
                var existingText = __instance.GetComponentInChildren<TextMeshProUGUI>(true);
                if (existingText == null)
                {
                    Debug.LogWarning("[FPS Compass] Could not find any existing TextMeshProUGUI to copy styling from.");
                }

                // Helper to create styled heading text
                TextMeshProUGUI CreateHeadingText(string name, Transform parent, Color color, Vector2 pivot, Vector2 anchorMin, Vector2 anchorMax)
                {
                    GameObject textGO = new GameObject(name);
                    textGO.transform.SetParent(parent, false);
                    textGO.layer = parent.gameObject.layer; // CRITICAL: Use the parent's UI layer!

                    var layoutElement = textGO.AddComponent<UnityEngine.UI.LayoutElement>();
                    layoutElement.ignoreLayout = true;

                    var tmp = textGO.AddComponent<TextMeshProUGUI>();
                    tmp.text = "000";
                    tmp.fontSize = 11; // Smaller font as requested
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = color;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.overflowMode = TextOverflowModes.Overflow;
                    tmp.enableWordWrapping = false;

                    var rectTransform = textGO.GetComponent<RectTransform>();
                    rectTransform.anchorMin = anchorMin;
                    rectTransform.anchorMax = anchorMax;
                    rectTransform.pivot = pivot;
                    rectTransform.sizeDelta = new Vector2(100f, 30f);
                    rectTransform.localScale = Vector3.one;
                    rectTransform.localRotation = Quaternion.identity;

                    if (existingText != null)
                    {
                        tmp.font = existingText.font;
                        tmp.fontSharedMaterial = existingText.fontSharedMaterial;
                    }

                    return tmp;
                }

                // 2. Ship heading text: parent to __instance.transform to avoid mask and tape letters
                ShipHeadingText = CreateHeadingText(
                    "ShipHeadingReadout",
                    __instance.transform,
                    Color.white, // Bright white
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f)
                );

                // 3. Velocity heading text: parent to __instance.transform to avoid mask and tape letters
                VelocityHeadingText = CreateHeadingText(
                    "VelocityHeadingReadout",
                    __instance.transform,
                    new Color(1f, 0.75f, 0.2f, 1f), // Light orange/yellow
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f)
                );

                Debug.Log("[FPS Compass] All heading text readouts successfully created.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FPS Compass] Error in Compass Init Postfix: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(HUDComponent_Compass), "UpdateByOwner")]
    public static class HUDComponent_Compass_UpdateByOwner_Patch
    {
        private static float _lastLogTime = 0f;

        [HarmonyPostfix]
        public static void Postfix(HUDComponent_Compass __instance)
        {
            try
            {
                var playerShip = __instance.CurrentWorld?.PlayerShip;
                if (playerShip != null)
                {
                    // Look up carets for tracking
                    var rose = __instance.transform.FindChildByName<RectTransform>("$Rose");
                    RectTransform shipForward = null;
                    RectTransform shipVelocity = null;
                    if (rose != null)
                    {
                        shipForward = rose.FindChildByName<RectTransform>("$ShipForward");
                        shipVelocity = rose.FindChildByName<RectTransform>("$ShipVelocity");
                    }

                    // 2. Update Ship Forward heading and position
                    if (__instance.CurrentWorld.PlayerShip != null && HUDComponent_Compass_Init_Patch.ShipHeadingText != null && shipForward != null)
                    {
                        float shipHeading = __instance.CurrentWorld.PlayerShip.GetRotation().eulerAngles.y;
                        int shipHeadingInt = Mathf.RoundToInt(shipHeading) % 360;
                        if (shipHeadingInt < 0) shipHeadingInt += 360;
                        HUDComponent_Compass_Init_Patch.ShipHeadingText.text = shipHeadingInt.ToString("D3");
                        
                        // Avoid mask: position in __instance.transform local space, below the caret
                        if (rose != null)
                        {
                            HUDComponent_Compass_Init_Patch.ShipHeadingText.rectTransform.localPosition = 
                                rose.localPosition + new Vector3(shipForward.localPosition.x, shipForward.localPosition.y - 18f, 0f);
                        }
                        
                        // Respect both the caret's active state AND our config setting
                        HUDComponent_Compass_Init_Patch.ShipHeadingText.gameObject.SetActive(
                            shipForward.gameObject.activeInHierarchy && CompassPlugin.ShowShipHeadingNumber.Value);
                    }

                    // 3. Update Ship Velocity heading and position
                    if (__instance.CurrentWorld.PlayerShip != null && HUDComponent_Compass_Init_Patch.VelocityHeadingText != null && shipVelocity != null)
                    {
                        Vector3 vel = Vector3.ProjectOnPlane(__instance.CurrentWorld.PlayerShip.GetVelocity(0.3f), Vector3.up);
                        if (vel.magnitude > 0.1f)
                        {
                            float velHeading = Vector3.Angle(Vector3.forward, vel);
                            if (Vector3.Cross(Vector3.forward, vel).y < 0f) velHeading *= -1f;
                            int velHeadingInt = Mathf.RoundToInt(velHeading) % 360;
                            if (velHeadingInt < 0) velHeadingInt += 360;
                            HUDComponent_Compass_Init_Patch.VelocityHeadingText.text = velHeadingInt.ToString("D3");
                            
                            // Avoid mask: position in __instance.transform local space, further below the caret so it doesn't overlap ship heading
                            if (rose != null)
                            {
                                HUDComponent_Compass_Init_Patch.VelocityHeadingText.rectTransform.localPosition = 
                                    rose.localPosition + new Vector3(shipVelocity.localPosition.x, shipVelocity.localPosition.y - 32f, 0f);
                            }
                            
                            // Respect both the caret's active state AND our config setting
                            HUDComponent_Compass_Init_Patch.VelocityHeadingText.gameObject.SetActive(
                                shipVelocity.gameObject.activeInHierarchy && CompassPlugin.ShowVelocityHeadingNumber.Value);
                        }
                        else
                        {
                            HUDComponent_Compass_Init_Patch.VelocityHeadingText.gameObject.SetActive(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (Time.time - _lastLogTime > 5f)
                {
                    Debug.LogError($"[FPS Compass] Error in Compass Update Postfix (throttled): {ex}");
                    _lastLogTime = Time.time;
                }
            }
        }
    }

    // ==========================================
    // Harmony Patch: Adds numerical marks (45, 135, 225, 315) to heading tape
    // ==========================================
    [HarmonyPatch]
    public static class CCompass_Rose_Constructor_Patch
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return AccessTools.Constructor(typeof(CCompass_Rose), new Type[] { typeof(HUDComponent_Compass) });
        }

        [HarmonyPostfix]
        public static void Postfix(CCompass_Rose __instance)
        {
            try
            {
                var traverse = Traverse.Create(__instance);
                var root = traverse.Field<RectTransform>("_root").Value;
                var descr = traverse.Field<CGUI_HUDDescr.CCompassDescr>("_descr").Value;
                var elements = traverse.Field("_elements").GetValue() as IList;

                if (root != null && descr != null && elements != null)
                {
                    RectTransform labelContainer = null;
                    RectTransform labelTemplate = null;

                    foreach (var roseElement in descr.RoseElements)
                    {
                        if (roseElement.Angle == 0f)
                        {
                            labelContainer = root.FindChildByName<RectTransform>(roseElement.Container);
                            labelTemplate = labelContainer?.FindChildByName<RectTransform>(roseElement.Template);
                            break;
                        }
                    }

                    if (labelContainer != null && labelTemplate != null)
                    {
                        // Add elements at 45, 135, 225, 315 degrees next to cardinal labels
                        float[] customAngles = new float[] { 45f, 135f, 225f, 315f };
                        var type = AccessTools.TypeByName("CGUI.CCompass_Rose+CElementView");

                        foreach (var angle in customAngles)
                        {
                            var elementView = Activator.CreateInstance(
                                type,
                                new object[] { __instance, labelContainer, labelTemplate, angle, ((int)angle).ToString() }
                            );

                            if (elementView != null)
                            {
                                elements.Add(elementView);
                                CompassPlugin.CustomMarks.Add(elementView, null);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FPS Compass] Error in CCompass_Rose constructor patch: {ex}");
            }
        }
    }

    // ==========================================
    // Harmony Patch: Dynamic visibility of custom marks
    // ==========================================
    [HarmonyPatch]
    public static class CCompass_Rose_CElementView_Update_Patch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("CGUI.CCompass_Rose+CElementView"), "Update");
        }

        public static void Postfix(object __instance)
        {
            try
            {
                if (CompassPlugin.CustomMarks.TryGetValue(__instance, out _))
                {
                    var element = Traverse.Create(__instance).Property<RectTransform>("Element").Value;
                    if (element != null)
                    {
                        bool shouldShow = CompassPlugin.ShowIntermediateCompassMarks.Value;
                        if (element.gameObject.activeSelf != shouldShow)
                        {
                            element.gameObject.SetActive(shouldShow);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FPS Compass] Error in CElementView Update patch: {ex}");
            }
        }
    }

    // ==========================================
    // Harmony Patch: Inject custom HUD controls
    // ==========================================
    [HarmonyPatch(typeof(CInfo_Controls), "CreateControls")]
    public static class CInfo_Controls_CreateControls_Patch
    {
        private static bool _injected = false;

        public static void Prefix()
        {
            if (_injected) return;
            _injected = true;

            try
            {
                var controls = CStaticDataManager.Instance.GUI.HUD.Info.Controls;
                var listType = controls.GetType();
                var addMethod = listType.GetMethod("Add");

                if (addMethod != null)
                {
                    EInputValueType defaultInput = EInputValueType.Ship_ToggleState;
                    if (controls.Count > 0 && controls[0].Commands.Count > 0)
                    {
                        defaultInput = controls[0].Commands[0].Input;
                    }

                    string[] jsons = new string[]
                    {
                        $@"{{""Text"": ""GUI_HUD_Controls_ShipHeadingNumber"",""Commands"": [{{""Text"": ""GUI_HUD_Controls_ToggleShipHeadingNumber"",""Input"": ""{defaultInput}""}}]}}",
                        $@"{{""Text"": ""GUI_HUD_Controls_VelocityHeadingNumber"",""Commands"": [{{""Text"": ""GUI_HUD_Controls_ToggleVelocityHeadingNumber"",""Input"": ""{defaultInput}""}}]}}",
                        $@"{{""Text"": ""GUI_HUD_Controls_CompassMarks"",""Commands"": [{{""Text"": ""GUI_HUD_Controls_ToggleCompassMarks"",""Input"": ""{defaultInput}""}}]}}"
                    };

                    foreach (var json in jsons)
                    {
                        var control = Newtonsoft.Json.JsonConvert.DeserializeObject<CGUI_HUDDescr.CInfoDescr.CControlDescr>(json);
                        addMethod.Invoke(controls, new object[] { control });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FPS Compass] Failed to inject HUD controls: {ex}");
            }
        }
    }

    [HarmonyPatch]
    public static class CControlView_Update_Patch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.Inner(typeof(CInfo_Controls), "CControlView"), "Update");
        }

        public static void Postfix(object __instance)
        {
            var traverse = Traverse.Create(__instance);
            var descr = traverse.Field<CGUI_HUDDescr.CInfoDescr.CControlDescr>("_descr").Value;
            var stateText = traverse.Field<LocTextMeshPro>("_state_text").Value;

            if (descr == null || stateText == null) return;

            if (descr.Text == "GUI_HUD_Controls_ShipHeadingNumber")
            {
                stateText.SetLocKey(CompassPlugin.ShowShipHeadingNumber.Value ? "GUI_HUD_Controls_StateOn" : "GUI_HUD_Controls_StateOff");
            }
            else if (descr.Text == "GUI_HUD_Controls_VelocityHeadingNumber")
            {
                stateText.SetLocKey(CompassPlugin.ShowVelocityHeadingNumber.Value ? "GUI_HUD_Controls_StateOn" : "GUI_HUD_Controls_StateOff");
            }
            else if (descr.Text == "GUI_HUD_Controls_CompassMarks")
            {
                stateText.SetLocKey(CompassPlugin.ShowIntermediateCompassMarks.Value ? "GUI_HUD_Controls_StateOn" : "GUI_HUD_Controls_StateOff");
            }
        }
    }

    [HarmonyPatch]
    public static class CCommand_Update_Patch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.Inner(AccessTools.Inner(typeof(CInfo_Controls), "CControlView"), "CCommand"), "Update");
        }

        public static bool Prefix(object __instance, float time)
        {
            try
            {
                var traverse = Traverse.Create(__instance);
                var descr = traverse.Field<CGUI_HUDDescr.CInfoDescr.CControlDescr.CCommandDescr>("_descr").Value;
                if (descr == null) return true;

                if (descr.Text == "GUI_HUD_Controls_ToggleShipHeadingNumber" ||
                    descr.Text == "GUI_HUD_Controls_ToggleVelocityHeadingNumber" ||
                    descr.Text == "GUI_HUD_Controls_ToggleCompassMarks")
                {
                    var view = traverse.Field("_view").GetValue();
                    var owner = traverse.Field("_owner").GetValue();
                    
                    var isSelectedMethod = owner.GetType().GetMethod("IsSelected", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    bool isSelected = isSelectedMethod != null ? (bool)isSelectedMethod.Invoke(owner, null) : false;
                    
                    var viewType = view.GetType();
                    viewType.GetMethod("SetVisible", new Type[] { typeof(bool) }).Invoke(view, new object[] { true });
                    viewType.GetMethod("SetActive", new Type[] { typeof(bool) }).Invoke(view, new object[] { isSelected });
                    
                    if (isSelected)
                    {
                        viewType.GetMethod("SetLocKeyInput", new Type[] { typeof(string), typeof(EInputValueType) }).Invoke(view, new object[] { descr.Text, descr.Input });
                    }
                    else
                    {
                        viewType.GetMethod("SetLocKey", new Type[] { typeof(string) }).Invoke(view, new object[] { descr.Text });
                    }
                    
                    return false; // Skip original
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FPS Compass] Error in CCommand_Update_Patch: {ex}");
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class CCommand_OnClick_Patch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.Inner(AccessTools.Inner(typeof(CInfo_Controls), "CControlView"), "CCommand"), "OnClick");
        }

        public static bool Prefix(object __instance)
        {
            var descr = Traverse.Create(__instance).Field<CGUI_HUDDescr.CInfoDescr.CControlDescr.CCommandDescr>("_descr").Value;
            if (descr == null) return true;

            if (descr.Text == "GUI_HUD_Controls_ToggleShipHeadingNumber")
            {
                CompassPlugin.ShowShipHeadingNumber.Value = !CompassPlugin.ShowShipHeadingNumber.Value;
                return false;
            }
            else if (descr.Text == "GUI_HUD_Controls_ToggleVelocityHeadingNumber")
            {
                CompassPlugin.ShowVelocityHeadingNumber.Value = !CompassPlugin.ShowVelocityHeadingNumber.Value;
                return false;
            }
            else if (descr.Text == "GUI_HUD_Controls_ToggleCompassMarks")
            {
                CompassPlugin.ShowIntermediateCompassMarks.Value = !CompassPlugin.ShowIntermediateCompassMarks.Value;
                return false;
            }

            return true;
        }
    }
}
