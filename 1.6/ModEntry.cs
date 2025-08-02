using HarmonyLib;
using Verse;
using UnityEngine;

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

            Log.Message("[BiosculpterDetox] Loaded version 1.0 successfully.");
        }
    }
}