using System;
using System.Collections.Generic;
using Verse;

namespace BiosculpterDetox.Core
{
    /// <summary>
    /// Decides when the pod's detox cycle button must be disabled, and disables it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A biotuned pod's cycle button opens no menu. Vanilla builds the biotuned pawn's row and, if
    /// there is one, invokes that row's action directly. A refused row is still built, with a null
    /// action, and the button's enabled test counts it as eligible, so the button stays enabled and
    /// the click throws a <c>NullReferenceException</c> inside the gizmo grid. The grid logs it with
    /// <c>Log.ErrorOnce</c> on a key every gizmo shares, so after the first click the button simply
    /// does nothing, silently, for the rest of the session.
    /// </para>
    /// <para>
    /// Vanilla can reach that on its own, with a biotuned child and the age reversal cycle or a
    /// biotuned pawn with no path to the pod. This mod made it the ordinary state: every completed
    /// detox biotunes the pod to a pawn who, by construction, has nothing left to treat.
    /// </para>
    /// <para>
    /// Everything here takes plain values and delegates rather than the pod, because a pod cannot
    /// be built in the test harness and Harmony cannot patch there. The patch gathers the readings
    /// and this class makes every decision over them.
    /// </para>
    /// </remarks>
    public static class DetoxCommandGate
    {
        /// <summary>
        /// Picks this mod's cycle out of a pod's cycles and collects the icon of every other one.
        /// </summary>
        /// <typeparam name="TCycle">The cycle type, which in game is the pod's cycle base class.</typeparam>
        /// <param name="cycles">The pod's cycles, in vanilla's order.</param>
        /// <param name="isDetox">Whether a cycle is this mod's.</param>
        /// <param name="iconOf">The icon vanilla will copy onto a cycle's button.</param>
        /// <param name="otherCycleIcons">
        /// Receives the icon of every cycle other than the one returned, in order. Never null.
        /// </param>
        /// <returns>
        /// The first cycle <paramref name="isDetox"/> accepts, or <c>null</c> when there is none.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The returned cycle's icon must never be among the others, and that is why this is a
        /// function the tests can reach rather than a loop inside the patch.
        /// <see cref="IsDistinctIcon"/> stands the gate down when the detox icon is among them, so
        /// a split that let it in would leave every refused button enabled and bring the crash
        /// back, with nothing logged anywhere.
        /// </para>
        /// <para>
        /// A second cycle the predicate accepts goes to the others. The spawn postfix never adds a
        /// second, but one that somehow existed would load its icon from the same path, and
        /// <c>ContentFinder</c> would hand both the same texture. The icon could then no longer say
        /// which button is which, and putting the second one's icon among the others is what makes
        /// the gate stand down rather than guess.
        /// </para>
        /// </remarks>
        public static TCycle SplitCycles<TCycle>(
            IEnumerable<TCycle> cycles,
            Func<TCycle, bool> isDetox,
            Func<TCycle, object> iconOf,
            out List<object> otherCycleIcons)
            where TCycle : class
        {
            otherCycleIcons = new List<object>();
            TCycle detox = null;

            if (cycles == null)
            {
                return null;
            }

            foreach (TCycle cycle in cycles)
            {
                if (detox == null && isDetox(cycle))
                {
                    detox = cycle;
                }
                else
                {
                    otherCycleIcons.Add(iconOf(cycle));
                }
            }

            return detox;
        }

