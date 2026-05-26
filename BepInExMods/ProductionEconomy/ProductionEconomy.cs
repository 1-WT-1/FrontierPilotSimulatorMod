using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using GameCitySystem;
using GUIManager.Messenger;

namespace FPS.ProductionEconomy
{
    [BepInPlugin("com.fps.mods.productioneconomy", "FPS Production Economy", "1.0.0")]
    public class ProductionEconomyPlugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        public static List<ProductionRecipe> Recipes = new List<ProductionRecipe>();
        private static Dictionary<string, bool> _pausedStates = new Dictionary<string, bool>();

        // Classes for json serialization/deserialization
        public class RecipeRequirement
        {
            public string Product { get; set; }
            public int BoxCount { get; set; }
        }

        public class ProductionRecipe
        {
            public string Product { get; set; }
            public List<RecipeRequirement> Requirements { get; set; }
        }

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo("FPS Production Economy: Awake started.");

            LoadRecipes();

            try
            {
                var harmony = new Harmony("com.fps.mods.productioneconomy");
                harmony.PatchAll();
                Log.LogInfo("FPS Production Economy: Harmony patching succeeded.");
            }
            catch (Exception ex)
            {
                Log.LogError($"FPS Production Economy: Harmony patching failed: {ex}");
            }
        }

        private void LoadRecipes()
        {
            try
            {
                string assemblyDir = Path.GetDirectoryName(typeof(ProductionEconomyPlugin).Assembly.Location);
                string configPath = Path.Combine(assemblyDir, "recipes.json");

                Log.LogInfo($"Loading recipes from: {configPath}");

                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    Recipes = JsonConvert.DeserializeObject<List<ProductionRecipe>>(json);
                    Log.LogInfo($"Successfully loaded {Recipes.Count} production recipes.");
                }
                else
                {
                    Log.LogWarning("recipes.json not found. Creating default configuration.");
                    // Create default recipes
                    Recipes = new List<ProductionRecipe>
                    {
                        new ProductionRecipe
                        {
                            Product = "product_components_food",
                            Requirements = new List<RecipeRequirement>
                            {
                                new RecipeRequirement { Product = "product_components_water", BoxCount = 1 }
                            }
                        },
                        new ProductionRecipe
                        {
                            Product = "product_components_battery",
                            Requirements = new List<RecipeRequirement>
                            {
                                new RecipeRequirement { Product = "product_empty_battery", BoxCount = 1 }
                            }
                        }
                    };

                    string json = JsonConvert.SerializeObject(Recipes, Formatting.Indented);
                    File.WriteAllText(configPath, json);
                    Log.LogInfo("Default recipes.json created successfully.");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Error loading recipes: {ex}");
            }
        }

        public static ProductionRecipe GetRecipeForProduct(string prodName)
        {
            if (Recipes == null) return null;
            return Recipes.Find(r => r.Product == prodName);
        }

        // ========================================================
        // Harmony Patch 1: Pause/Consume ingredients in refinery cycle
        // ========================================================
        [HarmonyPatch(typeof(CSellingProduct), "Update")]
        public class CSellingProduct_Update_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(CSellingProduct __instance, ref float inTime, CLogContext inLogContext)
            {
                try
                {
                    if (__instance == null || __instance.ProductDescr == null) return true;

                    string prodName = __instance.ProductDescr.Name.name;
                    ProductionRecipe recipe = GetRecipeForProduct(prodName);
                    if (recipe == null) return true; // No recipe, default game behavior

                    CCity city = (CCity)AccessTools.Field(typeof(CSellingProduct), "_owner").GetValue(__instance);
                    if (city == null) return true;

                    // Verify ingredients availability in the base warehouse (BuyingProducts volume)
                    bool hasIngredients = true;
                    RecipeRequirement missingReq = null;
                    CProductDescr missingIngredientDescr = null;

                    foreach (var req in recipe.Requirements)
                    {
                        CProductDescr ingredientDescr = CStaticDataManager.Instance.Products.GetDescr(NamedId.GetNamedId(req.Product));
                        if (ingredientDescr == null) continue;

                        float requiredMass = req.BoxCount * ingredientDescr.Mass;
                        float currentStock = 0f;
                        bool found = false;

                        for (int i = 0; i < city.GetProductsBuyingCount(); i++)
                        {
                            CBuyingProduct buyingProduct = city.GetProductsBuyingByIndex(i);
                            if (buyingProduct.Name.name == req.Product && buyingProduct.IsAvailable())
                            {
                                currentStock = (float)AccessTools.Field(typeof(CBuyingProduct), "_volume").GetValue(buyingProduct);
                                found = true;
                                break;
                            }
                        }

                        if (!found || currentStock < requiredMass)
                        {
                            hasIngredients = false;
                            missingReq = req;
                            missingIngredientDescr = ingredientDescr;
                            break;
                        }
                    }

                    string stateKey = $"{city.Name.name}_{prodName}";

                    if (!hasIngredients)
                    {
                        // Pause the production timer
                        inTime = 0f;

                        // Trigger HUD notification once
                        if (!_pausedStates.ContainsKey(stateKey) || !_pausedStates[stateKey])
                        {
                            _pausedStates[stateKey] = true;
                            string localCity = CLocalization.Instance.GetString(city.Name.name, inLogContext);
                            string localProd = CLocalization.Instance.GetString(__instance.ProductDescr.Name.name, inLogContext);
                            string localIngredient = CLocalization.Instance.GetString(missingIngredientDescr.Name.name, inLogContext);
                            
                            string msgText = $"{localCity}: Refining of {localProd} paused. Requires {missingReq.BoxCount} box(es) of {localIngredient}.";
                            CAppManager.Instance.GUIManager.Messages.Add(new CMessage(NamedId.GetNamedId(msgText), inLogContext));
                            Log.LogInfo($"Paused production of {prodName} at {city.Name.name} (missing {missingReq.Product}).");
                        }

                        return true; // Still call original update so dynamic checks run, but timer won't advance
                    }

                    // Reset paused state and notify if it was previously paused
                    if (_pausedStates.ContainsKey(stateKey) && _pausedStates[stateKey])
                    {
                        _pausedStates[stateKey] = false;
                        string localCity = CLocalization.Instance.GetString(city.Name.name, inLogContext);
                        string localProd = CLocalization.Instance.GetString(__instance.ProductDescr.Name.name, inLogContext);
                        
                        string msgText = $"{localCity}: Refining of {localProd} resumed.";
                        CAppManager.Instance.GUIManager.Messages.Add(new CMessage(NamedId.GetNamedId(msgText), inLogContext));
                        Log.LogInfo($"Resumed production of {prodName} at {city.Name.name}.");
                    }

                    // Check if the timer cycle is about to complete and produce a box
                    var descr = (CSellingProductDescr)AccessTools.Field(typeof(CSellingProduct), "_descr").GetValue(__instance);
                    CTimeCounter timer = (CTimeCounter)AccessTools.Field(typeof(CSellingProduct), "_timer").GetValue(__instance);

                    if (timer == null || descr == null) return true;

                    float passedTime = (float)AccessTools.Field(typeof(CTimeCounter), "_times").GetValue(timer);
                    bool willFinish = __instance.IsCanBeAvailable() &&
                                     __instance.Count < descr.MaxCount &&
                                     (passedTime + inTime >= descr.ProductionTime) &&
                                     BuildSettings.IsCreateCargoBoxEnable;

                    if (willFinish)
                    {
                        // Consume requirements from city's BuyingProducts volume
                        foreach (var req in recipe.Requirements)
                        {
                            CProductDescr ingredientDescr = CStaticDataManager.Instance.Products.GetDescr(NamedId.GetNamedId(req.Product));
                            if (ingredientDescr == null) continue;

                            float requiredMass = req.BoxCount * ingredientDescr.Mass;

                            for (int i = 0; i < city.GetProductsBuyingCount(); i++)
                            {
                                CBuyingProduct buyingProduct = city.GetProductsBuyingByIndex(i);
                                if (buyingProduct.Name.name == req.Product)
                                {
                                    float currentStock = (float)AccessTools.Field(typeof(CBuyingProduct), "_volume").GetValue(buyingProduct);
                                    float newStock = Math.Max(0f, currentStock - requiredMass);
                                    AccessTools.Field(typeof(CBuyingProduct), "_volume").SetValue(buyingProduct, newStock);
                                    break;
                                }
                            }
                        }

                        // Send success HUD notification
                        string localCity = CLocalization.Instance.GetString(city.Name.name, inLogContext);
                        string localProd = CLocalization.Instance.GetString(__instance.ProductDescr.Name.name, inLogContext);
                        string msgText = $"{localCity}: 1 crate of {localProd} has been successfully refined.";
                        
                        CAppManager.Instance.GUIManager.Messages.Add(new CMessage(NamedId.GetNamedId(msgText), inLogContext));
                        Log.LogInfo($"Refined 1 crate of {prodName} at {city.Name.name} and consumed required materials.");
                    }
                }
                catch (Exception ex)
                {
                    Log.LogError($"Error in CSellingProduct.Update patch prefix: {ex}");
                }
                return true;
            }
        }

        // ========================================================
        // Harmony Patch 2: Notify player upon raw material delivery
        // ========================================================
        [HarmonyPatch(typeof(CBuyingProduct), "Add")]
        public class CBuyingProduct_Add_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(CBuyingProduct __instance, CProduct inProduct, CLogContext inLogContext)
            {
                try
                {
                    if (__instance == null || inProduct == null || inProduct.Descr == null) return;

                    CCity city = (CCity)AccessTools.Field(typeof(CBuyingProduct), "_owner").GetValue(__instance);
                    if (city == null) return;

                    string localCity = CLocalization.Instance.GetString(city.Name.name, inLogContext);
                    string localProd = CLocalization.Instance.GetString(inProduct.Descr.Name.name, inLogContext);

                    string msgText = $"Delivered 1 crate of {localProd} to {localCity}.";
                    CAppManager.Instance.GUIManager.Messages.Add(new CMessage(NamedId.GetNamedId(msgText), inLogContext));
                    Log.LogInfo($"[HUD Msg] Delivered 1 crate of {localProd} to {localCity}.");
                }
                catch (Exception ex)
                {
                    Log.LogError($"Error in CBuyingProduct.Add patch postfix: {ex}");
                }
            }
        }

        public static string AppendRecipeToDescription(string key, string baseResult)
        {
            try
            {
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(baseResult)) return baseResult;

                // 1. Check if this key corresponds to any of our recipe products' descriptions
                foreach (var recipe in Recipes)
                {
                    CProductDescr productDescr = CStaticDataManager.Instance.Products.GetDescr(NamedId.GetNamedId(recipe.Product));
                    if (productDescr != null && productDescr.Description == key)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine(baseResult);
                        sb.AppendLine();
                        sb.AppendLine("<b><color=#FFD700>[Refinery Recipe]</color></b>");
                        sb.Append("Requires: ");

                        for (int i = 0; i < recipe.Requirements.Count; i++)
                        {
                            var req = recipe.Requirements[i];
                            CProductDescr reqProd = CStaticDataManager.Instance.Products.GetDescr(NamedId.GetNamedId(req.Product));
                            string reqName = reqProd != null ? CLocalization.Instance.GetString(reqProd.Name.name, null) : req.Product;
                            
                            sb.Append($"{req.BoxCount}x {reqName}");
                            if (i < recipe.Requirements.Count - 1)
                            {
                                sb.Append(", ");
                            }
                        }
                        sb.Append(" per crate.");
                        return sb.ToString();
                    }
                }

                // 2. Check if this key corresponds to any ingredient description
                // Find all recipes that use this product as an ingredient
                List<string> usedInRecipes = new List<string>();
                foreach (var recipe in Recipes)
                {
                    foreach (var req in recipe.Requirements)
                    {
                        CProductDescr descr = CStaticDataManager.Instance.Products.GetDescr(NamedId.GetNamedId(req.Product));
                        if (descr != null && descr.Description == key)
                        {
                            if (!usedInRecipes.Contains(recipe.Product))
                            {
                                usedInRecipes.Add(recipe.Product);
                            }
                        }
                    }
                }

                if (usedInRecipes.Count > 0)
                {
                    var world = CAppManager.Instance?.GameManager?.CurrentWorld;
                    if (world != null)
                    {
                        var citiesFilter = world.GetEntitiesByType(EEntityTypes.City);
                        var cities = citiesFilter.GetEntities(null);
                        if (cities != null)
                        {
                            Dictionary<string, List<string>> refinedAtBases = new Dictionary<string, List<string>>();

                            foreach (var entity in cities)
                            {
                                CCity city = entity as CCity;
                                if (city == null) continue;

                                for (int i = 0; i < city.GetProductsSellingCount(); i++)
                                {
                                    CSellingProduct sellingProduct = city.GetProductSellingByIndex(i);
                                    if (sellingProduct == null) continue;

                                    string sellingProdName = sellingProduct.ProductDescr.Name.name;
                                    if (usedInRecipes.Contains(sellingProdName))
                                    {
                                        if (!refinedAtBases.ContainsKey(sellingProdName))
                                        {
                                            refinedAtBases[sellingProdName] = new List<string>();
                                        }
                                        string localCity = CLocalization.Instance.GetString(city.Name.name, null);
                                        if (!refinedAtBases[sellingProdName].Contains(localCity))
                                        {
                                            refinedAtBases[sellingProdName].Add(localCity);
                                        }
                                    }
                                }
                            }

                            if (refinedAtBases.Count > 0)
                            {
                                StringBuilder sb = new StringBuilder();
                                sb.AppendLine(baseResult);
                                sb.AppendLine();
                                sb.AppendLine("<b><color=#00FF7F>[Refinery Industrial Ingredient]</color></b>");
                                foreach (var pair in refinedAtBases)
                                {
                                    CProductDescr refinedDescr = CStaticDataManager.Instance.Products.GetDescr(NamedId.GetNamedId(pair.Key));
                                    string refinedName = refinedDescr != null ? CLocalization.Instance.GetString(refinedDescr.Name.name, null) : pair.Key;
                                    sb.AppendLine($"Used to refine <b>{refinedName}</b> at: {string.Join(", ", pair.Value)}");
                                }
                                return sb.ToString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Error in AppendRecipeToDescription: {ex}");
            }
            return baseResult;
        }

        // ========================================================
        // Harmony Patch 3: GetString (No params overload)
        // ========================================================
        [HarmonyPatch]
        public static class CLocalization_GetString_Patch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod()
            {
                return typeof(CLocalization).GetMethod("GetString", new Type[] {
                    typeof(string),
                    typeof(CLogContext),
                    typeof(bool).MakeByRefType(),
                    typeof(bool)
                });
            }
            [HarmonyPostfix]
            public static void Postfix(string key, ref string __result)
            {
                __result = AppendRecipeToDescription(key, __result);
            }
        }

        // ========================================================
        // Harmony Patch 4: GetString (With ILocParamContainer overload)
        // ========================================================
        [HarmonyPatch]
        public static class CLocalization_GetStringWithParams_Patch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod()
            {
                return typeof(CLocalization).GetMethod("GetString", new Type[] {
                    typeof(string),
                    typeof(ILocParamContainer),
                    typeof(CLogContext),
                    typeof(bool)
                });
            }
            [HarmonyPostfix]
            public static void Postfix(string key, ref string __result)
            {
                __result = AppendRecipeToDescription(key, __result);
            }
        }
    }
}
