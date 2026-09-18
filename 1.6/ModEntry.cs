using HarmonyLib;
using Verse;

namespace BiosculpterDetox
{
    /// <summary>
    /// Main mod entry point for the BiosculpterDetox mod.
    /// Handles mod initialization and Harmony patching for biosculpter detox functionality.
    /// </summary>
    public class BiosculpterDetoxMod : Mod
    {   
        /// <summary>
        /// The Harmony instance used for applying patches to the base game.
        /// </summary>
        private readonly Harmony harmony;

        /// <summary>
        /// Initializes a new instance of the <see cref="BiosculpterDetoxMod"/> class.
        /// Sets up Harmony patches and logs successful initialization.
        /// </summary>
        /// <param name="pack">The mod content pack containing mod information and assets.</param>
        public BiosculpterDetoxMod(ModContentPack pack) : base(pack)
        {
            harmony = new Harmony("com.zei33.biosculpterdetox");
            harmony.PatchAll();

            // The version comes from the mod's own metadata rather than a literal. The literal said
            // "1.0" while About.xml said 1.0.1, which is the predictable end state for a number
            // written down in two places, and a log line claiming the wrong version is worse than
            // no log line when somebody is reading a player's output_log to work out what they ran.
            //
            // Gated on dev mode, matching the house pattern in Simple Improve: informational
            // logging is for whoever is debugging, and Log.Error is left alone.
            if (Prefs.DevMode)
            {
                Log.Message($"[BiosculpterDetox] Loaded version {pack.ModMetaData.ModVersion}.");
            }
        }
    }
}