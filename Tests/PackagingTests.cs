using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace BiosculpterDetox.Tests
{
    /// <summary>
    /// Holds <c>build.sh</c> to staging what the game reads and nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Until 2026-09-18 the script copied <c>1.6</c> wholesale and then deleted a few things back
    /// out, which shipped six C# source files to every subscriber. An allow-list is not merely
    /// tidier: it is the shape that stays correct when a folder is added, because a new source
    /// folder is simply not staged, where a deny-list ships it until somebody notices.
    /// </para>
    /// <para>
    /// Every assertion here reads the script with comments stripped. That is required rather than
    /// fastidious: this repository has twice had a test fail on the comment explaining the very
    /// thing the test forbids. Worse than the loud version is the quiet one, where a comment
    /// mentioning a name that really exists is silently read as a use of it.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class PackagingTests
    {
        [Test]
        public void TheScriptDoesNotStageTheWholeSourceFolder()
        {
            Assert.That(
                Commands(), Does.Not.Contain("cp -r 1.6 release"),
                "build.sh is copying 1.6 wholesale again. That ships the mod's C# source to every "
                + "subscriber, and a deny-list afterwards only removes what somebody remembered.");
        }

        [Test]
        public void TheScriptStagesEverythingTheGameActuallyReads()
        {
            string commands = Commands();

            Assert.That(commands, Does.Contain("cp -r 1.6/Languages release/1.6/Languages"),
                "Without the nine Keyed files every string in the mod renders as its raw key name.");
            Assert.That(commands, Does.Contain("cp -r 1.6/Textures release/1.6/Textures"),
                "Without the textures the detox cycle's gizmo has no icon.");
            Assert.That(commands, Does.Contain("BiosculpterDetox.dll"),
                "Without the assembly the mod does nothing at all.");
        }

        [Test]
        public void TheScriptStagesNothingTheGameIgnores()
        {
            string commands = Commands();

            foreach (string unwanted in new[] { "1.6/Core", "1.6/ModEntry.cs", "1.6/Libraries" })
            {
                Assert.That(
                    commands, Does.Not.Contain(unwanted),
                    unwanted + " is staged. The game reads none of it, and the vendored 0Harmony "
                    + "under Libraries is reference-only: ModAssemblyHandler.ReloadAll loads every "
                    + ".dll at any depth on extension alone, so shipping one is not inert.");
            }
        }

        [Test]
        public void TheScriptDoesNotStageThePatchesFolderBecauseThisModHasNoXmlPatches()
        {
            // The one that would be easy to get wrong by copying Simple Improve's script across.
            // That mod really does ship PatchOperations and stages Patches/*.xml. This mod's
            // 1.6/Patches holds a single C# file, the Harmony patch class, and RimWorld globs *.xml
            // from a folder named exactly Patches, so there is nothing there for the game to read.
            Assert.That(
                Commands(), Does.Not.Contain("release/1.6/Patches"),
                "build.sh stages a Patches folder. Check whether this mod has XML PatchOperations "
                + "before adding that: if it still does not, this only ships C# source.");

            string[] patchFiles = Directory.GetFiles(Path.Combine(RepoRoot(), "1.6", "Patches"));

            Assert.That(patchFiles, Is.Not.Empty, "Found no Patches folder, so this test read nothing.");
            Assert.That(
                patchFiles.Where(f => f.EndsWith(".xml", StringComparison.Ordinal)), Is.Empty,
                "1.6/Patches now holds XML, so the game WILL read it and build.sh must stage it. "
                + "Update the script and this test together.");
        }

        [Test]
        public void TheScriptShipsOnlyTheModsOwnAssembly()
        {
            string commands = Commands();

            Assert.That(
                commands, Does.Not.Contain("cp -r 1.6/Assemblies"),
                "The whole Assemblies folder is staged. It carries build artefacts and copies of "
                + "game assemblies RimWorld has already loaded, and first loader wins process-wide "
                + "by simple name with no diagnostic anywhere.");
            Assert.That(
                commands, Does.Contain("cp 1.6/Assemblies/net472/BiosculpterDetox.dll"),
                "The mod's own assembly is no longer staged by name.");
        }

        [Test]
        public void TheScriptKeepsTheDeveloperReadmesOutOfTheShippedFolders()
        {
            string commands = Commands();

            // The Languages sweep must spare the WordInfo tables, which the game reads too. A sweep
            // of everything but XML would delete the Russian case.txt from the release while it
            // stayed in the repo, so the fault would only show in game.
            Assert.That(commands, Does.Contain(
                "find release/1.6/Languages -type f ! -name '*.xml' ! -path '*/WordInfo/*.txt' -delete"));
            Assert.That(commands, Does.Contain("find release/1.6/Textures -type f ! -name '*.png' -delete"));
        }

        /// <summary>
        /// Reads build.sh with comment lines removed.
        /// </summary>
        /// <returns>The script's commands, without its commentary.</returns>
        private static string Commands()
        {
            string path = Path.Combine(RepoRoot(), "build.sh");

            Assert.That(File.Exists(path), Is.True, "build.sh is missing.");

            string[] lines = File.ReadAllLines(path)
                .Where(line => !line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                .ToArray();

            Assert.That(lines, Is.Not.Empty, "build.sh has no command lines, so this test read nothing.");

            return string.Join("\n", lines);
        }

        private static string RepoRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (directory != null
                   && !File.Exists(Path.Combine(directory.FullName, "rimworld-biosculpter-detox.sln")))
            {
                directory = directory.Parent;
            }

            Assert.That(directory, Is.Not.Null, "Could not find the repository root.");
            return directory.FullName;
        }
    }
}
