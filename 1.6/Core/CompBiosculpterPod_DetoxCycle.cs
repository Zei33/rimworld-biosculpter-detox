using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BiosculpterDetox.Core
{
    /// <summary>
    /// Biosculpter pod cycle component that removes drug addictions and tolerances.
    /// </summary>
    public class CompBiosculpterPod_DetoxCycle : CompBiosculpterPod_Cycle
    {
        /// <summary>
        /// Gets the detox-specific properties for this cycle.
        /// </summary>
        public new CompProperties_BiosculpterPod_DetoxCycle Props =>
            (CompProperties_BiosculpterPod_DetoxCycle)props;

        /// <summary>
        /// Called when the detox cycle completes. Treats the occupant and tells the player.
        /// </summary>
        /// <param name="occupant">The pawn who completed the detox cycle.</param>
        /// <remarks>
        /// <para>
        /// Both outcomes report now. The nothing-removed path used to send nothing at all: no
        /// letter, no message, nothing in the log. Combined with a twelve-day cycle and an
        /// eligibility gate that could never run, a player could lose a pawn to the pod for most of
        /// two seasons and be given no indication that it had achieved anything or not.
        /// </para>
        /// <para>
        /// Both are letters rather than messages, which is a deliberate divergence from vanilla.
        /// Every vanilla biosculpter cycle reports with <c>Messages.Message</c>, and vanilla sends
        /// nothing generic on completion, so there is no double-reporting either way. Two things
        /// argue for the louder channel here: this cycle is twelve days against vanilla's four to
        /// eight, so it is a much larger commitment to report on, and the success letter is the
        /// shipped behaviour that 1,706 subscribers already have. Quietly demoting it to a message
        /// would be a visible regression to fix a bug about there being too little feedback.
        /// </para>
        /// <para>
        /// The nothing-found letter is <c>NeutralEvent</c>, never <c>NegativeEvent</c>. The shipped
        /// def for the latter is the "a bad thing happened" slot and carries an urgent bad-news
        /// sound. Finding nothing to cure is not a bad thing happening, it is a result.
        /// </para>
        /// <para>
        /// <c>new LookTargets(occupant)</c> is valid even though the occupant is despawned inside
        /// the pod's container for the whole of this method: the eject happens on the next line of
        /// the caller. <c>GlobalTargetInfo.IsValid</c> does not test <c>Spawned</c>, and by the time
        /// a player clicks the letter the pawn is out and standing on the map.
        /// </para>
        /// <para>
        /// The two completion motes that used to live in the detox logic are gone rather than
        /// moved. They were unreachable: both were guarded on <c>pawn.Map != null</c> and a
        /// despawned pawn's map is always null, so neither ever fired. Without the guard they would
        /// not have been harmless, since <c>MoteMaker.ThrowText</c> dereferences the map and throws.
        /// A mote could be thrown from the pod, which is spawned throughout, but the letter already
        /// covers the feedback and a mote on the pod is not obviously about the pawn.
        /// </para>
        /// </remarks>
        public override void CycleCompleted(Pawn occupant)
        {
            if (occupant == null)
            {
                Log.Warning("[BiosculpterDetox] Detox cycle completed but no occupant was found.");
                return;
            }

            try
            {
                List<string> cured = DetoxCycle.PerformDetox(occupant);

                if (cured.Count > 0)
                {
                    Find.LetterStack.ReceiveLetter(
                        "BiosculpterDetox_LetterTitle".Translate(),
                        "BiosculpterDetox_LetterText".Translate(occupant.Name.ToStringShort)
                        + "\n\n"
                        + "BiosculpterDetox_Cured".Translate(cured.ToCommaList(useAnd: true)),
                        LetterDefOf.PositiveEvent,
                        new LookTargets(occupant));
                }
                else
                {
                    Find.LetterStack.ReceiveLetter(
                        "BiosculpterDetox_NoConditionsLetterTitle".Translate(),
                        "BiosculpterDetox_NoConditionsLetterText".Translate(occupant.Name.ToStringShort),
                        LetterDefOf.NeutralEvent,
                        new LookTargets(occupant));
                }
            }
            catch (System.Exception exception)
            {
                Log.Error($"[BiosculpterDetox] Error in DetoxCycle.CycleCompleted: {exception}");
            }
        }

        /// <summary>
        /// Describes what the cycle will do for a particular pawn.
        /// </summary>
        /// <param name="tunedFor">The pawn to analyse, which may be <c>null</c>.</param>
        /// <returns>A description of what the cycle will accomplish.</returns>
        /// <remarks>
        /// <para>
        /// This tells the truth because the list it reads is built from the same predicate the
        /// treatment uses. It previously came from a method that counted addictions only, while the
        /// gate beside it counted tolerances as well, so a pawn whose only treatable condition was a
        /// tolerance read "nothing to treat" and then had the tolerance removed anyway.
        /// </para>
        /// <para>
        /// The permanent-addiction line is stated here rather than in the cycle's static
        /// description, and it has to be. The description on the properties object is built once
        /// when a pod spawns, so a description that named the current policy would go stale the
        /// moment the player changed the setting, and would keep claiming the opposite of what the
        /// cycle now does until the next map load. This method runs every time the player looks.
        /// </para>
        /// </remarks>
        public override string Description(Pawn tunedFor)
        {
            string baseDescription = base.Description(tunedFor) + "\n\n" + PermanentAddictionPolicy();

            if (tunedFor == null)
            {
                return baseDescription;
            }

            List<string> conditions = DetoxCycle.GetDetoxifiableConditionNames(tunedFor);

            return baseDescription + "\n\n" + (conditions.Count > 0
                ? "BiosculpterDetox_WillTreat".Translate(conditions.ToCommaList(useAnd: true))
                : "BiosculpterDetox_NoConditionsToTreat".Translate());
        }

        /// <summary>
        /// Determines whether this cycle would do anything for the specified pawn.
        /// </summary>
        /// <param name="pawn">The pawn to check.</param>
        /// <returns><c>true</c> if the pawn has conditions this cycle can treat.</returns>
        /// <remarks>
        /// This used to be dead: the identifier appeared exactly once in the repository, in its own
        /// declaration, and it overrides nothing in vanilla. It is now the single place that answers
        /// the eligibility question, called from the <c>CannotUseNowPawnCycleReason</c> patch.
        /// </remarks>
        public bool CanUseOn(Pawn pawn)
        {
            return DetoxCycle.HasDetoxifiableConditions(pawn);
        }

        /// <summary>
        /// States whether the cycle currently treats addictions the game declares permanent.
        /// </summary>
        /// <returns>The sentence to show the player.</returns>
        /// <remarks>
        /// Read live from the settings, and phrased in terms of permanent addictions rather than by
        /// naming luciferium. Luciferium is the only one the shipped game has, but the rule the mod
        /// applies is the game's own <c>everCurableByItem</c> declaration, so a mod that adds its own
        /// permanent addiction is covered by the same sentence without this one being rewritten.
        /// </remarks>
        private static string PermanentAddictionPolicy()
        {
            BiosculpterDetoxSettings settings = BiosculpterDetoxMod.Settings;

            return settings != null && settings.CureIncurableAddictions
                ? "BiosculpterDetox_PermanentAddictionsTreated".Translate()
                : "BiosculpterDetox_PermanentAddictionsSkipped".Translate();
        }
    }
}
