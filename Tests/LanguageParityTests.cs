using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;

namespace BiosculpterDetox.Tests
{
    /// <summary>
    /// Holds the nine language files to the same key set, and to the same placeholders.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ported from Simple Improve after this mod gained a settings window. Eight keys went into
    /// nine files in one change and nothing in this repository would have noticed if one file had
    /// been missed: a key absent from a language renders in game as the raw key name, which is a
    /// shipped bug rather than a warning, and it is invisible to anyone testing in English.
    /// </para>
    /// <para>
    /// The placeholder check is here because of what this change nearly shipped. The duration label
    /// originally carried its own unit, "{0} days", which renders "1 days" at the slider minimum and
    /// is worse in languages that inflect. Moving the unit out to the game's own formatter meant
    /// editing the same string in nine files by hand, and a dropped or duplicated placeholder there
    /// would have reached players as visibly broken text.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class LanguageParityTests
    {
        private static readonly string[] Languages =
        {
            "English", "ChineseSimplified", "French", "German", "Japanese",
            "Polish", "PortugueseBrazilian", "Russian", "Spanish"
        };

        [Test]
        public void AllNineLanguagesShipAKeyedFile()
        {
            foreach (string language in Languages)
            {
                Assert.That(
                    File.Exists(KeyedPath(language)), Is.True,
                    language + " has no Keyed file, so every string in it falls back to the key name.");
            }
        }

        [Test]
        public void EveryLanguageDeclaresExactlyTheSameKeys()
        {
            Dictionary<string, List<string>> byLanguage = Languages.ToDictionary(
                language => language,
                language => Keys(language).ToList());

            var english = new HashSet<string>(byLanguage["English"]);

            Assert.That(english.Count, Is.GreaterThan(0), "English declares no keys at all.");

            foreach (string language in Languages)
            {
                var keys = new HashSet<string>(byLanguage[language]);

                Assert.That(
                    byLanguage[language].Count, Is.EqualTo(keys.Count),
                    language + " declares a key twice: "
                    + string.Join(", ", byLanguage[language].GroupBy(k => k)
                        .Where(g => g.Count() > 1).Select(g => g.Key)));

                Assert.That(
                    english.Except(keys).ToList(), Is.Empty,
                    language + " is missing keys English has, so those strings render in game as "
                    + "their raw key names.");

                Assert.That(
                    keys.Except(english).ToList(), Is.Empty,
                    language + " declares keys English does not, which is usually a rename that "
                    + "landed in only one file.");
            }
        }

        [Test]
        public void EveryKeyUsesTheSamePlaceholdersInEveryLanguage()
        {
            Dictionary<string, string> english = Values("English");

            foreach (string language in Languages)
            {
                Dictionary<string, string> values = Values(language);

                foreach (KeyValuePair<string, string> pair in english)
                {
                    if (!values.TryGetValue(pair.Key, out string translated))
                    {
                        continue;
                    }

                    Assert.That(
                        Placeholders(translated), Is.EquivalentTo(Placeholders(pair.Value)),
                        language + " has different placeholders from English in " + pair.Key
                        + ". English: \"" + pair.Value + "\". " + language + ": \"" + translated
                        + "\". A dropped placeholder renders as missing text and an extra one "
                        + "renders literally.");
                }
            }
        }

        [Test]
        public void NoShippedStringIsEmpty()
        {
            foreach (string language in Languages)
            {
                List<string> empty = Values(language)
                    .Where(pair => string.IsNullOrWhiteSpace(pair.Value))
                    .Select(pair => pair.Key)
                    .ToList();

                Assert.That(empty, Is.Empty,
                    language + " ships empty strings: " + string.Join(", ", empty));
            }
        }

        [Test]
        public void EveryKeyTheCodeAsksForIsDeclaredInEveryLanguage()
        {
            var declared = new HashSet<string>(Keys("English"));
            var asked = new HashSet<string>();

            // The literal must be followed by .Translate to count. Matching every
            // "BiosculpterDetox_" string instead would be too wide: a Harmony id or a cycle key
            // reads exactly like a translation key and is not one.
            //
            // The limit, stated rather than left to be found: a key reached through a variable or
            // built by concatenation is invisible here. This scan sees the direct form, which is
            // the form the whole mod uses.
            var pattern = new Regex("\"(BiosculpterDetox_[A-Za-z0-9_]+)\"\\s*\\.\\s*Translate");

            string[] sources = Directory.GetFiles(
                Path.Combine(RepoRoot(), "1.6"), "*.cs", SearchOption.AllDirectories);

            Assert.That(sources, Is.Not.Empty,
                "Found no C# under 1.6, so this test scanned nothing.");

            foreach (string file in sources)
            {
                foreach (Match match in pattern.Matches(File.ReadAllText(file)))
                {
                    asked.Add(match.Groups[1].Value);
                }
            }

            Assert.That(asked.Count, Is.GreaterThan(0),
                "Found no translation keys in the C# at all, so this test is scanning nothing.");

            Assert.That(
                asked.Except(declared).ToList(), Is.Empty,
                "The code asks for keys no language declares, which render as the raw key name.");
        }

        private static IEnumerable<string> Placeholders(string value)
        {
            // Deliberately NOT Distinct(). Is.EquivalentTo honours duplicate counts, so keeping
            // every match is what makes a placeholder repeated in one language a failure as well as
            // a placeholder dropped. Ordering is not compared, and must not be: word order moves
            // between languages, so a string taking two arguments can legitimately use them in a
            // different order.
            return Regex.Matches(value, "\\{[0-9]+\\}").Cast<Match>().Select(m => m.Value);
        }

        [Test]
        public void TheRussianCaseTableDeclinesTheCycleLabelTheWayTheGameLooksItUp()
        {
            // Vanilla's Russian pod strings put the cycle label through {lookup: {0}; Case; 1}, the
            // genitive, in the cycle button, the enter option, the entering message and the
            // refusal: "Начать цикл детоксикации". A label missing from the table is printed as it
            // is, in the nominative, which is ungrammatical in all four. The game ships no entry
            // for this mod's label, so the mod supplies one, and this holds that entry to the label
            // it declines: rename either and the table silently stops matching.
            string directory = Path.Combine(RepoRoot(), "1.6", "Languages", "Russian", "WordInfo");

            // Lowercase, and compared by exact name rather than File.Exists, which ignores case on
            // this machine. LanguageWordInfo.GetLookupTable lowercases the table name before it
            // builds the file name, and a mod folder is a plain filesystem lookup, so "Case.txt"
            // would be missed on a case-sensitive filesystem.
            Assert.That(
                Directory.GetFiles(directory).Select(Path.GetFileName),
                Is.EquivalentTo(new[] { "case.txt" }));

            // Parsed the way LanguageWordInfo.RegisterLut does: GenText.LinesFromString drops
            // blank and "//" lines and cuts a trailing comment, then TryGetSeparatedValues splits
            // on ';' and trims, and the first form lowercased is the key.
            var table = new Dictionary<string, string[]>();

            foreach (string raw in File.ReadAllText(Path.Combine(directory, "case.txt"))
                         .Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                string line = raw.Trim();

                if (line.StartsWith("//"))
                {
                    continue;
                }

                line = line.Split(new[] { "//" }, System.StringSplitOptions.None)[0];

                if (line.Length == 0)
                {
                    continue;
                }

                Assert.That(line, Does.Contain(";"), "The game fails to parse this line: " + line);

                string[] forms = line.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries)
                    .Select(form => form.Trim())
                    .ToArray();

                table[forms[0].ToLower()] = forms;
            }

            string label = Values("Russian")["BiosculpterDetox_CycleLabel"].ToLower().Trim();

            Assert.That(table.ContainsKey(label), Is.True,
                "The Russian case table has no entry for the cycle label \"" + label + "\".");
            Assert.That(table[label].Length, Is.EqualTo(6),
                "A case entry is nominative, genitive, dative, accusative, instrumental, prepositional.");
            Assert.That(table[label][1], Is.Not.EqualTo(table[label][0]),
                "The genitive, the form the pod's strings ask for, is the nominative again.");
        }

        private static IEnumerable<string> Keys(string language)
        {
            return XDocument.Load(KeyedPath(language)).Root.Elements().Select(e => e.Name.LocalName);
        }

        private static Dictionary<string, string> Values(string language)
        {
            return XDocument.Load(KeyedPath(language)).Root.Elements()
                .GroupBy(e => e.Name.LocalName)
                .ToDictionary(g => g.Key, g => g.First().Value);
        }

        private static string KeyedPath(string language)
        {
            return Path.Combine(
                RepoRoot(), "1.6", "Languages", language, "Keyed", "BiosculpterDetox_Keys.xml");
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
