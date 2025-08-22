using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BiosculpterDetox.Core
{
    /// <summary>
    /// Core logic for the biosculpter detox cycle.
    /// Handles the removal of drug addictions and withdrawal effects from pawns.
    /// </summary>
    public static class DetoxCycle
    {
        /// <summary>
        /// List of drug hediff definitions that can be detoxified.
        /// These are the core addiction hediffs that the detox cycle can remove.
        /// </summary>
        private static readonly List<string> DetoxifiableAddictions = new List<string>
        {
            "Addiction_Alcohol",
            "Addiction_Smokeleaf", 
            "Addiction_Psychite",
            "Addiction_WakeUp",
            "Addiction_GoJuice",
            "Addiction_Flake",
            "Addiction_Yayo",
            "Addiction_Beer",
            "Addiction_Ambrosia"
        };

        /// <summary>
        /// List of withdrawal hediff definitions that should be removed during detox.
        /// These are the withdrawal symptoms that occur when addictions are not satisfied.
        /// </summary>
        private static readonly List<string> DetoxifiableWithdrawals = new List<string>
        {
            "AlcoholWithdrawal",
            "SmokeleafWithdrawal",
            "PsychiteWithdrawal", 
            "WakeUpWithdrawal",
            "GoJuiceWithdrawal",
            "FlakeWithdrawal",
            "YayoWithdrawal",
            "AmbrosiaWithdrawal"
        };

        /// <summary>
        /// List of addiction hediff definitions that should NEVER be removed by detox.
        /// These are permanent addictions that cannot be cured through normal medical means.
        /// </summary>
        private static readonly List<string> NonDetoxifiableAddictions = new List<string>
        {
            "LuciferiumAddiction"  // Luciferium is permanent - "there is no way to get the mechanites out, ever"
        };

        /// <summary>
        /// Performs the detox treatment on a pawn, removing all drug addictions and withdrawal effects.
        /// This method is called when the biosculpter detox cycle completes.
        /// </summary>
        /// <param name="pawn">The pawn undergoing detox treatment.</param>
        /// <returns>True if any addictions or withdrawals were removed; otherwise, false.</returns>
        public static bool PerformDetox(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                Log.Warning("[BiosculpterDetox] Attempted to detox null pawn or pawn without health.");
                return false;
            }

            bool removedAny = false;
            var hediffsToRemove = new List<Hediff>();

            // Find all detoxifiable addictions and withdrawals
            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff?.def?.defName == null) continue;

                // Check if this is a detoxifiable addiction
                if (DetoxifiableAddictions.Contains(hediff.def.defName))
                {
                    hediffsToRemove.Add(hediff);
                    removedAny = true;
                    Log.Message($"[BiosculpterDetox] Removing addiction: {hediff.def.defName} from {pawn.Name}");
                }
                // Check if this is a detoxifiable withdrawal
                else if (DetoxifiableWithdrawals.Contains(hediff.def.defName))
                {
                    hediffsToRemove.Add(hediff);
                    removedAny = true;
                    Log.Message($"[BiosculpterDetox] Removing withdrawal: {hediff.def.defName} from {pawn.Name}");
                }
                // Also check for generic addiction patterns (for modded drugs)
                // But exclude any addictions that are explicitly non-detoxifiable
                else if ((hediff.def.defName.StartsWith("Addiction_") || 
                         hediff.def.defName.EndsWith("Withdrawal") ||
                         hediff.def.defName.EndsWith("Addiction")) &&
                         !NonDetoxifiableAddictions.Contains(hediff.def.defName))
                {
                    hediffsToRemove.Add(hediff);
                    removedAny = true;
                    Log.Message($"[BiosculpterDetox] Removing modded addiction/withdrawal: {hediff.def.defName} from {pawn.Name}");
                }
                // Log when we encounter non-detoxifiable addictions
                else if (NonDetoxifiableAddictions.Contains(hediff.def.defName))
                {
                    Log.Message($"[BiosculpterDetox] Skipping non-detoxifiable addiction: {hediff.def.defName} on {pawn.Name} (permanent addiction)");
                }
            }

            // Remove all identified hediffs
            foreach (var hediff in hediffsToRemove)
            {
                pawn.health.RemoveHediff(hediff);
            }

            // Clear any drug tolerances as well
            RemoveDrugTolerances(pawn);

            if (removedAny)
            {
                // Send notification about successful detox (only if pawn is on a map)
                if (pawn.Map != null)
                {
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, 
                        "BiosculpterDetox_DetoxComplete".Translate(), 6f);
                }
                
                // Note: We don't add a physical hediff for detox completion
                // The mental/mood benefits are handled by removing addictions
                // and can be complemented by thoughts/memories if desired

                Log.Message($"[BiosculpterDetox] Successfully detoxed {pawn.Name} - removed {hediffsToRemove.Count} addiction/withdrawal hediffs.");
            }
            else
            {
                // Show message if no addictions were found (only if pawn is on a map)
                if (pawn.Map != null)
                {
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, 
                        "BiosculpterDetox_NoAddictions".Translate(), 6f);
                }
                
                Log.Message($"[BiosculpterDetox] No addictions found on {pawn.Name} during detox cycle.");
            }

            return removedAny;
        }

        /// <summary>
        /// Removes drug tolerance hediffs from the pawn.
        /// Tolerance hediffs can contribute to addiction risk, so they should be cleared during detox.
        /// </summary>
        /// <param name="pawn">The pawn to remove tolerances from.</param>
        private static void RemoveDrugTolerances(Pawn pawn)
        {
            var tolerancesToRemove = new List<Hediff>();

            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff?.def?.defName == null) continue;

                // Look for tolerance hediffs, but exclude luciferium tolerance
                if ((hediff.def.defName.Contains("Tolerance") || 
                     hediff.def.defName.EndsWith("_Tolerance")) &&
                    !hediff.def.defName.Contains("Luciferium"))
                {
                    tolerancesToRemove.Add(hediff);
                    Log.Message($"[BiosculpterDetox] Removing tolerance: {hediff.def.defName} from {pawn.Name}");
                }
                // Log when we skip luciferium tolerance
                else if (hediff.def.defName.Contains("Luciferium") && 
                        (hediff.def.defName.Contains("Tolerance") || hediff.def.defName.EndsWith("_Tolerance")))
                {
                    Log.Message($"[BiosculpterDetox] Skipping luciferium tolerance: {hediff.def.defName} on {pawn.Name} (permanent)");
                }
            }

            foreach (var hediff in tolerancesToRemove)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        /// <summary>
        /// Checks if a pawn has any detoxifiable addictions or withdrawals.
        /// This is used to determine if the detox cycle should be available for a pawn.
        /// </summary>
        /// <param name="pawn">The pawn to check for addictions.</param>
        /// <returns>True if the pawn has any detoxifiable conditions; otherwise, false.</returns>
        public static bool HasDetoxifiableConditions(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return false;

            return pawn.health.hediffSet.hediffs.Any(hediff =>
                hediff?.def?.defName != null &&
                !NonDetoxifiableAddictions.Contains(hediff.def.defName) &&
                (DetoxifiableAddictions.Contains(hediff.def.defName) ||
                 DetoxifiableWithdrawals.Contains(hediff.def.defName) ||
                 hediff.def.defName.StartsWith("Addiction_") ||
                 hediff.def.defName.EndsWith("Withdrawal") ||
                 hediff.def.defName.EndsWith("Addiction") ||
                 (hediff.def.defName.Contains("Tolerance") && !hediff.def.defName.Contains("Luciferium"))));
        }

        /// <summary>
        /// Gets a human-readable description of the addictions that will be treated.
        /// This is used for UI display to show what the detox cycle will cure.
        /// </summary>
        /// <param name="pawn">The pawn to analyze.</param>
        /// <returns>A list of addiction names that can be treated.</returns>
        public static List<string> GetDetoxifiableConditionNames(Pawn pawn)
        {
            var conditions = new List<string>();
            
            if (pawn?.health?.hediffSet == null) return conditions;

            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff?.def == null) continue;

                if (!NonDetoxifiableAddictions.Contains(hediff.def.defName) &&
                    (DetoxifiableAddictions.Contains(hediff.def.defName) ||
                     DetoxifiableWithdrawals.Contains(hediff.def.defName) ||
                     hediff.def.defName.StartsWith("Addiction_") ||
                     hediff.def.defName.EndsWith("Withdrawal") ||
                     hediff.def.defName.EndsWith("Addiction")))
                {
                    // Use the hediff's label for display, or fall back to def name
                    string displayName = hediff.LabelCap;
                    if (string.IsNullOrEmpty(displayName))
                    {
                        displayName = hediff.def.LabelCap;
                    }
                    if (!conditions.Contains(displayName))
                    {
                        conditions.Add(displayName);
                    }
                }
            }

            return conditions;
        }
    }
}