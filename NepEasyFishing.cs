using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;


namespace NepEasyFishing
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private static Harmony _harmony;

        internal static ManualLogSource Log;

        private static ConfigEntry<bool> _FishBarQuickProgress;
        private static ConfigEntry<bool> _FishBarNoDecrease;
        private static ConfigEntry<bool> _FishBarQuickBites;
        private static ConfigEntry<bool> _InstantCatch;
        private static ConfigEntry<bool> _debugLogging;
        private static ConfigEntry<bool> _dontUseBait;



        public Plugin()
        {
            // bind to config settings
            _FishBarQuickProgress = Config.Bind("General", "Quick Progress", true, "Fishing minigame progress fills very quickly when fish is clicked on");
            _FishBarNoDecrease = Config.Bind("General", "No Bar Decrease", true, "Fishing minigame progress does not decrease");
            _FishBarQuickBites = Config.Bind("General", "Quick Bites", true, "Reduced time before bites, no fake bites");
            _debugLogging = Config.Bind("Debug", "Debug Logging", false, "Logs additional information to console");
            _InstantCatch = Config.Bind("General", "Instant Catch", true, "Instantly catch fish once hooked instead of starting the minigame");
            _dontUseBait =  Config.Bind("General", "Dont use bait", false, "Don't consume bait when fishing");
        }


        private void Awake()
        {
            // Plugin startup logic
            Log = base.Logger;
            _harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
            Logger.LogInfo($"NepEasyFishing: Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }
        private void OnDestroy()
        {
            _harmony.UnpatchSelf();
        }
        public static void DebugLog(string message) 
        {
            // Log a message to console only if debug is enabled in console
            if (_debugLogging.Value)
            {
                Log.LogInfo(String.Format("NepEasyFishing: Debug: {0}", message));
            }
        }

        //////////////////////////////////////////////////////////////////
        //  Fast Progress, No progress loss

        [HarmonyPatch(typeof(FishingUI), "StartFishingGame")]
        [HarmonyPrefix]
        static bool StartFishingGamePrefix(FishingUI __instance, Rod NBFLKCJPPAG)
        {
            //Plugin.DebugLog("StartFishingGamePrefix");
            if (_FishBarQuickProgress.Value)
            {
                __instance.hitProgression = 0.5f;           // default 0.05f
            }
            if (_FishBarNoDecrease.Value)
            { 
                __instance.hitReduction = 0.0002f;          // default 0.02f
                __instance.barReductionPerSecond = 0.0002f; // default 0.15f
            }
            // These control fish movement, type Vector2. I assume they are loaded from the fish type
            //__instance.movementDistanceMinMax =
            //__instance.movementTimeMinMax = 
            //__instance.stopTimeMinMax =
            return true;
        }


        //////////////////////////////////////////////////////////////////
        ///  quicker Bites

        [HarmonyPatch(typeof(FishingController), "StartFishingCoroutine")]
        [HarmonyPrefix]
        static bool StartFishingCoroutinePrefix(FishingController __instance, Vector3 EECADGJPJAP, Rod NBFLKCJPPAG)
        {

            Plugin.DebugLog("StartFishingCoroutinePrefix");
            if (_FishBarQuickBites.Value)
            {
                // One real bite shortly after starting. 
                __instance.timeBetweenBites = 0.1f;    // default 1f
                __instance.totalTime = 1;               // default 8
                __instance.bitesNum.x = 1;              // default bitesNum Vector2Int(3, 5); 
                __instance.bitesNum.y = 0;              // gets called as UnityEngine.Random.Range(this.bitesNum.x, this.bitesNum.y + 1);
            }
            return true;
        }


        //////////////////////////////////////////////////////////////////
        ///  Don't use bait
        ///  

        [HarmonyPatch(typeof(FishingController), "FinishFishing")]
        [HarmonyPrefix]
        static void FinishFishingPrefix(FishingController __instance)
        {
            if (_dontUseBait.Value)
            { 
                //We're going to add an extra piece of the selected bait to the players inventorey right before the code that (among other things) removes the bait.

                // If we can get the numeric id of the bait Item we can make an ItemInstance of it with "new ItemInstance" 
                //__instance.baitSelected is an enum 
                // FishingManager.BaitItem() returns an Item when given the bait enum
                Item baitItem = FishingManager.BaitItem(__instance.baitSelected);
                //Then use reflection to get the id from the private field
                int reflectedItemID = 0;
                reflectedItemID = Traverse.Create(baitItem).Field("id").GetValue<int>();
                if (reflectedItemID != 0)
                {
                    DebugLog(String.Format("FinishFishing Prefix: bait itemID {0}", reflectedItemID));
                    ItemInstance baitItemInstance = new ItemInstance(ItemDatabaseAccessor.GetItem(reflectedItemID, false, true));
                    PlayerInventory.GetPlayer(__instance.playerNum).AddItem(baitItemInstance); //AddItem wants an item instance, not an item
                }
                else
                {
                    DebugLog("FinishFishing Prefix: failed to get itemId for bait");
                }
            }

        }

        //////////////////////////////////////////////////////////////////
        ///  Instant Catch


        [HarmonyPatch(typeof(FishingUI), "LateUpdate")]
        [HarmonyPrefix]
        static bool LateUpdatePrefix(FishingUI __instance)
        {
            if (_InstantCatch.Value)
            {
                if (__instance.content.activeInHierarchy)
                {
                    // Get the private slider object
                    Slider reflectedSlider = Traverse.Create(__instance).Field("progress").GetValue<Slider>(); //type Unity.UI.Slider, NOT Unity.UIElements.Slider

                    if (reflectedSlider != null)
                    {
                        //Plugin.DebugLog(String.Format("LateUpdatePrefix: reflectedSlider found, value {0}", reflectedSlider.value));
                        // Set progress slider value to 1.0 for instant completion
                        reflectedSlider.value = 1.0f;
        
                    }
                    else
                    {
                        Plugin.DebugLog("LateUpdatePrefix: ProgressSlider is null");
                    }
                }
            }
            return true;
        }
        


    }


}


