using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BiosculpterDetox.Core;
using NUnit.Framework;

namespace BiosculpterDetox.Tests
{
    /// <summary>
    /// Checks the hardcoded defNames in <see cref="DetoxCycle"/> against the defs the game actually
    /// ships, as <c>HediffDef</c> specifically.
    /// </summary>
    /// <remarks>
    /// The mod walks <c>pawn.health.hediffSet.hediffs</c> and compares <c>hediff.def.defName</c>
    /// against two hardcoded lists. A name that is not a real <c>HediffDef</c> can therefore never
    /// match anything, no matter how plausible it looks.
    ///
    /// These tests pin what is true today rather than what ought to be true. Every list in this
    /// class is currently dead, and the tests say so out loud, so that fixing it produces a visible
    /// red test rather than a silent no-op.
    /// </remarks>
    [TestFixture]
    public class DefValidationTests
    {
        private static List<string> ListField(string name)
        {
            var field = typeof(DetoxCycle).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, name + " was renamed or removed from DetoxCycle.");
            return (List<string>)field.GetValue(null);
        }

        [Test]
        public void TheGameShipsHediffDefsAtAll()
        {
            // Guards the parser rather than the mod. If this is empty every other test here passes
            // vacuously, which would be the worst possible failure mode for a validation suite.
            Assert.That(GameDefs.OfType("HediffDef").Count, Is.GreaterThan(100));
        }

        [Test]
        public void NotOneDetoxifiableAddictionNameIsARealHediffDef()
        {
            // All nine entries are written Addiction_Alcohol. Every real addiction hediff is written
            // the other way round, AlcoholAddiction: suffix, not prefix, and no underscore. So the
            // list matches nothing and has never done anything.
            var hediffs = GameDefs.OfType("HediffDef");
            var live = ListField("DetoxifiableAddictions").Where(hediffs.Contains).ToList();

            Assert.That(live, Is.Empty, "A name started matching. Update this test and issue #4.");
        }

        [Test]
        public void NotOneDetoxifiableWithdrawalNameIsARealHediffDef()
        {
            var hediffs = GameDefs.OfType("HediffDef");
            var live = ListField("DetoxifiableWithdrawals").Where(hediffs.Contains).ToList();

            Assert.That(live, Is.Empty, "A name started matching. Update this test.");
        }

        [Test]
        public void TheUntypedCheckWouldCertifyTheDeadWithdrawalListAsHealthy()
        {
            // This is why the checks above are type-specific, and it is worth a test of its own.
            // Six of the eight withdrawal names do exist in the game's global defName set, as
            // ThoughtDefs. A validation test that compared against every defName rather than
            // against HediffDef would pass on this list and report the bug as healthy.
            var anyType = GameDefs.AnyType();
            var withdrawals = ListField("DetoxifiableWithdrawals");

            var existSomewhere = withdrawals.Where(anyType.Contains).ToList();
            var existAsHediff = withdrawals.Where(GameDefs.OfType("HediffDef").Contains).ToList();

            Assert.That(existSomewhere, Is.Not.Empty, "The untyped check no longer gives a false pass.");
            Assert.That(existAsHediff, Is.Empty);
            Assert.That(GameDefs.TypesDeclaring("AlcoholWithdrawal"), Does.Contain("ThoughtDef"));
        }

        [Test]
        public void RimWorldModelsWithdrawalAsAThoughtAndNotAsAHediffAtAll()
        {
            // The finding underneath the dead list, and a larger one. There is no withdrawal hediff
            // in the game, so the EndsWith("Withdrawal") fallback in DetoxCycle cannot fire either.
            // The withdrawal half of this mod has never been able to remove anything.
            var withdrawalHediffs = GameDefs.OfType("HediffDef").Where(n => n.Contains("Withdrawal")).ToList();

            Assert.That(withdrawalHediffs, Is.Empty);
        }

        [Test]
        public void TheEndsWithAddictionFallbackIsWhatActuallyCarriesTheFeature()
        {
            // Every vanilla addiction is caught, by the generic pattern rather than by the list.
            // This is the only reason the mod works at all.
            var addictionHediffs = GameDefs.OfType("HediffDef").Where(n => n.EndsWith("Addiction")).ToList();

            Assert.That(addictionHediffs.Count, Is.EqualTo(7));
            Assert.That(addictionHediffs, Does.Contain("LuciferiumAddiction"));
        }

        [Test]
        public void TheStartsWithAddictionUnderscoreFallbackMatchesNothingInVanilla()
        {
            var prefixed = GameDefs.OfType("HediffDef").Where(n => n.StartsWith("Addiction_")).ToList();

            Assert.That(prefixed, Is.Empty);
        }

        [Test]
        public void TheOnlyNonDetoxifiableAddictionIsRealAndIsCaughtByTheFallback()
        {
            // This one name does have to be right, because it is the exclusion that stops the mod
            // curing Luciferium. Unlike the other two lists, it is live.
            var nonDetoxifiable = ListField("NonDetoxifiableAddictions");

            Assert.That(nonDetoxifiable, Is.EqualTo(new[] { "LuciferiumAddiction" }));
            Assert.That(GameDefs.OfType("HediffDef"), Does.Contain("LuciferiumAddiction"));
            Assert.That("LuciferiumAddiction".EndsWith("Addiction"), Is.True);
        }

        [Test]
        public void NoToleranceHediffMentionsLuciferium()
        {
            // RemoveDrugTolerances excludes any tolerance whose name contains Luciferium. There is
            // no such hediff: Luciferium has an addiction but no tolerance. The guard is dead, and
            // the unanchored Contains("Tolerance") it guards is issue #5.
            var tolerances = GameDefs.OfType("HediffDef").Where(n => n.Contains("Tolerance")).ToList();

            Assert.That(tolerances.Count, Is.EqualTo(7));
            Assert.That(tolerances.Where(n => n.Contains("Luciferium")), Is.Empty);
        }
    }
}
