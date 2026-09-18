using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BiosculpterDetox.Patches;
using NUnit.Framework;
using RimWorld;
using Verse;

namespace BiosculpterDetox.Tests
{
    /// <summary>
    /// Checks that every member this mod's Harmony patches name still exists on the game's types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Harmony cannot patch on this runtime at all, in or out of the game process, so nothing here
    /// can test that a patch WORKS. What it can test is the half that fails silently: a patch whose
    /// target does not resolve. That is not hypothetical for this mod. Two of its three targets are
    /// reached by name, one of them has two public overloads, and this workspace has already
    /// recorded a case where a <c>[HarmonyPatch]</c> naming a method without argument types failed
    /// to resolve.
    /// </para>
    /// <para>
    /// The private-field test is the same class of check for Harmony's <c>___name</c> injection,
    /// which is matched by string at patch time and fails at runtime rather than at compile time.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class PatchTargetTests
    {
        [Test]
        public void TheEligibilityHookResolvesToExactlyOneOverload()
        {
            // CompBiosculpterPod.CannotUseNowPawnCycleReason has TWO public overloads: a
            // three-parameter one that delegates and a four-parameter one that is the real body.
            // Naming it without argument types would not resolve, so the patch spells them out and
            // this asserts the signature it spells is the one that exists.
            MethodInfo target = typeof(CompBiosculpterPod).GetMethod(
                "CannotUseNowPawnCycleReason",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(Pawn), typeof(Pawn), typeof(CompBiosculpterPod_Cycle), typeof(bool) },
                null);

            Assert.That(target, Is.Not.Null,
                "CompBiosculpterPod.CannotUseNowPawnCycleReason(Pawn, Pawn, "
                + "CompBiosculpterPod_Cycle, bool) is gone or changed shape, so the patch that "
                + "disables the detox cycle for a pawn with nothing to treat does not resolve and "
                + "silently does nothing.");

            Assert.That(target.ReturnType, Is.EqualTo(typeof(string)));

            Assert.That(
                typeof(CompBiosculpterPod).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Count(m => m.Name == "CannotUseNowPawnCycleReason"),
                Is.EqualTo(2),
                "The overload count changed. If it is now one, the argument types in the patch are "
                + "harmless but no longer necessary; if more, check the four-parameter one is still "
                + "the body the other delegates to.");
        }

        [Test]
        public void TheTwoOtherPatchTargetsExist()
        {
            Assert.That(
                typeof(CompBiosculpterPod).GetMethod("PostSpawnSetup", new[] { typeof(bool) }),
                Is.Not.Null,
                "CompBiosculpterPod.PostSpawnSetup is gone, so the detox cycle is never added.");

            Assert.That(
                typeof(CompBiosculpterPod).GetMethod("PostExposeData", Type.EmptyTypes),
                Is.Not.Null,
                "CompBiosculpterPod.PostExposeData is gone, so a save holding the legacy cycle key "
                + "is never migrated and every in-progress detox traps its occupant.");
        }

        [Test]
        public void ThePrivateFieldTheMigrationInjectsStillExists()
        {
            // Harmony matches ___currentCycleKey to a private field by string at patch time. A
            // rename is a runtime failure with no compiler warning, and the symptom would be
            // in-progress cycles breaking on a mod update rather than anything obvious.
            FieldInfo field = typeof(CompBiosculpterPod).GetField(
                "currentCycleKey", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null,
                "CompBiosculpterPod.currentCycleKey was renamed, so the ___currentCycleKey "
                + "injection in the migration patch will not bind.");
            Assert.That(field.FieldType, Is.EqualTo(typeof(string)));
        }

        [Test]
        public void TheCycleKeyIsNamespacedAndTheLegacyKeyIsRemembered()
        {
            // SetupCycleCaches writes cycleLookup[key] with no collision check, so a bare word is a
            // silent overwrite waiting for another mod to pick the same one.
            Assert.That(BiosculpterPatches.CycleKey, Does.StartWith("Zei33."));
            Assert.That(BiosculpterPatches.CycleKey, Is.Not.EqualTo(BiosculpterPatches.LegacyCycleKey));

            Assert.That(BiosculpterPatches.LegacyCycleKey, Is.EqualTo("detox"),
                "The legacy key is what existing saves hold. Changing it orphans every pod that is "
                + "mid-cycle on an upgrade, which is the exact failure the rename was guarded "
                + "against in the first place.");
        }

        [Test]
        public void TheCycleComponentIsNotSealed()
        {
            // The component is added at runtime with AllComps.Add, which does not populate
            // compsByType. GetComp<T> consults that dictionary once a thing has three or more
            // comps, and a vanilla biosculpter pod has well over three, so the lookup misses. It
            // still finds the component, because GetComp falls through to a linear scan for an
            // UNSEALED T and only returns null early for a sealed one.
            //
            // So sealing this class would turn a linear scan into a null, and the spawn postfix
            // uses exactly that lookup as its "have I already added this" test. The component would
            // be appended again on every spawn, and the pod would grow a duplicate cycle each time.
            Assert.That(typeof(Core.CompBiosculpterPod_DetoxCycle).IsSealed, Is.False,
                "Sealing the cycle component makes GetComp<T> return null for it on any thing with "
                + "three or more comps, which is every biosculpter pod.");
        }

        [Test]
        public void NothingIdentifiesAGizmoOrACycleByItsLabel()
        {
            // The rule this repo has been burned by: Command.defaultLabel is a display label and is
            // translated, so matching on it is a check that works in English and nowhere else. The
            // old gizmo gate tested defaultLabel.ToLower().Contains("detox"), which is false in all
            // eight other shipped languages; Polish is "detoks" and does not even contain it.
            // Comments are stripped first, and that is required rather than tidy. The first run of
            // this test failed on BiosculpterPatches.cs, whose comment EXPLAINING why the label
            // match was removed contains the word. A test that reads source as prose cannot tell a
            // use from a description of one.
            foreach (string file in Directory.GetFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories))
            {
                string source = CodeOnly(File.ReadAllText(file));

                Assert.That(
                    source, Does.Not.Contain("defaultLabel"),
                    Path.GetFileName(file) + " reads a gizmo's defaultLabel. That is a translated "
                    + "display string; match on the comp type or the cycle def instead.");
            }
        }

        /// <summary>
        /// Strips line and documentation comments, leaving only code.
        /// </summary>
        /// <param name="source">The file's text.</param>
        /// <returns>The same text with every comment line removed.</returns>
        private static string CodeOnly(string source)
        {
            return string.Join(
                "\n",
                source.Split('\n').Where(line => !line.TrimStart().StartsWith("//")));
        }

        private static string SourceRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null
                   && !File.Exists(Path.Combine(directory.FullName, "rimworld-biosculpter-detox.sln")))
            {
                directory = directory.Parent;
            }

            Assert.That(directory, Is.Not.Null, "Could not find the repository root.");
            return Path.Combine(directory.FullName, "1.6");
        }
    }
}