        /// <summary>
        /// Passes the pod's gizmos through, disabling the detox cycle's button when the pawn it
        /// would act on is refused.
        /// </summary>
        /// <param name="gizmos">The gizmos the pod yielded, in vanilla's order.</param>
        /// <param name="detoxIcon">The detox cycle's icon, which vanilla copies onto its button.</param>
        /// <param name="otherCycleIcons">The icon of every other cycle on the same pod.</param>
        /// <param name="refusal">
        /// Returns the reason to disable the button with, or <c>null</c> to leave it enabled. Asked
        /// at most once per pass, and only once the button is found and vanilla left it enabled.
        /// </param>
        /// <returns>The same gizmos, each exactly once, in the same order.</returns>
        /// <remarks>
        /// <para>
        /// An iterator on purpose. The Harmony postfix hands this the enumerable vanilla returned,
        /// before vanilla has built a single gizmo, and nothing may be decided until the caller
        /// walks it: vanilla sets each button's disabled state immediately before yielding it and
        /// never touches it again, so the moment a button arrives here is the one moment its state
        /// is both final and still ours to change.
        /// </para>
        /// <para>
        /// The button is picked out by its icon, compared by reference. Vanilla assigns
        /// <c>cycle.Props.Icon</c> to the button it builds for that cycle, so the reference is a
        /// marker set by the same code that makes the button, and it is not translated. The label
        /// would be; the position would not survive another mod inserting a gizmo ahead of the
        /// cycle buttons, and a wrong position disables some other cycle's button with this mod's
        /// reason, where a missed icon merely leaves vanilla's behaviour in place.
        /// </para>
        /// <para>
        /// A vanilla-disabled button is left exactly as it is. Its reason is about the pod (no
        /// power, nutrition not loaded, occupied, missing research) or is vanilla's own report that
        /// nobody, or the biotuned pawn, is around, and every one of those has to be dealt with
        /// before this mod's reason could matter.
        /// </para>
        /// </remarks>
        public static IEnumerable<Gizmo> DisableRefusedDetoxCommand(
            IEnumerable<Gizmo> gizmos, object detoxIcon, IEnumerable<object> otherCycleIcons, Func<string> refusal)
        {
            bool searching = IsDistinctIcon(detoxIcon, otherCycleIcons);

            foreach (Gizmo gizmo in gizmos)
            {
                // First match only. Vanilla yields every cycle button before anything else, so the
                // first button carrying the icon is this cycle's, and a later one is some other
                // gizmo that happens to reuse the texture.
                if (searching && gizmo is Command_Action command && ReferenceEquals(command.icon, detoxIcon))
                {
                    searching = false;

                    if (!command.Disabled)
                    {
                        string reason = refusal();

                        if (reason != null)
                        {
                            command.Disable(reason);
                        }
                    }
                }

                yield return gizmo;
            }
        }

        /// <summary>
        /// Decides whether an icon picks out exactly one cycle's button.
        /// </summary>
        /// <param name="icon">The icon to test.</param>
        /// <param name="otherCycleIcons">The icon of every other cycle on the same pod.</param>
        /// <returns><c>true</c> when no other cycle shares the icon and it is not missing.</returns>
        /// <remarks>
        /// <para>
        /// Both tests are by reference, and the parameters are typed <c>object</c> so that nothing
        /// can quietly reach for Unity's overloaded equality. That operator calls a destroyed
        /// texture equal to <c>null</c>, and every DEV button on the pod has a null icon, so an
        /// equality test would let a missing or destroyed detox icon match a DEV button.
        /// </para>
        /// <para>
        /// A missing icon is a real case rather than a tidy one: <c>ContentFinder</c> returns null
        /// when a texture fails to load, and the cycle's lazy <c>Icon</c> getter hands that null
        /// straight to vanilla, which puts it on the button.
        /// </para>
        /// </remarks>
        public static bool IsDistinctIcon(object icon, IEnumerable<object> otherCycleIcons)
        {
            if (icon is null)
            {
                return false;
            }

            if (otherCycleIcons == null)
            {
                return true;
            }

            foreach (object other in otherCycleIcons)
            {
                if (ReferenceEquals(other, icon))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Decides the reason a biotuned pod's detox button must be disabled with.
        /// </summary>
        /// <param name="podBiotuned">Whether the pod is biotuned to anyone.</param>
        /// <param name="biotunedPawnOnPodMap">
        /// Whether the biotuned pawn is alive, spawned and on the pod's map.
        /// </param>
        /// <param name="rowReason">
        /// Returns the reason vanilla puts on that pawn's row for this cycle, or <c>null</c> when it
        /// has none. Only called when the answer can matter.
        /// </param>
        /// <returns>The reason to disable with, or <c>null</c> to leave the button alone.</returns>
        /// <remarks>
        /// <para>
        /// An unbiotuned pod is never touched. Its button opens a menu of every colonist, and a
        /// refused row there is greyed with its reason and has nothing to invoke.
        /// </para>
        /// <para>
        /// A biotuned pawn who is dead, despawned or on another map is vanilla's to report. That is
        /// the one case vanilla's own button test handles, disabling the button with "Biotuned to
        /// ... and ... isn't around", and this mirrors the exact test it uses.
        /// </para>
        /// <para>
        /// The reason is the row's, returned verbatim. The row reads the pawn's label and then
        /// this, and the right-click menu reads "Cannot start detox cycle" and then this, so all
        /// three places the player can meet the refusal say the same thing, and no new string is
        /// needed in any of the nine languages.
        /// </para>
        /// </remarks>
        public static string BiotunedRefusal(bool podBiotuned, bool biotunedPawnOnPodMap, Func<string> rowReason)
        {
            if (!podBiotuned || !biotunedPawnOnPodMap || rowReason == null)
            {
                return null;
            }

            return rowReason();
        }
    }
}
