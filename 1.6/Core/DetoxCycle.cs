using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BiosculpterDetox.Core
{
    /// <summary>
    /// Decides what a detox cycle treats, and performs the treatment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything here used to be decided by matching hediff defNames as strings:
    /// <c>StartsWith("Addiction_")</c>, <c>EndsWith("Addiction")</c>,
    /// <c>EndsWith("Withdrawal")</c> and <c>Contains("Tolerance")</c>, against two allow-lists and
    /// one deny-list. The allow-lists were entirely dead. Not one of the nine names in the
    /// addiction list exists as a def of any type in any version of RimWorld, and the real names
    /// are <c>AlcoholAddiction</c>, <c>PsychiteAddiction</c> and so on. The mod worked by accident,
    /// through the catch-all suffix test beside them.
    /// </para>
    /// <para>
    /// A defName is a namespace other mods write into, so a substring test over it is unbounded by
    /// construction. <c>Contains("Tolerance")</c> would remove any modded hediff with that
    /// substring anywhere in its name, drug-related or not, and the failure is close to
    /// unreportable: a hediff vanishing after a biosculpter cycle gets blamed on the mod that added
    /// it. <c>EndsWith("Withdrawal")</c> has a confirmed victim in the shipped game already, since
    /// Anomaly's <c>CubeWithdrawal</c> is a real HediffDef with its own class and nothing to do
    /// with drugs.
    /// </para>
    /// <para>
    /// All four string tests are replaced by the game's own structure, and the deny-list goes with
    /// them. The predicates take the chemical list as a parameter rather than reading
    /// <c>DefDatabase</c>, because the test harness cannot touch <c>DefDatabase</c> and naming any
    /// <c>DefOf</c> member there throws. That is the whole reason the decisions in this file are
    /// now reachable by a test at all.
    /// </para>
    /// </remarks>
    public static class DetoxCycle
    {
        /// <summary>
        /// Decides whether a hediff is a drug addiction this cycle is allowed to cure.
        /// </summary>
        /// <param name="hediff">The hediff to judge.</param>
        /// <param name="cureIncurableAddictions">
        /// Whether the player has asked for addictions the game declares permanent to be treated.
        /// </param>
        /// <returns><c>true</c> when it is an addiction this cycle is allowed to cure.</returns>
        /// <remarks>
        /// <para>
        /// This is vanilla's own predicate, copied from <c>HealthUtility.FindAddiction</c>. The type
        /// test covers all seven shipped addictions and every modded one without an allow-list, and
        /// <c>everCurableByItem</c> is how the game itself declares an addiction incurable. It is
        /// what the vanilla biosculpter healing cycle gates on, so a detox cycle using it agrees
        /// with the pod it bolts onto.
        /// </para>
        /// <para>
        /// That replaces the hardcoded <c>LuciferiumAddiction</c> deny-list. Luciferium is still
        /// excluded, but now because Ludeon declared it incurable rather than because this mod
        /// spelled its name, so a modded permanent addiction is excluded too. Before, any modded
        /// addiction whose defName merely ended in "Addiction" was cured by the pod.
        /// </para>
        /// <para>
        /// The pair is load-bearing and neither half is redundant. <c>everCurableByItem</c> is a
        /// general medical flag, not a drug flag: it defaults to true on every hediff, and fourteen
        /// of the fifteen shipped defs that set it false have nothing to do with drugs, so it is only
        /// safe ANDed with the type test. The type test alone would cure luciferium, the one shipped
        /// <c>Hediff_Addiction</c> that sets the flag false, and keeping luciferium out is the job
        /// the flag does here. Biotech's <c>Hediff_ChemicalDependency</c> carries a
        /// <c>ChemicalDef</c> just as an addiction does, but neither half admits it: it derives from
        /// <c>HediffWithComps</c> beside <c>Hediff_Addiction</c> rather than from it, AND it sets the
        /// flag false, so this pair excludes it twice.
        /// </para>
        /// <para>
        /// The setting relaxes the flag and nothing else, which is what makes it safe. Of the
        /// fifteen hediffs the shipped game declares incurable, exactly one is an
        /// <c>Hediff_Addiction</c>, and it is luciferium; the other fourteen are excluded by the
        /// type test whatever the setting says. Biotech's chemical dependency is among those
        /// fourteen, and it is the one that most needs to stay excluded. Removing it would not kill
        /// the pawn, and nothing would put it back: <c>Gene_ChemicalDependency</c> has no tick, and
        /// recreates the hediff only from <c>PostAdd</c> and <c>Reset</c>, which run when the gene
        /// is added, when the pawn ingests that drug, and when the gene tracker is reset. A
        /// wrongful removal would therefore silently suspend a condition the game calls incurable:
        /// no deficiency, no mood penalty and no drug-seeking until the next dose, while the pawn
        /// keeps the metabolic efficiency the gene grants in exchange. It is a sibling of
        /// <c>Hediff_Addiction</c> rather than a subclass, so the type test alone carries that
        /// exclusion and the setting cannot reach it.
        /// </para>
        /// <para>
        /// The toggle is therefore described to the player in terms of permanent addictions rather
        /// than by naming luciferium, for the same reason nothing else in this mod matches on a
        /// name: a mod that adds its own permanent addiction is covered by the same rule, and the
        /// game's own declaration is what decides.
        /// </para>
        /// </remarks>
        public static bool IsCurableAddiction(Hediff hediff, bool cureIncurableAddictions)
        {
            if (!(hediff is Hediff_Addiction) || hediff.def == null)
            {
                return false;
            }

            return cureIncurableAddictions || hediff.def.everCurableByItem;
        }

        /// <summary>
        /// Decides whether a hediff def is a drug tolerance.
        /// </summary>
        /// <param name="def">The def to judge.</param>
        /// <param name="chemicals">Every chemical in the game.</param>
        /// <returns><c>true</c> when the def is a drug tolerance.</returns>
        /// <remarks>
        /// <para>
        /// Tolerance has no distinguishing class to type-test: every shipped tolerance inherits an
        /// abstract base whose <c>hediffClass</c> is the plain <c>HediffWithComps</c>. The
        /// structural marker is the comp, <c>HediffCompProperties_DrugEffectFactor</c>, which
        /// carries a back-reference to the chemical it dampens, and which no other shipped hediff
        /// declares.
        /// </para>
        /// <para>
        /// Both clauses are wanted. The comp test is the semantic one and covers all seven shipped
        /// tolerances, including <c>GoJuiceTolerance</c> and <c>WakeUpTolerance</c>, which are
        /// orphans in 1.6 that no chemical points at any more. The back-reference catches a modded
        /// tolerance that skips the comp. Chemicals are enumerated whole rather than filtered to
        /// the addictive ones, because Odyssey's Psilocap has a tolerance and cannot be addictive.
        /// </para>
        /// <para>
        /// This does NOT gate on <c>everCurableByItem</c>, unlike the addiction test. Every shipped
        /// tolerance leaves that flag at its default true, so it would discriminate nothing here. A
        /// Luciferium tolerance does not exist, which is why the old <c>Contains("Luciferium")</c>
        /// guard beside the tolerance test was dead code; if a mod ever adds one, exclude it by
        /// asking the comp which chemical it belongs to, never by name.
        /// </para>
        /// </remarks>
        public static bool IsDrugTolerance(HediffDef def, IEnumerable<ChemicalDef> chemicals)
        {
            if (def == null)
            {
                return false;
            }

            if (def.CompProps<HediffCompProperties_DrugEffectFactor>() != null)
            {
                return true;
            }

            return chemicals != null && chemicals.Any(chemical => chemical.toleranceHediff == def);
        }

        /// <summary>
        /// Decides whether a hediff is anything this cycle treats.
        /// </summary>
        /// <param name="hediff">The hediff to judge.</param>
        /// <param name="chemicals">Every chemical in the game.</param>
        /// <param name="cureIncurableAddictions">
        /// Whether addictions the game declares permanent are being treated.
        /// </param>
        /// <returns><c>true</c> when the cycle would remove it.</returns>
        /// <remarks>
        /// <para>
        /// One predicate, used by the eligibility gate, the preview and the removal alike. That is
        /// the fix for the worst-behaved bug in this mod: the gate counted tolerances and the
        /// preview did not, so a pawn whose only treatable condition was a drug tolerance was
        /// offered the cycle, told the cycle would treat nothing, kept in the pod for twelve days,
        /// quietly had the tolerance removed, and was then reported as a failure. Every one of
        /// those four steps was a different method disagreeing with the others about the same
        /// question, so the question is asked in one place now.
        /// </para>
        /// <para>
        /// Withdrawal is deliberately absent, and its removal is not an omission. Withdrawal is not
        /// a hediff at all: it is stage index 1 of the addiction hediff, selected at runtime from
        /// the pawn's chemical need level. The mod's eight-name withdrawal list could never have
        /// matched anything, six of those names being ThoughtDefs and two existing nowhere. Curing
        /// the addiction removes the stage, removes the chemical need (which is declared
        /// <c>onlyIfCausedByHediff</c>) and flips the withdrawal thought to inactive, all without
        /// this mod touching needs or thoughts.
        /// </para>
        /// </remarks>
        public static bool IsDetoxifiable(
            Hediff hediff, IEnumerable<ChemicalDef> chemicals, bool cureIncurableAddictions)
        {
            if (hediff == null || hediff.def == null)
            {
                return false;
            }

            return IsCurableAddiction(hediff, cureIncurableAddictions)
                || IsDrugTolerance(hediff.def, chemicals);
        }

        /// <summary>
        /// Collects every condition on a pawn that this cycle would treat.
        /// </summary>
        /// <param name="pawn">The pawn to examine.</param>
        /// <param name="chemicals">Every chemical in the game.</param>
        /// <param name="cureIncurableAddictions">
        /// Whether addictions the game declares permanent are being treated.
        /// </param>
        /// <returns>The hediffs the cycle would remove, which may be empty.</returns>
        public static List<Hediff> DetoxifiableConditions(
            Pawn pawn, IEnumerable<ChemicalDef> chemicals, bool cureIncurableAddictions)
        {
            var found = new List<Hediff>();

            if (pawn?.health?.hediffSet == null)
            {
                return found;
            }

            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (IsDetoxifiable(hediff, chemicals, cureIncurableAddictions))
                {
                    found.Add(hediff);
                }
            }

            return found;
        }

        /// <summary>
        /// Checks if a pawn has any condition this cycle can treat.
        /// </summary>
        /// <param name="pawn">The pawn to check.</param>
        /// <returns><c>true</c> if the pawn has any detoxifiable condition.</returns>
        public static bool HasDetoxifiableConditions(Pawn pawn)
        {
            return DetoxifiableConditions(pawn, AllChemicals(), CureIncurableAddictions()).Count > 0;
        }

        /// <summary>
        /// Gets the display labels of the conditions this cycle would treat.
        /// </summary>
        /// <param name="pawn">The pawn to analyse.</param>
        /// <returns>The distinct labels, in the order the hediffs appear on the pawn.</returns>
        /// <remarks>
        /// The labels come from <see cref="ConditionLabels"/>, which is also where the completion
        /// letter's list comes from, so the preview and the letter name each condition the same way.
        /// </remarks>
        public static List<string> GetDetoxifiableConditionNames(Pawn pawn)
        {
            return ConditionLabels(
                DetoxifiableConditions(pawn, AllChemicals(), CureIncurableAddictions()));
        }

        /// <summary>
        /// Gets the names to show the player for a set of conditions this cycle treats.
        /// </summary>
        /// <param name="hediffs">The conditions, in the order they should be named.</param>
        /// <returns>
        /// One capitalised name per distinct condition, in the order first seen. Empty when there is
        /// nothing to name.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The "Will treat" preview and the "Cured" letter both build their lists here, so they
        /// cannot name the same condition two different ways. Each used to read
        /// <c>Hediff.LabelCap</c>, which appends the hediff's live bracket: an addiction's recovery
        /// percentage, or a tolerance's stage word. Both move on their own while the pawn waits to
        /// go in, so a tolerance previewed as "(large)" could be reported as "(small)".
        /// </para>
        /// <para>
        /// The def label is what vanilla shows for the same job. <c>HealthUtility.Cure</c>, which
        /// <see cref="PerformDetox"/> calls for every removal, builds its own message from
        /// <c>hediff.def.label</c>. The def label is also the same on either side of the cure, and
        /// it belongs to the def alone, so a test can reach it without a pawn.
        /// <c>Hediff.LabelBaseCap</c> would not do instead: <c>HediffWithComps</c>, the class of
        /// every shipped tolerance, prefixes it with its comps' labels.
        /// </para>
        /// <para>
        /// A def with no label is named by its defName rather than dropped. A removed condition has
        /// to appear in the letter, and an empty entry would read as a gap in the list.
        /// </para>
        /// </remarks>
        public static List<string> ConditionLabels(IEnumerable<Hediff> hediffs)
        {
            var labels = new List<string>();

            if (hediffs == null)
            {
                return labels;
            }

            foreach (Hediff hediff in hediffs)
            {
                if (hediff?.def == null)
                {
                    continue;
                }

                string label = hediff.def.LabelCap;

                if (label.NullOrEmpty())
                {
                    label = hediff.def.defName;
                }

                if (!labels.Contains(label))
                {
                    labels.Add(label);
                }
            }

            return labels;
        }

        /// <summary>
        /// Removes every treatable condition from a pawn.
        /// </summary>
        /// <param name="pawn">The pawn undergoing treatment.</param>
        /// <returns>
        /// The names of what was removed, one per distinct condition, from
        /// <see cref="ConditionLabels"/>. Empty when nothing was removed.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This reports what it did and says nothing to the player. All feedback belongs to the
        /// cycle component, which is the only place that knows the cycle has finished and is the
        /// only place with a spawned thing to hang a message on. Moving it there is what fixes the
        /// old accounting bug: the tolerance pass never set the <c>removedAny</c> flag, so a
        /// tolerance-only detox removed the tolerance and then reported failure.
        /// </para>
        /// <para>
        /// Removal goes through <c>HealthUtility.Cure</c> rather than
        /// <c>pawn.health.RemoveHediff</c>, matching the vanilla healing cycle. <c>Cure</c> honours
        /// <c>cureAllAtOnceIfCuredByItem</c>, which the raw call does not.
        /// </para>
        /// <para>
        /// The list is materialised before anything is removed, which it has to be, since removing
        /// a hediff mutates the set being walked. Hediffs are re-checked as they are cured, because
        /// curing one can cascade and remove another.
        /// </para>
        /// <para>
        /// The permanent addiction setting is read HERE, when the cycle completes, rather than
        /// captured when it started. A player who turns the setting off while a pawn is already in
        /// the pod therefore gets a cycle that was offered on one promise and delivered on another,
        /// which is a smaller cousin of the worst bug this mod has had. It is left this way
        /// deliberately, because the alternative is worse: the only place to record the setting at
        /// cycle start is the cycle comp, and this comp is added at runtime rather than declared in
        /// <c>def.comps</c>, so <c>ThingWithComps.ExposeData</c> rebuilds the comp list on load and
        /// anything scribed on it is orphaned. A captured flag would survive until the player saved
        /// and then quietly revert, which is harder to explain than a setting that simply applies
        /// when the cycle ends.
        /// </para>
        /// </remarks>
        public static List<string> PerformDetox(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                Log.Warning("[BiosculpterDetox] Attempted to detox a null pawn or a pawn with no health tracker.");
                return new List<string>();
            }

            var cured = new List<Hediff>();

            foreach (Hediff hediff in
                     DetoxifiableConditions(pawn, AllChemicals(), CureIncurableAddictions()))
            {
                // Re-check rather than trusting the list. HealthUtility.Cure can remove more than
                // the one hediff it is given, so an entry collected a moment ago may already be off
                // the pawn by the time its turn comes.
                if (!pawn.health.hediffSet.hediffs.Contains(hediff))
                {
                    continue;
                }

                HealthUtility.Cure(hediff);
                cured.Add(hediff);
            }

            return ConditionLabels(cured);
        }

        /// <summary>
        /// Gets every chemical the game knows about.
        /// </summary>
        /// <returns>The chemical defs.</returns>
        /// <remarks>
        /// Kept to one line and one call site per public method so that every decision above can be
        /// handed a list by a test instead. <c>DefDatabase</c> is populated only by a running game.
        /// </remarks>
        private static List<ChemicalDef> AllChemicals()
        {
            return DefDatabase<ChemicalDef>.AllDefsListForReading;
        }

        /// <summary>
        /// Reads whether the player has asked for permanent addictions to be treated.
        /// </summary>
        /// <returns><c>true</c> when the setting is on.</returns>
        /// <remarks>
        /// <para>
        /// Kept to one line and one call site per public method for the same reason
        /// <see cref="AllChemicals"/> is: it is the boundary between the decisions above, which a
        /// test can reach, and the game state, which it cannot. Every predicate takes this as a
        /// parameter, so a test supplies both values and never touches a static.
        /// </para>
        /// <para>
        /// The null check is what stops that boundary leaking. <c>LoadedModManager.GetMod</c>
        /// returns null outside a running game rather than throwing, so a predicate that read the
        /// static directly would not fail loudly in the harness, it would throw a
        /// <c>NullReferenceException</c> from somewhere unrelated. Falling back to false here means
        /// the shipped behaviour is what an unconfigured caller gets.
        /// </para>
        /// </remarks>
        private static bool CureIncurableAddictions()
        {
            BiosculpterDetoxSettings settings = BiosculpterDetoxMod.Settings;

            return settings != null && settings.CureIncurableAddictions;
        }
    }
}
