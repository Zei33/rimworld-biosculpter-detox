using System.Collections.Generic;
using BiosculpterDetox.Core;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BiosculpterDetox.Patches
{
    /// <summary>
    /// Harmony patches that add the detox cycle to biosculpter pods and gate its use.
    /// </summary>
    public static class BiosculpterPatches
    {
        /// <summary>
        /// The key this mod registers its cycle under in the pod's cycle lookup.
        /// </summary>
        /// <remarks>
        /// Namespaced, because <c>SetupCycleCaches</c> writes
        /// <c>cycleLookup[cycle.Props.key] = cycle</c> with no collision check, so two mods choosing
        /// the same string silently overwrite each other and leave an ambiguous lookup. The old key
        /// was the bare word "detox".
        /// </remarks>
        public const string CycleKey = "Zei33.BiosculpterDetox.Detox";

        /// <summary>
        /// The key this mod used before the one above, still present in existing saves.
        /// </summary>
        public const string LegacyCycleKey = "detox";

        /// <summary>
        /// Adds the detox cycle component to a biosculpter pod as it spawns.
        /// </summary>
        [HarmonyPatch(typeof(CompBiosculpterPod), nameof(CompBiosculpterPod.PostSpawnSetup))]
        public static class CompBiosculpterPod_PostSpawnSetup_Patch
        {
            /// <summary>
            /// Appends the detox cycle comp, once per pod.
            /// </summary>
            /// <param name="__instance">The pod's own biosculpter component.</param>
            /// <remarks>
            /// <para>
            /// The properties are still built per pod rather than hoisted into a shared static,
            /// which the backlog suggested. Hoisting would be wrong here, and the reason is the
            /// translated label. A static built once per process keeps whatever language was active
            /// when the first pod spawned; building per spawn gets the label right, because
            /// changing language reloads play data and returns to the menu, so every pod respawns
            /// and rebuilds its properties. The allocation is one object per pod, and there are
            /// only ever a handful of pods.
            /// </para>
            /// <para>
            /// The research lookup no longer sits inside a swallowing try/catch.
            /// <c>GetNamed</c> does not throw on a miss, it logs a red error and returns null, so
            /// the catch never caught anything and a null was appended to
            /// <c>requiredResearch</c> instead. Without Ideology there is no Bioregeneration
            /// project and no biosculpter pod either, so this postfix cannot fire at all; the
            /// silent lookup is belt and braces against another mod removing the project.
            /// </para>
            /// </remarks>
            public static void Postfix(CompBiosculpterPod __instance)
            {
                try
                {
                    if (__instance.parent.GetComp<CompBiosculpterPod_DetoxCycle>() != null)
                    {
                        return;
                    }

                    ResearchProjectDef research =
                        DefDatabase<ResearchProjectDef>.GetNamedSilentFail("Bioregeneration");

                    var props = new CompProperties_BiosculpterPod_DetoxCycle
                    {
                        key = CycleKey,
                        label = "BiosculpterDetox_CycleLabel".Translate(),
                        description = "BiosculpterDetox_CycleDescription".Translate(),
                        iconPath = "UI/Commands/Detox",
                        durationDays = 12f,
                        operatingColor = new UnityEngine.Color(0.2f, 0.8f, 0.2f),
                        requiredResearch = research == null
                            ? new List<ResearchProjectDef>()
                            : new List<ResearchProjectDef> { research }
                    };

                    var comp = new CompBiosculpterPod_DetoxCycle
                    {
                        parent = __instance.parent
                    };

                    __instance.parent.AllComps.Add(comp);
                    comp.Initialize(props);
                }
                catch (System.Exception exception)
                {
                    Log.Error($"[BiosculpterDetox] Error adding the detox cycle component: {exception}");
                }
            }
        }

        /// <summary>
        /// Refuses the detox cycle for a pawn with nothing to treat.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This replaces a postfix on <c>CompGetGizmosExtra</c> that was dead twice over. It read
        /// <c>__instance.Occupant</c>, which is null during cycle selection, which is the only
        /// moment the check could be useful: <c>Occupant</c> needs a current cycle key, and no
        /// cycle is current while the player is looking at the cycle gizmos. And it found the
        /// gizmo by testing <c>command.defaultLabel.ToLower().Contains("detox")</c>, a translated
        /// display label, so the match was false in all eight non-English languages. Polish is
        /// "detoks", which does not even contain the substring.
        /// </para>
        /// <para>
        /// <c>CannotUseNowPawnCycleReason</c> is the hook vanilla built for exactly this. It is
        /// called with the pawn at the point the decision is made, needs no occupant, and feeds all
        /// three consumers at once: the gizmo's enabled state, the float menu and
        /// <c>CanUseNow</c>. Vanilla gates its own age-reversal cycle from inside it.
        /// </para>
        /// <para>
        /// The cycle is identified by its component type, never by a label. That is the general
        /// rule this repo has been burned by before: a display label is translated, so matching on
        /// one is a check that works in English and nowhere else.
        /// </para>
        /// <para>
        /// The argument types are spelled out because there are TWO public overloads of this
        /// method, and a <c>[HarmonyPatch]</c> naming it without them does not resolve. The
        /// four-parameter one is the real body; the three-parameter one delegates to it, so
        /// patching this one covers both.
        /// </para>
        /// </remarks>
        [HarmonyPatch(typeof(CompBiosculpterPod), nameof(CompBiosculpterPod.CannotUseNowPawnCycleReason),
            new[] { typeof(Pawn), typeof(Pawn), typeof(CompBiosculpterPod_Cycle), typeof(bool) })]
        public static class CompBiosculpterPod_CannotUseNowPawnCycleReason_Patch
        {
            /// <summary>
            /// Adds the detox cycle's own reason, without displacing vanilla's.
            /// </summary>
            /// <param name="__result">The reason so far, or <c>null</c> when the cycle is usable.</param>
            /// <param name="biosculptee">The pawn who would enter the pod.</param>
            /// <param name="cycle">The cycle being considered.</param>
            public static void Postfix(ref string __result, Pawn biosculptee, CompBiosculpterPod_Cycle cycle)
            {
                // A reason vanilla already gave wins. Missing ingredients is a better thing to tell
                // the player than "nothing to treat", because it is the one they can act on.
                if (__result != null)
                {
                    return;
                }

                if (!(cycle is CompBiosculpterPod_DetoxCycle detox) || biosculptee == null)
                {
                    return;
                }

                if (!detox.CanUseOn(biosculptee))
                {
                    __result = "BiosculpterDetox_NoAddictionsToTreat".Translate().CapitalizeFirst();
                }
            }
        }

        /// <summary>
        /// Migrates a pod still holding this mod's old, unnamespaced cycle key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Renaming the key would otherwise strand every save with a detox cycle in progress.
        /// <c>CurrentCycle</c> resolves through <c>cycleLookup[key]</c>, so a saved key that no
        /// longer exists throws out of the pod's tick every tick and traps the occupant, which is
        /// exactly the failure issue #6 describes for uninstalling the mod mid-cycle.
        /// </para>
        /// <para>
        /// The shape is vanilla's own. <c>CompBiosculpterPod.PostExposeData</c> ends with
        /// <c>if (currentCycleKey == "healing") currentCycleKey = "medic";</c> under
        /// <c>PostLoadInit</c>, which is the same rename problem solved the same way. This runs at
        /// the same moment for the same reason.
        /// </para>
        /// <para>
        /// It cannot help somebody who removes the mod mid-cycle, because the code that would fix
        /// their save leaves with the mod. Nothing in this mod can fix that case; what it can do is
        /// make sure that the mod's own rename is not a second way to reach it.
        /// </para>
        /// </remarks>
        [HarmonyPatch(typeof(CompBiosculpterPod), nameof(CompBiosculpterPod.PostExposeData))]
        public static class CompBiosculpterPod_PostExposeData_Patch
        {
            /// <summary>
            /// Rewrites the legacy key after the save has been read.
            /// </summary>
            /// <param name="___currentCycleKey">The pod's private current cycle key.</param>
            public static void Postfix(ref string ___currentCycleKey)
            {
                if (Scribe.mode == LoadSaveMode.PostLoadInit && ___currentCycleKey == LegacyCycleKey)
                {
                    ___currentCycleKey = CycleKey;
                }
            }
        }
    }
}
