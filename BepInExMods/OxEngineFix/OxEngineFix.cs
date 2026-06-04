using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace FPS.OxEngineFix
{
    [BepInPlugin("com.fps.mods.oxenginefix", "FPS Ox Engine Fix", "1.0.0")]
    public class OxEngineFixPlugin : BaseUnityPlugin
    {
        public static OxEngineFixPlugin Instance;

        // Front Left Configs for Drag Wings (Slinker)
        public static ConfigEntry<float> OffsetX_LF_Drag;
        public static ConfigEntry<float> OffsetY_LF_Drag;
        public static ConfigEntry<float> OffsetZ_LF_Drag;

        // Front Left Configs for Power Wings (Colossus)
        public static ConfigEntry<float> OffsetX_LF_Power;
        public static ConfigEntry<float> OffsetY_LF_Power;
        public static ConfigEntry<float> OffsetZ_LF_Power;

        // Front Left Configs for Power2 Wings (Titanium M)
        public static ConfigEntry<float> OffsetX_LF_Power2;
        public static ConfigEntry<float> OffsetY_LF_Power2;
        public static ConfigEntry<float> OffsetZ_LF_Power2;

        // Front Left Configs for Base Wings
        public static ConfigEntry<float> OffsetX_LF_Base;
        public static ConfigEntry<float> OffsetY_LF_Base;
        public static ConfigEntry<float> OffsetZ_LF_Base;

        private void Awake()
        {
            try
            {
                Instance = this;

                // Drag Wings LF defaults
                OffsetX_LF_Drag = Config.Bind("Offsets_FrontLeft_Drag", "OffsetX", -0.50f, "Front Left Horizontal offset for Slinker wings");
                OffsetY_LF_Drag = Config.Bind("Offsets_FrontLeft_Drag", "OffsetY", 0.50f, "Front Left Vertical offset for Slinker wings");
                OffsetZ_LF_Drag = Config.Bind("Offsets_FrontLeft_Drag", "OffsetZ", -1.05f, "Front Left Forward/backward offset for Slinker wings");

                // Power Wings LF defaults 
                OffsetX_LF_Power = Config.Bind("Offsets_FrontLeft_Power", "OffsetX", -0.50f, "Front Left Horizontal offset for Colossus wings");
                OffsetY_LF_Power = Config.Bind("Offsets_FrontLeft_Power", "OffsetY", 0.50f, "Front Left Vertical offset for Colossus wings");
                OffsetZ_LF_Power = Config.Bind("Offsets_FrontLeft_Power", "OffsetZ", -0.90f, "Front Left Forward/backward offset for Colossus wings");

                // Power2 Wings LF defaults 
                OffsetX_LF_Power2 = Config.Bind("Offsets_FrontLeft_Power2", "OffsetX", -0.50f, "Front Left Horizontal offset for Titanium M wings");
                OffsetY_LF_Power2 = Config.Bind("Offsets_FrontLeft_Power2", "OffsetY", 0.50f, "Front Left Vertical offset for Titanium M wings");
                OffsetZ_LF_Power2 = Config.Bind("Offsets_FrontLeft_Power2", "OffsetZ", -1.05f, "Front Left Forward/backward offset for Titanium M wings");

                // Base Wings LF defaults
                OffsetX_LF_Base = Config.Bind("Offsets_FrontLeft_Base", "OffsetX", 0.0f, "Front Left Horizontal offset for Base wings");
                OffsetY_LF_Base = Config.Bind("Offsets_FrontLeft_Base", "OffsetY", 0.0f, "Front Left Vertical offset for Base wings");
                OffsetZ_LF_Base = Config.Bind("Offsets_FrontLeft_Base", "OffsetZ", 0.0f, "Front Left Forward/backward offset for Base wings");

                var harmony = new Harmony("com.fps.mods.oxenginefix");
                harmony.PatchAll();
                Logger.LogInfo("Ox Engine Fix initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ox Engine Fix Init failed: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(CShipUpgradeVisualBase), "AttachToHook")]
    public static class CShipUpgradeVisualBase_AttachToHook_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CShipUpgradeVisualBase __instance)
        {
            try
            {
                var traverse = Traverse.Create(__instance);
                var owner = traverse.Field<CShipUpgrade>("_owner").Value;
                if (owner == null) return;

                var ship = owner.Ship;
                if (ship == null) return;

                // Only apply to Ox
                var capsuleNameId = ship.GetSavingCapsuleName();
                if (capsuleNameId.IsEmpty || capsuleNameId.name != "CapsuleOx") return;

                // Check hook name (Only Front Left EngineLFHook is visually misaligned)
                var hookNameId = traverse.Field<NamedId>("_hook_name").Value;
                if (hookNameId.IsEmpty) return;

                string hookName = hookNameId.name;
                if (hookName != "EngineLFHook") return;

                var obj = traverse.Field<GameObject>("_object").Value;
                if (obj == null) return;

                // Start coroutine to wait 1 frame to bypass initialization race conditions
                if (OxEngineFixPlugin.Instance != null)
                {
                    OxEngineFixPlugin.Instance.StartCoroutine(ApplyOffsetDelayed(obj, ship));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OxEngineFix] Error in AttachToHook Postfix: {ex}");
            }
        }

        private static IEnumerator ApplyOffsetDelayed(GameObject obj, IShip ship)
        {
            yield return null; // Wait 1 frame for upgrades lists to fully populate

            if (obj == null || ship == null) yield break;

            try
            {
                // Check if current wings are non-default
                var wingUpgrade = ship.Upgrades?.FindUpgradeBySlot(new NamedId("Wings_Ox"));
                if (wingUpgrade == null) yield break;

                var wingNameId = wingUpgrade.Descr != null ? wingUpgrade.Descr.Name : NamedId.Empty;
                if (wingNameId.IsEmpty || wingNameId.name == "OxWingsStandart") yield break;

                string wingName = wingNameId.name;
                Vector3 correction = Vector3.zero;

                if (wingName == "OxWingsDrag")
                {
                    correction = new Vector3(
                        OxEngineFixPlugin.OffsetX_LF_Drag.Value,
                        OxEngineFixPlugin.OffsetY_LF_Drag.Value,
                        OxEngineFixPlugin.OffsetZ_LF_Drag.Value
                    );
                }
                else if (wingName == "OxWingsPower")
                {
                    correction = new Vector3(
                        OxEngineFixPlugin.OffsetX_LF_Power.Value,
                        OxEngineFixPlugin.OffsetY_LF_Power.Value,
                        OxEngineFixPlugin.OffsetZ_LF_Power.Value
                    );
                }
                else if (wingName == "OxWingsPower2")
                {
                    correction = new Vector3(
                        OxEngineFixPlugin.OffsetX_LF_Power2.Value,
                        OxEngineFixPlugin.OffsetY_LF_Power2.Value,
                        OxEngineFixPlugin.OffsetZ_LF_Power2.Value
                    );
                }
                else if (wingName == "OxWingsBase")
                {
                    correction = new Vector3(
                        OxEngineFixPlugin.OffsetX_LF_Base.Value,
                        OxEngineFixPlugin.OffsetY_LF_Base.Value,
                        OxEngineFixPlugin.OffsetZ_LF_Base.Value
                    );
                }

                obj.transform.localPosition = correction;
                Debug.Log($"[OxEngineFix] Applied misalignment correction {correction} to Front Left engine with wing {wingName} (delayed)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OxEngineFix] Error in ApplyOffsetDelayed: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(GUIWindowScript_Console), "SetActive")]
    public static class GUIWindowScript_Console_SetActive_Patch
    {
        private static bool _commandsRegistered = false;

        [HarmonyPostfix]
        public static void Postfix(GUIWindowScript_Console __instance, bool isActive)
        {
            if (isActive && !_commandsRegistered)
            {
                try
                {
                    __instance.AddCommandHandler(
                        __instance,
                        "SetEngineOffsetLF",
                        new ConsoleArgumentBase[3]
                        {
                            new CAB_Float("X"),
                            new CAB_Float("Y"),
                            new CAB_Float("Z")
                        },
                        OnConsole_SetEngineOffsetLF
                    );

                    __instance.AddCommandHandler(
                        __instance,
                        "CheckEnginePositions",
                        new ConsoleArgumentBase[0],
                        OnConsole_CheckEnginePositions
                    );

                    _commandsRegistered = true;
                    Debug.Log("[OxEngineFix] Live console commands successfully registered dynamically on SetActive!");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[OxEngineFix] Error registering console commands on SetActive: {ex}");
                }
            }
        }

        private static void OnConsole_SetEngineOffsetLF(string[] inArgs, CLogContext inLogContext)
        {
            try
            {
                float x = CGameConsole.GetFloatFromArg(inArgs, 1);
                float y = CGameConsole.GetFloatFromArg(inArgs, 2);
                float z = CGameConsole.GetFloatFromArg(inArgs, 3);

                var playerShip = CAppManager.Instance?.GameManager?.CurrentWorld?.PlayerShip;
                bool applied = false;
                string wingName = "Unknown";

                if (playerShip != null)
                {
                    var capsuleNameId = playerShip.GetSavingCapsuleName();
                    if (!capsuleNameId.IsEmpty && capsuleNameId.name == "CapsuleOx")
                    {
                        var wingUpgrade = playerShip.Upgrades?.FindUpgradeBySlot(new NamedId("Wings_Ox"));
                        if (wingUpgrade != null && wingUpgrade.Descr != null)
                        {
                            wingName = wingUpgrade.Descr.Name.name;
                            if (wingName == "OxWingsDrag")
                            {
                                OxEngineFixPlugin.OffsetX_LF_Drag.Value = x;
                                OxEngineFixPlugin.OffsetY_LF_Drag.Value = y;
                                OxEngineFixPlugin.OffsetZ_LF_Drag.Value = z;
                            }
                            else if (wingName == "OxWingsPower")
                            {
                                OxEngineFixPlugin.OffsetX_LF_Power.Value = x;
                                OxEngineFixPlugin.OffsetY_LF_Power.Value = y;
                                OxEngineFixPlugin.OffsetZ_LF_Power.Value = z;
                            }
                            else if (wingName == "OxWingsPower2")
                            {
                                OxEngineFixPlugin.OffsetX_LF_Power2.Value = x;
                                OxEngineFixPlugin.OffsetY_LF_Power2.Value = y;
                                OxEngineFixPlugin.OffsetZ_LF_Power2.Value = z;
                            }
                            else if (wingName == "OxWingsBase")
                            {
                                OxEngineFixPlugin.OffsetX_LF_Base.Value = x;
                                OxEngineFixPlugin.OffsetY_LF_Base.Value = y;
                                OxEngineFixPlugin.OffsetZ_LF_Base.Value = z;
                            }

                            if (OxEngineFixPlugin.Instance != null)
                            {
                                OxEngineFixPlugin.Instance.Config.Save();
                            }
                        }

                        var lfHook = playerShip.GetHookTransform(new NamedId("EngineLFHook"), inLogContext);
                        if (lfHook != null)
                        {
                            for (int i = 0; i < lfHook.childCount; i++)
                            {
                                lfHook.GetChild(i).localPosition = new Vector3(x, y, z);
                            }
                            applied = true;
                        }
                    }
                }

                string msg = $"Front Left Offset for {wingName} set to X: {x:F3}, Y: {y:F3}, Z: {z:F3} (saved).";
                if (applied) msg += " Front Left engine adjusted instantly!";
                else msg += " Active ship is not Ox; will apply on boarding.";

                CAppManager.Instance.GetConsole().WriteResult(true, msg);
            }
            catch (Exception ex)
            {
                if (CAppManager.Instance?.GetConsole() != null)
                {
                    CAppManager.Instance.GetConsole().WriteResult(false, $"Error: {ex.Message}");
                }
            }
        }

        private static void OnConsole_CheckEnginePositions(string[] inArgs, CLogContext inLogContext)
        {
            try
            {
                var playerShip = CAppManager.Instance?.GameManager?.CurrentWorld?.PlayerShip;
                if (playerShip == null)
                {
                    CAppManager.Instance.GetConsole().WriteResult(false, "Player ship is not active.");
                    return;
                }

                var capsuleNameId = playerShip.GetSavingCapsuleName();
                if (capsuleNameId.IsEmpty || capsuleNameId.name != "CapsuleOx")
                {
                    CAppManager.Instance.GetConsole().WriteResult(false, "Active ship is not Ox.");
                    return;
                }

                var lf = playerShip.GetHookTransform(new NamedId("EngineLFHook"), inLogContext);
                var rf = playerShip.GetHookTransform(new NamedId("EngineRFHook"), inLogContext);

                if (lf == null || rf == null)
                {
                    CAppManager.Instance.GetConsole().WriteResult(false, "Front engine hooks not found.");
                    return;
                }

                var shipMono = playerShip as MonoBehaviour;
                if (shipMono == null)
                {
                    CAppManager.Instance.GetConsole().WriteResult(false, "Failed to cast player ship to MonoBehaviour.");
                    return;
                }

                // Get positions relative to the ship transform
                Vector3 lfPos = shipMono.transform.InverseTransformPoint(lf.position);
                Vector3 rfPos = shipMono.transform.InverseTransformPoint(rf.position);

                var console = CAppManager.Instance.GetConsole();
                console.WriteText($"--- Ox Engine Hook Positions (Local to Ship) ---");
                console.WriteText($"Left Front Hook:  X: {lfPos.x:F4}, Y: {lfPos.y:F4}, Z: {lfPos.z:F4}");
                console.WriteText($"Right Front Hook: X: {rfPos.x:F4}, Y: {rfPos.y:F4}, Z: {rfPos.z:F4}");

                console.WriteText($"--- Symmetry Discrepancies (Left vs Right) ---");
                float diffFrontY = lfPos.y - rfPos.y;
                float diffFrontZ = lfPos.z - rfPos.z;
                float sumFrontX = lfPos.x + rfPos.x;
                console.WriteText($"Front Left shift: Y: {diffFrontY:F4}, Z: {diffFrontZ:F4}, X Sum: {sumFrontX:F4}");

                console.WriteText($"--- Recommended Correction Commands ---");
                console.WriteText($"SetEngineOffsetLF {-sumFrontX:F3} {-diffFrontY:F3} {-diffFrontZ:F3}");

                CAppManager.Instance.GetConsole().WriteResult(true, "Checked engine hook symmetry.");
            }
            catch (Exception ex)
            {
                if (CAppManager.Instance?.GetConsole() != null)
                {
                    CAppManager.Instance.GetConsole().WriteResult(false, $"Error: {ex.Message}");
                }
            }
        }
    }
}
