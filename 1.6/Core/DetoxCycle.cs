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
        /// <returns><c>true</c> when it is a curable addiction.</returns>
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
        /// general medical flag, not a drug flag: fourteen of the fifteen shipped defs that set it
        /// false have nothing to do with drugs, so it is only safe ANDed with the type test.
        /// And the type test alone would sweep up Biotech's <c>Hediff_ChemicalDependency</c>, which
        /// carries a <c>ChemicalDef</c> just like an addiction does; removing that one is fatal,
        /// because the gene re-adds it and the pawn dies without the drug. It happens to derive
        /// from a different base AND to set the flag false, so this pair excludes it twice.
        /// </para>
        /// </remarks>
        public static bool IsCurableAddiction(Hediff hediff)
        {
            return hediff is Hediff_Addiction && hediff.def != null && hediff.def.everCurableByItem;
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
        public static bool IsDetoxifiable(Hediff hediff, IEnumerable<ChemicalDef> chemicals)
        {
            if (hediff == null || hediff.def == null)
            {
                return false;
            }

            return IsCurableAddiction(hediff) || IsDrugTolerance(hediff.def, chemicals);
        }

        /// <summary>
        /// Collects every condition on a pawn that this cycle would treat.
        /// </summary>
        /// <param name="pawn">The pawn to examine.</param>
        /// <param name="chemicals">Every chemical in the game.</param>
        /// <returns>The hediffs the cycle would remove, which may be empty.</returns>
        public static List<Hediff> DetoxifiableConditions(Pawn pawn, IEnumerable<ChemicalDef> chemicals)
        {
            var found = new List<Hediff>();

            if (pawn?.health?.hediffSet == null)
            {
                return found;
            }

            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (IsDetoxifiable(hediff, chemicals))
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
            return DetoxifiableConditions(pawn, AllChemicals()).Count > 0;
        }

        /// <summary>
        /// Gets the display labels of the conditions this cycle would treat.
        /// </summary>
        /// <param name="pawn">The pawn to analyse.</param>
        /// <returns>The distinct labels, in the order the hediffs appear on the pawn.</returns>
        public static List<string> GetDetoxifiableConditionNames(Pawn pawn)
        {
            var names = new List<string>();

            foreach (Hediff hediff in DetoxifiableConditions(pawn, AllChemicals()))
            {
                string label = hediff.LabelCap;

                if (string.IsNullOrEmpty(label))
                {
                    label = hediff.def.LabelCap;
                }

                if (!names.Contains(label))
                {
                    names.Add(label);
                }
            }

            return names;
        }

        /// <summary>
        /// Removes every treatable condition from a pawn.
        /// </summary>
        /// <param name="pawn">The pawn undergoing treatment.</param>
        /// <returns>The labels of what was removed, which is empty when nothing was.</returns>
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
        /// </remarks>
        public static List<string> PerformDetox(Pawn pawn)
        {
            var removed = new List<string>();

            if (pawn?.health?.hediffSet == null)
            {
                Log.Warning("[BiosculpterDetox] Attempted to detox a null pawn or a pawn with no health tracker.");
                return removed;
            }

            foreach (Hediff hediff in DetoxifiableConditions(pawn, AllChemicals()))
            {
                // Re-check rather than trusting the list. HealthUtility.Cure can remove more than
                // the one hediff it is given, so an entry collected a moment ago may already be off
                // the pawn by the time its turn comes.
                if (!pawn.health.hediffSet.hediffs.Contains(hediff))
                {
                    continue;
                }

                string label = hediff.LabelCap;
                HealthUtility.Cure(hediff);
                removed.Add(label);
            }

            return removed;
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
    }
}
