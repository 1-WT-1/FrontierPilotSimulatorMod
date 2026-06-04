using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;

namespace FPS.Approaches
{
    [BepInPlugin("com.fps.mods.approaches", "FPS Approaches Mod", "1.0.0")]
    public class ApproachesPlugin : BaseUnityPlugin
    {
        public static Dictionary<string, List<Vector3>> Approaches = new Dictionary<string, List<Vector3>>();
        
        private void Awake()
        {
            try
            {
                LoadApproaches();
                var harmony = new Harmony("com.fps.mods.approaches");
                harmony.PatchAll();
                Logger.LogInfo("Approaches Mod initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Approaches Init failed: {ex}");
            }
        }

        public static void LoadApproaches()
        {
            string pluginDir = Paths.PluginPath;
            string jsonPath = Path.Combine(pluginDir, "approaches.json");
            
            if (File.Exists(jsonPath))
            {
                try
                {
                    string json = File.ReadAllText(jsonPath);
                    var rawDict = JsonConvert.DeserializeObject<Dictionary<string, List<Dictionary<string, float>>>>(json);
                    Approaches.Clear();
                    foreach (var kvp in rawDict)
                    {
                        var list = new List<Vector3>();
                        foreach (var wp in kvp.Value)
                        {
                            list.Add(new Vector3(wp["X"], wp["Y"], wp["Z"]));
                        }
                        Approaches[kvp.Key.ToLower()] = list;
                    }
                    Debug.Log($"[FPS Approaches] Loaded {Approaches.Count} approaches from {jsonPath}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FPS Approaches] Failed to parse approaches.json: {ex}");
                }
            }
            else
            {
                Debug.LogWarning($"[FPS Approaches] approaches.json not found at {jsonPath}");
            }
        }
    }

    [HarmonyPatch(typeof(GUIWindowScript_Console), "Init")]
    public static class GUIWindowScript_Console_Init_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GUIWindowScript_Console __instance)
        {
            try
            {
                __instance.AddCommandHandler(
                    __instance,
                    "SetWaypoint",
                    new ConsoleArgumentBase[3]
                    {
                        new CAB_Float("X"),
                        new CAB_Float("Y"),
                        new CAB_Float("Z")
                    },
                    OnConsole_SetWaypoint
                );

                __instance.AddCommandHandler(
                    __instance,
                    "SetApproach",
                    new ConsoleArgumentBase[1]
                    {
                        new CAB_String("ApproachName")
                    },
                    OnConsole_SetApproach
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FPS Approaches] Error registering console commands: {ex}");
            }
        }

        private static void OnConsole_SetWaypoint(string[] inArgs, CLogContext inLogContext)
        {
            try
            {
                float x = CGameConsole.GetFloatFromArg(inArgs, 1);
                float y = CGameConsole.GetFloatFromArg(inArgs, 2);
                float z = CGameConsole.GetFloatFromArg(inArgs, 3);

                var playerShip = CAppManager.Instance?.GameManager?.CurrentWorld?.PlayerShip;
                if (playerShip != null)
                {
                    var pos = new RVector(new Vector3(x, y, z));
                    var targetRef = new STargetRef(pos);
                    playerShip.Navigator.AddRouteTarget(targetRef, CLogContext.Current);

                    CAppManager.Instance.GetConsole().WriteResult(
                        true,
                        $"Waypoint set at X: {x:F1}, Y: {y:F1}, Z: {z:F1}"
                    );
                }
                else
                {
                    CAppManager.Instance.GetConsole().WriteResult(
                        false,
                        "Player ship is not active."
                    );
                }
            }
            catch (Exception ex)
            {
                CAppManager.Instance.GetConsole().WriteResult(
                    false,
                    $"Error: {ex.Message}"
                );
            }
        }

        private static void OnConsole_SetApproach(string[] inArgs, CLogContext inLogContext)
        {
            try
            {
                string approachName = CGameConsole.GetStringFromArg(inArgs, 1).ToLower();
                var playerShip = CAppManager.Instance?.GameManager?.CurrentWorld?.PlayerShip;

                if (playerShip == null)
                {
                    CAppManager.Instance.GetConsole().WriteResult(false, "Player ship is not active.");
                    return;
                }

                if (approachName == "reload")
                {
                    ApproachesPlugin.LoadApproaches();
                    CAppManager.Instance.GetConsole().WriteResult(true, "Reloaded approaches.json");
                    return;
                }

                if (ApproachesPlugin.Approaches.TryGetValue(approachName, out var waypoints))
                {
                    foreach (var wp in waypoints)
                    {
                        var targetRef = new STargetRef(new RVector(wp));
                        playerShip.Navigator.AddRouteTarget(targetRef, CLogContext.Current);
                    }
                    CAppManager.Instance.GetConsole().WriteResult(true, $"Loaded {approachName} approach with {waypoints.Count} waypoints.");
                }
                else
                {
                    CAppManager.Instance.GetConsole().WriteResult(false, $"Unknown approach. Available: {string.Join(", ", ApproachesPlugin.Approaches.Keys.ToArray())} (or 'reload')");
                }
            }
            catch (Exception ex)
            {
                CAppManager.Instance.GetConsole().WriteResult(false, $"Error: {ex.Message}");
            }
        }
    }
}
