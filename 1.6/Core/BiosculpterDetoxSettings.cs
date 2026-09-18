using RimWorld;
using UnityEngine;
using Verse;

namespace BiosculpterDetox.Core
{
    /// <summary>
    /// Player-configurable settings for the detox cycle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two settings, both of which were previously hardcoded and both of which are the subject of
    /// standing player requests: how long the cycle takes, and whether it will treat an addiction
    /// the game declares permanent.
    /// </para>
    /// <para>
    /// Nothing in this class reads the settings on behalf of the detox logic, and that is
    /// deliberate. The predicates in <see cref="DetoxCycle"/> take their inputs as parameters
    /// precisely so they stay reachable by a test, which cannot construct a
    /// <see cref="Verse.Mod"/>. A static accessor read from inside a predicate would be the single
    /// change that takes this mod's only tested surface back to zero, and it would not look like a
    /// mistake while writing it, because <c>LoadedModManager.GetMod</c> returns null in the harness
    /// rather than throwing. The static is read at the call boundary instead.
    /// </para>
    /// </remarks>
    public class BiosculpterDetoxSettings : ModSettings
    {
        /// <summary>
        /// The cycle duration a fresh install uses, in days.
        /// </summary>
        /// <remarks>
        /// This is the value the mod shipped with, so an existing subscriber who never opens the
        /// settings window sees no change in behaviour whatsoever.
        /// </remarks>
        public const float DefaultCycleDurationDays = 12f;

        /// <summary>
        /// The shortest cycle the settings will allow, in days.
        /// </summary>
        /// <remarks>
        /// Not zero, and the reason is arithmetic rather than balance. The pod sets its countdown to
        /// <c>durationDays * 60000f</c> and the progress bar mote divides by that same number, so a
        /// duration of zero completes on the first tick and feeds a division by zero into the mote.
        /// </remarks>
        public const float MinimumCycleDurationDays = 1f;

        /// <summary>
        /// The longest cycle the settings will allow, in days.
        /// </summary>
        public const float MaximumCycleDurationDays = 30f;

        private bool cureIncurableAddictions;
        private float cycleDurationDays = DefaultCycleDurationDays;

        /// <summary>
        /// Gets whether the cycle treats addictions the game declares permanent.
        /// </summary>
        /// <remarks>
        /// Defaults to false, which is the behaviour every existing subscriber already has.
        /// </remarks>
        public bool CureIncurableAddictions => cureIncurableAddictions;

        /// <summary>
        /// Gets how long a detox cycle takes, in days.
        /// </summary>
        public float CycleDurationDays => cycleDurationDays;

