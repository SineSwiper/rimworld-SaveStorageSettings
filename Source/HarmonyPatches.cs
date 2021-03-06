﻿using HarmonyLib;
using RimWorld;
using SaveStorageSettings.Dialog;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using System;

namespace SaveStorageSettings
{
    [StaticConstructorOnStartup]
    class HarmonyPatches
    {
        public static readonly Texture2D DeleteXTexture;
        public static readonly Texture2D SaveTexture;
        public static readonly Texture2D LoadTexture;
        public static readonly Texture2D AppendTexture;

        static HarmonyPatches()
        {
            var harmony = new Harmony("com.savestoragesettings.rimworld.mod");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.Message(
                "SaveStorageSettings: Harmony Patches:" + Environment.NewLine +
                "    Postfix:" + Environment.NewLine +
                "        Building.GetGizmos(IEnumerable<Gizmo>)" + Environment.NewLine +
                "        Zone_Stockpile.GetGizmos(IEnumerable<Gizmo>)" + Environment.NewLine +
                "        Dialog_ManageOutfits.DoWindowContents(Rect)" + Environment.NewLine +
                "        Dialog_ManageDrugPolicies.DoWindowContents(Rect)" + Environment.NewLine +
                "        Building_Storage.GetGizmos");

            DeleteXTexture = ContentFinder<Texture2D>.Get("UI/Buttons/Delete", true);
            SaveTexture = ContentFinder<Texture2D>.Get("UI/save", true);
            LoadTexture = ContentFinder<Texture2D>.Get("UI/load", true);
            AppendTexture = ContentFinder<Texture2D>.Get("UI/append", true);
        }
    }

    [HarmonyPatch(typeof(HealthCardUtility), "DrawHealthSummary")]
    static class Patch_HealthCardUtility_DrawHealthSummary
    {
        public static long LastCallTime = 0;

