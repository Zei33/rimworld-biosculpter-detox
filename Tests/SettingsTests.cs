using System.Reflection;
using System.Xml;
using BiosculpterDetox.Core;
using NUnit.Framework;
using Verse;

namespace BiosculpterDetox.Tests
{
    /// <summary>
    /// Covers the settings object: its defaults, its clamping, and what it reads back out of a save.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The settings class is reachable here because it touches no game state. The window it draws is
    /// not: <c>DoSettingsWindowContents</c> needs IMGUI. So the values and the serialisation are
    /// covered and the drawing is on the in-game list, which is the same split Simple Improve uses.
    /// </para>
    /// <para>
    /// The load half of <c>Scribe</c> is driven by hand below, because
    /// <c>ScribeLoader.FinalizeLoading</c> is not reachable outside a game. Assigning
    /// <c>curXmlParent</c> and the mode directly is what vanilla's own loader does either side of
    /// <c>ExposeData</c>, and the mode is reset in a finally so that a failure here cannot leave
    /// every later test in the run inside <c>LoadingVars</c>.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class SettingsTests
    {
        [Test]
        public void AFreshInstallGetsTheBehaviourTheModShippedWith()
        {
            var settings = new BiosculpterDetoxSettings();

            Assert.That(settings.CureIncurableAddictions, Is.False,
                "The permanent addiction setting must default off, or an existing subscriber's "
                + "pods start curing luciferium the moment they update.");
            Assert.That(settings.CycleDurationDays,
                Is.EqualTo(BiosculpterDetoxSettings.DefaultCycleDurationDays));
            Assert.That(BiosculpterDetoxSettings.DefaultCycleDurationDays, Is.EqualTo(12f),
                "Twelve days is the duration this mod has always had. Changing the default changes "
                + "the game for every existing player who never opens the settings window.");
        }

        [Test]
        public void AnExistingSettingsFileWithoutTheNewKeysGetsTheSameDefaults()
        {
            // The trap this test exists for. Two different players reach the default by two
            // different routes: somebody with no settings file at all gets a fresh object and the
            // FIELD INITIALISER, because ReadModSettings never calls ExposeData on it, while
            // somebody whose file predates these keys gets the defaultValue argument passed to
            // Scribe_Values.Look. If those two numbers ever disagree the same build behaves
            // differently for the two of them and nothing reports it.
            //
            // The field is pushed off its initialiser first, so that reading the default back is
            // evidence the Scribe branch ran rather than evidence it did nothing.
            var settings = new BiosculpterDetoxSettings();
            SetDuration(settings, 99f);

            LoadInto(settings, "<somethingElse>1</somethingElse>");

            Assert.That(settings.CycleDurationDays,
                Is.EqualTo(BiosculpterDetoxSettings.DefaultCycleDurationDays),
                "An old settings file resolved the duration to something other than the default a "
                + "fresh install gets.");
            Assert.That(settings.CureIncurableAddictions, Is.False);
        }

        [Test]
        public void AStoredValueIsReadBackUnderTheNameItWasWrittenWith()
        {
            var settings = new BiosculpterDetoxSettings();

            LoadInto(
                settings,
                "<cureIncurableAddictions>True</cureIncurableAddictions>"
                + "<cycleDurationDays>7</cycleDurationDays>");

            Assert.That(settings.CureIncurableAddictions, Is.True);
            Assert.That(settings.CycleDurationDays, Is.EqualTo(7f));
        }

        [Test]
        public void AHandEditedConfigCannotPutTheDurationSomewhereTheGameCannotUse()
        {
            // The window cannot produce these; a text editor can. Zero is the one that matters: the
            // pod sets its countdown to durationDays * 60000 and the progress bar mote divides by
            // that same number, so zero completes on the first tick and divides by zero.
            var settings = new BiosculpterDetoxSettings();

            // Zero and below resolve to the DEFAULT, not the minimum, because zero is also what
            // Scribe hands back for a node that will not parse. Giving that player a one day cycle
            // with no explanation is worse than giving them the twelve they started with.
            LoadInto(settings, "<cycleDurationDays>0</cycleDurationDays>");
            Assert.That(settings.CycleDurationDays,
                Is.EqualTo(BiosculpterDetoxSettings.DefaultCycleDurationDays));

            LoadInto(settings, "<cycleDurationDays>-5</cycleDurationDays>");
            Assert.That(settings.CycleDurationDays,
                Is.EqualTo(BiosculpterDetoxSettings.DefaultCycleDurationDays));

            // Genuinely too small, as opposed to corrupt, so this one clamps.
            LoadInto(settings, "<cycleDurationDays>0.25</cycleDurationDays>");
            Assert.That(settings.CycleDurationDays,
                Is.EqualTo(BiosculpterDetoxSettings.MinimumCycleDurationDays));

            // The shape a corrupt config actually arrives in. Scribe_Values.Look forwards its
            // defaultValue only when the node is ABSENT; a present node carrying IsNull comes back
            // as default(float), which is zero, and the clamp is what turns that back into twelve.
            //
            // The sibling case, a node whose text will not parse, is NOT asserted here and cannot
            // be. ScribeExtractor.ValueFromNode calls Log.Error on that path before returning
            // default(T), and Log.Error throws MissingMethodException in this harness because it
            // reaches UnityEngine.Debug through a native call. So the assertion would fail on the
            // logging rather than on the value. It reaches the same zero by the same line, and it
            // is on the in-game list instead.
            LoadInto(settings, "<cycleDurationDays IsNull=\"true\" />");
            Assert.That(settings.CycleDurationDays,
                Is.EqualTo(BiosculpterDetoxSettings.DefaultCycleDurationDays),
                "A null duration node resolved to something other than the default.");

            LoadInto(settings, "<cycleDurationDays>9999</cycleDurationDays>");
            Assert.That(settings.CycleDurationDays,
                Is.EqualTo(BiosculpterDetoxSettings.MaximumCycleDurationDays));
        }

        [Test]
        public void ClampingHandlesTheValuesThatAreNotOrdinaryNumbers()
        {
            // Mathf.Clamp passes NaN straight through, so the finite test in ClampDuration is not
            // redundant with the clamp beside it. A NaN duration reaches the pod as a countdown that
            // no comparison is ever true for.
            Assert.That(BiosculpterDetoxSettings.ClampDuration(float.NaN),
                Is.EqualTo(BiosculpterDetoxSettings.DefaultCycleDurationDays));
            Assert.That(BiosculpterDetoxSettings.ClampDuration(float.PositiveInfinity),
                Is.EqualTo(BiosculpterDetoxSettings.DefaultCycleDurationDays));
            Assert.That(BiosculpterDetoxSettings.ClampDuration(float.NegativeInfinity),
                Is.EqualTo(BiosculpterDetoxSettings.DefaultCycleDurationDays));
        }

        [Test]
        public void TheShippedDurationIsInsideTheRangeTheSettingsAllow()
        {
            // Otherwise the default is a value the player can never return to once they move away
            // from it, which is the defect Simple Improve's material cost field had.
            Assert.That(
                BiosculpterDetoxSettings.ClampDuration(BiosculpterDetoxSettings.DefaultCycleDurationDays),
                Is.EqualTo(BiosculpterDetoxSettings.DefaultCycleDurationDays));
            Assert.That(
                BiosculpterDetoxSettings.MinimumCycleDurationDays,
                Is.LessThan(BiosculpterDetoxSettings.MaximumCycleDurationDays));
        }

        [Test]
        public void TheMinimumDurationCannotBeZero()
        {
            // Stated as its own assertion because the reason is arithmetic rather than balance, and
            // a later change to the range would otherwise look free.
            Assert.That(BiosculpterDetoxSettings.MinimumCycleDurationDays, Is.GreaterThan(0f),
                "A zero duration divides by zero in the pod's progress bar and completes the cycle "
                + "on its first tick.");
        }

        private static void LoadInto(BiosculpterDetoxSettings settings, string innerXml)
        {
            var document = new XmlDocument();
            document.LoadXml("<settings>" + innerXml + "</settings>");

            try
            {
                Scribe.loader.curXmlParent = document.DocumentElement;
                Scribe.mode = LoadSaveMode.LoadingVars;
                settings.ExposeData();
            }
            finally
            {
                Scribe.mode = LoadSaveMode.Inactive;
                Scribe.loader.curXmlParent = null;
            }
        }

        private static void SetDuration(BiosculpterDetoxSettings settings, float days)
        {
            FieldInfo field = typeof(BiosculpterDetoxSettings)
                .GetField("cycleDurationDays", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null,
                "The private duration field was renamed. This test reaches it by name to push it "
                + "off its initialiser, and without that the default assertion proves nothing.");

            field.SetValue(settings, days);
        }
    }
}
