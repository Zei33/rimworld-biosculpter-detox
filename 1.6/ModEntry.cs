using BiosculpterDetox.Core;
using BiosculpterDetox.Patches;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace BiosculpterDetox
{
    /// <summary>
    /// Main mod entry point for the BiosculpterDetox mod.
    /// Handles mod initialization, settings and Harmony patching for biosculpter detox functionality.
    /// </summary>
    public class BiosculpterDetoxMod : Mod
    {   
        /// <summary>
        /// Gets the mod settings instance for BiosculpterDetox.
        /// </summary>
        /// <remarks>
        /// Read at call boundaries only, never from inside the detox predicates. Those take their
        /// inputs as parameters so that they remain reachable by the test harness, which cannot
        /// construct a <see cref="Mod"/> and gets a null back from <c>LoadedModManager.GetMod</c>
        /// rather than an exception.
        /// </remarks>
        public static BiosculpterDetoxSettings Settings { get; private set; }

        /// <summary>
        /// The Harmony instance used for applying patches to the base game.
        /// </summary>
        private readonly Harmony harmony;

        /// <summary>
        /// Initializes a new instance of the <see cref="BiosculpterDetoxMod"/> class.
        /// Sets up settings and Harmony patches and logs successful initialization.
        /// </summary>
        /// <param name="pack">The mod content pack containing mod information and assets.</param>
        public BiosculpterDetoxMod(ModContentPack pack) : base(pack)
        {
            Settings = GetSettings<BiosculpterDetoxSettings>();

            harmony = new Harmony("com.zei33.biosculpterdetox");
            harmony.PatchAll();

            // The version comes from the mod's own metadata rather than a literal. The literal said
            // "1.0" while About.xml said 1.0.1, which is the predictable end state for a number
            // written down in two places, and a log line claiming the wrong version is worse than
            // no log line when somebody is reading a player's output_log to work out what they ran.
            //
            // Gated on dev mode: informational logging is for whoever is debugging, and Log.Error is
            // left alone.
            if (Prefs.DevMode)
            {
                Log.Message($"[BiosculpterDetox] Loaded version {pack.ModMetaData.ModVersion}.");
            }
        }

        /// <summary>
        /// Gets the category name for this mod in the settings menu.
        /// </summary>
        /// <returns>The display name for the mod's settings category.</returns>
        /// <remarks>
        /// Returning a non-empty string here is the whole of the registration. There is no
        /// <c>OptionCategoryDef</c>, no <c>About.xml</c> field and no def to write; this mod ships
        /// no XML at all and still does not need any.
        /// </remarks>
        public override string SettingsCategory() => "BiosculpterDetox_SettingsCategory".Translate();

        /// <summary>
        /// Renders the mod settings window content.
        /// </summary>
        /// <param name="inRect">The rectangle area available for drawing the settings interface.</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }

        /// <summary>
        /// Saves the settings when the window closes, and pushes the duration to pods already built.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>Dialog_ModSettings.PreClose</c> calls this, and it is the only notice a mod gets that
        /// the settings window has gone.
        /// </para>
        /// <para>
        /// The push is necessary rather than tidy. The cycle's properties are built per pod inside
        /// the spawn postfix, so without this a duration change would reach no pod already standing
        /// on the map and would only take effect on the next load. To a player that reads as the
        /// setting doing nothing, and the obvious next step is to change it again.
        /// </para>
        /// </remarks>
        public override void WriteSettings()
        {
            base.WriteSettings();
            BiosculpterPatches.ApplyDurationToSpawnedPods(Settings.CycleDurationDays);
        }
    }
}
