using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BiosculpterDetox.Core
{
    /// <summary>
    /// Biosculpter pod cycle component that handles detox functionality.
    /// This cycle removes drug addictions and withdrawal effects from pawns.
    /// </summary>
    public class CompBiosculpterPod_DetoxCycle : CompBiosculpterPod_Cycle
    {
        /// <summary>
        /// Gets the detox-specific properties for this cycle.
        /// </summary>
        public new CompProperties_BiosculpterPod_DetoxCycle Props => 
            (CompProperties_BiosculpterPod_DetoxCycle)props;

        /// <summary>
        /// Called when the detox cycle completes.
        /// Performs the actual detox treatment on the occupant.
        /// </summary>
        /// <param name="occupant">The pawn who completed the detox cycle.</param>
        public override void CycleCompleted(Pawn occupant)
        {
            if (occupant == null)
            {
                Log.Warning("[BiosculpterDetox] DetoxCycle completed but no occupant found.");
                return;
            }

            try
            {
                bool success = DetoxCycle.PerformDetox(occupant);
                
                if (success)
                {
                    // Send letter to player about successful detox
                    Find.LetterStack.ReceiveLetter(
                        "BiosculpterDetox_LetterTitle".Translate(),
                        "BiosculpterDetox_LetterText".Translate(occupant.Name.ToStringShort),
                        LetterDefOf.PositiveEvent,
                        new LookTargets(occupant)
                    );
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[BiosculpterDetox] Error in DetoxCycle.CycleCompleted: {ex}");
            }
        }

        /// <summary>
        /// Provides a detailed description of what the detox cycle will do for the specified pawn.
        /// Shows which addictions will be treated.
        /// </summary>
        /// <param name="tunedFor">The pawn to analyze for detox potential.</param>
        /// <returns>A description of what the cycle will accomplish.</returns>
        public override string Description(Pawn tunedFor)
        {
            string baseDescription = base.Description(tunedFor);
            
            if (tunedFor != null)
            {
                var conditions = DetoxCycle.GetDetoxifiableConditionNames(tunedFor);
                if (conditions.Count > 0)
                {
                    string conditionList = string.Join(", ", conditions);
                    baseDescription += "\n\n" + "BiosculpterDetox_WillTreat".Translate(conditionList);
                }
                else
                {
                    baseDescription += "\n\n" + "BiosculpterDetox_NoConditionsToTreat".Translate();
                }
            }

            return baseDescription;
        }

        /// <summary>
        /// Determines if this cycle can be used by the specified pawn.
        /// Only pawns with detoxifiable conditions should be able to use the detox cycle.
        /// </summary>
        /// <param name="pawn">The pawn to check.</param>
        /// <returns>True if the pawn has detoxifiable conditions; otherwise, false.</returns>
        public bool CanUseOn(Pawn pawn)
        {
            return DetoxCycle.HasDetoxifiableConditions(pawn);
        }
    }
}