using HarmonyLib;
using RimWorld;
using Verse;
using BiosculpterDetox.Core;
using System.Collections.Generic;
using System.Linq;

namespace BiosculpterDetox.Patches
{
    /// <summary>
    /// Harmony patches for integrating detox functionality into the biosculpter pod system.
    /// These patches add the detox cycle component to biosculpter pods.
    /// </summary>
    public static class BiosculpterPatches
    {
        /// <summary>
        /// Patch to add the detox cycle component to biosculpter pods when they are spawned.
        /// This ensures all biosculpter pods have the detox cycle available.
        /// </summary>
        [HarmonyPatch(typeof(CompBiosculpterPod), "PostSpawnSetup")]
        public static class CompBiosculpterPod_PostSpawnSetup_Patch
        {
            /// <summary>
            /// Postfix method that adds the detox cycle component to the biosculpter pod.
            /// </summary>
            /// <param name="__instance">The CompBiosculpterPod instance.</param>
            public static void Postfix(CompBiosculpterPod __instance)
            {
                try
                {
                    // Check if the detox cycle component is already present
                    var detoxCycle = __instance.parent.GetComp<CompBiosculpterPod_DetoxCycle>();
                    if (detoxCycle == null)
                    {
                        // Add the detox cycle component
                        var props = new CompProperties_BiosculpterPod_DetoxCycle
                        {
                            key = "detox",
                            label = "BiosculpterDetox_CycleLabel".Translate(),
                            description = "BiosculpterDetox_CycleDescription".Translate(),
                            iconPath = "UI/Commands/Detox",
                            durationDays = 12f,
                            operatingColor = new UnityEngine.Color(0.2f, 0.8f, 0.2f),
                            requiredResearch = new List<ResearchProjectDef> { DefDatabase<ResearchProjectDef>.GetNamed("Bioregeneration") }
                        };

                        var comp = new CompBiosculpterPod_DetoxCycle();
                        comp.props = props;
                        comp.parent = __instance.parent;
                        __instance.parent.AllComps.Add(comp);
                        comp.Initialize(props);
                        comp.PostSpawnSetup(false);

                        Log.Message($"[BiosculpterDetox] Added detox cycle component to {__instance.parent.Label}");
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[BiosculpterDetox] Error adding detox cycle component: {ex}");
                }
            }
        }

        /// <summary>
        /// Patch to show detox-specific information in the biosculpter pod's gizmos.
        /// This ensures the detox option shows appropriate information about what will be treated.
        /// </summary>
        [HarmonyPatch(typeof(CompBiosculpterPod), "CompGetGizmosExtra")]
        public static class CompBiosculpterPod_CompGetGizmosExtra_Patch
        {
            /// <summary>
            /// Postfix method that adds detox-specific gizmo modifications.
            /// </summary>
            /// <param name="__result">The enumerable of gizmos from the original method.</param>
            /// <param name="__instance">The CompBiosculpterPod instance.</param>
            public static void Postfix(ref IEnumerable<Gizmo> __result, CompBiosculpterPod __instance)
            {
                try
                {
                    if (__result == null) return;

                    var gizmos = __result.ToList();
                    
                    // Find any cycle selection gizmos and modify them for detox cycles
                    foreach (var gizmo in gizmos)
                    {
                        // Look for gizmos that might be cycle selection commands
                        if (gizmo is Command command)
                        {
                            // Check if this is a detox cycle command by examining the label or other properties
                            if (command.defaultLabel != null && command.defaultLabel.ToLower().Contains("detox"))
                            {
                                var occupant = __instance.Occupant;
                                if (occupant != null && !DetoxCycle.HasDetoxifiableConditions(occupant))
                                {
                                    // Disable the command if the pawn has no addictions
                                    command.Disable("BiosculpterDetox_NoAddictionsToTreat".Translate());
                                }
                                else if (occupant != null)
                                {
                                    // Show what addictions will be treated
                                    var conditions = DetoxCycle.GetDetoxifiableConditionNames(occupant);
                                    if (conditions.Count > 0)
                                    {
                                        string conditionList = string.Join(", ", conditions);
                                        command.defaultDesc += "\n\n" + "BiosculpterDetox_WillTreat".Translate(conditionList);
                                    }
                                }
                            }
                        }
                    }

                    __result = gizmos;
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[BiosculpterDetox] Error in GetGizmos patch: {ex}");
                }
            }
        }
    }
}