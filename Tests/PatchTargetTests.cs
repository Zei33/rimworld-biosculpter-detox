using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BiosculpterDetox.Patches;
using HarmonyLib;
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
    /// can test that a patch WORKS. What it can test is everything Harmony decides by name before
    /// it patches anything: which method each <c>[HarmonyPatch]</c> resolves to, and what each
    /// <c>__</c> and <c>___</c> parameter binds to. That is not hypothetical for this mod. All four
    /// of its targets are resolved by name at patch time, one of them has two public overloads, one
    /// is an override, and this workspace has already recorded a case where a
    /// <c>[HarmonyPatch]</c> naming a method without argument types failed to resolve.
    /// </para>
    /// <para>
    /// Harmony looks a target up among the named type's DECLARED members only
    /// (<c>AccessTools.DeclaredMethod</c>, in both the vendored 2.3.6 and the Workshop's 2.4.1).
    /// So an override that disappeared would not move the patch onto the base class: the lookup
    /// would find nothing and <c>PatchAll</c> would throw "Undefined target method".
    /// </para>
    /// <para>
    /// The private-field tests are the same class of check for Harmony's <c>___name</c> injection,
    /// seen from the game's side, and <see cref="EveryPatchBindsTheWayHarmonyWill"/> checks the
    /// patch's side. Both are matched by string at patch time and fail at runtime rather than at
    /// compile time. They fail loudly, which is worse rather than better: Harmony throws from
    /// <c>PatchAll</c>, which applies patch classes one after another with nothing catching
    /// between them, so one renamed field can leave every patch after it unapplied. That is why the
    /// order is pinned too. Two failures are silent instead: a class that has lost its
    /// <c>[HarmonyPatch]</c>, which <c>PatchAll</c> skips without a word, and a patch method whose
    /// name Harmony does not recognise, which it never calls.
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
        public void TheGizmoHookIsThePodsOwnOverride()
        {
            // The patch names CompGetGizmosExtra by string, and nameof still compiles against an
            // inherited member. Harmony resolves the name among CompBiosculpterPod's declared
            // members only, so if the pod ever stopped overriding it the lookup would return null
            // rather than ThingComp's method, and PatchAll would throw "Undefined target method".
            // The declaring type is asserted, not merely that the name resolves.
            MethodInfo target = typeof(CompBiosculpterPod).GetMethod(
                "CompGetGizmosExtra",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);

            Assert.That(target, Is.Not.Null,
                "CompBiosculpterPod.CompGetGizmosExtra() is gone or changed shape, so the patch that "
                + "disables a refused detox button does not resolve and the button throws again.");
            Assert.That(target.DeclaringType, Is.EqualTo(typeof(CompBiosculpterPod)),
                "CompBiosculpterPod no longer overrides CompGetGizmosExtra. Harmony looks the target "
                + "up among the declared members only, so the gizmo patch has no target and "
                + "PatchAll throws \"Undefined target method\".");
            Assert.That(target.ReturnType, Is.EqualTo(typeof(IEnumerable<Gizmo>)),
                "The postfix replaces __result with an IEnumerable<Gizmo>. A different return type "
                + "makes Harmony refuse the patch.");
        }

        [Test]
        public void ThePrivateFieldTheGizmoPatchInjectsStillExists()
        {
            // The same class of check as the migration's field below: Harmony matches
            // ___biotunedTo to a private field by string at patch time. This is the game's side
            // of that match; EveryPatchBindsTheWayHarmonyWill holds the patch's side, the
            // parameter name.
            FieldInfo field = typeof(CompBiosculpterPod).GetField(
                "biotunedTo", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null,
                "CompBiosculpterPod.biotunedTo was renamed, so the ___biotunedTo injection in the "
                + "gizmo patch will not bind, and Harmony throws from PatchAll.");
            Assert.That(field.FieldType, Is.EqualTo(typeof(Pawn)));
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
        public void EveryPatchClassIsOneHarmonyWillApply()
        {
            // The control for the two tests below, which walk PatchClasses() and would pass over
            // an empty walk. It also holds the one failure that is silent: PatchAll skips a class
            // with no [HarmonyPatch] without a word, so deleting the attribute leaves the fix
            // unapplied and nothing in the log. And Harmony finds a patch method by its name, so a
            // method called anything else is one it never calls.
            Type[] patchClasses = PatchClasses();

            Assert.That(
                patchClasses.Select(t => t.Name),
                Is.EquivalentTo(new[]
                {
                    nameof(BiosculpterPatches.CompBiosculpterPod_PostSpawnSetup_Patch),
                    nameof(BiosculpterPatches.CompBiosculpterPod_CannotUseNowPawnCycleReason_Patch),
                    nameof(BiosculpterPatches.CompBiosculpterPod_PostExposeData_Patch),
                    nameof(BiosculpterPatches.CompBiosculpterPod_CompGetGizmosExtra_Patch)
                }),
                "The set of patch classes changed. Add a new one here so the tests below check it.");

            foreach (Type patchClass in patchClasses)
            {
                Assert.That(
                    HarmonyMethodExtensions.GetFromType(patchClass), Is.Not.Empty,
                    patchClass.Name + " has no [HarmonyPatch], so PatchAll skips it without a word "
                    + "and the patch is never applied.");

                Assert.That(
                    PatchMethods(patchClass), Is.Not.Empty,
                    patchClass.Name + " declares no Prefix, Postfix, Transpiler or Finalizer, so "
                    + "Harmony has nothing to apply.");

                foreach (MethodInfo method in DeclaredStaticMethods(patchClass))
                {
                    Assert.That(
                        PatchKinds.Concat(AuxiliaryNames), Does.Contain(method.Name),
                        patchClass.Name + "." + method.Name + " is not a name Harmony recognises, so "
                        + "it is never called.");
                }
            }
        }

        [Test]
        public void EveryPatchBindsTheWayHarmonyWill()
        {
            // Resolves every patch exactly as Harmony does before it patches: the class's
            // [HarmonyPatch] attributes folded together with HarmonyMethod.Merge, the method's own
            // merged over them, the target looked up with AccessTools.DeclaredMethod, and then each
            // parameter bound by its name. The tests above pin the game's half of each name; this
            // pins the mod's half, which is just as much a string. A renamed ___biotunedTo, an
            // attribute pointed at another method, or a __result of the wrong type all compile, and
            // all make PatchAll throw in game.
            var kinds = new HashSet<string>();

            foreach (Type patchClass in PatchClasses())
            {
                HarmonyMethod container = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(patchClass));

                foreach (MethodInfo patch in PatchMethods(patchClass))
                {
                    HarmonyMethod info = container.Merge(HarmonyMethodExtensions.GetMergedFromMethod(patch));
                    string where = patchClass.Name + "." + patch.Name;

                    Assert.That(
                        info.methodType ?? MethodType.Normal, Is.EqualTo(MethodType.Normal),
                        where + " targets a getter, setter, constructor or enumerator, which this "
                        + "test does not resolve. Extend it before relying on that.");

                    MethodInfo target = AccessTools.DeclaredMethod(
                        info.declaringType, info.methodName, info.argumentTypes);

                    Assert.That(
                        target, Is.Not.Null,
                        where + " names " + info.declaringType?.Name + "." + info.methodName
                        + ", which that type does not declare with those argument types. Harmony "
                        + "looks among declared members only, so PatchAll throws \"Undefined target "
                        + "method\".");

                    foreach (ParameterInfo parameter in patch.GetParameters())
                    {
                        kinds.Add(AssertBinds(where, target, parameter));
                    }
                }
            }

            // The positive control has to cover every kind of binding asserted above, or a kind
            // could stop being checked with the test still green.
            Assert.That(
                kinds, Is.EquivalentTo(new[] { "__instance", "__result", "___field", "argument" }),
                "The patches no longer use every kind of parameter this test checks, or one kind "
                + "was never reached.");
        }

        [Test]
        public void TheGizmoPatchIsAppliedLast()
        {
            // PatchAll walks Assembly.GetTypes() and applies each class in turn, and a class that
            // throws stops every class after it. The gizmo patch has the most ways to throw at
            // patch time, and losing it costs one button; losing the migration traps an occupant.
            // So it goes last, and a future break there takes nothing else with it.
            //
            // This reads the test build rather than the shipped one. Both compile the same source,
            // and the compiler emits a class's nested types in declaration order, so the patch
            // classes come out in the same order relative to each other.
            List<string> applied = typeof(BiosculpterPatches).Assembly.GetTypes()
                .Where(t => t.DeclaringType == typeof(BiosculpterPatches)
                            && HarmonyMethodExtensions.GetFromType(t).Count > 0)
                .Select(t => t.Name)
                .ToList();

            Assert.That(
                applied, Is.EquivalentTo(PatchClasses().Select(t => t.Name)),
                "The walk did not find every patch class, so its order says nothing.");
            Assert.That(
                applied.Last(),
                Is.EqualTo(nameof(BiosculpterPatches.CompBiosculpterPod_CompGetGizmosExtra_Patch)),
                "The gizmo patch is no longer declared last in BiosculpterPatches. If it ever throws "
                + "at patch time it now takes the patches after it down too, and the migration may "
                + "be one of them.");
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
            string[] files = Directory.GetFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories);

            // The control, without which this test passes by scanning nothing: an empty sweep never
            // enters the loop, and "no file reads defaultLabel" becomes indistinguishable from "no
            // file was read". The premise, that there is C# under 1.6, is a fact about the layout of
            // the repository rather than something this test establishes, so it has to be asserted
            // rather than inherited. Every other test in this fixture works through reflection on the
            // built assembly, so nothing here would fail in its place.
            Assert.That(
                files, Is.Not.Empty,
                "Found no C# under " + SourceRoot() + ", so this test scanned nothing.");

            foreach (string file in files)
            {
                string source = CodeOnly(File.ReadAllText(file));

                Assert.That(
                    source, Does.Not.Contain("defaultLabel"),
                    Path.GetFileName(file) + " reads a gizmo's defaultLabel. That is a translated "
                    + "display string; match on the comp type or the cycle def instead.");
            }
        }

        /// <summary>
        /// The method names Harmony applies as patches.
        /// </summary>
        private static readonly string[] PatchKinds = { "Prefix", "Postfix", "Transpiler", "Finalizer" };

        /// <summary>
        /// The method names Harmony calls on a patch class without applying them.
        /// </summary>
        private static readonly string[] AuxiliaryNames = { "Prepare", "Cleanup", "TargetMethod", "TargetMethods" };

        /// <summary>
        /// Every class nested in <see cref="BiosculpterPatches"/> that the compiler did not generate.
        /// </summary>
        private static Type[] PatchClasses()
        {
            return typeof(BiosculpterPatches)
                .GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .Where(t => !t.Name.StartsWith("<", StringComparison.Ordinal))
                .ToArray();
        }

        /// <summary>
        /// The static methods a class declares itself, leaving out compiler-generated ones.
        /// </summary>
        private static IEnumerable<MethodInfo> DeclaredStaticMethods(Type type)
        {
            return type
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                            | BindingFlags.DeclaredOnly)
                .Where(m => !m.Name.StartsWith("<", StringComparison.Ordinal));
        }

        /// <summary>
        /// The methods of a patch class that Harmony applies as patches.
        /// </summary>
        private static List<MethodInfo> PatchMethods(Type type)
        {
            return DeclaredStaticMethods(type)
                .Where(m => PatchKinds.Contains(m.Name))
                .ToList();
        }

        /// <summary>
        /// Asserts that one patch parameter binds the way Harmony will bind it.
        /// </summary>
        /// <param name="where">The patch, for messages.</param>
        /// <param name="target">The method the patch resolved to.</param>
        /// <param name="parameter">The patch method's parameter.</param>
        /// <returns>The kind of binding checked.</returns>
        /// <remarks>
        /// The rules are Harmony's own, from <c>MethodPatcher</c>: <c>__instance</c> is the target's
        /// instance, <c>__result</c> its return value, <c>___name</c> the field that
        /// <c>AccessTools.Field</c> finds on the target's declaring type, and any other name the
        /// target's parameter of that name. Every other <c>__</c> name is refused rather than
        /// passed, so that nothing reaches the game unchecked.
        /// </remarks>
        private static string AssertBinds(string where, MethodInfo target, ParameterInfo parameter)
        {
            string name = parameter.Name;
            string what = where + " parameter " + name;

            if (name == "__instance")
            {
                Assert.That(target.IsStatic, Is.False, what + ": the target is static and has no instance.");
                Assert.That(
                    parameter.ParameterType.IsAssignableFrom(target.DeclaringType), Is.True,
                    what + " cannot hold the " + target.DeclaringType.Name + " Harmony passes.");
                return "__instance";
            }

            if (name == "__result")
            {
                Assert.That(target.ReturnType, Is.Not.EqualTo(typeof(void)), what + ": the target returns nothing.");
                AssertHolds(what, parameter.ParameterType, target.ReturnType);
                return "__result";
            }

            if (name.StartsWith("___", StringComparison.Ordinal))
            {
                FieldInfo field = AccessTools.Field(target.DeclaringType, name.Substring(3));

                Assert.That(
                    field, Is.Not.Null,
                    what + " injects a field " + target.DeclaringType.Name + " does not have, so "
                    + "Harmony throws \"No such field defined in class\" from PatchAll.");
                AssertHolds(what, parameter.ParameterType, field.FieldType);
                return "___field";
            }

            Assert.That(
                name.StartsWith("__", StringComparison.Ordinal), Is.False,
                what + " is a Harmony special name this test does not check. Extend it first.");

            ParameterInfo argument = target.GetParameters().FirstOrDefault(p => p.Name == name);

            Assert.That(
                argument, Is.Not.Null,
                what + " names no parameter of " + target.Name + ", so Harmony throws \"Parameter "
                + "not found in method\" from PatchAll.");
            AssertHolds(what, parameter.ParameterType, argument.ParameterType);
            return "argument";
        }

        /// <summary>
        /// Asserts that a patch parameter can receive a value of the given type.
        /// </summary>
        /// <remarks>
        /// A ref parameter is handed the address of the value itself, so its type must match
        /// exactly; a plain one only has to be able to hold the value.
        /// </remarks>
        private static void AssertHolds(string what, Type parameterType, Type valueType)
        {
            Type value = valueType.IsByRef ? valueType.GetElementType() : valueType;

            if (parameterType.IsByRef)
            {
                Assert.That(
                    parameterType.GetElementType(), Is.EqualTo(value),
                    what + " is a ref to a different type from the " + value.Name + " it is bound to.");
            }
            else
            {
                Assert.That(
                    parameterType.IsAssignableFrom(value), Is.True,
                    what + " cannot hold the " + value.Name + " it is bound to.");
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
