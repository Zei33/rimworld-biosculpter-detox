using System.Collections.Generic;
using NUnit.Framework;
using RimWorld;
using BiosculpterDetox.Core;
using Verse;

namespace BiosculpterDetox.Tests
{
    /// <summary>
    /// Covers the three predicates that decide what a detox cycle treats.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are reachable only because they were rewritten to take the chemical list as a
    /// parameter. The harness cannot touch <c>DefDatabase</c>, and naming any <c>DefOf</c> member
    /// throws a <c>TypeInitializationException</c> rather than returning null, so a predicate that
    /// reads either is untestable by construction. Everything else in this mod takes a
    /// <c>Pawn</c> and stays out of reach.
    /// </para>
    /// <para>
    /// The old string tests could not have been covered at all in a useful way. A test over
    /// <c>Contains("Tolerance")</c> can only restate the substring rule; it cannot say whether the
    /// rule is right, because the rule's failures are hediffs that do not exist in the shipped game.
    /// The cases below are exactly those: a modded hediff that the old code would have eaten.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class DetoxPredicateTests
    {
        [Test]
        public void AnOrdinaryAddictionIsCurable()
        {
            Assert.That(DetoxCycle.IsCurableAddiction(Addiction("AlcoholAddiction", curable: true), cureIncurableAddictions: false), Is.True);
        }

        [Test]
        public void AnAddictionTheGameCallsIncurableIsNotCured()
        {
            // This is how luciferium is excluded now: LuciferiumAddiction sets everCurableByItem
            // false, which is the same flag the vanilla biosculpter healing cycle gates on. The old
            // code spelled the defName instead, so a modded permanent addiction was cured anyway.
            Assert.That(DetoxCycle.IsCurableAddiction(Addiction("LuciferiumAddiction", curable: false), cureIncurableAddictions: false), Is.False);
        }

        [Test]
        public void TheExclusionDoesNotDependOnTheName()
        {
            // The point of the change. A modded permanent addiction with a name this mod has never
            // heard of is excluded, and a hediff merely NAMED like luciferium is not.
            Assert.That(DetoxCycle.IsCurableAddiction(Addiction("SomeModdedForeverAddiction", curable: false), cureIncurableAddictions: false), Is.False);
            Assert.That(DetoxCycle.IsCurableAddiction(Addiction("LuciferiumFlavouredBeerAddiction", curable: true), cureIncurableAddictions: false), Is.True);
        }

        [Test]
        public void SomethingThatIsNotAnAddictionIsNotCuredHoweverItIsNamed()
        {
            // EndsWith("Addiction") was the test that actually did the work in the old code, so a
            // plain hediff with that suffix was removed. Nothing but a real addiction qualifies now.
            var impostor = new Hediff { def = HediffDef("TotallyRealAddiction", curable: true) };

            Assert.That(DetoxCycle.IsCurableAddiction(impostor, cureIncurableAddictions: false), Is.False);
        }

        [Test]
        public void ARealDrugToleranceIsRecognisedByItsComp()
        {
            HediffDef tolerance = HediffDef("AlcoholTolerance", curable: true);
            tolerance.comps = new List<HediffCompProperties> { new HediffCompProperties_DrugEffectFactor() };

            Assert.That(DetoxCycle.IsDrugTolerance(tolerance, NoChemicals()), Is.True,
                "The comp is the only structural marker a tolerance has. Every shipped tolerance "
                + "inherits a base whose hediffClass is the plain HediffWithComps, so there is no "
                + "type to test.");
        }

        [Test]
        public void ADrugToleranceIsAlsoFoundThroughTheChemicalThatPointsAtIt()
        {
            // The second clause, for a modded tolerance that skips the comp.
            HediffDef tolerance = HediffDef("SomeModTolerance", curable: true);
            var chemical = new ChemicalDef { toleranceHediff = tolerance };

            Assert.That(DetoxCycle.IsDrugTolerance(tolerance, new List<ChemicalDef> { chemical }), Is.True);
        }

        [Test]
        public void AModdedHediffMerelyNamedLikeAToleranceIsLeftAlone()
        {
            // The defect in issue #5, stated as a test. Contains("Tolerance") is unanchored and
            // type-unchecked, so ANY modded hediff carrying that substring anywhere in its defName
            // was removed by a biosculpter cycle, and the player would blame the mod that added it.
            HediffDef impostor = HediffDef("PainToleranceImplant", curable: true);

            Assert.That(DetoxCycle.IsDrugTolerance(impostor, NoChemicals()), Is.False);
            Assert.That(DetoxCycle.IsDetoxifiable(new Hediff { def = impostor }, NoChemicals(), cureIncurableAddictions: false), Is.False);
        }

        [Test]
        public void AnomalysCubeWithdrawalIsLeftAlone()
        {
            // EndsWith("Withdrawal") had a victim in the shipped game: CubeWithdrawal is a real
            // HediffDef with its own class and nothing to do with drugs. No name is read now, so it
            // is safe for a reason that does not depend on anybody remembering it exists.
            HediffDef cube = HediffDef("CubeWithdrawal", curable: true);

            Assert.That(DetoxCycle.IsDetoxifiable(new Hediff { def = cube }, NoChemicals(), cureIncurableAddictions: false), Is.False);
        }

        [Test]
        public void ATolerancePawnIsTreatableAndWasTheBugInIssueOne()
        {
            // The whole of issue #1 is that the gate counted tolerances and the preview did not, so
            // a pawn whose only treatable condition was a tolerance was offered the cycle, told
            // there was nothing to treat, and then had it removed anyway. One predicate answers for
            // all three call sites now, so the three cannot disagree.
            HediffDef tolerance = HediffDef("PsychiteTolerance", curable: true);
            tolerance.comps = new List<HediffCompProperties> { new HediffCompProperties_DrugEffectFactor() };

            Assert.That(
                DetoxCycle.IsDetoxifiable(new Hediff { def = tolerance }, NoChemicals(), cureIncurableAddictions: false), Is.True,
                "A drug tolerance is treatable, and every method that asks must get this answer.");
        }

        [Test]
        public void ANullHediffOrDefIsNotTreatable()
        {
            Assert.That(DetoxCycle.IsDetoxifiable(null, NoChemicals(), cureIncurableAddictions: false), Is.False);
            Assert.That(DetoxCycle.IsDetoxifiable(new Hediff(), NoChemicals(), cureIncurableAddictions: false), Is.False);
            Assert.That(DetoxCycle.IsDrugTolerance(null, NoChemicals()), Is.False);
        }

        [Test]
        public void AToleranceLookupSurvivesAChemicalWithNoToleranceDeclared()
        {
            // Three of the eight shipped chemicals declare no toleranceHediff at all, and Odyssey's
            // Psilocap declares a tolerance while being unable to cause addiction. Walking the list
            // must not assume either field is populated.
            HediffDef tolerance = HediffDef("AmbrosiaTolerance", curable: true);
            var chemicals = new List<ChemicalDef>
            {
                new ChemicalDef { toleranceHediff = null },
                new ChemicalDef { toleranceHediff = tolerance }
            };

            Assert.That(DetoxCycle.IsDrugTolerance(tolerance, chemicals), Is.True);
            Assert.That(DetoxCycle.IsDrugTolerance(HediffDef("Other", curable: true), chemicals), Is.False);
        }

        [Test]
        public void TheSettingLetsThroughAnAddictionTheGameCallsPermanent()
        {
            // The whole point of the toggle. Luciferium is the only shipped addiction that declares
            // itself incurable, so this is that case and, in practice, only that case.
            Assert.That(
                DetoxCycle.IsCurableAddiction(
                    Addiction("LuciferiumAddiction", curable: false), cureIncurableAddictions: true),
                Is.True);
        }

        [Test]
        public void TheSettingStillWillNotTouchSomethingThatIsNotAnAddiction()
        {
            // The safety property, and the reason the toggle relaxes ONE of the two terms rather
            // than replacing the predicate. Fifteen shipped hediffs declare themselves incurable and
            // fourteen of them have nothing to do with drugs: sterilisation, deathrest, psychic
            // bonds, a vat-growing child. The type test is what excludes those, and the setting must
            // not be able to reach past it.
            var impostor = new Hediff { def = HediffDef("PsychicBondTorn", curable: false) };

            Assert.That(
                DetoxCycle.IsCurableAddiction(impostor, cureIncurableAddictions: true), Is.False);
            Assert.That(
                DetoxCycle.IsDetoxifiable(impostor, NoChemicals(), cureIncurableAddictions: true),
                Is.False);
        }

        [Test]
        public void TheSettingCannotReachABiotechChemicalDependency()
        {
            // The one that kills a pawn if it is ever wrong, so it is asserted directly rather than
            // left to follow from the test above.
            //
            // A gene-driven chemical dependency is excluded twice over in the shipped game: it sets
            // everCurableByItem false AND its hediff class is Hediff_ChemicalDependency, which is a
            // SIBLING of Hediff_Addiction under HediffWithComps rather than a subclass of it. The
            // setting relaxes only the flag, so the type test still carries the exclusion on its
            // own. Removing one of these is fatal: the gene re-adds it and the pawn dies without
            // the drug.
            var dependency = new Hediff_ChemicalDependency
            {
                def = HediffDef("GeneticDrugNeed", curable: false)
            };

            Assert.That(
                DetoxCycle.IsCurableAddiction(dependency, cureIncurableAddictions: true), Is.False,
                "A chemical dependency became curable. This kills pawns.");
            Assert.That(
                DetoxCycle.IsDetoxifiable(dependency, NoChemicals(), cureIncurableAddictions: true),
                Is.False,
                "A chemical dependency became detoxifiable. This kills pawns.");
        }

        [Test]
        public void ChemicalDependencyIsASiblingOfAddictionRatherThanASubclass()
        {
            // The structural fact the test above rests on, asserted so that it fails loudly if a
            // future RimWorld ever reparents the class. If this becomes a subclass, the type test
            // stops excluding chemical dependencies and the setting above becomes lethal.
            Assert.That(
                typeof(Hediff_Addiction).IsAssignableFrom(typeof(Hediff_ChemicalDependency)),
                Is.False,
                "Hediff_ChemicalDependency now derives from Hediff_Addiction. The permanent "
                + "addiction setting must gain an explicit exclusion for it immediately.");
        }

        [Test]
        public void TheSettingChangesNothingAboutTolerances()
        {
            // Tolerance never consulted everCurableByItem, so the setting has no business moving it.
            HediffDef tolerance = HediffDef("AmbrosiaTolerance", curable: true);
            var chemicals = new List<ChemicalDef> { new ChemicalDef { toleranceHediff = tolerance } };
            var hediff = new Hediff { def = tolerance };

            Assert.That(
                DetoxCycle.IsDetoxifiable(hediff, chemicals, cureIncurableAddictions: false), Is.True);
            Assert.That(
                DetoxCycle.IsDetoxifiable(hediff, chemicals, cureIncurableAddictions: true), Is.True);
        }

        [Test]
        public void WithTheSettingOffTheBehaviourIsExactlyWhatShipped()
        {
            // The regression guard for 1,706 existing subscribers. Whatever the toggle does, the
            // default must leave the mod behaving as it did before the toggle existed.
            Assert.That(
                DetoxCycle.IsCurableAddiction(
                    Addiction("LuciferiumAddiction", curable: false), cureIncurableAddictions: false),
                Is.False);
            Assert.That(
                DetoxCycle.IsCurableAddiction(
                    Addiction("AlcoholAddiction", curable: true), cureIncurableAddictions: false),
                Is.True);
        }

        private static List<ChemicalDef> NoChemicals()
        {
            return new List<ChemicalDef>();
        }

        private static HediffDef HediffDef(string defName, bool curable)
        {
            return new HediffDef { defName = defName, everCurableByItem = curable };
        }

        private static Hediff_Addiction Addiction(string defName, bool curable)
        {
            return new Hediff_Addiction { def = HediffDef(defName, curable) };
        }
    }
}
