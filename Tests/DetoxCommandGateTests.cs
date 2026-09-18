using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using BiosculpterDetox.Core;
using NUnit.Framework;
using UnityEngine;
using Verse;

namespace BiosculpterDetox.Tests
{
    /// <summary>
    /// Covers the decisions behind disabling a biotuned pod's refused detox button.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The patch that calls these cannot run here, and neither can a pod, so these tests hold the
    /// decisions, the split of the pod's cycles and the wrapper, and nothing else. What joins them
    /// to the game (the type test and the icon read the patch hands the split, the biotuned pawn it
    /// reads, and the two methods that build the row reason) is in-game check 14 in
    /// <c>Tests/README.md</c>. How the patch binds to the pod is <c>PatchTargetTests</c>.
    /// </para>
    /// <para>
    /// Commands are built with <c>FormatterServices.GetUninitializedObject</c> because
    /// <c>new Command_Action()</c> throws in the harness: <c>Command</c>'s static constructor loads
    /// a texture through <c>ContentFinder</c>, which reaches native Unity. An uninitialised command
    /// has every field at its default, which is exactly the vanilla state that matters here
    /// (enabled, no reason) until a test sets otherwise. Icons are uninitialised textures for the
    /// same reason.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class DetoxCommandGateTests
    {
        private const string Reason = "Nothing this cycle can treat";

        [Test]
        public void ItDisablesTheDetoxButtonWithTheReasonItIsGivenAndTouchesNothingElse()
        {
            Texture2D medicIcon = Icon(), pleasureIcon = Icon(), detoxIcon = Icon();
            Command_Action medic = Command(medicIcon);
            Command_Action pleasure = Command(pleasureIcon);
            Command_Action detox = Command(detoxIcon);
            Command_Action dev = Command(null);
            var gizmos = new List<Gizmo> { medic, pleasure, detox, dev };

            List<Gizmo> passed = DetoxCommandGate
                .DisableRefusedDetoxCommand(gizmos, detoxIcon, new object[] { medicIcon, pleasureIcon }, () => Reason)
                .ToList();

            // The whole sequence, by reference: every gizmo once, in vanilla's order.
            Assert.That(passed, Is.EqualTo(gizmos));

            Assert.That(detox.Disabled, Is.True);
            Assert.That(detox.disabledReason, Is.SameAs(Reason),
                "The reason must reach the button verbatim, so the button, the pawn's row and the "
                + "right-click menu all say the same thing.");

            foreach (Command_Action other in new[] { medic, pleasure, dev })
            {
                Assert.That(other.Disabled, Is.False);
                Assert.That(other.disabledReason, Is.Null);
            }
        }

        [Test]
        public void AReasonVanillaAlreadyGaveWinsAndTheRefusalIsNeverAsked()
        {
            Texture2D detoxIcon = Icon();
            Command_Action detox = Command(detoxIcon);
            detox.Disable("No power");
            int asked = 0;

            DetoxCommandGate
                .DisableRefusedDetoxCommand(new List<Gizmo> { detox }, detoxIcon, new object[0], () => { asked++; return Reason; })
                .ToList();

            Assert.That(detox.disabledReason, Is.EqualTo("No power"));
            Assert.That(asked, Is.EqualTo(0));
        }

        [Test]
        public void AUsableCycleIsLeftEnabled()
        {
            Texture2D detoxIcon = Icon();
            Command_Action detox = Command(detoxIcon);

            DetoxCommandGate
                .DisableRefusedDetoxCommand(new List<Gizmo> { detox }, detoxIcon, new object[0], () => null)
                .ToList();

            Assert.That(detox.Disabled, Is.False);
            Assert.That(detox.disabledReason, Is.Null);
        }

        [Test]
        public void NothingIsReadOrDecidedUntilTheGizmosAreWalked()
        {
            // The postfix receives vanilla's iterator before vanilla has built a single button.
            // Anything this did at call time would read buttons that do not exist yet, so the
            // source and the refusal must both be untouched until the caller walks the result.
            Texture2D detoxIcon = Icon();
            var source = new CountingGizmos(new List<Gizmo> { Command(detoxIcon) });
            int asked = 0;

            IEnumerable<Gizmo> wrapped = DetoxCommandGate.DisableRefusedDetoxCommand(
                source, detoxIcon, new object[0], () => { asked++; return Reason; });

            Assert.That(source.Enumerations, Is.EqualTo(0));
            Assert.That(asked, Is.EqualTo(0));

            wrapped.ToList();

            Assert.That(source.Enumerations, Is.EqualTo(1));
            Assert.That(asked, Is.EqualTo(1));
        }

        [Test]
        public void OnlyTheFirstButtonCarryingTheIconIsTheDetoxButton()
        {
            // Vanilla yields every cycle button before anything else, so a later gizmo carrying
            // the same texture belongs to something else and must be left alone. The refusal is
            // asked once per pass, not once per match.
            Texture2D detoxIcon = Icon();
            Command_Action detox = Command(detoxIcon);
            Command_Action lookalike = Command(detoxIcon);
            int asked = 0;

            DetoxCommandGate
                .DisableRefusedDetoxCommand(
                    new List<Gizmo> { detox, lookalike }, detoxIcon, new object[0], () => { asked++; return Reason; })
                .ToList();

            Assert.That(detox.Disabled, Is.True);
            Assert.That(lookalike.Disabled, Is.False);
            Assert.That(asked, Is.EqualTo(1));
        }

        [Test]
        public void AVanillaDisabledDetoxButtonStillEndsTheSearch()
        {
            // The first match is the detox button whatever state vanilla left it in. If a disabled
            // one did not end the search, a later lookalike would be taken for it and greyed with
            // this mod's reason.
            Texture2D detoxIcon = Icon();
            Command_Action detox = Command(detoxIcon);
            detox.Disable("No power");
            Command_Action lookalike = Command(detoxIcon);
            int asked = 0;

            DetoxCommandGate
                .DisableRefusedDetoxCommand(
                    new List<Gizmo> { detox, lookalike }, detoxIcon, new object[0], () => { asked++; return Reason; })
                .ToList();

            Assert.That(detox.disabledReason, Is.EqualTo("No power"));
            Assert.That(lookalike.Disabled, Is.False);
            Assert.That(asked, Is.EqualTo(0));
        }

        [Test]
        public void AnEnabledDetoxButtonThatIsNotRefusedStillEndsTheSearch()
        {
            // Asked at most once per pass, whatever the answer. A null answer that left the search
            // open would ask again for a lookalike, and could grey it.
            Texture2D detoxIcon = Icon();
            Command_Action detox = Command(detoxIcon);
            Command_Action lookalike = Command(detoxIcon);
            int asked = 0;

            DetoxCommandGate
                .DisableRefusedDetoxCommand(
                    new List<Gizmo> { detox, lookalike }, detoxIcon, new object[0], () => { asked++; return null; })
                .ToList();

            Assert.That(detox.Disabled, Is.False);
            Assert.That(lookalike.Disabled, Is.False);
            Assert.That(asked, Is.EqualTo(1));
        }

        [Test]
        public void TheSplitNeverPutsTheDetoxIconAmongTheOthers()
        {
            // The failure this guards is silent in game: with the detox icon among the others,
            // IsDistinctIcon says it identifies nothing, the gate stands down, and every refused
            // button is enabled again, with no error logged anywhere.
            FakeCycle medic = Cycle("medic"), detox = Cycle("detox"), pleasure = Cycle("pleasure");

            FakeCycle found = DetoxCommandGate.SplitCycles(
                new[] { medic, detox, pleasure }, c => c.Name == "detox", c => c.Icon, out List<object> others);

            Assert.That(found, Is.SameAs(detox));
            Assert.That(others, Is.EqualTo(new[] { medic.Icon, pleasure.Icon }),
                "The others must be every other cycle's icon, each once, in order.");
            Assert.That(DetoxCommandGate.IsDistinctIcon(found.Icon, others), Is.True,
                "The split left the gate unable to identify the detox button.");
        }

        [Test]
        public void ASecondDetoxCycleMakesTheGateStandDown()
        {
            // Two detox cycles load one texture, so the icon cannot tell their buttons apart. The
            // first is the one returned; the second's icon goes among the others, which is what
            // stops the gate guessing.
            object shared = new object();
            FakeCycle first = Cycle("detox", shared), medic = Cycle("medic"), second = Cycle("detox", shared);

            FakeCycle found = DetoxCommandGate.SplitCycles(
                new[] { first, medic, second }, c => c.Name == "detox", c => c.Icon, out List<object> others);

            Assert.That(found, Is.SameAs(first));
            Assert.That(others, Is.EqualTo(new[] { medic.Icon, shared }));
            Assert.That(DetoxCommandGate.IsDistinctIcon(found.Icon, others), Is.False);
        }

        [Test]
        public void APodWithoutTheDetoxCycleGivesNothingToGate()
        {
            FakeCycle medic = Cycle("medic"), pleasure = Cycle("pleasure");

            Assert.That(
                DetoxCommandGate.SplitCycles(
                    new[] { medic, pleasure }, c => c.Name == "detox", c => c.Icon, out List<object> others),
                Is.Null);
            Assert.That(others, Is.EqualTo(new[] { medic.Icon, pleasure.Icon }));

            Assert.That(
                DetoxCommandGate.SplitCycles<FakeCycle>(null, c => true, c => c.Icon, out List<object> none),
                Is.Null);
            Assert.That(none, Is.Not.Null.And.Empty);
        }

        [Test]
        public void AMissingIconMatchesNothingAndAboveAllNotTheDevButtons()
        {
            // ContentFinder returns null when a texture fails to load, and every DEV button on the
            // pod has a null icon. Without the guard a missing detox icon would be "found" on the
            // first DEV button and disable it with this mod's reason.
            Command_Action detox = Command(null);
            Command_Action dev = Command(null);
            int asked = 0;

            DetoxCommandGate
                .DisableRefusedDetoxCommand(new List<Gizmo> { detox, dev }, null, new object[0], () => { asked++; return Reason; })
                .ToList();

            Assert.That(detox.Disabled, Is.False);
            Assert.That(dev.Disabled, Is.False);
            Assert.That(asked, Is.EqualTo(0));
        }

        [Test]
        public void ADeadTextureIsNotMistakenForNoTexture()
        {
            // The premise, asserted rather than assumed: Unity's overloaded equality calls a
            // texture with no native object equal to null. An uninitialised one has none, which is
            // the state a destroyed texture is in. If this ever stops holding, the test below it
            // stops testing anything, so it must fail rather than skip.
            Texture2D dead = Icon();
            Assert.That(dead == null, Is.True, "Unity no longer treats a dead texture as null.");

            Command_Action dev = Command(null);

            DetoxCommandGate
                .DisableRefusedDetoxCommand(new List<Gizmo> { dev }, dead, new object[0], () => Reason)
                .ToList();

            Assert.That(dev.Disabled, Is.False,
                "A dead detox icon matched a DEV button's null icon, so the icon is being compared "
                + "with Unity's equality rather than by reference.");
        }

        [Test]
        public void AnIconSharedWithAnotherCycleIdentifiesNothing()
        {
            // Two cycles resolving the same texture means the icon cannot say which button is
            // which, so the gate stands down rather than guess.
            Texture2D shared = Icon();
            Command_Action other = Command(shared);
            Command_Action detox = Command(shared);
            int asked = 0;

            DetoxCommandGate
                .DisableRefusedDetoxCommand(
                    new List<Gizmo> { other, detox }, shared, new object[] { shared }, () => { asked++; return Reason; })
                .ToList();

            Assert.That(other.Disabled, Is.False);
            Assert.That(detox.Disabled, Is.False);
            Assert.That(asked, Is.EqualTo(0));
        }

        [Test]
        public void OnlyAnActionButtonCanBeTheCycleButton()
        {
            // Vanilla builds every cycle button as a Command_Action. A toggle carrying the icon is
            // somebody else's gizmo.
            Texture2D detoxIcon = Icon();
            var toggle = (Command_Toggle)FormatterServices.GetUninitializedObject(typeof(Command_Toggle));
            toggle.icon = detoxIcon;

            DetoxCommandGate
                .DisableRefusedDetoxCommand(new List<Gizmo> { toggle }, detoxIcon, new object[0], () => Reason)
                .ToList();

            Assert.That(toggle.Disabled, Is.False);
        }

        [Test]
        public void IconsAreComparedByReferenceNotByEquality()
        {
            var icon = new AlwaysEqual();
            var twin = new AlwaysEqual();

            Assert.That(icon.Equals(twin), Is.True, "The control: the twin really is Equals-equal.");
            Assert.That(DetoxCommandGate.IsDistinctIcon(icon, new object[] { twin }), Is.True);
            Assert.That(DetoxCommandGate.IsDistinctIcon(icon, new object[] { icon }), Is.False);
            Assert.That(DetoxCommandGate.IsDistinctIcon(icon, null), Is.True);
            Assert.That(DetoxCommandGate.IsDistinctIcon(null, new object[0]), Is.False);
        }

        [Test]
        public void AnUnbiotunedPodIsLeftToVanilla()
        {
            // Its button opens a menu of every colonist, where a refused row is greyed and has
            // nothing to invoke, so there is no crash to prevent.
            Assert.That(DetoxCommandGate.BiotunedRefusal(false, true, Unasked), Is.Null);
            Assert.That(DetoxCommandGate.BiotunedRefusal(false, false, Unasked), Is.Null);
        }

        [Test]
        public void ABiotunedPawnWhoIsNotHereIsLeftToVanilla()
        {
            // Vanilla disables the button itself in that case, with "Biotuned to ... and ... isn't
            // around", and it tests exactly this before it looks at the pawn's row.
            Assert.That(DetoxCommandGate.BiotunedRefusal(true, false, Unasked), Is.Null);
        }

        [Test]
        public void ABiotunedPawnWhoIsHereGetsTheRowsReasonVerbatim()
        {
            Assert.That(DetoxCommandGate.BiotunedRefusal(true, true, () => Reason), Is.SameAs(Reason));
            Assert.That(DetoxCommandGate.BiotunedRefusal(true, true, () => null), Is.Null);
            Assert.That(DetoxCommandGate.BiotunedRefusal(true, true, null), Is.Null);
        }

        private static string Unasked()
        {
            throw new AssertionException("The row reason was computed when it could not matter.");
        }

        private static Texture2D Icon()
        {
            return (Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D));
        }

