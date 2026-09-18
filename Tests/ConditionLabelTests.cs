using System.Collections.Generic;
using NUnit.Framework;
using RimWorld;
using BiosculpterDetox.Core;
using Verse;

namespace BiosculpterDetox.Tests
{
    /// <summary>
    /// Covers the one method that turns treated conditions into the names the player reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The "Will treat" preview and the "Cured" letter both take their lists from
    /// <see cref="DetoxCycle.ConditionLabels"/>. They used to read <c>Hediff.LabelCap</c> each,
    /// which carries the hediff's live bracket, so the two could name one hediff two ways.
    /// </para>
    /// <para>
    /// What is pinned here is the naming. Both callers take a <c>Pawn</c> and are out of reach,
    /// so nothing here holds either of them to calling this method; that is the same gap the
    /// repo records for the settings.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class ConditionLabelTests
    {
        [Test]
        public void AConditionIsNamedByItsDefRatherThanItsCurrentStage()
        {
            StagedHediff tolerance = Tolerance();

            // The controls make the fixture prove something: without a bracket on the live label,
            // the assertions after them would pass against the old code too.
            tolerance.SetSeverityDirectly(0.5f);
            Assert.That(tolerance.LabelCap, Is.EqualTo("Psychite tolerance (large)"),
                "The fixture no longer carries a stage bracket, so this test proves nothing.");
            Assert.That(DetoxCycle.ConditionLabels(new Hediff[] { tolerance }),
                Is.EqualTo(new[] { "Psychite tolerance" }));

            // The drift itself: the stage word changes and the name must not.
            tolerance.SetSeverityDirectly(0.3f);
            Assert.That(tolerance.LabelCap, Is.EqualTo("Psychite tolerance (small)"),
                "The fixture's stage did not move, so this half does not exercise the drift.");
            Assert.That(DetoxCycle.ConditionLabels(new Hediff[] { tolerance }),
                Is.EqualTo(new[] { "Psychite tolerance" }));
        }

        [Test]
        public void AStageThatRenamesTheConditionDoesNotRenameItInTheList()
        {
            // Why the def label rather than Hediff.LabelBaseCap, which also drops the bracket. A
            // stage may carry an overrideLabel, and LabelBase switches to it as severity moves, so
            // it would bring back the same drift under another name. No shipped tolerance or
            // addiction sets one, but the nearest shipped relative does: Biotech's chemical
            // dependency renames itself "deficiency coma" at its last stage.
            HediffDef def = Def("SomeModdedTolerance", "some tolerance");
            def.stages = new List<HediffStage>
            {
                new HediffStage { minSeverity = 0f },
                new HediffStage { minSeverity = 0.5f, overrideLabel = "severe tolerance" }
            };
            var hediff = new StagedHediff { def = def };
            hediff.SetSeverityDirectly(0.6f);

            Assert.That(hediff.LabelBaseCap, Is.EqualTo("Severe tolerance"),
                "The fixture's stage no longer renames the hediff, so this test proves nothing.");
            Assert.That(DetoxCycle.ConditionLabels(new Hediff[] { hediff }),
                Is.EqualTo(new[] { "Some tolerance" }));
        }

        [Test]
        public void EachConditionIsNamedOnceInTheOrderFound()
        {
            // The fixture has to tell first-seen order apart from every other order a plausible
            // bug would produce, or it pins only the de-duplication. Its first-seen order is not
            // alphabetical, the list is not a palindrome (so walking it backwards differs), and the
            // repeat is not the last entry (so keeping each name's last occurrence differs too).
            HediffDef psychite = Def("PsychiteTolerance", "psychite tolerance");
            HediffDef alcohol = Def("AlcoholAddiction", "alcohol addiction");
            HediffDef smokeleaf = Def("SmokeleafAddiction", "smokeleaf addiction");

            List<string> labels = DetoxCycle.ConditionLabels(new Hediff[]
            {
                new HediffWithComps { def = psychite },
                new Hediff_Addiction { def = alcohol },
                new HediffWithComps { def = psychite },
                new Hediff_Addiction { def = smokeleaf }
            });

            Assert.That(labels, Is.EqualTo(new[] { "Psychite tolerance", "Alcohol addiction", "Smokeleaf addiction" }));
        }

        [Test]
        public void ADefWithNoLabelIsNamedByItsDefNameRatherThanDropped()
        {
            // A removed condition has to appear in the letter. Dropping it would also make a
            // cure of only unlabelled defs report as though nothing had been removed.
            var unlabelled = new HediffWithComps { def = Def("SomeModdedTolerance", label: null) };

            Assert.That(DetoxCycle.ConditionLabels(new Hediff[] { unlabelled }),
                Is.EqualTo(new[] { "SomeModdedTolerance" }));
        }

        [Test]
        public void NothingToNameGivesAnEmptyList()
        {
            Assert.That(DetoxCycle.ConditionLabels(null), Is.Empty);
            Assert.That(DetoxCycle.ConditionLabels(new List<Hediff>()), Is.Empty);
            Assert.That(DetoxCycle.ConditionLabels(new Hediff[] { null, new Hediff() }), Is.Empty);
        }

        private static StagedHediff Tolerance()
        {
            // The shape of the shipped DrugToleranceBase: HediffWithComps, three stages whose labels
            // are the words the Health tab shows in brackets.
            HediffDef def = Def("PsychiteTolerance", "psychite tolerance");
            def.stages = new List<HediffStage>
            {
                new HediffStage { minSeverity = 0f, label = "small" },
                new HediffStage { minSeverity = 0.5f, label = "large" },
                new HediffStage { minSeverity = 0.8f, label = "massive" }
            };

            return new StagedHediff { def = def };
        }

        private static HediffDef Def(string defName, string label)
        {
            return new HediffDef { defName = defName, label = label };
        }

        /// <summary>
        /// A hediff whose severity can be set without a pawn.
        /// </summary>
        /// <remarks>
        /// The <c>Severity</c> setter notifies the pawn's health tracker when the stage changes,
        /// and there is no pawn here, so the test writes the field directly.
        /// </remarks>
        private sealed class StagedHediff : HediffWithComps
        {
            public void SetSeverityDirectly(float severity)
            {
                severityInt = severity;
            }
        }
    }
}
