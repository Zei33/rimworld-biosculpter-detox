using RimWorld;

namespace BiosculpterDetox.Core
{
    /// <summary>
    /// Properties class for the detox biosculpter cycle.
    /// Defines the configuration and behavior of the detox cycle.
    /// </summary>
    public class CompProperties_BiosculpterPod_DetoxCycle : CompProperties_BiosculpterPod_BaseCycle
    {
        /// <summary>
        /// Initializes a new instance of the detox cycle properties.
        /// Sets the component class to CompBiosculpterPod_DetoxCycle.
        /// </summary>
        public CompProperties_BiosculpterPod_DetoxCycle()
        {
            compClass = typeof(CompBiosculpterPod_DetoxCycle);
        }
    }
}