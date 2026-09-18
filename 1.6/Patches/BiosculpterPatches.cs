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
                    CompBiosculpterPod_DetoxCycle existing =
                        __instance.parent.GetComp<CompBiosculpterPod_DetoxCycle>();

                    if (existing != null)
                    {
                        // A pod that was uninstalled and put back down is the SAME Thing instance:
                        // MinifyUtility.MakeMinified despawns the thing and hands the minified
                        // wrapper that same object, so the comp added here survives and a second one
                        // must not be added. It does still need its duration refreshed, because a
                        // settings change made while the pod sat minified swept the spawned pods and
                        // could not see this one. Without this the pod comes back running the old
                        // length while every other pod on the map runs the new one, for the rest of
                        // the session, since only a load rebuilds comps from def.comps.
                        if (existing.Props != null)
                        {
                            existing.Props.durationDays = CurrentDurationDays();
                        }

                        return;
                    }

                    CompProperties_BiosculpterPod_DetoxCycle props = BuildProps();

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
        /// Builds the detox cycle's properties from the current settings.
        /// </summary>
        /// <returns>A fresh properties object for one pod.</returns>
        /// <remarks>
        /// <para>
        /// One method so that the spawn postfix and the settings push cannot drift apart about what
        /// a detox cycle is. The duration is the only field the player can move; everything else is
        /// fixed.
        /// </para>
        /// <para>
        /// Still built per pod rather than hoisted into a shared static, which would be wrong here
        /// for the translated label. A static built once per process keeps whatever language was
        /// active when the first pod spawned; building per spawn gets the label right, because
        /// changing language reloads play data and returns to the menu, so every pod respawns.
        /// </para>
        /// <para>
        /// The research lookup does not sit inside a swallowing try/catch. <c>GetNamed</c> does not
        /// throw on a miss, it logs a red error and returns null, so a catch would never have caught
        /// anything and a null would be appended to <c>requiredResearch</c> instead. Without
        /// Ideology there is no Bioregeneration project and no biosculpter pod either, so this
        /// cannot fire at all; the silent lookup is belt and braces against another mod removing the
        /// project.
        /// </para>
        /// </remarks>
        public static CompProperties_BiosculpterPod_DetoxCycle BuildProps()
        {
            ResearchProjectDef research =
                DefDatabase<ResearchProjectDef>.GetNamedSilentFail("Bioregeneration");

            return new CompProperties_BiosculpterPod_DetoxCycle
            {
                key = CycleKey,
                label = "BiosculpterDetox_CycleLabel".Translate(),
                description = "BiosculpterDetox_CycleDescription".Translate(),
                iconPath = "UI/Commands/Detox",
                durationDays = CurrentDurationDays(),
                operatingColor = new UnityEngine.Color(0.2f, 0.8f, 0.2f),
                requiredResearch = research == null
                    ? new List<ResearchProjectDef>()
                    : new List<ResearchProjectDef> { research }
            };
        }

        /// <summary>
        /// Reads the configured cycle duration, falling back to the shipped default.
        /// </summary>
        /// <returns>The duration in days.</returns>
        /// <remarks>
        /// The settings object is populated by the mod's constructor, which the game runs long
        /// before any pod spawns. The fallback covers the order being different than expected rather
        /// than any known case, and it returns the value this mod shipped with so that an unexpected
        /// null changes nothing a player would notice.
        /// </remarks>
        private static float CurrentDurationDays()
        {
            BiosculpterDetoxSettings settings = BiosculpterDetoxMod.Settings;

            return settings == null
                ? BiosculpterDetoxSettings.DefaultCycleDurationDays
                : settings.CycleDurationDays;
        }

        /// <summary>
        /// Applies a changed duration to every detox cycle already standing on a map.
        /// </summary>
        /// <param name="days">The new duration, in days.</param>
        /// <remarks>
        /// <para>
        /// Without this a duration change reaches no existing pod, because the properties are built
        /// once per spawn. The player would change the setting, watch nothing happen, and reasonably
        /// conclude the setting is broken.
        /// </para>
        /// <para>
        /// A cycle already running is NOT retimed by this, and that is the game's doing rather than
        /// a choice made here: <c>CompBiosculpterPod</c> captures the duration into its own scribed
        /// countdown when the pawn enters, so an occupant keeps the length the cycle started with
        /// and nothing here can strand somebody in a pod.
        /// </para>
        /// <para>
        /// The progress bar is the exception, and it is worse than a brief flicker. The mote
        /// recomputes its denominator from the live properties every tick while the countdown still
        /// holds the old length, so shortening the duration mid-cycle pins the bar at empty for the
        /// whole of the difference. Twelve days cut to three, with a cycle that has just begun,
        /// shows an empty bar for nine days and then fills over the last three. Lengthening it has
        /// the opposite effect and reads further along than the cycle really is. The numbers stay
        /// correct throughout, the bar is bounded by the game's own clamp, and the cycle ends when
        /// it always would have; only the drawing is wrong, and only until that cycle finishes.
        /// </para>
        /// <para>
        /// Colonist buildings only. A biosculpter pod that matters to this setting is one the player
        /// built and can put a pawn into.
        /// </para>
        /// </remarks>
        public static void ApplyDurationToSpawnedPods(float days)
        {
            if (Current.ProgramState != ProgramState.Playing)
            {
                return;
            }

            List<Map> maps = Find.Maps;

            if (maps == null)
            {
                return;
            }

            foreach (Map map in maps)
            {
                foreach (Building building in map.listerBuildings.allBuildingsColonist)
                {
                    CompBiosculpterPod_DetoxCycle cycle =
                        building.GetComp<CompBiosculpterPod_DetoxCycle>();

                    if (cycle != null && cycle.Props != null)
                    {
                        cycle.Props.durationDays = days;
                    }
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
        /// called with the pawn at the point the decision is made, needs no occupant, and is what
        /// every per-pawn question about a cycle asks: the pawn's row in the menu the cycle button
        /// opens, the pod's right-click menu, the carry-to-pod menu, and <c>PawnCanUseNow</c>,
        /// whose only caller is the automatic age reversal job. Vanilla gates its own age reversal
        /// cycle from inside it.
        /// </para>
        /// <para>
        /// It does NOT decide whether the cycle button is enabled, and an earlier version of this
        /// comment said it did. The button asks whether any pawn has a row, and a refused row is
        /// still a row, so this reason reaches the row and never the button. On an unbiotuned pod
        /// that is harmless, because the button opens the menu and the row is greyed there. On a
        /// biotuned pod it is not: the button skips the menu and invokes the one row's action,
        /// which is null for a refused row. <see cref="CompBiosculpterPod_CompGetGizmosExtra_Patch"/>
        /// closes that.
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

        /// <summary>
        /// Disables a biotuned pod's detox button when the pawn it would act on is refused.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Without this, every successful detox leaves a trap behind it. The cycle biotunes the pod
        /// to the pawn it has just treated, that pawn now has nothing to treat, and vanilla keeps
        /// "Begin detox cycle (pawn)" enabled because it counts the refused row as eligible. The
        /// click then invokes that row's null action and throws. Confirmed in game on 2026-09-18.
        /// The reasoning, and every decision, lives in <see cref="DetoxCommandGate"/>; this class
        /// only gathers what the decisions need.
        /// </para>
        /// <para>
        /// Detox only. Vanilla's four cycles have the same latent crash, for a biotuned child and
        /// the age reversal cycle or a biotuned pawn with no path, but that is vanilla's defect,
        /// every player has it with or without this mod, and a patch that greyed vanilla's own
        /// buttons would put this mod's name on a change nobody installed it for.
        /// </para>
        /// <para>
        /// Declared last on purpose, and it has to stay last. <c>PatchAll</c> applies patch classes
        /// in the order they are declared, and a class that throws stops every class after it.
        /// This one has the most ways to throw at patch time (a private field injected by name, a
        /// replaced result whose type must match, a target that is an override), and losing it
        /// costs only this button. Losing the migration above it would trap an occupant, so a
        /// future break here must not take that with it. <c>PatchTargetTests</c> pins the order.
        /// </para>
        /// <para>
        /// The target is an iterator, so the original returns before any of its body runs. The
        /// postfix therefore replaces <c>__result</c> with a wrapper and decides nothing itself.
        /// Every read it does make (the cycle list, each cycle's icon) is one vanilla makes on the
        /// same objects a moment later, so it adds no way to fail that vanilla does not have.
        /// </para>
        /// <para>
        /// <c>___biotunedTo</c> is read when the postfix runs, which is when
        /// <c>ThingWithComps.GetGizmos</c> reaches this comp, immediately before it walks the
        /// result. The gizmo grid builds its list once per frame and reuses those objects for every
        /// event in the frame, and ticks run in Unity's update rather than between those events, so
        /// the reason decided here is the one the click would meet.
        /// </para>
        /// </remarks>
        [HarmonyPatch(typeof(CompBiosculpterPod), nameof(CompBiosculpterPod.CompGetGizmosExtra))]
        public static class CompBiosculpterPod_CompGetGizmosExtra_Patch
        {
            /// <summary>
            /// Wraps the pod's gizmos so that a refused detox button arrives disabled.
            /// </summary>
            /// <param name="__result">The pod's gizmos, replaced by a wrapper over them.</param>
            /// <param name="__instance">The pod's own biosculpter component.</param>
            /// <param name="___biotunedTo">The pawn the pod is biotuned to, or <c>null</c>.</param>
            public static void Postfix(
                ref IEnumerable<Gizmo> __result, CompBiosculpterPod __instance, Pawn ___biotunedTo)
            {
                if (__result == null)
                {
                    return;
                }

                var detox = DetoxCommandGate.SplitCycles(
                    __instance.AvailableCycles,
                    cycle => cycle is CompBiosculpterPod_DetoxCycle,
                    cycle => cycle.Props.Icon,
                    out List<object> otherCycleIcons) as CompBiosculpterPod_DetoxCycle;

                if (detox == null)
                {
                    return;
                }

                CompBiosculpterPod pod = __instance;
                Pawn pawn = ___biotunedTo;

                __result = DetoxCommandGate.DisableRefusedDetoxCommand(
                    __result,
                    detox.Props.Icon,
                    otherCycleIcons,
                    () => DetoxCommandGate.BiotunedRefusal(
                        pawn != null,
                        pawn != null && !pawn.Dead && pawn.Spawned && pawn.Map == pod.parent.Map,
                        // The same two calls, in the same order, that vanilla's
                        // SelectPawnCycleOption makes to build this pawn's row.
                        () => pod.CannotUseNowPawnReason(pawn)
                              ?? pod.CannotUseNowPawnCycleReason(pawn, detox, checkIngredients: false)));
            }
        }
    }
}