        /// <summary>
        /// Reads and writes the settings.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <c>defaultValue</c> arguments below are the same constants as the field initialisers
        /// above, and they have to be. The two are read by different players: somebody with no
        /// settings file at all gets <c>new BiosculpterDetoxSettings()</c> and therefore the field
        /// initialiser, because <c>LoadedModManager.ReadModSettings</c> returns a fresh object
        /// without ever calling this method, while somebody whose file predates a key gets the
        /// <c>defaultValue</c> passed here. If those two numbers ever disagree the same build hands
        /// two different defaults to two different players and nothing reports it.
        /// </para>
        /// <para>
        /// The clamp afterwards is for a hand-edited config file rather than for the settings
        /// window, which cannot produce an out of range value. Without it a typed zero or a negative
        /// reaches the pod's countdown arithmetic.
        /// </para>
        /// </remarks>
        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref cureIncurableAddictions, "cureIncurableAddictions", defaultValue: false);
            Scribe_Values.Look(ref cycleDurationDays, "cycleDurationDays", DefaultCycleDurationDays);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                cycleDurationDays = ClampDuration(cycleDurationDays);
            }
        }

        /// <summary>
        /// Brings a duration inside the range the mod supports.
        /// </summary>
        /// <param name="days">The duration to clamp, in days.</param>
        /// <returns>The duration, clamped, with a non-finite value replaced by the default.</returns>
        /// <remarks>
        /// <para>
        /// <c>Mathf.Clamp</c> passes NaN straight through, so the finite test is not redundant with
        /// the clamp. A NaN duration would reach the pod as a countdown that no comparison is ever
        /// true for.
        /// </para>
        /// <para>
        /// Anything at or below zero resolves to the default rather than to the minimum, and the
        /// difference matters more than it looks. <c>Scribe_Values.Look</c> hands back its
        /// <c>defaultValue</c> only when the node is ABSENT. When the node is present but will not
        /// parse, or carries <c>IsNull</c>, <c>ScribeExtractor.ValueFromNode</c> returns
        /// <c>default(T)</c>, which for a float is zero. So zero is not a duration somebody chose;
        /// it is the shape a corrupt or hand-mangled config arrives in, and the slider cannot
        /// produce it. Clamping it to one day would give that player a one day cycle and no
        /// explanation. A value between zero and the minimum is a different case and is genuinely
        /// just too small, so it clamps.
        /// </para>
        /// </remarks>
        public static float ClampDuration(float days)
        {
            if (float.IsNaN(days) || float.IsInfinity(days) || days <= 0f)
            {
                return DefaultCycleDurationDays;
            }

            return Mathf.Clamp(days, MinimumCycleDurationDays, MaximumCycleDurationDays);
        }

        /// <summary>
        /// Renders a number of days the way the game renders every other duration.
        /// </summary>
        /// <param name="days">The duration in days.</param>
        /// <returns>A localised duration, such as "12 days" or "1 day".</returns>
        /// <remarks>
        /// <para>
        /// Writing "{0} days" into the nine language files instead was the obvious approach and it
        /// was wrong. The slider reaches one, so every one of those strings would render "1 days",
        /// which is ungrammatical in English and worse elsewhere; German needs "1 Tag" against
        /// "2 Tage", and Russian needs three forms rather than two.
        /// </para>
        /// <para>
        /// <c>ToStringTicksToDays</c> is the game's own answer and it is already translated into
        /// every language RimWorld ships. It picks between the <c>Period1Day</c> and
        /// <c>PeriodDays</c> keys, and the Russian <c>PeriodDays</c> is
        /// <c>{0_numCase ? день : дня : дней}</c>, so the game's numeric case machinery handles the
        /// Slavic agreement that no hand-written string in this mod could have got right.
        /// </para>
        /// <para>
        /// The format has to be "F0" rather than the default "F1". The singular branch tests the
        /// formatted text against the literal "1", so "1.0" would never match it and a one-day cycle
        /// would read as "1.0 days". The slider only ever yields whole numbers, so nothing is lost.
        /// </para>
        /// </remarks>
        private static string FormatDays(float days)
        {
            return ((int)(days * GenDate.TicksPerDay)).ToStringTicksToDays("F0");
        }

        /// <summary>
        /// Draws the settings window.
        /// </summary>
        /// <param name="inRect">The area available for drawing.</param>
        /// <remarks>
        /// <para>
        /// The duration is a slider rather than a numeric text field, which is a deliberate
        /// divergence from Simple Improve's material cost field. That field had to solve two real
        /// defects to be usable: <c>Widgets.TextFieldNumeric</c> clamps and rewrites its own buffer
        /// on the same keystroke, making every value below the minimum untypable, and nothing in
        /// RimWorld's settings window ever moves keyboard focus off a text field, so a field that
        /// waits to lose focus waits forever. A slider has neither problem by construction: it
        /// cannot hold an unparseable buffer, cannot be dragged out of range, and needs no commit
        /// point when the window closes.
        /// </para>
        /// <para>
        /// Coordinates inside the listing are relative to <paramref name="inRect"/>, because
        /// <c>Listing.Begin</c> calls <c>Widgets.BeginGroup</c>. Adding <c>inRect.y</c> to anything
        /// here would apply the window's offset a second time.
        /// </para>
        /// </remarks>
        public void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Small;

            listing.CheckboxLabeled(
                "BiosculpterDetox_CureIncurableAddictions".Translate(),
                ref cureIncurableAddictions,
                "BiosculpterDetox_CureIncurableAddictionsTooltip".Translate());

            listing.Gap(12f);

            listing.Label(
                "BiosculpterDetox_CycleDuration".Translate(FormatDays(cycleDurationDays)),
                tooltip: "BiosculpterDetox_CycleDurationTooltip".Translate(
                    FormatDays(DefaultCycleDurationDays)));

            cycleDurationDays = Mathf.Round(
                listing.Slider(cycleDurationDays, MinimumCycleDurationDays, MaximumCycleDurationDays));

            listing.Gap(12f);

            if (listing.ButtonText("BiosculpterDetox_ResetToDefaults".Translate()))
            {
                cureIncurableAddictions = false;
                cycleDurationDays = DefaultCycleDurationDays;
            }

            listing.End();
        }
    }
}
