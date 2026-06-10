using System;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MainMenu.Subs;
using Rewired;
using CGUI;
using TMPro;
using FreeFSM;

namespace ModSettingsCore
{
    [BepInPlugin("com.fps.mods.modsettingscore", "ModSettingsCore", "1.0.0")]
    public class ModSettingsCorePlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            var harmony = new Harmony("com.fps.mods.modsettingscore");
            harmony.PatchAll(typeof(SettingsMenuPatches));
            Logger.LogInfo("ModSettingsCore initialized.");
        }
    }

    public static class ModSettingsAPI
    {
        public const EGUI_SettingsMenuOptionGroup ModSettingsGroup = (EGUI_SettingsMenuOptionGroup)100;
        public const EGUI_SettingsMenuOptionType KeybindType = (EGUI_SettingsMenuOptionType)100;

        // Add a header option
        public static void AddHeader(string locKey)
        {
            var descr = new CGUI_SettingsMenuOptionDescr(
                ModSettingsGroup,
                EGUI_SettingsMenuOptionType.Header,
                locKey,
                true,
                null
            );
            AddOptionToStaticData(descr);
        }

        // Add a toggle option
        public static void AddToggle(string locKey, Func<bool> getter, Action<bool> setter)
        {
            var values = new string[] { "GUI_Mods_Toggle_Off", "GUI_Mods_Toggle_On" };
            var descr = new CGUI_SettingsMenuOptionDescr(
                ModSettingsGroup,
                EGUI_SettingsMenuOptionType.Listbox,
                locKey,
                values,
                true,
                (ctx) => getter() ? values[1] : values[0],
                (val, ctx) => { setter(val == values[1]); return val; },
                null
            );
            AddOptionToStaticData(descr);
        }

        // Add a slider option
        public static void AddSlider(string locKey, float minValue, float maxValue, float stepSize, Func<float> getter, Action<float> setter)
        {
            var descr = new CGUI_SettingsMenuOptionDescr(
                ModSettingsGroup,
                EGUI_SettingsMenuOptionType.Slider,
                locKey,
                (ctx) => getter(),
                (val, ctx) => { setter(val); return val; },
                null
            );
            SliderConfigs[descr] = (minValue, maxValue, stepSize);
            AddOptionToStaticData(descr);
        }

        // Add a keybind option (stores Controller element or KeyCode as string)
        public static void AddKeybind(string locKey, ConfigEntry<string> configEntry)
        {
            var descr = new CGUI_SettingsMenuOptionDescr(
                ModSettingsGroup,
                KeybindType,
                locKey,
                true,
                null
            );

            // Store ConfigEntry mapping for retrieval without passing custom objects
            KeybindConfigs[descr] = configEntry;

            AddOptionToStaticData(descr);
        }

        private static void AddOptionToStaticData(CGUI_SettingsMenuOptionDescr descr)
        {
            try
            {
                var list = typeof(CStaticDataManager).GetField("_settingsMenuOptions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(CStaticDataManager.Instance) as IList;
                list.Add(descr);
            }
            catch (Exception e)
            {
                Debug.LogError("ModSettingsCore: Failed to add option to CStaticDataManager: " + e);
            }
        }

        internal static Dictionary<CGUI_SettingsMenuOptionDescr, ConfigEntry<string>> KeybindConfigs = new Dictionary<CGUI_SettingsMenuOptionDescr, ConfigEntry<string>>();
        internal static Dictionary<CGUI_SettingsMenuOptionDescr, (float min, float max, float stepSize)> SliderConfigs = new Dictionary<CGUI_SettingsMenuOptionDescr, (float min, float max, float stepSize)>();

        internal static bool IsDrawingSlider = false;

        // Helper to check if a keybind is pressed this frame
        public static bool GetKeyDown(ConfigEntry<string> configEntry)
        {
            if (configEntry == null || string.IsNullOrEmpty(configEntry.Value) || configEntry.Value == "None") return false;

            string[] bindings = configEntry.Value.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var bind in bindings)
            {
                if (CheckSingleBinding(bind.Trim())) return true;
            }
            return false;
        }

        private static bool CheckSingleBinding(string binding)
        {
            string[] parts = binding.Split('+');
            string mainKey = parts[0];
            
            string[] kParts = mainKey.Split(':');
            if (kParts.Length != 3) return false;

            bool isMainDown = false;
            if (Enum.TryParse<ControllerType>(kParts[0], out var cType) && int.TryParse(kParts[1], out var cId) && int.TryParse(kParts[2], out var eId))
            {
                if (cType == ControllerType.Keyboard)
                {
                    isMainDown = UnityEngine.Input.GetKeyDown((KeyCode)eId);
                }
                else
                {
                    var player = ReInput.players.GetPlayer(0);
                    if (player != null)
                    {
                        var controller = player.controllers.GetController(cType, cId);
                        if (controller != null) isMainDown = controller.GetButtonDownById(eId);
                    }
                }
            }

            if (!isMainDown) return false;

            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].StartsWith("Mod:"))
                {
                    if (Enum.TryParse<KeyCode>(parts[i].Substring(4), out var modCode))
                    {
                        if (!UnityEngine.Input.GetKey(modCode)) return false;
                    }
                }
            }
            return true;
        }
    }

    // Keybind UI Component
    public class CKeybindOptionView : CBaseOptionView
    {
        private CGUI_SettingsMenuOptionDescr _descr;
        private ConfigEntry<string> _configEntry;
        private LocTextMeshPro _labelText;
        private RectTransform _valuesContainer;
        private RectTransform _valueTemplate;

        private GameObject _secondRowObj;
        
        private CValueView _valueView;
        private CValueView _addView;
        private CValueView _clearView;

        private class CValueView : CGUIElement
        {
            private MenuButton _button;
            private LocTextMeshPro _buttonText;
            private RectTransform _select;
            private CKeybindOptionView _parent;
            
            public enum ButtonType { Main, Add, Clear }
            private ButtonType _type;

            public MenuButton MenuButton => _button;

            public CValueView(CKeybindOptionView parent, RectTransform inContainer, RectTransform inTemplate, CLogContext inLogContext, ButtonType type)
                : base(inContainer, inTemplate)
            {
                _parent = parent;
                _type = type;
                _button = base.Element.GetComponent<MenuButton>();
                _button.OnClickEvent += OnClickEvent;
                _buttonText = base.Element.FindChildByName<LocTextMeshPro>("$Text");
                _select = base.Element.FindChildByName<RectTransform>("$Select");
                _select.gameObject.SetActiveCheck(value: false);
            }

            private void OnClickEvent(object sender, SelectableEventArgs e)
            {
                if (_type == ButtonType.Clear)
                    _parent.ClearBinding();
                else
                    _parent.StartKeybindPolling(_type == ButtonType.Add);
            }

            public void SetText(string text, bool isLoc)
            {
                if (isLoc)
                    _buttonText.SetLocKey(text);
                else
                    _buttonText.SetText(text);
                base.Element.gameObject.SetActiveCheck(value: true);
            }

            public void Select()
            {
                _select.gameObject.SetActiveCheck(value: true);
            }

            public void Deselect()
            {
                _select.gameObject.SetActiveCheck(value: false);
            }
        }

        public override Selectable GetSelectable()
        {
            return _valueView.MenuButton.Button;
        }

        public override bool IsContains(Selectable selectable)
        {
            return _valueView.MenuButton.Button == selectable;
        }

        public CKeybindOptionView(RectTransform inContainer, RectTransform inTemplate, CGUI_SettingsMenuOptionDescr inDescr, ConfigEntry<string> configEntry, CLogContext inLogContext)
            : base(inContainer, inTemplate)
        {
            _descr = inDescr;
            _configEntry = configEntry;
            _labelText = base.Element.FindChildByName<LocTextMeshPro>("$LabelText");
            _valuesContainer = base.Element.FindChildByName<RectTransform>("$ValuesContainer");
            _valueTemplate = base.Element.FindChildByName<RectTransform>("$ValueTemplate");
            _valueTemplate.gameObject.SetActiveCheck(value: false);
            
            _labelText.SetLocKey(_descr.Label);
            _valueView = new CValueView(this, _valuesContainer, _valueTemplate, inLogContext, CValueView.ButtonType.Main);
            
            // Create a second native row for the Add and Clear buttons
            _secondRowObj = UnityEngine.Object.Instantiate(inTemplate.gameObject, inContainer);
            _secondRowObj.name = "Keybind_AddClearRow";
            _secondRowObj.transform.SetSiblingIndex(base.Element.GetSiblingIndex() + 1);
            _secondRowObj.SetActive(true);

            var secondLabel = _secondRowObj.transform.FindChildByName<LocTextMeshPro>("$LabelText");
            if (secondLabel != null) secondLabel.SetText("");

            var secondValues = _secondRowObj.transform.FindChildByName<RectTransform>("$ValuesContainer");
            var secondTemplate = _secondRowObj.transform.FindChildByName<RectTransform>("$ValueTemplate");
            secondTemplate.gameObject.SetActiveCheck(value: false);

            _addView = new CValueView(this, secondValues, secondTemplate, inLogContext, CValueView.ButtonType.Add);
            _addView.SetText("GUI_Mods_AddBinding", true);
            var layoutAdd = _addView.MenuButton.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layoutAdd.minWidth = 120;
            layoutAdd.preferredWidth = 120;
            layoutAdd.flexibleWidth = 0;

            _clearView = new CValueView(this, secondValues, secondTemplate, inLogContext, CValueView.ButtonType.Clear);
            _clearView.SetText("GUI_Mods_ClearBinding", true);
            var layoutClear = _clearView.MenuButton.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layoutClear.minWidth = 120;
            layoutClear.preferredWidth = 120;
            layoutClear.flexibleWidth = 0;

            RedrawValue();
            
            base.Element.gameObject.SetActiveCheck(value: true);
        }

        private bool _isPolling = false;
        private bool _isPollingForAdd = false;
        private PollerHelper _pollerHelper;

        public void StartKeybindPolling(bool isAdd = false)
        {
            if (_isPolling) return;
            _isPolling = true;
            _isPollingForAdd = isAdd;

            if (_pollerHelper == null)
            {
                _pollerHelper = base.Element.gameObject.AddComponent<PollerHelper>();
            }
            _pollerHelper.StartPolling(this);
        }

        public void UpdateToModifierHoldText()
        {
            // Popup handles this internally via reading the held modifier
        }

        public void EndKeybindPolling(ControllerPollingInfo info)
        {
            _isPolling = false;
            if (info.elementType != ControllerElementType.Axis || info.axisPole != Pole.Positive)
            {
                string newVal = "";
                if (info.controllerType == ControllerType.Keyboard)
                {
                    newVal = $"{info.controllerType}:{info.controllerId}:{(int)info.keyboardKey}";
                }
                else
                {
                    newVal = $"{info.controllerType}:{info.controllerId}:{info.elementIdentifierId}";
                }

                if (UnityEngine.Input.GetKey(KeyCode.LeftShift)) newVal += "+Mod:LeftShift";
                else if (UnityEngine.Input.GetKey(KeyCode.RightShift)) newVal += "+Mod:RightShift";

                if (UnityEngine.Input.GetKey(KeyCode.LeftControl)) newVal += "+Mod:LeftControl";
                else if (UnityEngine.Input.GetKey(KeyCode.RightControl)) newVal += "+Mod:RightControl";

                if (UnityEngine.Input.GetKey(KeyCode.LeftAlt)) newVal += "+Mod:LeftAlt";
                else if (UnityEngine.Input.GetKey(KeyCode.RightAlt)) newVal += "+Mod:RightAlt";

                if (_isPollingForAdd && !string.IsNullOrEmpty(_configEntry.Value) && _configEntry.Value != "None")
                    _configEntry.Value = _configEntry.Value + " | " + newVal;
                else
                    _configEntry.Value = newVal;
            }
            RedrawValue();
        }

        public void EndKeybindPollingWithModifier(KeyCode modifierKey)
        {
            _isPolling = false;
            string newVal = $"Keyboard:0:{(int)modifierKey}";
            
            if (_isPollingForAdd && !string.IsNullOrEmpty(_configEntry.Value) && _configEntry.Value != "None")
                _configEntry.Value = _configEntry.Value + " | " + newVal;
            else
                _configEntry.Value = newVal;

            RedrawValue();
        }

        public void CancelPolling()
        {
            _isPolling = false;
            RedrawValue();
        }

        public void ClearBinding()
        {
            _isPolling = false;
            _configEntry.Value = "None";
            RedrawValue();
        }

        private void RedrawValue()
        {
            string val = _configEntry.Value;
            if (string.IsNullOrEmpty(val) || val == "None")
            {
                _valueView.SetText("None", false);
                return;
            }

            string[] bindings = val.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < bindings.Length; i++)
            {
                if (i > 0) sb.Append(" | ");
                string[] parts = bindings[i].Trim().Split('+');
                string mainKey = parts[0];
                string[] kParts = mainKey.Split(':');
                if (kParts.Length == 3 && Enum.TryParse<ControllerType>(kParts[0], out var cType) && int.TryParse(kParts[1], out var cId) && int.TryParse(kParts[2], out var eId))
                {
                    for (int m = 1; m < parts.Length; m++)
                    {
                        if (parts[m].StartsWith("Mod:"))
                        {
                            sb.Append(parts[m].Substring(4).Replace("Left", "").Replace("Right", "")).Append("+");
                        }
                    }

                    if (cType == ControllerType.Keyboard)
                    {
                        sb.Append(((KeyCode)eId).ToString());
                    }
                    else
                    {
                        Controller c = ReInput.controllers.GetController(cType, cId);
                        if (c != null && c.GetElementIdentifierById(eId) != null)
                        {
                            sb.Append($"{c.name} {c.GetElementIdentifierById(eId).name}");
                        }
                        else
                        {
                            sb.Append($"Joy {eId}");
                        }
                    }
                }
                else
                {
                    sb.Append("Unknown");
                }
            }
            _valueView.SetText(sb.ToString(), false);
        }

        public override void Dispose()
        {
            base.Dispose();
            if (_pollerHelper != null) UnityEngine.Object.Destroy(_pollerHelper);
            
            // Clean up the second row
            if (_secondRowObj != null)
            {
                UnityEngine.Object.Destroy(_secondRowObj);
                _secondRowObj = null;
            }
        }
    }

    public class PollerHelper : MonoBehaviour
    {
        private CKeybindOptionView _view;
        private int _frameWait = 0;
        private float _modifierHoldTimer = 0f;
        private KeyCode _currentHeldModifier = KeyCode.None;
        private Texture2D _bgTexture;

        public KeyCode CurrentHeldModifier => _currentHeldModifier;

        public void StartPolling(CKeybindOptionView view)
        {
            _view = view;
            _frameWait = 5;
            _modifierHoldTimer = 0f;
            _currentHeldModifier = KeyCode.None;
        }

        private void OnGUI()
        {
            if (_view == null) return;
            
            if (_bgTexture == null)
            {
                _bgTexture = new Texture2D(1, 1);
                _bgTexture.SetPixel(0, 0, new Color(0, 0, 0, 0.85f));
                _bgTexture.Apply();
            }

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _bgTexture);

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = Screen.height / 20; // responsive font size
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.white;
            style.wordWrap = true;

            string textKey = (_currentHeldModifier != KeyCode.None) ? "GUI_Mods_HoldModifier" : "GUI_Mods_PressAnyKey";
            string text = CLocalization.Instance.GetString(textKey, null);
            
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), text, style);
        }

        void Update()
        {
            if (_view == null) return;

            if (_frameWait > 0)
            {
                _frameWait--;
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                _view.CancelPolling();
                _view = null;
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Delete) || UnityEngine.Input.GetKeyDown(KeyCode.Backspace))
            {
                _view.ClearBinding();
                _view = null;
                return;
            }

            ControllerPollingInfo info = ReInput.controllers.polling.PollAllControllersForFirstElementDown();
            if (info.success)
            {
                if (info.controllerType == ControllerType.Keyboard)
                {
                    KeyCode k = info.keyboardKey;
                    if (k == KeyCode.LeftShift || k == KeyCode.RightShift ||
                        k == KeyCode.LeftControl || k == KeyCode.RightControl ||
                        k == KeyCode.LeftAlt || k == KeyCode.RightAlt)
                    {
                        if (_currentHeldModifier != k)
                        {
                            _currentHeldModifier = k;
                            _modifierHoldTimer = 0f;
                            _view.UpdateToModifierHoldText();
                        }
                        return;
                    }
                }

                _view.EndKeybindPolling(info);
                _view = null;
                return;
            }

            if (_currentHeldModifier != KeyCode.None)
            {
                if (UnityEngine.Input.GetKey(_currentHeldModifier))
                {
                    _modifierHoldTimer += Time.deltaTime;
                    if (_modifierHoldTimer >= 1.5f)
                    {
                        _view.EndKeybindPollingWithModifier(_currentHeldModifier);
                        _view = null;
                    }
                }
                else
                {
                    _currentHeldModifier = KeyCode.None;
                    _modifierHoldTimer = 0f;
                }
            }
        }
    }

    public static class SettingsMenuPatches
    {
        private static MenuButton _modsButton;
        private static CBaseSettingsMenu _modsMenu;

        [HarmonyPatch(typeof(CSettingsMenu), MethodType.Constructor)]
        [HarmonyPatch(new Type[] { typeof(GUIScript_MainMenu), typeof(CLogContext) })]
        [HarmonyPostfix]
        public static void CSettingsMenu_Ctor(CSettingsMenu __instance, GUIScript_MainMenu inOwner, CLogContext inLogContext)
        {
            try
            {
                RectTransform panel = (RectTransform)typeof(CSettingsMenu).GetField("_panel", BindingFlags.Public | BindingFlags.Instance).GetValue(__instance);
                MenuButton otherButton = panel.FindChildByName<MenuButton>("$OtherButton");
                if (otherButton != null)
                {
                    _modsButton = UnityEngine.Object.Instantiate(otherButton, otherButton.transform.parent);
                    _modsButton.name = "$ModsButton";
                    
                    LocTextMeshPro text = _modsButton.GetComponentInChildren<LocTextMeshPro>();
                    if (text != null) text.SetLocKey("GUI_ModsSettingsMenu_Button");
                    
                    _modsButton.OnClickEvent += OnModsButtonClick;

                    // Reorder the button so it sits at the end but before the Back button 
                    _modsButton.transform.SetSiblingIndex(otherButton.transform.GetSiblingIndex() + 1);

                }
            }
            catch (Exception e)
            {
                Debug.LogError("ModSettingsCore: Failed to inject Mods button: " + e);
            }
        }

        private static void OnModsButtonClick(object sender, SelectableEventArgs e)
        {
            // Switch to custom menu state
            if (_modsMenu != null)
            {
                var owner = (GUIScript_MainMenu)typeof(CBaseMenu).GetField("_owner", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_modsMenu);
                var fsm = typeof(GUIScript_MainMenu).GetField("_fsm", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
                fsm.GetType().GetMethod("Switch", new Type[] { typeof(CBaseMenu) }).Invoke(fsm, new object[] { _modsMenu });
            }
        }

        [HarmonyPatch(typeof(CSettingsMenu), "OnEnter")]
        [HarmonyPostfix]
        public static void CSettingsMenu_OnEnter(CSettingsMenu __instance, CBaseMenu inPrevState, CLogContext inLogContext)
        {
            if (inPrevState == _modsMenu && _modsButton != null)
            {
                __instance.Select(_modsButton.Button);
            }
        }

        [HarmonyPatch(typeof(CSettingsMenu), "Dispose")]
        [HarmonyPostfix]
        public static void CSettingsMenu_Dispose(CSettingsMenu __instance, CLogContext inLogContext)
        {
            if (_modsButton != null)
            {
                _modsButton.OnClickEvent -= OnModsButtonClick;
                _modsButton = null;
            }
        }

        [HarmonyPatch(typeof(GUIScript_MainMenu), "Init")]
        [HarmonyPostfix]
        public static void GUIScriptMainMenu_Init(GUIScript_MainMenu __instance, CLogContext inLogContext)
        {
            _modsMenu = new CBaseSettingsMenu(__instance, "$BaseSettingsMenu", "GUI_ModsSettingsMenu_HeaderText", ModSettingsAPI.ModSettingsGroup, inLogContext);
        }

        [HarmonyPatch(typeof(GUIScript_MainMenu), "Dispose")]
        [HarmonyPostfix]
        public static void GUIScriptMainMenu_Dispose(GUIScript_MainMenu __instance, CLogContext inLogContext)
        {
            if (_modsMenu != null)
            {
                _modsMenu.Dispose(inLogContext);
                _modsMenu = null;
            }
        }

        [HarmonyPatch(typeof(CBaseSettingsMenu), "RedrawSettingsList")]
        [HarmonyPrefix]
        public static bool CBaseSettingsMenu_RedrawSettingsList_Prefix(CBaseSettingsMenu __instance, CLogContext inLogContext)
        {
            var panelGroup = (EGUI_SettingsMenuOptionGroup)typeof(CBaseSettingsMenu).GetField("_panelGroup", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);
            var options = typeof(CBaseSettingsMenu).GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance) as IList;
            var optionsContainer = (RectTransform)typeof(CBaseSettingsMenu).GetField("_optionsContainer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);
            
            var headerOptionTemplate = (RectTransform)typeof(CBaseSettingsMenu).GetField("_headerOptionTemplate", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);
            var labelOptionTemplate = (RectTransform)typeof(CBaseSettingsMenu).GetField("_labelOptionTemplate", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);
            var listboxOptionTemplate = (RectTransform)typeof(CBaseSettingsMenu).GetField("_listboxOptionTemplate", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);
            var sliderOptionTemplate = (RectTransform)typeof(CBaseSettingsMenu).GetField("_sliderOptionTemplate", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);

            var sOptionInfoType = typeof(CBaseSettingsMenu).GetNestedType("SOptionInfo", BindingFlags.NonPublic);

            // Call Clear and Dispose Elements
            var disposeElementsMethod = options.GetType().GetMethod("DisposeElements", BindingFlags.Public | BindingFlags.Instance);
            if (disposeElementsMethod != null) disposeElementsMethod.Invoke(options, null);
            options.Clear();

            foreach (CGUI_SettingsMenuOptionDescr descr in CStaticDataManager.Instance.SettingsMenuOptions)
            {
                if (descr.Group == panelGroup)
                {
                    object view = null;
                    if (descr.Type == EGUI_SettingsMenuOptionType.Header)
                    {
                        view = new CHeaderOptionView(optionsContainer, headerOptionTemplate, descr, inLogContext);
                    }
                    else if (descr.Type == EGUI_SettingsMenuOptionType.Label)
                    {
                        view = new CHeaderOptionView(optionsContainer, labelOptionTemplate, descr, inLogContext);
                    }
                    else if (descr.Type == EGUI_SettingsMenuOptionType.Listbox)
                    {
                        view = new CListboxOptionView(optionsContainer, listboxOptionTemplate, descr, inLogContext);
                    }
                    else if (descr.Type == EGUI_SettingsMenuOptionType.Slider)
                    {
                        view = new CSliderOptionView(optionsContainer, sliderOptionTemplate, descr, inLogContext);
                    }
                    else if (descr.Type == EGUI_SettingsMenuOptionType.SliderReadOnly)
                    {
                        view = new CSliderOptionView(optionsContainer, sliderOptionTemplate, descr, inLogContext)
                        {
                            Interactable = false,
                            ShowHandler = false
                        };
                    }
                    else if (descr.Type == ModSettingsAPI.KeybindType)
                    {
                        if (ModSettingsAPI.KeybindConfigs.TryGetValue(descr, out var configEntry))
                        {
                            view = new CKeybindOptionView(optionsContainer, listboxOptionTemplate, descr, configEntry, inLogContext);
                        }
                    }

                    if (view != null)
                    {
                        var sOptionInfo = Activator.CreateInstance(sOptionInfoType, descr, view);
                        options.Add(sOptionInfo);
                    }
                }
            }

            // Call Select next
            var tryGetNextMethod = typeof(CBaseSettingsMenu).GetMethod("TryGetNextSelectable", BindingFlags.NonPublic | BindingFlags.Instance);
            if (tryGetNextMethod != null)
            {
                object[] args = new object[] { -1, null };
                bool result = (bool)tryGetNextMethod.Invoke(__instance, args);
                if (result)
                {
                    var selectMethod = typeof(CBaseSettingsMenu).GetMethod("Select", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(Selectable) }, null);
                    if (selectMethod != null) selectMethod.Invoke(__instance, new object[] { args[1] });
                }
            }

            return false; // Skip original method
        }

        [HarmonyPatch(typeof(CSliderOptionView), "Draw")]
        [HarmonyPrefix]
        public static void CSliderOptionView_Draw_Prefix(CSliderOptionView __instance, CLogContext inLogContext)
        {
            ModSettingsAPI.IsDrawingSlider = true;
            var descr = typeof(CSliderOptionView).GetField("_descr", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance) as CGUI_SettingsMenuOptionDescr;
            if (descr != null && descr.Group == ModSettingsAPI.ModSettingsGroup)
            {
                var slider = typeof(CSliderOptionView).GetField("_slider", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance) as Slider;
                if (slider != null && ModSettingsAPI.SliderConfigs.TryGetValue(descr, out var config))
                {
                    slider.minValue = config.min;
                    slider.maxValue = config.max;
                    slider.wholeNumbers = config.stepSize == 1f;
                }
            }
        }

        [HarmonyPatch(typeof(CSliderOptionView), "OnValueChange")]
        [HarmonyPrefix]
        public static bool CSliderOptionView_OnValueChange_Prefix(CSliderOptionView __instance, float value)
        {
            if (ModSettingsAPI.IsDrawingSlider) return false;
            var descr = typeof(CSliderOptionView).GetField("_descr", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance) as CGUI_SettingsMenuOptionDescr;
            if (descr != null && descr.Group == ModSettingsAPI.ModSettingsGroup)
            {
                if (ModSettingsAPI.SliderConfigs.TryGetValue(descr, out var config) && config.stepSize > 0f && config.stepSize != 1f)
                {
                    float snapped = Mathf.Round(value / config.stepSize) * config.stepSize;
                    if (Mathf.Abs(value - snapped) > 0.0001f)
                    {
                        var slider = typeof(CSliderOptionView).GetField("_slider", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance) as Slider;
                        slider.value = snapped;
                    }
                }
            }
            return true;
        }

        [HarmonyPatch(typeof(CSliderOptionView), "OnValueChange")]
        [HarmonyPostfix]
        public static void CSliderOptionView_OnValueChange_Postfix(CSliderOptionView __instance, float value)
        {
            if (ModSettingsAPI.IsDrawingSlider) return;
            var descr = typeof(CSliderOptionView).GetField("_descr", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance) as CGUI_SettingsMenuOptionDescr;
            if (descr != null && descr.Group == ModSettingsAPI.ModSettingsGroup)
            {
                var labelText = typeof(CSliderOptionView).GetField("_labelText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance) as LocTextMeshPro;
                if (labelText != null)
                {
                    string originalLoc = CLocalization.Instance != null ? CLocalization.Instance.GetString(descr.Label, null, CLogContext.Current) : descr.Label;
                    if (string.IsNullOrEmpty(originalLoc)) originalLoc = descr.Label;
                    
                    bool isWhole = ModSettingsAPI.SliderConfigs.TryGetValue(descr, out var config) && config.stepSize == 1f;
                    labelText.SetEmpty();
                    if (isWhole)
                        labelText.SetText($"{originalLoc}: {value:F0}");
                    else
                        labelText.SetText($"{originalLoc}: {value:F1}");
                }
            }
        }

        [HarmonyPatch(typeof(CSliderOptionView), "Draw")]
        [HarmonyPostfix]
        public static void CSliderOptionView_Draw(CSliderOptionView __instance, CLogContext inLogContext)
        {
            try
            {
                var descr = typeof(CSliderOptionView).GetField("_descr", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance) as CGUI_SettingsMenuOptionDescr;
                if (descr != null && descr.Group == ModSettingsAPI.ModSettingsGroup)
                {
                    var labelText = typeof(CSliderOptionView).GetField("_labelText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance) as LocTextMeshPro;
                    var slider = typeof(CSliderOptionView).GetField("_slider", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance) as Slider;
                    if (labelText != null && slider != null)
                    {
                        // For Mods sliders, append the value to the label
                        string originalLoc = CLocalization.Instance != null ? CLocalization.Instance.GetString(descr.Label, null, inLogContext) : descr.Label;
                        if (string.IsNullOrEmpty(originalLoc)) originalLoc = descr.Label;
                        
                        bool isWhole = ModSettingsAPI.SliderConfigs.TryGetValue(descr, out var config) && config.stepSize == 1f;
                        labelText.SetEmpty();
                        if (isWhole)
                            labelText.SetText($"{originalLoc}: {slider.value:F0}");
                        else
                            labelText.SetText($"{originalLoc}: {slider.value:F1}");
                    }
                }
            }
            finally
            {
                ModSettingsAPI.IsDrawingSlider = false;
            }
        }

        [HarmonyPatch(typeof(CBaseSettingsMenu), "OnInput")]
        [HarmonyPrefix]
        public static void CBaseSettingsMenu_OnInput_Prefix(CBaseSettingsMenu __instance, CInputFireEventArgs args)
        {
            try
            {
                var component = EventSystem.current.currentSelectedGameObject?.GetComponent<Selectable>();
                if (component is Slider slider)
                {
                    if (args.InputValueType == EInputValueType.Menu_Left || args.InputValueType == EInputValueType.Menu_Right)
                    {
                        float step = 0.05f;
                        
                        var options = typeof(CBaseSettingsMenu).GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance) as IList;
                        CGUI_SettingsMenuOptionDescr descr = null;
                        if (options != null)
                        {
                            foreach (var opt in options)
                            {
                                var element = opt.GetType().GetField("Element", BindingFlags.Public | BindingFlags.Instance)?.GetValue(opt) as CBaseOptionView;
                                if (element is CSliderOptionView sov)
                                {
                                    var sovSlider = typeof(CSliderOptionView).GetField("_slider", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sov) as Slider;
                                    if (sovSlider == slider)
                                    {
                                        descr = typeof(CSliderOptionView).GetField("_descr", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sov) as CGUI_SettingsMenuOptionDescr;
                                        break;
                                    }
                                }
                            }
                        }

                        if (descr != null && ModSettingsAPI.SliderConfigs.TryGetValue(descr, out var config) && config.stepSize > 0f)
                        {
                            step = config.stepSize;
                        }
                        else if (slider.wholeNumbers)
                        {
                            step = 1f;
                        }

                        if (step != 0.05f)
                        {
                            if (args.InputValueType == EInputValueType.Menu_Left)
                            {
                                slider.value -= (step - 0.05f);
                            }
                            else if (args.InputValueType == EInputValueType.Menu_Right)
                            {
                                slider.value += (step - 0.05f);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("ModSettingsCore: Error in CBaseSettingsMenu_OnInput_Prefix: " + e);
            }
        }

        [HarmonyPatch(typeof(CSliderOptionView), "OnValueChange")]
        [HarmonyPostfix]
        public static void CSliderOptionView_OnValueChange(CSliderOptionView __instance, float value)
        {
            CSliderOptionView_Draw(__instance, null);
        }
    }
}
