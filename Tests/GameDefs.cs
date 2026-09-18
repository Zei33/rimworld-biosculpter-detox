using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace BiosculpterDetox.Tests
{
    /// <summary>
    /// Reads the installed game's shipped def XML, grouped by def type.
    /// </summary>
    /// <remarks>
    /// This deliberately parses the XML rather than going through <c>DefDatabase</c>. The database
    /// is populated only by a running game, and the whole point of these checks is to confirm that
    /// a hardcoded defName in this mod refers to a real def of the right type. Reading the files is
    /// the only way to answer that outside the game, and it is also the more honest check: it sees
    /// exactly what ships.
    /// </remarks>
    internal static class GameDefs
    {
        private static readonly Lazy<Dictionary<string, HashSet<string>>> byType =
            new Lazy<Dictionary<string, HashSet<string>>>(Load);

        /// <summary>
        /// The defNames declared for one def type, for example <c>HediffDef</c>.
        /// </summary>
        public static ISet<string> OfType(string defType)
        {
            return byType.Value.TryGetValue(defType, out var names)
                ? names
                : new HashSet<string>();
        }

        /// <summary>
        /// Every defName the game declares, of any type at all.
        /// </summary>
        public static ISet<string> AnyType()
        {
            return new HashSet<string>(byType.Value.Values.SelectMany(n => n));
        }

        /// <summary>
        /// The def types a name is declared under, which is empty when the name does not exist.
        /// </summary>
        public static IEnumerable<string> TypesDeclaring(string defName)
        {
            return byType.Value.Where(kv => kv.Value.Contains(defName)).Select(kv => kv.Key).OrderBy(t => t);
        }

        /// <summary>
        /// Whether a def declares itself permanently incurable.
        /// </summary>
        /// <param name="defName">The def to look up.</param>
        /// <returns><c>true</c> when the shipped XML sets <c>everCurableByItem</c> to false.</returns>
        /// <remarks>
        /// <para>
        /// This walks the files again rather than being folded into the defName index, because it
        /// is the only field any test here needs and building a general field index would be a lot
        /// of parsing for one answer. It is called once.
        /// </para>
        /// <para>
        /// It does NOT follow ParentName inheritance. That is correct for the one def it is used
        /// on, which declares the field directly, and it would be wrong to rely on for a def that
        /// inherits it. A caller wanting inheritance has to say so and this has to grow.
        /// </para>
        /// </remarks>
        public static bool DeclaresIncurable(string defName)
        {
            foreach (var node in AllDefNodes())
            {
                if (node.Element("defName")?.Value?.Trim() != defName)
                {
                    continue;
                }

                var declared = node.Element("everCurableByItem")?.Value?.Trim();

                if (declared != null)
                {
                    return declared.Equals("false", StringComparison.OrdinalIgnoreCase);
                }
            }

            return false;
        }

        private static IEnumerable<XElement> AllDefNodes()
        {
            foreach (var file in DefFiles())
            {
                XDocument document;
                try
                {
                    document = XDocument.Load(file);
                }
                catch (Exception)
                {
                    continue;
                }

                if (document.Root == null)
                {
                    continue;
                }

                foreach (var node in document.Root.Elements())
                {
                    yield return node;
                }
            }
        }

        private static IEnumerable<string> DefFiles()
        {
            var rimWorldDir = Environment.GetEnvironmentVariable("RimWorldDir");
            if (string.IsNullOrEmpty(rimWorldDir))
            {
                throw new InvalidOperationException(
                    "RimWorldDir is not set. The test project needs it to find the game's Defs, and " +
                    "the build needs it to resolve the game assemblies.");
            }

            var dataDir = Path.Combine(rimWorldDir, "Data");
            if (!Directory.Exists(dataDir))
            {
                throw new DirectoryNotFoundException("No Data directory under RimWorldDir: " + dataDir);
            }

            foreach (var module in Directory.GetDirectories(dataDir))
            {
                var defsDir = Path.Combine(module, "Defs");
                if (!Directory.Exists(defsDir))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(defsDir, "*.xml", SearchOption.AllDirectories))
                {
                    yield return file;
                }
            }
        }

        private static Dictionary<string, HashSet<string>> Load()
        {
            var rimWorldDir = Environment.GetEnvironmentVariable("RimWorldDir");
            if (string.IsNullOrEmpty(rimWorldDir))
            {
                throw new InvalidOperationException(
                    "RimWorldDir is not set. The test project needs it to find the game's Defs, and " +
                    "the build needs it to resolve the game assemblies.");
            }

            var dataDir = Path.Combine(rimWorldDir, "Data");
            if (!Directory.Exists(dataDir))
            {
                throw new DirectoryNotFoundException("No Data directory under RimWorldDir: " + dataDir);
            }

            var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var module in Directory.GetDirectories(dataDir))
            {
                var defsDir = Path.Combine(module, "Defs");
                if (!Directory.Exists(defsDir))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(defsDir, "*.xml", SearchOption.AllDirectories))
                {
                    XDocument document;
                    try
                    {
                        document = XDocument.Load(file);
                    }
                    catch (Exception)
                    {
                        // A file the game itself would reject is not this mod's problem.
                        continue;
                    }

                    if (document.Root == null)
                    {
                        continue;
                    }

                    foreach (var node in document.Root.Elements())
                    {
                        var defName = node.Element("defName")?.Value?.Trim();
                        if (string.IsNullOrEmpty(defName))
                        {
                            // Abstract parents carry a Name attribute instead, and are not defs.
                            continue;
                        }

                        var defType = node.Name.LocalName;
                        if (!result.TryGetValue(defType, out var names))
                        {
                            names = new HashSet<string>(StringComparer.Ordinal);
                            result[defType] = names;
                        }

                        names.Add(defName);
                    }
                }
            }

            return result;
        }
    }
}
