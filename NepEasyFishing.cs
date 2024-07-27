using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using UnityEngine;


namespace NepEasyFishing
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private static Harmony _harmony;

        internal static ManualLogSource Log;

        private readonly ConfigEntry<bool> _FishBarQuickProgress;
        private readonly ConfigEntry<bool> _FishBarNoDecrease;
        private readonly ConfigEntry<bool> _FishBarQuickBites;
        private readonly ConfigEntry<bool> _debugLogging;

        private readonly static bool debugLoggingStatic = true;


        public Plugin()
        {

            _FishBarQuickProgress = Config.Bind("General", "Quick Progress", true, "Fishing progress fills quickly when fish is clicked on");
            _FishBarNoDecrease = Config.Bind("General", "No Bar Decrease", true, "Fishing progress does not lower");
            _FishBarQuickBites = Config.Bind("General", "Quick Bites", true, "reduced time before bites");
            _debugLogging = Config.Bind("Debug", "Debug Logging", false, "logs additional information to console");
        }


        private void Awake()
        {
            // Plugin startup logic
            Log = base.Logger;
            _harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }
        private void OnDestroy()
        {
            _harmony.UnpatchSelf();
        }
        public static void DebugLog(string message) //How to call? Can't make it static and still reference _debugLogging and Logger, , can't make _debugLogging static because then config does not work.

        {
            //if (_debugLogging.Value)
            if (debugLoggingStatic)
            {
                Log.LogInfo(String.Format("### {0}", message));
            }
        }

        //////////////////////////////////////////////////////////////////
        //  Harmony Patches

        [HarmonyPatch(typeof(FishingUI), "StartFishingGame")]
        [HarmonyPrefix]
        static bool StartFishingGamePrefix(FishingUI __instance, Rod NBFLKCJPPAG)
        {
            //Plugin.DebugLog("StartFishingGamePrefix");
            //Plugin.DebugLog(String.Format("Pre:  {0}, {1}", __instance.hitProgression, __instance.hitReduction));
            __instance.hitProgression = 0.5f;
            __instance.hitReduction = 0.0002f;
            //Plugin.DebugLog(String.Format("Post: {0}, {1}", __instance.hitProgression, __instance.hitReduction));
            return true;
        }
    }

}