        [HarmonyPriority(Priority.First)]
        static void Prefix()
        {
            LastCallTime = DateTime.Now.Ticks;
        }
    }

    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    static class Patch_Pawn_GetGizmos
    {
        const long TENTH_SECOND = TimeSpan.TicksPerSecond / 10;
        static FieldInfo OnOperationTab = null;
        static Patch_Pawn_GetGizmos()
        {
            OnOperationTab = typeof(HealthCardUtility).GetField("onOperationTab", BindingFlags.Static | BindingFlags.NonPublic);
        }
        static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (!(bool)OnOperationTab.GetValue(null))
                return;

            if (!__instance.IsColonist && !__instance.IsPrisoner)
                return;
            
            if (DateTime.Now.Ticks - Patch_HealthCardUtility_DrawHealthSummary.LastCallTime < TENTH_SECOND)
            {
                string type = "OperationHuman";
                if (__instance.RaceProps.Animal)
                    type = "OperationAnimal";

                List<Gizmo> gizmos = new List<Gizmo>(__result)
                {
                    new Command_Action
                    {
                        icon = HarmonyPatches.SaveTexture,
                        defaultLabel = "SaveStorageSettings.SaveOperations".Translate(),
                        defaultDesc = "SaveStorageSettings.SaveOperations".Translate(),
                        activateSound = SoundDef.Named("Click"),
                        action = delegate {
                            Find.WindowStack.Add(new SaveOperationDialog(type, __instance));
                        },
                        groupKey = 987764552
                    },

                    new Command_Action
                    {
                        icon = HarmonyPatches.AppendTexture,
                        defaultLabel = "SaveStorageSettings.LoadOperations".Translate(),
                        defaultDesc = "SaveStorageSettings.LoadOperations".Translate(),
                        activateSound = SoundDef.Named("Click"),
                        action = delegate {
                            Find.WindowStack.Add(new LoadOperationDialog(__instance, type));
                        },
                        groupKey = 987764553
                    },
                };

                __result = gizmos;
            }
        }
    }

    [HarmonyPatch(typeof(Building), "GetGizmos")]
    static class Patch_Building_GetGizmos
    {
        static void Postfix(Building __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance.def.IsWorkTable)
            {
                string type = GetType(__instance.def.defName);
                if (type == null)
                    return;

                List<Gizmo> gizmos = new List<Gizmo>(__result)
                {
                    new Command_Action
                    {
                        icon = HarmonyPatches.SaveTexture,
                        defaultLabel = "SaveStorageSettings.SaveBills".Translate(),
                        defaultDesc = "SaveStorageSettings.SaveBillsDesc".Translate(),
                        activateSound = SoundDef.Named("Click"),
                        action = delegate {
                            Find.WindowStack.Add(new SaveCraftingDialog(type, ((Building_WorkTable)__instance).billStack));
                        },
                        groupKey = 987767552
                    },

                    new Command_Action
                    {
                        icon = HarmonyPatches.AppendTexture,
                        defaultLabel = "SaveStorageSettings.AppendBills".Translate(),
                        defaultDesc = "SaveStorageSettings.AppendBillsDesc".Translate(),
                        activateSound = SoundDef.Named("Click"),
                        action = delegate {
                            Find.WindowStack.Add(new LoadCraftingDialog(type, ((Building_WorkTable)__instance).billStack, LoadCraftingDialog.LoadType.Append));
                        },
                        groupKey = 987767553
                    },

                    new Command_Action
                    {
                        icon = HarmonyPatches.LoadTexture,
                        defaultLabel = "SaveStorageSettings.LoadBills".Translate(),
                        defaultDesc = "SaveStorageSettings.LoadBillsDesc".Translate(),
                        activateSound = SoundDef.Named("Click"),
                        action = delegate {
                            Find.WindowStack.Add(new LoadCraftingDialog(type, ((Building_WorkTable)__instance).billStack, LoadCraftingDialog.LoadType.Replace));
                        },
                        groupKey = 987767554
                    }
                };

                __result = gizmos;
            }
        }

        private static string GetType(string defName)
        {
            switch(defName)
            {
                case "ButcherSpot":
                case "TableButcher":
                    return "Butcher";
                case "HandTailoringBench":
                case "ElectricTailoringBench":
                    return "TailoringBench";
                case "FueledSmithy":
                case "ElectricSmithy":
                    return "Smithy";
                case "FueledStove":
                case "ElectricStove":
                    return "Stove";
                case "SimpleResearchBench":
                case "HiTechResearchBench":
                    return null;
            }
            return defName;
        }
    }

    [HarmonyPatch(typeof(Building_Storage), "GetGizmos")]
    static class Patch_BuildingStorage_GetGizmos
    {
        static void Postfix(Building __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance.def.defName.Equals("Shelf"))
            {
                List<Gizmo> gizmos = new List<Gizmo>(__result)
                    {
                        new Command_Action
                        {
                            icon = HarmonyPatches.SaveTexture,
                            defaultLabel = "SaveStorageSettings.SaveZoneSettings".Translate(),
                            defaultDesc = "SaveStorageSettings.SaveZoneSettingsDesc".Translate(),
                            activateSound = SoundDef.Named("Click"),
                            action = delegate {
                                Find.WindowStack.Add(new SaveFilterDialog("shelf", ((Building_Storage)__instance).settings.filter));
                            },
                            groupKey = 987767552
                        },

                        new Command_Action
                        {
                            icon = HarmonyPatches.LoadTexture,
                            defaultLabel = "SaveStorageSettings.LoadZoneSettings".Translate(),
                            defaultDesc = "SaveStorageSettings.LoadZoneSettingsDesc".Translate(),
                            activateSound = SoundDef.Named("Click"),
                            action = delegate {
                                Find.WindowStack.Add(new LoadFilterDialog("shelf", ((Building_Storage)__instance).settings.filter));
                            },
                            groupKey = 987767553
                        }
                    };

                __result = gizmos;
            }
        }
    }

    [HarmonyPatch(typeof(Zone_Stockpile), "GetGizmos")]
    static class Patch_Zone_Stockpile_GetGizmos
    {
        static void Postfix(Zone_Stockpile __instance, ref IEnumerable<Gizmo> __result)
        {
            __result = GizmoUtil.AddSaveLoadGizmos(__result, "Zone_Stockpile", __instance.settings.filter);
        }
    }

    [HarmonyPatch(typeof(Dialog_ManageOutfits), "DoWindowContents")]
    static class Patch_Dialog_ManageOutfits_DoWindowContents
    {
        static void Postfix(Dialog_ManageOutfits __instance, Rect inRect)
        {
            if (Widgets.ButtonText(new Rect(480f, 0f, 150f, 35f), "SaveStorageSettings.LoadAsNew".Translate(), true, false, true))
            {
                Outfit outfit = Current.Game.outfitDatabase.MakeNewOutfit();
                SetSelectedOutfit(__instance, outfit);
                
                Find.WindowStack.Add(new LoadFilterDialog("Apparel_Management", outfit.filter));
            }

            Outfit selectedOutfit = GetSelectedOutfit(__instance);
            if (selectedOutfit != null)
            {
                Text.Font = GameFont.Small;
                GUI.BeginGroup(new Rect(220f, 49f, 300, 32f));
                if (Widgets.ButtonText(new Rect(0f, 0f, 150f, 32f), "SaveStorageSettings.LoadOutfit".Translate(), true, false, true))
                {
                    Find.WindowStack.Add(new LoadFilterDialog("Apparel_Management", selectedOutfit.filter));
                }
                if (Widgets.ButtonText(new Rect(160f, 0f, 150f, 32f), "SaveStorageSettings.SaveOutfit".Translate(), true, false, true))
                {
                    Find.WindowStack.Add(new SaveFilterDialog("Apparel_Management", selectedOutfit.filter));
                }
                GUI.EndGroup();
            }
        }

        private static Outfit GetSelectedOutfit(Dialog_ManageOutfits dialog)
        {
            return (Outfit)typeof(Dialog_ManageOutfits).GetProperty("SelectedOutfit", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetProperty).GetValue(dialog, null);
        }

        private static void SetSelectedOutfit(Dialog_ManageOutfits dialog, Outfit selectedOutfit)
        {
            typeof(Dialog_ManageOutfits).GetProperty("SelectedOutfit", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetProperty).SetValue(dialog, selectedOutfit, null);
        }
    }

    [HarmonyPatch(typeof(Dialog_ManageDrugPolicies), "DoWindowContents")]
    static class Patch_Dialog_Dialog_ManageDrugPolicies
    {
        static void Postfix(Dialog_ManageDrugPolicies __instance, Rect inRect)
        {
            float x = 500;
            if (Widgets.ButtonText(new Rect(x, 0, 150f, 35f), "SaveStorageSettings.LoadAsNew".Translate(), true, false, true))
            {
                DrugPolicy policy = Current.Game.drugPolicyDatabase.MakeNewDrugPolicy();
                SetDrugPolicy(__instance, policy);

                Find.WindowStack.Add(new LoadPolicyDialog("DrugPolicy", policy));
            }
            x += 160;

            DrugPolicy selectedPolicy = GetDrugPolicy(__instance);
            if (selectedPolicy != null)
            {
                Text.Font = GameFont.Small;
                if (Widgets.ButtonText(new Rect(x, 0f, 75, 35f), "LoadGameButton".Translate(), true, false, true))
                {
                    string label = selectedPolicy.label;
                    Find.WindowStack.Add(new LoadPolicyDialog("DrugPolicy", selectedPolicy));
                    selectedPolicy.label = label;
                }
                x += 80;
                if (Widgets.ButtonText(new Rect(x, 0f, 75, 35f), "SaveGameButton".Translate(), true, false, true))
                {
                    Find.WindowStack.Add(new SavePolicyDialog("DrugPolicy", selectedPolicy));
                }
            }
        }

        private static DrugPolicy GetDrugPolicy(Dialog_ManageDrugPolicies dialog)
        {
            return (DrugPolicy)typeof(Dialog_ManageDrugPolicies).GetProperty("SelectedPolicy", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetProperty).GetValue(dialog, null);
        }

        private static void SetDrugPolicy(Dialog_ManageDrugPolicies dialog, DrugPolicy selectedPolicy)
        {
            typeof(Dialog_ManageDrugPolicies).GetProperty("SelectedPolicy", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetProperty).SetValue(dialog, selectedPolicy, null);
        }
    }
}