        private static Command_Action Command(Texture icon)
        {
            var command = (Command_Action)FormatterServices.GetUninitializedObject(typeof(Command_Action));
            command.icon = icon;
            return command;
        }

        private static FakeCycle Cycle(string name, object icon = null)
        {
            return new FakeCycle { Name = name, Icon = icon ?? new object() };
        }

        /// <summary>
        /// Stands in for a pod's cycle: a name to pick it out by and an icon compared by reference.
        /// </summary>
        private sealed class FakeCycle
        {
            public string Name;
            public object Icon;
        }

        /// <summary>
        /// Counts how many times it has been walked.
        /// </summary>
        private sealed class CountingGizmos : IEnumerable<Gizmo>
        {
            private readonly List<Gizmo> gizmos;

            public CountingGizmos(List<Gizmo> gizmos)
            {
                this.gizmos = gizmos;
            }

            public int Enumerations { get; private set; }

            public IEnumerator<Gizmo> GetEnumerator()
            {
                Enumerations++;
                return gizmos.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        /// <summary>
        /// Equal to every other instance, to show a comparison is by reference.
        /// </summary>
        private sealed class AlwaysEqual
        {
            public override bool Equals(object obj) => obj is AlwaysEqual;

            public override int GetHashCode() => 0;
        }
    }
}
