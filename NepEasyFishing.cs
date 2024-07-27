using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace NepEasyFishing
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        private readonly ConfigEntry<bool> _FishBarQuickProgress;
        private readonly ConfigEntry<bool> _FishBarNoDecrease;
        private readonly ConfigEntry<bool> _FishBarQuickBites;
        public Plugin()
        {

            _FishBarQuickProgress = Config.Bind("General", "Quick Progress", true, "Fishing progress fills quickly when fish is clicked on");
            _FishBarNoDecrease = Config.Bind("General", "No Bar Decrease", true, "Fishing progress does not lower");
            _FishBarQuickBites = Config.Bind("General", "Quick Bites", true, "reduced time before bites");
        }


        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }
    }
}
