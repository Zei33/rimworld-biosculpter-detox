using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BiosculpterDetox.Core;
using NUnit.Framework;

namespace BiosculpterDetox.Tests
{
    /// <summary>
    /// Holds the facts about the shipped game that the detox predicates rest on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This fixture used to assert that three hardcoded defName lists in <c>DetoxCycle</c> were
    /// dead: it pinned the bug rather than the fix, deliberately, so that repairing it would show
    /// up as a red test rather than as a silent no-op. That worked. The lists are gone, those tests
    /// went red, and what is left here is their evidence rather than their subject.
    /// </para>
    /// <para>
    /// The names are kept as literals in this file now, because they are historical: they are what
    /// the mod used to look for, and the assertion is that the game has never declared any of them
    /// as a <c>HediffDef</c>. The type restriction is the load-bearing part and has a test of its
    /// own, since six of the eight withdrawal names DO exist as <c>ThoughtDef</c>s and an untyped
    /// check would have reported the dead list as healthy.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class DefValidationTests
    {
        /// <summary>
        /// The defName lists this mod used to match hediffs against, all of them now deleted.
        /// </summary>
        private static readonly string[] RetiredLists =
        {
            "DetoxifiableAddictions", "DetoxifiableWithdrawals", "NonDetoxifiableAddictions"
        };

        /// <summary>
        /// The names the old addiction allow-list held, none of which has ever been a def.
        /// </summary>
        private static readonly string[] NamesTheOldListHeld =
        {
            "Addiction_Alcohol", "Addiction_Smokeleaf", "Addiction_Psychite", "Addiction_WakeUp",
            "Addiction_GoJuice", "Addiction_Flake", "Addiction_Yayo", "Addiction_Beer",
            "Addiction_Ambrosia"
        };

        /// <summary>
        /// The names the old withdrawal allow-list held.
        /// </summary>
        private static readonly string[] WithdrawalNamesTheOldListHeld =
        {
            "AlcoholWithdrawal", "SmokeleafWithdrawal", "PsychiteWithdrawal", "WakeUpWithdrawal",
            "GoJuiceWithdrawal", "FlakeWithdrawal", "YayoWithdrawal", "AmbrosiaWithdrawal"
        };

        [Test]
        public void TheGameShipsHediffDefsAtAll()
        {
            // Guards the parser rather than the mod. If this is empty every other test here passes
            // vacuously, which would be the worst possible failure mode for a validation suite.
            Assert.That(GameDefs.OfType("HediffDef").Count, Is.GreaterThan(100));
        }

        [Test]
        public void NoneOfTheNamesTheOldAllowListHeldWasEverAHediffDef()
        {
            // Kept after the list itself was deleted, because it is the evidence for the deletion
            // rather than a description of it. All nine entries were written Addiction_Alcohol;
            // every real addiction hediff is written the other way round, AlcoholAddiction, suffix
            // rather than prefix and no underscore. The list matched nothing for the whole life of
            // the mod, and every cure ran through the substring fallback beside it.
            var hediffs = GameDefs.OfType("HediffDef");
            var live = NamesTheOldListHeld.Where(hediffs.Contains).ToList();

            Assert.That(live, Is.Empty,
                "One of the names the dead allow-list held now exists as a HediffDef, which would "
                + "mean RimWorld renamed its addiction hediffs. Check the predicate still holds.");
        }

        [Test]
        public void TheDefNameListsAreGoneAndMustStayGone()
        {
            // The fix for issues #4 and #5 was to stop matching defNames as strings at all. A
            // reader adding an allow-list back would be reintroducing the whole defect class, so
            // the absence is asserted rather than left to reviewer memory.
            foreach (string name in RetiredLists)
            {
                Assert.That(
                    typeof(DetoxCycle).GetField(name, BindingFlags.NonPublic | BindingFlags.Static
                        | BindingFlags.Public | BindingFlags.Instance),
                    Is.Null,
                    name + " is back. Matching hediffs by defName is what issues #4 and #5 removed: "
                    + "a defName is a namespace other mods write into, so a substring test over it "
                    + "is unbounded, and an allow-list of exact names is dead the moment it is "
                    + "spelled wrong, which is what happened here for a year.");
            }
        }

        [Test]
        public void NoneOfTheWithdrawalNamesWasEverAHediffDefEither()
        {
            var hediffs = GameDefs.OfType("HediffDef");
            var live = WithdrawalNamesTheOldListHeld.Where(hediffs.Contains).ToList();

            Assert.That(live, Is.Empty, "A withdrawal name started existing as a HediffDef.");
        }

        [Test]
        public void TheUntypedCheckWouldCertifyTheDeadWithdrawalListAsHealthy()
        {
            // This is why the checks above are type-specific, and it is worth a test of its own.
            // Six of the eight withdrawal names do exist in the game's global defName set, as
            // ThoughtDefs. A validation test that compared against every defName rather than
            // against HediffDef would pass on this list and report the bug as healthy.
            var anyType = GameDefs.AnyType();
            var withdrawals = WithdrawalNamesTheOldListHeld;

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
        public void LuciferiumIsExcludedByTheGamesOwnFlagRatherThanByItsName()
        {
            // The exclusion used to be the string "LuciferiumAddiction" in a deny-list. It is now
            // everCurableByItem, which is what Ludeon sets on that def and what the vanilla
            // biosculpter healing cycle gates on. This asserts the premise the new predicate rests
            // on: that the shipped def really does declare itself incurable. If Ludeon ever drops
            // the flag, the predicate silently starts curing luciferium and this is the test that
            // says so.
            Assert.That(GameDefs.OfType("HediffDef"), Does.Contain("LuciferiumAddiction"));

            Assert.That(
                GameDefs.DeclaresIncurable("LuciferiumAddiction"), Is.True,
                "LuciferiumAddiction no longer sets everCurableByItem false, so the detox cycle "
                + "will now cure it. That is a balance change two players have asked for and one "
                + "has asked against, so it is a decision rather than a bug, but it must not "
                + "happen by accident.");
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
