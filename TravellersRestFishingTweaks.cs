using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;


namespace TravellersRestFishingTweaks
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private static Harmony _harmony;

        internal static ManualLogSource Log;

        private static ConfigEntry<bool> _FishBarQuickProgress;
        private static ConfigEntry<float> _FishBarQuickProgressAmount;
        private static ConfigEntry<bool> _FishBarQuickProgressOnMiss;
        private static ConfigEntry<bool> _FishBarNoDecrease;
        private static ConfigEntry<bool> _FishBarQuickBites;
        private static ConfigEntry<bool> _InstantCatch;
        private static ConfigEntry<bool> _debugLogging;
        private static ConfigEntry<bool> _dontUseBait;
        private static ConfigEntry<bool> _autoFish;
        private static ConfigEntry<bool> _autoReel;
        private static ConfigEntry<float> _autoFishRecastDelay;
        private static ConfigEntry<bool> _removeRecastDelay;
        private static bool _loggedFishingUiActive;
        private static bool _loggedPollingFallbackActive;
        private static bool _loggedEasyMinigameFallbackActive;
        private static bool _loggedAutoFishActive;
        private static bool _loggedUpdateProof;
        private static float _nextPollingDiagnosticsTime;
        private static int _uiProbeLogFramesRemaining = 600;
        private static readonly float[] _maxFishingProgressByPlayer = new float[5];
        private static readonly float[] _baitProtectionUntilByPlayer = new float[8];
        private static readonly int[] _protectedBaitIdByPlayer = new int[8];
        private static readonly int[] _protectedBaitBaselineByPlayer = new int[8];
        private static readonly int[] BaitIds = { 1444, 1445, 1446, 1447, 1448 };
        private static readonly int[,] _lastBaitCountsByPlayer = new int[5, 5];
        private static readonly bool[] _baitMonitorInitializedByPlayer = new bool[5];
        private static readonly float[] _rodSelectedUntilByPlayer = new float[5];
        private static readonly float[] _fishingContextUntilByPlayer = new float[5];
        private static readonly int[] _lastSelectedBaitIdByPlayer = new int[5];
        private static readonly float[] _lastSelectedBaitSeenAtByPlayer = new float[5];
        private static int _lastDontUseBaitMonitorFrame = -1;
        private static bool _isRefundingBait;
        private static int _lastAutoFishPollFrame = -1;
        private static readonly float[] _nextAutoFishCastAtByPlayer = new float[5];
        private static readonly float[] _autoRecastEndUseAtByPlayer = new float[5];
        private static readonly float[] _lastAutoRecastCastAtByPlayer = new float[5];
        private static readonly bool[] _autoRecastSessionActiveByPlayer = new bool[5];
        private static readonly bool[] _autoRecastCastingNowByPlayer = new bool[5];
        private static readonly float[] _autoRecastPausedUntilByPlayer = new float[5];
        private static readonly float[] _autoRecastIgnoreManualInputUntilByPlayer = new float[5];
        private static readonly float[] _autoFishReelUntilByPlayer = new float[5];
        private static readonly float[] _autoReelInputUntilByPlayer = new float[5];
        private static readonly float[] _autoReelDirectFinishCooldownUntilByPlayer = new float[5];
        private static readonly float[] _lastFishingStartAtByPlayer = new float[5];
        private static readonly float[] _lastHookSetBaitAtByPlayer = new float[5];
        private static readonly float[] _lastAutoReelDirectFinishAtByPlayer = new float[5];
        private static readonly float[] _autoReelLastArmLogAtByPlayer = new float[5];
        private static readonly float[] _lastRodActionAcceptedAtByPlayer = new float[5];
        private static readonly float[] _lastRodActionSuppressedAtByPlayer = new float[5];
        private static int _lastAutoReelPollFrame = -1;
        private static string _autoReelCoroutineMethod = string.Empty;
        private static float _nextAutoReelCoroutineDiagAt;
        private static readonly float[] _lastFinishFishingAtByPlayer = new float[5];
        private static readonly float[] _lastEndSelectedUseAtByPlayer = new float[5];
        private static readonly float[] _lastEndSelectedUseFinishAtByPlayer = new float[5];
        private static readonly float[] _lastRemoveRecastCleanupAtByPlayer = new float[5];
        private static readonly float[] _removeRecastDelayCleanupAtByPlayer = new float[5];
        private static readonly float[] _recastUnlockWindowUntilByPlayer = new float[5];
        private static readonly float[] _recastUnlockWindowFinishAtByPlayer = new float[5];
        private static readonly float[] _nextRecastUnlockDiagAtByPlayer = new float[5];
        private static readonly bool[] _pendingRecastByPlayer = new bool[5];
        private static readonly GameObject[] _preservedFishResultVisualByPlayer = new GameObject[5];
        private static readonly float[] _preservedFishResultVisualUntilByPlayer = new float[5];
        private static int _lastRemoveRecastDelayPollFrame = -1;
        private static readonly HashSet<int> _baitItemIds = new HashSet<int> { 1444, 1445, 1446, 1447, 1448 };
        private static readonly Dictionary<int, string> _baitNamesByItemId = new Dictionary<int, string>
        {
            { 1444, "Worm" },
            { 1445, "Larva" },
            { 1446, "Meat Bait" },
            { 1447, "Seafood Bait" },
            { 1448, "Lure" }
        };

        private const int MinSafePlayerNum = 1;
        private const int MaxSafePlayerNum = 2;
        private const int AutoFishPlayer = 1;

        private const string BuildProofStamp = "20260513-recast-zero-all-waits";
        private const float RecastUnlockWindowDuration = 2.0f;

        private const float MinAutoRecastDelay = 1.25f;
        private const float AutoRecastPostFinishSettle = 1.25f;
        private const float AutoRecastPostCleanupSettle = 1.25f;
        private const float AutoRecastFailedAttemptCooldown = 1.0f;
        private const float AutoRecastInterruptPause = 2.0f;
        private const float AutoRecastEndUseDelay = 0.15f;
        private const float AutoReelDirectFinishMinBaitAge = 0.50f;
        private const float QuickBitesMinFishingAge = 0.25f;
        private const float DuplicateRodActionWindow = 0.35f;
        private const float FishResultDisplayMinTime = 1.25f;
        private const string PreservedFishResultVisualSuffix = ".EasyFishingPreservedResult";

        private static readonly FieldInfo FishingControllerSettings =
            AccessTools.Field(typeof(FishingController), "settings");

        private static readonly Dictionary<string, float> _nextInputButtonDiagAtByPlayerButton = new Dictionary<string, float>();
        private static readonly Dictionary<string, float> _nextQuickBitesDiagAtByPlayerReason = new Dictionary<string, float>();
        private static readonly Dictionary<string, float> _nextStateOnlyCleanupDiagAtByPlayerSource = new Dictionary<string, float>();
        private static readonly Dictionary<string, float> _nextObservedCompletedSessionDiagAtByPlayerSource = new Dictionary<string, float>();
        private static readonly float[] _nextCompletedStateHeartbeatAtByPlayer = new float[5];
        private static GameObject _preservedFishResultRoot;
        private static MethodInfo _rodActionEndMethod;
        private static MethodInfo _rodActionEndFallbackMethod;
        private static bool _commonReferencesWait1_5Zeroed = false;

        private static readonly FieldInfo FishingUISettings = AccessTools.Field(typeof(FishingUI), "settings");
        private static readonly FieldInfo FishingUIFishIcon = AccessTools.Field(typeof(FishingUI), "fishIcon");
        private static readonly FieldInfo FishingUIBox = AccessTools.Field(typeof(FishingUI), "box");
        private static readonly FieldInfo FishingUIHitThreshold = AccessTools.Field(typeof(FishingUI), "DNNFOPAGBPD");

        private static FieldInfo DifficultySettingsField;

        public Plugin()
        {
            // bind to config settings
            _FishBarQuickBites = Config.Bind("General", "Quick Bites", true,
                "Reduces the wait before a fish bites and removes fake bites. Default: true.");
            _InstantCatch = Config.Bind("General", "Instant Catch", true,
                "Instantly completes the catch after a real hook, skipping the fishing minigame. Default: true.");

            _FishBarQuickProgress = Config.Bind("General", "Quick Progress", true,
                "Makes fishing minigame progress fill faster while you hold the fishing input. Default: true.");
            _FishBarQuickProgressAmount = Config.Bind("General", "Quick Progress Amount", 0.15f,
                "Progress added per second while Quick Progress is enabled. Values below 0 are treated as 0. Default: 0.15.");
            _FishBarQuickProgressOnMiss = Config.Bind("General", "Quick Progress On Miss", false,
                "If enabled, Quick Progress still increases while you hold the fishing input even when the fish is outside the target box. Default: false.");
            _FishBarNoDecrease = Config.Bind("General", "No Bar Decrease", true,
                "Prevents fishing minigame progress from decreasing over time. Default: true.");

            _dontUseBait = Config.Bind("General", "Dont use bait", false,
                "Prevents bait from being consumed while fishing. Default: false.");

            _autoReel = Config.Bind("General", "Auto Reel", false,
                "Automatically reels in when a real bite occurs. If Instant Catch is enabled, the fish is caught immediately. Default: false.");
            _autoFish = Config.Bind("General", "Auto Recast Rod", false,
                "Automatically recasts the selected fishing rod after a catch finishes. Cast manually once with the rod selected to start the session. To stop it, switch away from the fishing rod on the action bar. When enabled, the mod also handles reeling so the recast loop can continue. Default: false.");
            _autoFishRecastDelay = Config.Bind("General", "Auto Recast Rod Delay", 1.25f,
                "Seconds Auto Recast Rod waits between recast attempts. Values below 1.25 are treated as 1.25. Default: 1.25.");
            _removeRecastDelay = Config.Bind("General", "Remove Recast Delay", false,
                "Removes the extra post-catch delay so you can cast again sooner after rewards are granted. Default: false.");

            _debugLogging = Config.Bind("Debug", "Debug Logging", false,
                "Writes additional diagnostic information to the BepInEx log and console. Default: false.");
        }


        private void Awake()
        {
            // Plugin startup logic
            Log = Logger;
            Logger.LogInfo($"TravellersRestFishingTweaks: Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            LogBuildProof("Awake");
            RemoveLegacyAutoRecastPlayerConfig();
            LogConfigProof();
            InstallTickerFallback();

            // Since this field is compiler generated and the name changes each build, it needs to be looked up by type
            var expectedType = typeof(FishingManagerSettings.DifficultySettings);
            DifficultySettingsField = typeof(FishingUI).GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(f =>
                    f.FieldType ==
                    expectedType);
            if (DifficultySettingsField == null)
                Log.LogWarning(
                    $"Could not find field for {nameof(FishingManagerSettings)}.{nameof(FishingManagerSettings.DifficultySettings)} on {nameof(FishingUI)}.");
            else
                DebugLog($"Found DifficultySettings field: {DifficultySettingsField.Name}");

            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);

            var startFishingGameMethod = AccessTools.Method(typeof(FishingUI), nameof(FishingUI.StartFishingGame), new[] { typeof(Rod) });
            var startFishingCoroutineMethod = AccessTools.Method(typeof(FishingController), nameof(FishingController.StartFishingCoroutine), new[] { typeof(Vector3), typeof(Rod) });
            var startFishingCoroutineAliasEhofMethod = ResolveMethod(typeof(FishingController), "EHOFOMHDPFJ", new[] { typeof(Vector3), typeof(Rod) });
            var startFishingCoroutineAliasJfmcMethod = ResolveMethod(typeof(FishingController), "JFMCNPDJLLI", new[] { typeof(Vector3), typeof(Rod) });
            var startFishingCoroutineAliasFfkpMethod = ResolveMethod(typeof(FishingController), "FFKPLBGGKLB", new[] { typeof(Vector3), typeof(Rod) });
            var startFishingCoroutineAliasJkofMethod = ResolveMethod(typeof(FishingController), "JKOFBKKPJAN", new[] { typeof(Vector3), typeof(Rod) });
            var startFishingCoroutineAliasMojhMethod = ResolveMethod(typeof(FishingController), "MOJHEIHKEKO", new[] { typeof(Vector3), typeof(Rod) });
            var createBitesListMethod = AccessTools.Method(typeof(FishingController), nameof(FishingController.CreateBitesList), Type.EmptyTypes);
            var createBitesListAliasFmceMethod = ResolveMethod(typeof(FishingController), "FMCEGPBACPC", Type.EmptyTypes);
            var createBitesListAliasIhiaMethod = ResolveMethod(typeof(FishingController), "IHIAGODKCLJ", Type.EmptyTypes);
            var createBitesListAliasEgdoMethod = ResolveMethod(typeof(FishingController), "EGDOBIADPMA", Type.EmptyTypes);
            var finishFishingMethod = AccessTools.Method(typeof(FishingController), nameof(FishingController.FinishFishing), new[] { typeof(bool) });
            var finishFishingAliasMoeckMethod = ResolveMethod(typeof(FishingController), "MOECKIFAFII", new[] { typeof(bool) });
            var finishFishingAliasIaacMethod = ResolveMethod(typeof(FishingController), "IAACFAPNOFI", new[] { typeof(bool) });
            var finishFishingAliasHjpiMethod = ResolveMethod(typeof(FishingController), "HJPINMGNLJB", new[] { typeof(bool) });
            var lateUpdateMethod = AccessTools.Method(typeof(FishingUI), "LateUpdate", Type.EmptyTypes);
            var fishingHookSetFakeMethod = ResolveMethod(typeof(FishingHook), "SetFake", Type.EmptyTypes);
            var fishingHookSetBaitMethod = ResolveMethod(typeof(FishingHook), "SetBait", Type.EmptyTypes);
            var playerInventoryRemoveItemOneArgMethod = ResolveMethod(typeof(PlayerInventory), "RemoveItem", new[] { typeof(Item) });
            var playerInventoryRemoveItemMethod = ResolveMethod(typeof(PlayerInventory), "RemoveItem", new[] { typeof(Item), typeof(bool) });
            var playerInventoryRemoveItemAliasMethod = ResolveMethod(typeof(PlayerInventory), "OOEJMKIAPLC", new[] { typeof(Item), typeof(bool) });
            var playerInventoryAddItemMethod = ResolveMethod(typeof(PlayerInventory), "AddItem", new[] { typeof(ItemInstance), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
            var playerInventoryAddItemAliasMethod = ResolveMethod(typeof(PlayerInventory), "OJDGOADOCMG", new[] { typeof(ItemInstance), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
            var containerRemoveItemMethod = ResolveMethod(typeof(Container), "RemoveItem", new[] { typeof(Item), typeof(bool) });
            var slotConsumeOneMethod = ResolveMethod(typeof(Slot), "MEODNPFJDMH", new[] { typeof(bool) });
            var slotConsumeOneAliasMethod = ResolveMethod(typeof(Slot), "MBCIJPPOGJG", new[] { typeof(bool) });
            var slotSetStackMethod = ResolveMethod(typeof(Slot), "BGJPNGLONLP", new[] { typeof(int), typeof(bool), typeof(bool) });
            var playerInputsGetButtonDownMethod = ResolveMethod(typeof(PlayerInputs), "GetButtonDown", new[] { typeof(string) });
            var playerInputsGetButtonMethod = ResolveMethod(typeof(PlayerInputs), "GetButton", new[] { typeof(string) });
            var playerInputsJcmButtonDownMethod = ResolveMethod(typeof(PlayerInputs), "JCMOPOMLPLL", new[] { typeof(string) });
            var playerInputsDlfButtonDownMethod = ResolveMethod(typeof(PlayerInputs), "DLFAMOCKNMA", new[] { typeof(string) });
            var monoBehaviourStartCoroutineMethod = ResolveMethod(typeof(MonoBehaviour), "StartCoroutine", new[] { typeof(IEnumerator) });
            var fishingCoroutineMoveNextMethods = FindFishingCoroutineMoveNextMethods().ToList();

            DebugLog($"Target resolution: FishingUI.StartFishingGame(Rod) => {(startFishingGameMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingController.StartFishingCoroutine(Vector3, Rod) => {(startFishingCoroutineMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingController.EHOFOMHDPFJ(Vector3, Rod) => {(startFishingCoroutineAliasEhofMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingController.JFMCNPDJLLI(Vector3, Rod) => {(startFishingCoroutineAliasJfmcMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingController.FFKPLBGGKLB(Vector3, Rod) => {(startFishingCoroutineAliasFfkpMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingController.JKOFBKKPJAN(Vector3, Rod) => {(startFishingCoroutineAliasJkofMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingController.MOJHEIHKEKO(Vector3, Rod) => {(startFishingCoroutineAliasMojhMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingController.CreateBitesList() => {(createBitesListMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingController.FMCEGPBACPC() => {(createBitesListAliasFmceMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingController.IHIAGODKCLJ() => {(createBitesListAliasIhiaMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingController.EGDOBIADPMA() => {(createBitesListAliasEgdoMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingController.FinishFishing(bool) => {(finishFishingMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingController.MOECKIFAFII(bool) => {(finishFishingAliasMoeckMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingController.IAACFAPNOFI(bool) => {(finishFishingAliasIaacMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingController.HJPINMGNLJB(bool) => {(finishFishingAliasHjpiMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingUI.LateUpdate() => {(lateUpdateMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingHook.SetFake() => {(fishingHookSetFakeMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingHook.SetBait() => {(fishingHookSetBaitMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: PlayerInventory.RemoveItem(Item) => {(playerInventoryRemoveItemOneArgMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: PlayerInventory.RemoveItem(Item, bool) => {(playerInventoryRemoveItemMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: PlayerInventory.OOEJMKIAPLC(Item, bool) => {(playerInventoryRemoveItemAliasMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: PlayerInventory.AddItem(ItemInstance,bool,bool,bool,bool) => {(playerInventoryAddItemMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: PlayerInventory.OJDGOADOCMG(ItemInstance,bool,bool,bool,bool) => {(playerInventoryAddItemAliasMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: Container.RemoveItem(Item, bool) => {(containerRemoveItemMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: Slot.MEODNPFJDMH(bool) => {(slotConsumeOneMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: Slot.MBCIJPPOGJG(bool) => {(slotConsumeOneAliasMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: Slot.BGJPNGLONLP(int,bool,bool) => {(slotSetStackMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: PlayerInputs.GetButtonDown(string) => {(playerInputsGetButtonDownMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: PlayerInputs.GetButton(string) => {(playerInputsGetButtonMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: PlayerInputs.JCMOPOMLPLL(string) => {(playerInputsJcmButtonDownMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: PlayerInputs.DLFAMOCKNMA(string) => {(playerInputsDlfButtonDownMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: MonoBehaviour.StartCoroutine(IEnumerator) => {(monoBehaviourStartCoroutineMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingController nested coroutine MoveNext methods => {fishingCoroutineMoveNextMethods.Count}");
            foreach (var moveNext in fishingCoroutineMoveNextMethods)
                DebugLog($"EASYFISHING_COROUTINE_TARGET type={moveNext.DeclaringType?.FullName} fields={DescribeCoroutineFields(moveNext.DeclaringType)}");

            try
            {
                var startFishingGamePostfix = AccessTools.Method(typeof(Plugin), nameof(StartFishingGamePostfix), new[] { typeof(FishingUI) });
                var startFishingCoroutinePrefix = AccessTools.Method(typeof(Plugin), nameof(StartFishingAnyPrefix), new[] { typeof(FishingController), typeof(MethodBase) });
                var createBitesListPostfix = AccessTools.Method(typeof(Plugin), nameof(CreateBitesListAnyPostfix), new[] { typeof(FishingController), typeof(MethodBase) });
                var finishFishingPrefix = AccessTools.Method(typeof(Plugin), nameof(FinishFishingPrefix), new[] { typeof(FishingController), typeof(bool) });
                var playerInventoryRemoveItemItemPrefix = AccessTools.Method(typeof(Plugin), nameof(PlayerInventoryRemoveItemItemPrefix), new[] { typeof(PlayerInventory), typeof(Item), typeof(Slot).MakeByRefType(), typeof(MethodBase) });
                var playerInventoryRemoveItemItemBoolPrefix = AccessTools.Method(typeof(Plugin), nameof(PlayerInventoryRemoveItemItemBoolPrefix), new[] { typeof(PlayerInventory), typeof(Item), typeof(bool), typeof(Slot).MakeByRefType(), typeof(MethodBase) });
                var playerInventoryAddItemPostfix = AccessTools.Method(typeof(Plugin), nameof(PlayerInventoryAddItemPostfix), new[] { typeof(PlayerInventory), typeof(ItemInstance), typeof(bool), typeof(MethodBase) });
                var containerRemoveItemPrefix = AccessTools.Method(typeof(Plugin), nameof(ContainerRemoveItemPrefix), new[] { typeof(Container), typeof(Item), typeof(bool), typeof(Slot).MakeByRefType(), typeof(MethodBase) });
                var slotConsumeOnePrefix = AccessTools.Method(typeof(Plugin), nameof(SlotConsumeOnePrefix), new[] { typeof(Slot), typeof(bool), typeof(bool).MakeByRefType(), typeof(MethodBase) });
                var slotSetStackPrefix = AccessTools.Method(typeof(Plugin), nameof(SlotSetStackPrefix), new[] { typeof(Slot), typeof(int).MakeByRefType(), typeof(bool), typeof(bool), typeof(MethodBase) });
                var playerInputsGetButtonDownPrefix = AccessTools.Method(typeof(Plugin), nameof(PlayerInputsButtonDownAnyPrefix), new[] { typeof(PlayerInputs), typeof(string), typeof(bool).MakeByRefType(), typeof(MethodBase) });
                var monoBehaviourStartCoroutinePrefix = AccessTools.Method(typeof(Plugin), nameof(MonoBehaviourStartCoroutinePrefix), new[] { typeof(MonoBehaviour), typeof(IEnumerator), typeof(MethodBase) });
                var lateUpdatePrefix = AccessTools.Method(typeof(Plugin), nameof(LateUpdatePrefix), new[] { typeof(FishingUI) });
                var fishingHookSetFakePrefix = AccessTools.Method(typeof(Plugin), nameof(FishingHookSetFakePrefix), new[] { typeof(FishingHook) });
                var fishingHookSetBaitPostfix = AccessTools.Method(typeof(Plugin), nameof(FishingHookSetBaitPostfix), new[] { typeof(FishingHook), typeof(MethodBase) });
                var fishingCoroutineMoveNextPrefix = AccessTools.Method(typeof(Plugin), nameof(FishingCoroutineMoveNextPrefix), new[] { typeof(object), typeof(MethodBase) });

                var rodActionMethod = ResolveMethod(typeof(Rod), "Action", new[] { typeof(int), typeof(bool) });
                var rodActionAliasEhhcMethod = ResolveMethod(typeof(Rod), "EHHCPOCLAJA", new[] { typeof(int), typeof(bool) });
                var rodActionAliasFodgMethod = ResolveMethod(typeof(Rod), "FODGNFMBOFE", new[] { typeof(int), typeof(bool) });
                var rodActionAliasEhooMethod = ResolveMethod(typeof(Rod), "EHOOBFJPPOI", new[] { typeof(int), typeof(bool) });
                var rodActionAliasOhinMethod = ResolveMethod(typeof(Rod), "OHINFBCDKLI", new[] { typeof(int), typeof(bool) });
                var rodActionAliasGgahMethod = ResolveMethod(typeof(Rod), "GGAHICGOLLN", new[] { typeof(int), typeof(bool) });
                var rodNbfbMethod = ResolveMethod(typeof(Rod), "NBFBPMNMBJG", new[] { typeof(int) });
                var rodOfakMethod = ResolveMethod(typeof(Rod), "OFAKNHNLKGI", new[] { typeof(int) });
                var rodAnimStartMethod = ResolveMethod(typeof(Rod), "JGNPMBNGKNG", new[] { typeof(int) });
                var rodAnimHitMethod = ResolveMethod(typeof(Rod), "HNCGNIJLEMH", new[] { typeof(int) });
                var rodAnimEndMethod = ResolveMethod(typeof(Rod), "NAIECLGMMJA", new[] { typeof(int) });
                var rodActionEndMethod = ResolveMethod(typeof(Rod), "ActionEnd", new[] { typeof(int) });
                _rodActionEndMethod = rodActionEndMethod;
                _rodActionEndFallbackMethod = rodAnimEndMethod;

                var animatorToolStartMethod = ResolveMethod(typeof(CharacterAnimator), nameof(CharacterAnimator.ToolStart), Type.EmptyTypes);
                var animatorToolHitMethod = ResolveMethod(typeof(CharacterAnimator), nameof(CharacterAnimator.ToolHit), Type.EmptyTypes);
                var animatorToolEndMethod = ResolveMethod(typeof(CharacterAnimator), nameof(CharacterAnimator.ToolEnd), Type.EmptyTypes);

                var fishingUiOpenMethod = ResolveMethod(typeof(FishingUI), "OpenUI", Type.EmptyTypes, new[] { typeof(int) });
                var fishingUiCloseMethod = ResolveMethod(typeof(FishingUI), "CloseUI", Type.EmptyTypes, new[] { typeof(int) });

                var fishingManagerSelectFishMethod = ResolveMethod(typeof(FishingManager), "SelectAFish", new[] { typeof(int), typeof(Rod) });
                var useObjectUseSelectedItemMethod = ResolveMethod(typeof(UseObject), "UseSelectedItem", new[] { typeof(bool), typeof(bool), typeof(int) });
                var actionBarActionSelectedItemMethod = ResolveMethod(typeof(ActionBarInventory), "ActionSelectedItem", new[] { typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(int) });
                var actionBarMbohMethod = ResolveMethod(typeof(ActionBarInventory), "MBOHNGNNCED", Type.EmptyTypes);

                var rodActionPrefix = AccessTools.Method(typeof(Plugin), nameof(RodActionPrefix), new[] { typeof(int), typeof(bool), typeof(MethodBase) });
                var rodActionPostfix = AccessTools.Method(typeof(Plugin), nameof(RodActionPostfix), new[] { typeof(int), typeof(bool), typeof(bool), typeof(MethodBase) });
                var rodNbfbPostfix = AccessTools.Method(typeof(Plugin), nameof(RodNbfbPostfix), new[] { typeof(int), typeof(bool) });
                var rodOfakPostfix = AccessTools.Method(typeof(Plugin), nameof(RodOfakPostfix), new[] { typeof(int), typeof(bool) });
                var rodAnimStagePrefix = AccessTools.Method(typeof(Plugin), nameof(RodAnimationStagePrefix), new[] { typeof(int), typeof(MethodBase) });
                var animatorToolStagePrefix = AccessTools.Method(typeof(Plugin), nameof(CharacterAnimatorToolStagePrefix), new[] { typeof(MethodBase) });
                var fishingUiOpenClosePrefix = AccessTools.Method(typeof(Plugin), nameof(FishingUiOpenClosePrefix), new[] { typeof(MethodBase), typeof(object[]) });
                var selectFishPostfix = AccessTools.Method(typeof(Plugin), nameof(SelectAFishPostfix), new[] { typeof(int), typeof(Fish) });
                var useSelectedItemPostfix = AccessTools.Method(typeof(Plugin), nameof(UseSelectedItemPostfix), new[] { typeof(UseObject), typeof(bool), typeof(bool), typeof(int), typeof(bool) });
                var actionSelectedItemPostfix = AccessTools.Method(typeof(Plugin), nameof(ActionSelectedItemPostfix), new[] { typeof(ActionBarInventory), typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(int), typeof(bool) });
                var actionBarMbohPostfix = AccessTools.Method(typeof(Plugin), nameof(ActionBarMbohPostfix), new[] { typeof(ActionBarInventory), typeof(object) });
                var actionBarAnyActionPrefix = AccessTools.Method(typeof(Plugin), nameof(ActionBarAnyActionPrefix), new[] { typeof(ActionBarInventory), typeof(int), typeof(bool), typeof(MethodBase) });

                PatchWithLogging("FishingUI.StartFishingGame(Rod)", startFishingGameMethod, startFishingGamePostfix, isPrefix: false);
                PatchWithLogging("FishingController.StartFishingCoroutine(Vector3, Rod)", startFishingCoroutineMethod, startFishingCoroutinePrefix, isPrefix: true);
                PatchWithLogging("FishingController.EHOFOMHDPFJ(Vector3, Rod)", startFishingCoroutineAliasEhofMethod, startFishingCoroutinePrefix, isPrefix: true);
                PatchWithLogging("FishingController.JFMCNPDJLLI(Vector3, Rod)", startFishingCoroutineAliasJfmcMethod, startFishingCoroutinePrefix, isPrefix: true);
                PatchWithLogging("FishingController.FFKPLBGGKLB(Vector3, Rod)", startFishingCoroutineAliasFfkpMethod, startFishingCoroutinePrefix, isPrefix: true);
                PatchWithLogging("FishingController.JKOFBKKPJAN(Vector3, Rod)", startFishingCoroutineAliasJkofMethod, startFishingCoroutinePrefix, isPrefix: true);
                PatchWithLogging("FishingController.MOJHEIHKEKO(Vector3, Rod)", startFishingCoroutineAliasMojhMethod, startFishingCoroutinePrefix, isPrefix: true);
                PatchWithLogging("FishingController.CreateBitesList()", createBitesListMethod, createBitesListPostfix, isPrefix: false);
                PatchWithLogging("FishingController.FMCEGPBACPC()", createBitesListAliasFmceMethod, createBitesListPostfix, isPrefix: false);
                PatchWithLogging("FishingController.IHIAGODKCLJ()", createBitesListAliasIhiaMethod, createBitesListPostfix, isPrefix: false);
                PatchWithLogging("FishingController.EGDOBIADPMA()", createBitesListAliasEgdoMethod, createBitesListPostfix, isPrefix: false);
                PatchWithLogging("FishingController.FinishFishing(bool)", finishFishingMethod, finishFishingPrefix, isPrefix: true);
                PatchWithLogging("FishingController.MOECKIFAFII(bool)", finishFishingAliasMoeckMethod, finishFishingPrefix, isPrefix: true);
                PatchWithLogging("FishingController.IAACFAPNOFI(bool)", finishFishingAliasIaacMethod, finishFishingPrefix, isPrefix: true);
                PatchWithLogging("FishingController.HJPINMGNLJB(bool)", finishFishingAliasHjpiMethod, finishFishingPrefix, isPrefix: true);
                PatchWithLogging("FishingUI.LateUpdate()", lateUpdateMethod, lateUpdatePrefix, isPrefix: true);
                PatchWithLogging("FishingHook.SetFake()", fishingHookSetFakeMethod, fishingHookSetFakePrefix, isPrefix: true);
                PatchWithLogging("FishingHook.SetBait()", fishingHookSetBaitMethod, fishingHookSetBaitPostfix, isPrefix: false);
                PatchWithLogging("PlayerInventory.RemoveItem(Item)", playerInventoryRemoveItemOneArgMethod, playerInventoryRemoveItemItemPrefix, isPrefix: true);
                PatchWithLogging("PlayerInventory.RemoveItem(Item, bool)", playerInventoryRemoveItemMethod, playerInventoryRemoveItemItemBoolPrefix, isPrefix: true);
                PatchWithLogging("PlayerInventory.OOEJMKIAPLC(Item, bool)", playerInventoryRemoveItemAliasMethod, playerInventoryRemoveItemItemBoolPrefix, isPrefix: true);
                PatchWithLogging("PlayerInventory.AddItem(ItemInstance,bool,bool,bool,bool)", playerInventoryAddItemMethod, playerInventoryAddItemPostfix, isPrefix: false);
                PatchWithLogging("PlayerInventory.OJDGOADOCMG(ItemInstance,bool,bool,bool,bool)", playerInventoryAddItemAliasMethod, playerInventoryAddItemPostfix, isPrefix: false);
                PatchWithLogging("Container.RemoveItem(Item, bool)", containerRemoveItemMethod, containerRemoveItemPrefix, isPrefix: true);
                PatchWithLogging("Slot.MEODNPFJDMH(bool)", slotConsumeOneMethod, slotConsumeOnePrefix, isPrefix: true);
                PatchWithLogging("Slot.MBCIJPPOGJG(bool)", slotConsumeOneAliasMethod, slotConsumeOnePrefix, isPrefix: true);
                PatchWithLogging("Slot.BGJPNGLONLP(int,bool,bool)", slotSetStackMethod, slotSetStackPrefix, isPrefix: true);
                PatchWithLogging("PlayerInputs.GetButtonDown(string)", playerInputsGetButtonDownMethod, playerInputsGetButtonDownPrefix, isPrefix: true);
                PatchWithLogging("PlayerInputs.GetButton(string)", playerInputsGetButtonMethod, playerInputsGetButtonDownPrefix, isPrefix: true);
                PatchWithLogging("PlayerInputs.JCMOPOMLPLL(string)", playerInputsJcmButtonDownMethod, playerInputsGetButtonDownPrefix, isPrefix: true);
                PatchWithLogging("PlayerInputs.DLFAMOCKNMA(string)", playerInputsDlfButtonDownMethod, playerInputsGetButtonDownPrefix, isPrefix: true);
                var playerInputsPatched = new HashSet<MethodBase>();
                if (playerInputsGetButtonDownMethod != null)
                    playerInputsPatched.Add(playerInputsGetButtonDownMethod);
                if (playerInputsGetButtonMethod != null)
                    playerInputsPatched.Add(playerInputsGetButtonMethod);
                if (playerInputsJcmButtonDownMethod != null)
                    playerInputsPatched.Add(playerInputsJcmButtonDownMethod);
                if (playerInputsDlfButtonDownMethod != null)
                    playerInputsPatched.Add(playerInputsDlfButtonDownMethod);

                foreach (var method in typeof(PlayerInputs).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    try
                    {
                        if (method == null || method.IsSpecialName)
                            continue;
                        if (playerInputsPatched.Contains(method))
                            continue;
                        if (method.ReturnType != typeof(bool))
                            continue;
                        var parameters = method.GetParameters();
                        if (parameters.Length != 1 || parameters[0].ParameterType != typeof(string))
                            continue;

                        _harmony.Patch(method, prefix: new HarmonyMethod(playerInputsGetButtonDownPrefix));
                        playerInputsPatched.Add(method);
                        DebugLog($"Dynamic patch PlayerInputs.{method.Name}(string) => PREFIX");
                    }
                    catch (Exception ex)
                    {
                        DebugLog($"Dynamic patch failed PlayerInputs.{method?.Name}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
                PatchWithLogging("MonoBehaviour.StartCoroutine(IEnumerator)", monoBehaviourStartCoroutineMethod, monoBehaviourStartCoroutinePrefix, isPrefix: true);
                foreach (var moveNext in fishingCoroutineMoveNextMethods)
                {
                    PatchWithLogging($"FishingController coroutine {moveNext.DeclaringType?.Name}.MoveNext()", moveNext, fishingCoroutineMoveNextPrefix, isPrefix: true);
                }
                PatchWithLogging("Rod.Action(int, bool) protection", rodActionMethod, rodActionPrefix, isPrefix: true);
                PatchWithLogging("Rod.EHHCPOCLAJA(int, bool) protection", rodActionAliasEhhcMethod, rodActionPrefix, isPrefix: true);
                PatchWithLogging("Rod.FODGNFMBOFE(int, bool) protection", rodActionAliasFodgMethod, rodActionPrefix, isPrefix: true);
                PatchWithLogging("Rod.EHOOBFJPPOI(int, bool) protection", rodActionAliasEhooMethod, rodActionPrefix, isPrefix: true);
                PatchWithLogging("Rod.OHINFBCDKLI(int, bool) protection", rodActionAliasOhinMethod, rodActionPrefix, isPrefix: true);
                PatchWithLogging("Rod.GGAHICGOLLN(int, bool) protection", rodActionAliasGgahMethod, rodActionPrefix, isPrefix: true);
                PatchWithLogging("Rod.Action(int, bool)", rodActionMethod, rodActionPostfix, isPrefix: false);
                PatchWithLogging("Rod.EHHCPOCLAJA(int, bool)", rodActionAliasEhhcMethod, rodActionPostfix, isPrefix: false);
                PatchWithLogging("Rod.FODGNFMBOFE(int, bool)", rodActionAliasFodgMethod, rodActionPostfix, isPrefix: false);
                PatchWithLogging("Rod.EHOOBFJPPOI(int, bool)", rodActionAliasEhooMethod, rodActionPostfix, isPrefix: false);
                PatchWithLogging("Rod.OHINFBCDKLI(int, bool)", rodActionAliasOhinMethod, rodActionPostfix, isPrefix: false);
                PatchWithLogging("Rod.GGAHICGOLLN(int, bool)", rodActionAliasGgahMethod, rodActionPostfix, isPrefix: false);
                PatchWithLogging("Rod.NBFBPMNMBJG(int)", rodNbfbMethod, rodNbfbPostfix, isPrefix: false);
                PatchWithLogging("Rod.OFAKNHNLKGI(int)", rodOfakMethod, rodOfakPostfix, isPrefix: false);
                PatchWithLogging("Rod.JGNPMBNGKNG(int)", rodAnimStartMethod, rodAnimStagePrefix, isPrefix: true);
                PatchWithLogging("Rod.HNCGNIJLEMH(int)", rodAnimHitMethod, rodAnimStagePrefix, isPrefix: true);
                PatchWithLogging("Rod.NAIECLGMMJA(int)", rodAnimEndMethod, rodAnimStagePrefix, isPrefix: true);
                DebugLog($"Target resolution: Rod.ActionEnd(int) => {(rodActionEndMethod != null ? "FOUND" : "MISSING")}");
                PatchWithLogging("CharacterAnimator.ToolStart()", animatorToolStartMethod, animatorToolStagePrefix, isPrefix: true);
                PatchWithLogging("CharacterAnimator.ToolHit()", animatorToolHitMethod, animatorToolStagePrefix, isPrefix: true);
                PatchWithLogging("CharacterAnimator.ToolEnd()", animatorToolEndMethod, animatorToolStagePrefix, isPrefix: true);
                PatchWithLogging("FishingUI.OpenUI(...)", fishingUiOpenMethod, fishingUiOpenClosePrefix, isPrefix: true);
                PatchWithLogging("FishingUI.CloseUI(...)", fishingUiCloseMethod, fishingUiOpenClosePrefix, isPrefix: true);
                PatchWithLogging("FishingManager.SelectAFish(int, Rod)", fishingManagerSelectFishMethod, selectFishPostfix, isPrefix: false);
                PatchWithLogging("UseObject.UseSelectedItem(bool, bool, int)", useObjectUseSelectedItemMethod, useSelectedItemPostfix, isPrefix: false);
                PatchWithLogging("ActionBarInventory.ActionSelectedItem(int, bool, bool, bool, bool, int)", actionBarActionSelectedItemMethod, actionSelectedItemPostfix, isPrefix: false);
                PatchWithLogging("ActionBarInventory.MBOHNGNNCED()", actionBarMbohMethod, actionBarMbohPostfix, isPrefix: false);

                var actionBarPatched = new HashSet<MethodBase>();
                if (actionBarActionSelectedItemMethod != null)
                    actionBarPatched.Add(actionBarActionSelectedItemMethod);
                var actionBarMethods = typeof(ActionBarInventory).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var method in actionBarMethods)
                {
                    try
                    {
                        if (method == null)
                            continue;
                        if (method.IsSpecialName)
                            continue;
                        if (actionBarPatched.Contains(method))
                            continue;
                        if (method.ReturnType != typeof(bool))
                            continue;

                        var parameters = method.GetParameters();
                        if (parameters.Length < 2)
                            continue;
                        if (parameters[0].ParameterType != typeof(int) || parameters[1].ParameterType != typeof(bool))
                            continue;

                        _harmony.Patch(method, prefix: new HarmonyMethod(actionBarAnyActionPrefix));
                        actionBarPatched.Add(method);
                        DebugLog($"Dynamic patch ActionBarInventory.{method.Name}(...) => PREFIX");
                    }
                    catch (Exception ex)
                    {
                        DebugLog($"Dynamic patch failed ActionBarInventory.{method?.Name}: {ex.GetType().Name}: {ex.Message}");
                    }
                }

                DebugLog("Harmony explicit patching completed");

                foreach (var method in _harmony.GetPatchedMethods())
                {
                    DebugLog($"Patched method: {method.DeclaringType?.FullName}.{method.Name}");
                }

                LogPatchInfo("FishingUI.StartFishingGame(Rod)", startFishingGameMethod);
                LogPatchInfo("FishingController.StartFishingCoroutine(Vector3, Rod)", startFishingCoroutineMethod);
                LogPatchInfo("FishingController.EHOFOMHDPFJ(Vector3, Rod)", startFishingCoroutineAliasEhofMethod);
                LogPatchInfo("FishingController.JFMCNPDJLLI(Vector3, Rod)", startFishingCoroutineAliasJfmcMethod);
                LogPatchInfo("FishingController.FFKPLBGGKLB(Vector3, Rod)", startFishingCoroutineAliasFfkpMethod);
                LogPatchInfo("FishingController.JKOFBKKPJAN(Vector3, Rod)", startFishingCoroutineAliasJkofMethod);
                LogPatchInfo("FishingController.MOJHEIHKEKO(Vector3, Rod)", startFishingCoroutineAliasMojhMethod);
                LogPatchInfo("FishingController.CreateBitesList()", createBitesListMethod);
                LogPatchInfo("FishingController.FMCEGPBACPC()", createBitesListAliasFmceMethod);
                LogPatchInfo("FishingController.IHIAGODKCLJ()", createBitesListAliasIhiaMethod);
                LogPatchInfo("FishingController.EGDOBIADPMA()", createBitesListAliasEgdoMethod);
                LogPatchInfo("FishingController.FinishFishing(bool)", finishFishingMethod);
                LogPatchInfo("FishingController.MOECKIFAFII(bool)", finishFishingAliasMoeckMethod);
                LogPatchInfo("FishingController.IAACFAPNOFI(bool)", finishFishingAliasIaacMethod);
                LogPatchInfo("FishingController.HJPINMGNLJB(bool)", finishFishingAliasHjpiMethod);
                LogPatchInfo("FishingUI.LateUpdate()", lateUpdateMethod);
                LogPatchInfo("FishingHook.SetFake()", fishingHookSetFakeMethod);
                LogPatchInfo("FishingHook.SetBait()", fishingHookSetBaitMethod);
                LogPatchInfo("PlayerInventory.RemoveItem(Item)", playerInventoryRemoveItemOneArgMethod);
                LogPatchInfo("PlayerInventory.RemoveItem(Item, bool)", playerInventoryRemoveItemMethod);
                LogPatchInfo("PlayerInventory.OOEJMKIAPLC(Item, bool)", playerInventoryRemoveItemAliasMethod);
                LogPatchInfo("PlayerInventory.AddItem(ItemInstance,bool,bool,bool,bool)", playerInventoryAddItemMethod);
                LogPatchInfo("PlayerInventory.OJDGOADOCMG(ItemInstance,bool,bool,bool,bool)", playerInventoryAddItemAliasMethod);
                LogPatchInfo("Container.RemoveItem(Item, bool)", containerRemoveItemMethod);
                LogPatchInfo("Slot.MEODNPFJDMH(bool)", slotConsumeOneMethod);
                LogPatchInfo("Slot.MBCIJPPOGJG(bool)", slotConsumeOneAliasMethod);
                LogPatchInfo("Slot.BGJPNGLONLP(int,bool,bool)", slotSetStackMethod);
                LogPatchInfo("PlayerInputs.GetButtonDown(string)", playerInputsGetButtonDownMethod);
                LogPatchInfo("PlayerInputs.GetButton(string)", playerInputsGetButtonMethod);
                LogPatchInfo("PlayerInputs.JCMOPOMLPLL(string)", playerInputsJcmButtonDownMethod);
                LogPatchInfo("PlayerInputs.DLFAMOCKNMA(string)", playerInputsDlfButtonDownMethod);
                LogPatchInfo("MonoBehaviour.StartCoroutine(IEnumerator)", monoBehaviourStartCoroutineMethod);
                foreach (var moveNext in fishingCoroutineMoveNextMethods)
                {
                    LogPatchInfo($"FishingController coroutine {moveNext.DeclaringType?.Name}.MoveNext()", moveNext);
                }
                LogPatchInfo("Rod.Action(int, bool)", rodActionMethod);
                LogPatchInfo("Rod.EHHCPOCLAJA(int, bool)", rodActionAliasEhhcMethod);
                LogPatchInfo("Rod.FODGNFMBOFE(int, bool)", rodActionAliasFodgMethod);
                LogPatchInfo("Rod.EHOOBFJPPOI(int, bool)", rodActionAliasEhooMethod);
                LogPatchInfo("Rod.OHINFBCDKLI(int, bool)", rodActionAliasOhinMethod);
                LogPatchInfo("Rod.GGAHICGOLLN(int, bool)", rodActionAliasGgahMethod);
                LogPatchInfo("Rod.NBFBPMNMBJG(int)", rodNbfbMethod);
                LogPatchInfo("Rod.OFAKNHNLKGI(int)", rodOfakMethod);
                LogPatchInfo("Rod.JGNPMBNGKNG(int)", rodAnimStartMethod);
                LogPatchInfo("Rod.HNCGNIJLEMH(int)", rodAnimHitMethod);
                LogPatchInfo("Rod.NAIECLGMMJA(int)", rodAnimEndMethod);
                LogPatchInfo("CharacterAnimator.ToolStart()", animatorToolStartMethod);
                LogPatchInfo("CharacterAnimator.ToolHit()", animatorToolHitMethod);
                LogPatchInfo("CharacterAnimator.ToolEnd()", animatorToolEndMethod);
                LogPatchInfo("FishingUI.OpenUI(...)", fishingUiOpenMethod);
                LogPatchInfo("FishingUI.CloseUI(...)", fishingUiCloseMethod);
                LogPatchInfo("FishingManager.SelectAFish(int, Rod)", fishingManagerSelectFishMethod);
                LogPatchInfo("UseObject.UseSelectedItem(bool, bool, int)", useObjectUseSelectedItemMethod);
                LogPatchInfo("ActionBarInventory.ActionSelectedItem(int, bool, bool, bool, bool, int)", actionBarActionSelectedItemMethod);
                LogPatchInfo("ActionBarInventory.MBOHNGNNCED()", actionBarMbohMethod);
            }
            catch (Exception ex)
            {
                Log.LogError($"Harmony patching failed: {ex}");
            }
        }

        private static void LogPatchInfo(string label, MethodBase method)
        {
            if (method == null)
            {
                DebugLog($"Patch info {label}: target method missing");
                return;
            }

            var patchInfo = Harmony.GetPatchInfo(method);
            if (patchInfo == null)
            {
                DebugLog($"Patch info {label}: no patch info");
                return;
            }

            var owners = new HashSet<string>(patchInfo.Owners ?? Enumerable.Empty<string>());
            DebugLog($"Patch info {label}: prefixes={patchInfo.Prefixes.Count}, postfixes={patchInfo.Postfixes.Count}, transpilers={patchInfo.Transpilers.Count}, finalizers={patchInfo.Finalizers.Count}, owners=[{string.Join(", ", owners)}]");
        }

        private static void PatchWithLogging(string label, MethodBase targetMethod, MethodInfo patchMethod, bool isPrefix)
        {
            if (targetMethod == null)
            {
                Log.LogError($"Cannot patch {label}: target method missing");
                return;
            }

            if (patchMethod == null)
            {
                Log.LogError($"Cannot patch {label}: patch method missing");
                return;
            }

            if (isPrefix)
                _harmony.Patch(targetMethod, prefix: new HarmonyMethod(patchMethod));
            else
                _harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
        }

        private static MethodInfo ResolveMethod(Type type, string methodName, params Type[][] signatures)
        {
            if (type == null || string.IsNullOrWhiteSpace(methodName))
                return null;

            if (signatures != null)
            {
                foreach (var signature in signatures)
                {
                    var method = AccessTools.Method(type, methodName, signature);
                    if (method != null)
                        return method;
                }
            }

            if (signatures != null && signatures.Length > 0)
                return null;

            try
            {
                return AccessTools.Method(type, methodName);
            }
            catch (AmbiguousMatchException ex)
            {
                DebugLog($"ResolveMethod ambiguous: {type.FullName}.{methodName}: {ex.Message}");
                return null;
            }
        }

        private static string SafeMemberReadAsString(object instance, string memberName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(memberName))
                return "n/a";

            try
            {
                var traverse = Traverse.Create(instance);
                var field = traverse.Field(memberName);
                if (field != null)
                    return field.GetValue()?.ToString() ?? "null";

                var prop = traverse.Property(memberName);
                if (prop != null)
                    return prop.GetValue()?.ToString() ?? "null";
            }
            catch
            {
                // ignored, diagnostics fallback
            }

            return "n/a";
        }

        private static string DescribeFishResult(object fishResult)
        {
            if (fishResult == null)
                return "null";

            var typeName = fishResult.GetType().Name;
            var fishName = SafeMemberReadAsString(fishResult, "name");
            var fishType = SafeMemberReadAsString(fishResult, "type");
            var fishId = SafeMemberReadAsString(fishResult, "id");
            return $"typeClass={typeName}, name={fishName}, fishType={fishType}, id={fishId}";
        }

        private static string DescribeSelectedItemState(object actionBarInventoryInstance)
        {
            if (actionBarInventoryInstance == null)
                return "selectedItemType=n/a, selectedInstanceType=n/a, currentSlot=n/a, actionable=n/a, hasActionable=n/a";

            var selectedItemType = SafeMemberReadAsString(Traverse.Create(actionBarInventoryInstance).Field("selectedItem")?.GetValue(), "GetType");
            if (selectedItemType == "n/a" || selectedItemType == "null")
            {
                var selectedItemObj = Traverse.Create(actionBarInventoryInstance).Field("selectedItem")?.GetValue();
                selectedItemType = selectedItemObj?.GetType().Name ?? "null";
            }

            var selectedInstanceObj = Traverse.Create(actionBarInventoryInstance).Field("selectedItemInstance")?.GetValue();
            var selectedInstanceType = selectedInstanceObj?.GetType().Name ?? "null";

            var currentSlot = SafeMemberReadAsString(actionBarInventoryInstance, "currentSlot");
            var actionable = SafeMemberReadAsString(actionBarInventoryInstance, "actionable");
            var hasActionable = SafeMemberReadAsString(actionBarInventoryInstance, "hasActionable");

            return $"selectedItemType={selectedItemType}, selectedInstanceType={selectedInstanceType}, currentSlot={currentSlot}, actionable={actionable}, hasActionable={hasActionable}";
        }

        private void OnDestroy()
        {
            _harmony.UnpatchSelf();
        }

        private void Update()
        {
            PollAutoRecastEndUse();

            if (!_loggedUpdateProof)
            {
                _loggedUpdateProof = true;
                LogBuildProof("Update");
                Logger.LogInfo(
                    $"EASYFISHING_UPDATE_PROOF stamp={BuildProofStamp} frame={Time.frameCount} enabled={enabled} " +
                    $"activeSelf={gameObject.activeSelf} activeInHierarchy={gameObject.activeInHierarchy}");
            }

            // Harmony-independent fallback. If the game reaches the fishing UI through a path
            // that our patches do not observe, the BepInEx plugin Update still runs every frame.
            if (_InstantCatch?.Value == true || _FishBarQuickProgress?.Value == true || _FishBarNoDecrease?.Value == true)
            {
                PollFishingUiFallbacks();
            }

            if (_FishBarQuickBites?.Value == true)
            {
                PollQuickBitesFallback();
            }

            if (IsAutoReelEnabled())
            {
                PollAutoReel();
            }

            if (IsRemoveRecastDelayEnabled())
            {
                PollRemoveRecastDelay();
            }

            if (_dontUseBait?.Value == true)
            {
                PollDontUseBaitGlobalMonitor();
            }

            PollAutoFish();

            if (_debugLogging?.Value == true && Input.GetKeyDown(KeyCode.F8))
            {
                DumpFishingState("F8");
            }
        }

        private static void InstallTickerFallback()
        {
            try
            {
                var tickerObject = new GameObject("TravellersRestFishingTweaks.UpdateTicker");
                UnityEngine.Object.DontDestroyOnLoad(tickerObject);
                tickerObject.hideFlags = HideFlags.HideAndDontSave;
                tickerObject.AddComponent<TravellersRestFishingTweaksTicker>();
                Log.LogInfo($"EASYFISHING_TICKER_INSTALLED stamp={BuildProofStamp}");
            }
            catch (Exception ex)
            {
                Log.LogError($"EASYFISHING_TICKER_INSTALL_FAILED stamp={BuildProofStamp}: {ex}");
            }
        }

        private class TravellersRestFishingTweaksTicker : MonoBehaviour
        {
            private bool _loggedTickerUpdateProof;

            private void Update()
            {
                PollAutoRecastEndUse();

                if (!_loggedTickerUpdateProof)
                {
                    _loggedTickerUpdateProof = true;
                    LogBuildProof("TickerUpdate");
                    Log.LogInfo(
                        $"EASYFISHING_TICKER_UPDATE_PROOF stamp={BuildProofStamp} frame={Time.frameCount} " +
                        $"enabled={enabled} activeSelf={gameObject.activeSelf} activeInHierarchy={gameObject.activeInHierarchy}");
                }

                if (_InstantCatch?.Value == true || _FishBarQuickProgress?.Value == true || _FishBarNoDecrease?.Value == true)
                {
                    PollFishingUiFallbacks();
                }

                if (_FishBarQuickBites?.Value == true)
                {
                    PollQuickBitesFallback();
                }

                if (IsAutoReelEnabled())
                {
                    PollAutoReel();
                }

                if (IsRemoveRecastDelayEnabled())
                {
                    PollRemoveRecastDelay();
                }

                if (_dontUseBait?.Value == true)
                {
                    PollDontUseBaitGlobalMonitor();
                }

                PollAutoFish();

                if (_debugLogging?.Value == true && Input.GetKeyDown(KeyCode.F8))
                {
                    DumpFishingState("F8 ticker");
                }
            }
        }

        private static void PollFishingUiFallbacks()
        {
            bool shouldLogProbe = _uiProbeLogFramesRemaining > 0 && Time.frameCount % 60 == 0;
            if (shouldLogProbe)
                _uiProbeLogFramesRemaining--;

            for (int playerNum = MinSafePlayerNum; playerNum <= MaxSafePlayerNum; playerNum++)
            {
                if (!IsPlayerRuntimeReady(playerNum))
                    continue;

                FishingUI fishingUi = null;
                try
                {
                    fishingUi = FishingUI.Get(playerNum);
                }
                catch
                {
                    // Some player slots may not exist in single player or current mode.
                    continue;
                }

                var hasUi = fishingUi != null;
                var hasContent = fishingUi?.content != null;
                var active = fishingUi?.content != null && fishingUi.content.activeInHierarchy;

                if (shouldLogProbe)
                {
                    Log.LogInfo(
                        $"EASYFISHING_UI_PROBE stamp={BuildProofStamp} player={playerNum} " +
                        $"ui={hasUi} content={hasContent} active={active}");
                }

                if (!active)
                {
                    if (playerNum >= 0 && playerNum < _maxFishingProgressByPlayer.Length)
                        _maxFishingProgressByPlayer[playerNum] = 0f;
                    continue;
                }

                if (_InstantCatch?.Value == true)
                {
                    if (!_loggedPollingFallbackActive)
                    {
                        _loggedPollingFallbackActive = true;
                        Log.LogInfo($"EASYFISHING_UI_ACTIVE stamp={BuildProofStamp} player={playerNum}; forcing progress to 1.0");
                    }

                    ForceFishingProgressComplete(fishingUi, $"polling fallback player {playerNum}");
                    continue;
                }

                ApplyEasyMinigameFallback(fishingUi, playerNum);
            }
        }

        private static void ApplyEasyMinigameFallback(FishingUI fishingUi, int playerNum)
        {
            try
            {
                Slider reflectedSlider = Traverse.Create(fishingUi)
                    .Field("progress")
                    .GetValue<Slider>();

                if (reflectedSlider == null)
                {
                    LogThrottledDiagnostic($"TravellersRestFishingTweaks: easy minigame fallback player {playerNum}: FishingUI progress slider is null");
                    return;
                }

                var before = reflectedSlider.value;

                if (playerNum >= 0 && playerNum < _maxFishingProgressByPlayer.Length)
                {
                    if (_FishBarNoDecrease?.Value == true && before < _maxFishingProgressByPlayer[playerNum])
                        reflectedSlider.value = _maxFishingProgressByPlayer[playerNum];
                }

                if (_FishBarQuickProgress?.Value == true)
                {
                    if (IsFishingMinigameInputActive(fishingUi, playerNum) && (_FishBarQuickProgressOnMiss?.Value == true || IsFishInBox(fishingUi)))
                        reflectedSlider.value = Mathf.Clamp01(Mathf.Max(reflectedSlider.value, before + (Mathf.Max(0f, _FishBarQuickProgressAmount?.Value ?? 0.15f) * Time.deltaTime)));
                }

                if (playerNum >= 0 && playerNum < _maxFishingProgressByPlayer.Length)
                    _maxFishingProgressByPlayer[playerNum] = Mathf.Max(_maxFishingProgressByPlayer[playerNum], reflectedSlider.value);

                if (!_loggedEasyMinigameFallbackActive)
                {
                    _loggedEasyMinigameFallbackActive = true;
                    Log.LogInfo(
                        $"EASYFISHING_MINIGAME_FALLBACK_ACTIVE stamp={BuildProofStamp} player={playerNum} " +
                        $"QuickProgress={_FishBarQuickProgress?.Value} QuickProgressAmount={_FishBarQuickProgressAmount?.Value} QuickProgressOnMiss={_FishBarQuickProgressOnMiss?.Value} NoBarDecrease={_FishBarNoDecrease?.Value} " +
                        $"progressBefore={before:0.000} progressAfter={reflectedSlider.value:0.000}");
                }
            }
            catch (Exception ex)
            {
                LogThrottledDiagnostic($"TravellersRestFishingTweaks: easy minigame fallback player {playerNum} failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static bool IsFishingMinigameInputActive(FishingUI fishingUi, int playerNum)
        {
            if (fishingUi == null)
                return false;

            if (fishingUi.content == null || !fishingUi.content.activeInHierarchy)
                return false;

            if (!fishingUi.IsOpen())
                return false;

            if (fishingUi.fish == null)
                return false;

            if (playerNum <= 0)
                playerNum = fishingUi.JIIGOACEIKL;

            PlayerInputs inputs = PlayerInputs.GetPlayer(playerNum);
            if (inputs == null)
                return false;

            if (!PlayerInputs.IsGamepadActive(playerNum) && inputs.GetButton("Start"))
                return true;

            return inputs.GetButton("UIInteract") || inputs.GetButton("UIAddRemove");
        }

        private static bool IsFishInBox(FishingUI fishingUi)
        {
            if (fishingUi == null)
                return false;

            try
            {
                var fishIcon = FishingUIFishIcon?.GetValue(fishingUi) as RectTransform;
                var box = FishingUIBox?.GetValue(fishingUi) as RectTransform;
                if (fishIcon == null || box == null)
                    return false;

                var threshold = 0f;
                var rawThreshold = FishingUIHitThreshold?.GetValue(fishingUi);
                if (rawThreshold is float reflectedThreshold)
                    threshold = reflectedThreshold;

                if (threshold <= 0f)
                    threshold = box.sizeDelta.x * 0.5f;

                return Mathf.Abs(fishIcon.anchoredPosition.x - box.anchoredPosition.x) < threshold;
            }
            catch (Exception ex)
            {
                LogThrottledDiagnostic($"TravellersRestFishingTweaks: failed to read fish-in-box state: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static void ForceFishingProgressComplete(FishingUI fishingUi, string source)
        {
            if (fishingUi == null)
                return;

            try
            {
                Slider reflectedSlider = Traverse.Create(fishingUi)
                    .Field("progress")
                    .GetValue<Slider>();

                if (reflectedSlider == null)
                {
                    LogThrottledDiagnostic($"TravellersRestFishingTweaks: {source}: FishingUI progress slider is null");
                    return;
                }

                reflectedSlider.value = 1.0f;
            }
            catch (Exception ex)
            {
                LogThrottledDiagnostic($"TravellersRestFishingTweaks: {source}: failed to force FishingUI progress: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void LogThrottledDiagnostic(string message)
        {
            if (_debugLogging?.Value != true)
                return;

            if (Time.realtimeSinceStartup < _nextPollingDiagnosticsTime)
                return;

            _nextPollingDiagnosticsTime = Time.realtimeSinceStartup + 5f;
            Log.LogInfo(message);
        }

        private static void DumpFishingState(string reason)
        {
            Log.LogInfo($"TravellersRestFishingTweaks: Debug dump requested ({reason})");

            for (int playerNum = MinSafePlayerNum; playerNum <= MaxSafePlayerNum; playerNum++)
            {
                try
                {
                    var fishingUi = FishingUI.Get(playerNum);
                    var uiActive = fishingUi?.content != null && fishingUi.content.activeInHierarchy;
                    var progress = "n/a";
                    if (fishingUi != null)
                    {
                        var slider = Traverse.Create(fishingUi).Field("progress").GetValue<Slider>();
                        progress = slider != null ? slider.value.ToString("0.000") : "null";
                    }

                    var controller = FishingController.Get(playerNum);
                    var fishing = SafeMemberReadAsString(controller, "fishing");
                    var baitSelected = SafeMemberReadAsString(controller, "baitSelected");

                    Log.LogInfo($"TravellersRestFishingTweaks: Debug dump player={playerNum}, fishingUiActive={uiActive}, progress={progress}, controllerFishing={fishing}, baitSelected={baitSelected}");
                }
                catch (Exception ex)
                {
                    DebugLog($"Debug dump player={playerNum} unavailable: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private static bool IsAutoFishEnabled()
        {
            return _autoFish?.Value == true;
        }

        private static bool IsAutoReelEnabled()
        {
            return _autoReel?.Value == true || IsAutoFishEnabled();
        }

        private static bool IsRemoveRecastDelayEnabled()
        {
            return _removeRecastDelay?.Value == true;
        }

        private static void ResetAutoFishCooldowns()
        {
            for (var i = 0; i < _nextAutoFishCastAtByPlayer.Length; i++)
            {
                _nextAutoFishCastAtByPlayer[i] = 0f;
                _autoRecastEndUseAtByPlayer[i] = 0f;
            }
        }

        private static void PollAutoFish()
        {
            if (!IsAutoFishEnabled())
                return;

            if (_lastAutoFishPollFrame == Time.frameCount)
                return;

            _lastAutoFishPollFrame = Time.frameCount;

            const int playerNum = AutoFishPlayer;
            var now = Time.realtimeSinceStartup;

            if (!_loggedAutoFishActive)
            {
                _loggedAutoFishActive = true;
                Log.LogInfo($"EASYFISHING_AUTO_RECAST_ROD_ACTIVE stamp={BuildProofStamp} player={playerNum} recastDelay={GetAutoFishRecastDelay():0.000} mode=session-interrupt");
            }

            if (!_autoRecastSessionActiveByPlayer[playerNum])
                return;

            if (ShouldStopAutoRecastSession(playerNum, out var stopReason))
            {
                StopAutoRecastSession(playerNum, stopReason);
                SetAutoFishCooldown(playerNum, AutoRecastInterruptPause);
                return;
            }

            if (playerNum >= 0 && playerNum < _nextAutoFishCastAtByPlayer.Length && now < _nextAutoFishCastAtByPlayer[playerNum])
            {
                DebugLog($"EASYFISHING_AUTO_RECAST_ROD_WAIT player={playerNum} reason=cooldown cooldownDelta={_nextAutoFishCastAtByPlayer[playerNum] - now:0.000}");
                return;
            }

            if (!CanAutoFishRecast(playerNum, out var reason))
            {
                SetAutoFishCooldown(playerNum, 0.15f);
                if (reason != "active")
                    LogAutoRecastWait(playerNum, reason);
                return;
            }

            bool castStarted = false;
            try
            {
                var useObject = UseObject.GetPlayer(playerNum);
                if (useObject == null)
                {
                    SetAutoFishCooldown(playerNum, 1.0f);
                    LogThrottledDiagnostic($"TravellersRestFishingTweaks: Auto Recast Rod player={playerNum}: UseObject missing");
                    return;
                }

                if (!CanSelectedRodCastNow(playerNum, out var castReason))
                {
                    SetAutoFishCooldown(playerNum, AutoRecastFailedAttemptCooldown);
                    LogThrottledDiagnostic($"TravellersRestFishingTweaks: Auto Recast Rod waiting player={playerNum} reason={castReason}");
                    return;
                }

                _autoRecastCastingNowByPlayer[playerNum] = true;
                try
                {
                    LogAutoRecastAttempt(playerNum, "attempt");
                    castStarted = useObject.UseSelectedItem(true, true, 1);
                }
                finally
                {
                    _autoRecastCastingNowByPlayer[playerNum] = false;
                }
            }
            catch (Exception ex)
            {
                SetAutoFishCooldown(playerNum, 1.0f);
                LogThrottledDiagnostic($"TravellersRestFishingTweaks: Auto Recast Rod cast failed player={playerNum}: {ex.GetType().Name}: {ex.Message}");
                return;
            }

            SetAutoFishCooldown(playerNum, castStarted ? GetAutoFishRecastDelay() : 1.0f);
            if (castStarted)
            {
                _lastAutoRecastCastAtByPlayer[playerNum] = Time.realtimeSinceStartup;
                _autoRecastEndUseAtByPlayer[playerNum] = Time.realtimeSinceStartup + AutoRecastEndUseDelay;
                ContinueAutoRecastSession(playerNum, "auto_cast");
                DebugLog($"EASYFISHING_AUTO_RECAST_ROD_CAST player={playerNum} source=session");
            }
            else
            {
                SetAutoFishCooldown(playerNum, AutoRecastFailedAttemptCooldown);
                LogAutoRecastAttempt(playerNum, "cast_rejected");
                LogThrottledDiagnostic($"TravellersRestFishingTweaks: Auto Recast Rod cast attempt returned false player={playerNum}");
            }
        }

        private static float GetAutoFishRecastDelay()
        {
            return Mathf.Max(MinAutoRecastDelay, _autoFishRecastDelay?.Value ?? 1.0f);
        }

        private static void SetAutoFishCooldown(int playerNum, float seconds)
        {
            if (playerNum < 0 || playerNum >= _nextAutoFishCastAtByPlayer.Length)
                return;

            _nextAutoFishCastAtByPlayer[playerNum] = Time.realtimeSinceStartup + Mathf.Max(0.01f, seconds);
        }

        private static bool CanAutoFishRecast(int playerNum, out string reason)
        {
            reason = "ok";

            if (!IsPlayerRuntimeReady(playerNum))
            {
                reason = "runtime_not_ready";
                return false;
            }

            if (SafeMainUiOpen(playerNum))
            {
                reason = "ui_open";
                return false;
            }

            if (IsAutoRecastMovementActive(playerNum))
            {
                reason = "movement";
                return false;
            }

            var now = Time.realtimeSinceStartup;
            if (playerNum < _lastFinishFishingAtByPlayer.Length && _lastFinishFishingAtByPlayer[playerNum] > 0f && now - _lastFinishFishingAtByPlayer[playerNum] < AutoRecastPostFinishSettle)
            {
                reason = "post_finish_settle";
                return false;
            }

            if (playerNum < _lastRemoveRecastCleanupAtByPlayer.Length && _lastRemoveRecastCleanupAtByPlayer[playerNum] > 0f && now - _lastRemoveRecastCleanupAtByPlayer[playerNum] < AutoRecastPostCleanupSettle)
            {
                reason = "post_cleanup_settle";
                return false;
            }

            try
            {
                var player = PlayerController.GetPlayer(playerNum);
                if (player == null)
                {
                    reason = "player_missing";
                    return false;
                }

                if (player.NILLCIMMKJE)
                {
                    reason = "player_busy";
                    return false;
                }
            }
            catch
            {
                reason = "player_unavailable";
                return false;
            }

            FishingController controller = null;
            try
            {
                controller = FishingController.Get(playerNum);
            }
            catch
            {
            }

            if (controller != null)
            {
                var fishingActive = controller.fishing || IsControllerFishingCameraActive(controller);

                if (HasPendingBiteList(controller))
                {
                    if (fishingActive && IsAutoReelEnabled())
                    {
                        reason = "pending_bite";
                        return false;
                    }

                    if (!fishingActive)
                    {
                        DebugLog($"EASYFISHING_AUTO_RECAST_ROD_STALE_BITE_CLEARED player={playerNum} fishing={controller.fishing} camera={IsControllerFishingCameraActive(controller)} count={controller.bitesList?.Count ?? 0}");
                    }

                    ClearBiteList(controller, fishingActive ? "AutoFishRecastGate" : "AutoRecastStaleBiteGate");
                }

                if (fishingActive)
                {
                    reason = "active";
                    return false;
                }

                try
                {
                    var hook = Traverse.Create(controller).Field("fishingHook")?.GetValue() as FishingHook;
                    if (hook != null)
                    {
                        if (hook.fishInfo != null && hook.fishInfo.activeInHierarchy)
                        {
                            if (IsRemoveRecastDelayEnabled() && !controller.fishing && !IsControllerFishingCameraActive(controller))
                            {
                                DebugLog($"EASYFISHING_INSTANT_RECAST_IGNORE_POPUP player={playerNum}");
                            }
                            else
                            {
                                reason = "fish_popup_active";
                                return false;
                            }
                        }

                        if (hook.gameObject != null && hook.gameObject.activeInHierarchy)
                        {
                            if (IsRemoveRecastDelayEnabled() && !controller.fishing && !IsControllerFishingCameraActive(controller))
                            {
                                TryClearCompletedFishingHook(playerNum, "auto-recast-gate");
                                reason = "completed_hook_cleanup_settle";
                                return false;
                            }

                            reason = "hook_active";
                            return false;
                        }
                    }
                }
                catch
                {
                }
            }

            if (!IsRodSelectedForPlayer(playerNum))
            {
                reason = "rod_not_selected";
                return false;
            }

            return true;
        }

        private static bool HasPendingBiteList(FishingController controller)
        {
            try
            {
                return controller?.bitesList != null && controller.bitesList.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static void ClearBiteList(FishingController controller, string source)
        {
            try
            {
                if (controller?.bitesList == null || controller.bitesList.Count == 0)
                    return;
                var count = controller.bitesList.Count;
                controller.bitesList.Clear();
                DebugLog($"EASYFISHING_BITE_LIST_CLEARED source={source} player={controller.playerNum} count={count}");
            }
            catch
            {
            }
        }

        private static bool CanSelectedRodCastNow(int playerNum, out string reason)
        {
            reason = "ok";
            var rod = GetSelectedRodForPlayer(playerNum);
            if (rod == null)
            {
                reason = "rod_unresolved";
                return false;
            }

            return true;
        }

        private static void StartAutoRecastSession(int playerNum, string source)
        {
            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum)
                return;

            _autoRecastSessionActiveByPlayer[playerNum] = true;
            _autoRecastPausedUntilByPlayer[playerNum] = 0f;
            _autoRecastIgnoreManualInputUntilByPlayer[playerNum] = Time.realtimeSinceStartup + 0.5f;
            SetAutoFishCooldown(playerNum, GetAutoFishRecastDelay());
            Log.LogInfo($"EASYFISHING_AUTO_RECAST_ROD_SESSION_START player={playerNum} source={source} recastDelay={GetAutoFishRecastDelay():0.000}");
        }

        private static void ContinueAutoRecastSession(int playerNum, string source)
        {
            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum)
                return;

            if (!_autoRecastSessionActiveByPlayer[playerNum])
                StartAutoRecastSession(playerNum, source);
        }

        private static void StopAutoRecastSession(int playerNum, string reason)
        {
            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum)
                return;

            if (!_autoRecastSessionActiveByPlayer[playerNum])
                return;

            _autoRecastSessionActiveByPlayer[playerNum] = false;
            _autoRecastPausedUntilByPlayer[playerNum] = Time.realtimeSinceStartup + AutoRecastInterruptPause;
            _autoRecastIgnoreManualInputUntilByPlayer[playerNum] = 0f;
            SetAutoFishCooldown(playerNum, AutoRecastInterruptPause);
            Log.LogInfo($"EASYFISHING_AUTO_RECAST_ROD_SESSION_STOP player={playerNum} reason={reason}");
        }

        private static void ArmAutoRecastSessionIfEligible(int playerNum, string source)
        {
            if (!IsAutoFishEnabled())
                return;

            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum)
                return;

            if (!IsRodSelectedForPlayer(playerNum))
            {
                DebugLog($"EASYFISHING_AUTO_RECAST_ROD_SESSION_ARM_SKIP player={playerNum} source={source} reason=rod_not_selected");
                return;
            }

            ContinueAutoRecastSession(playerNum, source);
        }

        private static bool ShouldStopAutoRecastSession(int playerNum, out string reason)
        {
            reason = null;

            if (!IsAutoFishEnabled())
            {
                reason = "config_disabled";
                return true;
            }

            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum)
            {
                reason = "player_invalid";
                return true;
            }

            if (Time.realtimeSinceStartup < _autoRecastPausedUntilByPlayer[playerNum])
            {
                reason = "paused";
                return true;
            }

            try
            {
                if (MainUI.IsAnyUIOpen(playerNum))
                    DebugLog($"EASYFISHING_AUTO_RECAST_ROD_BLOCKER player={playerNum} blocker=ui_open action=wait");
            }
            catch
            {
            }

            if (!IsRodSelectedForPlayer(playerNum))
            {
                reason = "rod_not_selected";
                return true;
            }

            try
            {
                var inputs = PlayerInputs.GetPlayer(playerNum);
                if (inputs != null)
                {
                    if (IsAxisActive(inputs, "HorizontalMove") || IsAxisActive(inputs, "VerticalMove"))
                        DebugLog($"EASYFISHING_AUTO_RECAST_ROD_BLOCKER player={playerNum} blocker=movement action=wait");

                    var interruptButton = GetPressedAutoRecastInterruptButton(inputs);
                    if (!_autoRecastCastingNowByPlayer[playerNum] && interruptButton != null)
                    {
                        var inDebounce = Time.realtimeSinceStartup < _autoRecastIgnoreManualInputUntilByPlayer[playerNum];
                        DebugLog($"EASYFISHING_AUTO_RECAST_ROD_MANUAL_INPUT_SEEN player={playerNum} button={interruptButton} action=ignored_diagnostic inDebounce={inDebounce} ignoreUntilDelta={_autoRecastIgnoreManualInputUntilByPlayer[playerNum] - Time.realtimeSinceStartup:0.000}");
                        // Diagnostic build: manual button-down signals are noisy around fishing/reel input.
                        // Movement, UI-open, rod deselect, and config disable still stop the session.
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static string GetPressedAutoRecastInterruptButton(PlayerInputs inputs)
        {
            if (inputs == null)
                return null;

            var buttons = new[] { "Use", "Interact", "Select", "Pause", "OpenInventory", "ClosePopUp", "LeftMouseDetect" };
            foreach (var button in buttons)
            {
                if (IsButtonDown(inputs, button))
                    return button;
            }

            return null;
        }

        private static bool IsAxisActive(PlayerInputs inputs, string axisName)
        {
            try
            {
                return Mathf.Abs(inputs.GetAxis(axisName)) > 0.1f;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsAutoRecastMovementActive(int playerNum)
        {
            try
            {
                var inputs = PlayerInputs.GetPlayer(playerNum);
                return inputs != null && (IsAxisActive(inputs, "HorizontalMove") || IsAxisActive(inputs, "VerticalMove"));
            }
            catch
            {
                return false;
            }
        }

        private static bool IsButtonDown(PlayerInputs inputs, string buttonName)
        {
            try
            {
                return inputs.GetButtonDown(buttonName);
            }
            catch
            {
                return false;
            }
        }

        private static void LogAutoRecastAttempt(int playerNum, string stage)
        {
            if (_debugLogging?.Value != true)
                return;

            try
            {
                var controller = FishingController.Get(playerNum);
                var player = PlayerController.GetPlayer(playerNum);
                var hook = Traverse.Create(controller).Field("fishingHook")?.GetValue() as FishingHook;
                var now = Time.realtimeSinceStartup;
                var sinceFinish = playerNum < _lastFinishFishingAtByPlayer.Length && _lastFinishFishingAtByPlayer[playerNum] > 0f ? now - _lastFinishFishingAtByPlayer[playerNum] : -1f;
                var sinceCleanup = playerNum < _lastRemoveRecastCleanupAtByPlayer.Length && _lastRemoveRecastCleanupAtByPlayer[playerNum] > 0f ? now - _lastRemoveRecastCleanupAtByPlayer[playerNum] : -1f;
                DebugLog($"EASYFISHING_AUTO_RECAST_ROD_ATTEMPT player={playerNum} stage={stage} sinceFinish={sinceFinish:0.000} sinceCleanup={sinceCleanup:0.000} uiOpen={SafeMainUiOpen(playerNum)} playerBusy={player?.NILLCIMMKJE} fishing={controller?.fishing} camera={(controller != null && IsControllerFishingCameraActive(controller))} hookActive={(hook?.gameObject != null && hook.gameObject.activeInHierarchy)} fishInfoActive={(hook?.fishInfo != null && hook.fishInfo.activeInHierarchy)} rodSelected={IsRodSelectedForPlayer(playerNum)}");
            }
            catch (Exception ex)
            {
                DebugLog($"EASYFISHING_AUTO_RECAST_ROD_ATTEMPT_FAILED player={playerNum} stage={stage}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void LogAutoRecastWait(int playerNum, string reason)
        {
            if (_debugLogging?.Value != true)
            {
                LogThrottledDiagnostic($"TravellersRestFishingTweaks: Auto Recast Rod waiting player={playerNum} reason={reason}");
                return;
            }

            try
            {
                var controller = FishingController.Get(playerNum);
                var now = Time.realtimeSinceStartup;
                var sinceFinish = playerNum < _lastFinishFishingAtByPlayer.Length && _lastFinishFishingAtByPlayer[playerNum] > 0f ? now - _lastFinishFishingAtByPlayer[playerNum] : -1f;
                var sinceCleanup = playerNum < _lastRemoveRecastCleanupAtByPlayer.Length && _lastRemoveRecastCleanupAtByPlayer[playerNum] > 0f ? now - _lastRemoveRecastCleanupAtByPlayer[playerNum] : -1f;
                var sessionAge = -1f;
                DebugLog($"EASYFISHING_AUTO_RECAST_ROD_WAIT player={playerNum} reason={reason} session={_autoRecastSessionActiveByPlayer[playerNum]} sessionAge={sessionAge:0.000} sinceFinish={sinceFinish:0.000} sinceCleanup={sinceCleanup:0.000} uiOpen={SafeMainUiOpen(playerNum)} rodSelected={IsRodSelectedForPlayer(playerNum)} fishing={controller?.fishing} camera={(controller != null && IsControllerFishingCameraActive(controller))}");
            }
            catch (Exception ex)
            {
                DebugLog($"EASYFISHING_AUTO_RECAST_ROD_WAIT_FAILED player={playerNum} reason={reason}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static bool SafeMainUiOpen(int playerNum)
        {
            try
            {
                return MainUI.IsAnyUIOpen(playerNum);
            }
            catch
            {
                return false;
            }
        }

        private static void PollAutoRecastEndUse()
        {
            var now = Time.realtimeSinceStartup;
            for (var playerNum = MinSafePlayerNum; playerNum <= MaxSafePlayerNum; playerNum++)
            {
                if (playerNum >= _autoRecastEndUseAtByPlayer.Length)
                    continue;
                if (_autoRecastEndUseAtByPlayer[playerNum] <= 0f || now < _autoRecastEndUseAtByPlayer[playerNum])
                    continue;

                _autoRecastEndUseAtByPlayer[playerNum] = 0f;
                try
                {
                    UseObject.GetPlayer(playerNum)?.EndSelectedItem(1);
                    DebugLog($"EASYFISHING_AUTO_RECAST_ROD_END_USE player={playerNum}");
                }
                catch (Exception ex)
                {
                    DebugLog($"EASYFISHING_AUTO_RECAST_ROD_END_USE_FAILED player={playerNum}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private static bool IsDuplicateRodAction(int playerNum)
        {
            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum || playerNum >= _lastRodActionAcceptedAtByPlayer.Length)
                return false;

            var lastAcceptedAt = _lastRodActionAcceptedAtByPlayer[playerNum];
            return lastAcceptedAt > 0f && Time.realtimeSinceStartup - lastAcceptedAt < DuplicateRodActionWindow;
        }

        private static void MarkRodActionAccepted(int playerNum)
        {
            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum || playerNum >= _lastRodActionAcceptedAtByPlayer.Length)
                return;

            _lastRodActionAcceptedAtByPlayer[playerNum] = Time.realtimeSinceStartup;
        }

        private static bool ShouldSuppressDuplicateRodActionLog(int playerNum)
        {
            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum || playerNum >= _lastRodActionSuppressedAtByPlayer.Length)
                return false;

            var now = Time.realtimeSinceStartup;
            if (now - _lastRodActionSuppressedAtByPlayer[playerNum] < 0.75f)
                return true;

            _lastRodActionSuppressedAtByPlayer[playerNum] = now;
            return false;
        }

        private static bool IsControllerFishingCameraActive(FishingController controller)
        {
            if (controller == null)
                return false;

            try
            {
                var raw = Traverse.Create(controller).Field("fishingCamera")?.GetValue();
                if (raw is bool b)
                    return b;
                if (raw is Camera camera)
                    return camera != null;
                if (raw is GameObject go)
                    return go.activeInHierarchy;
            }
            catch
            {
            }

            DebugLog("EASYFISHING_CAMERA_STATE_UNRESOLVED assuming=false");
            return false;
        }

        private static float GetFishingStartAge(int playerNum)
        {
            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum || playerNum >= _lastFishingStartAtByPlayer.Length)
                return -1f;

            var startAt = _lastFishingStartAtByPlayer[playerNum];
            return startAt > 0f ? Time.realtimeSinceStartup - startAt : -1f;
        }

        private static bool HasFishingStartedSettled(int playerNum, float minAge, out float age, out string reason)
        {
            age = GetFishingStartAge(playerNum);
            reason = "ok";

            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum)
            {
                reason = "player_invalid";
                return false;
            }

            if (age < 0f)
            {
                reason = "fishing_start_not_seen";
                return false;
            }

            if (age < minAge)
            {
                reason = "fishing_start_not_settled";
                return false;
            }

            return true;
        }

        private static bool HasSettledBait(int playerNum, float minAge, out float age, out string reason)
        {
            age = -1f;
            reason = "ok";

            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum || playerNum >= _lastHookSetBaitAtByPlayer.Length)
            {
                reason = "player_invalid";
                return false;
            }

            var baitAt = _lastHookSetBaitAtByPlayer[playerNum];
            if (baitAt <= 0f)
            {
                reason = "bait_not_seen";
                return false;
            }

            age = Time.realtimeSinceStartup - baitAt;
            if (age < minAge)
            {
                reason = "bait_not_settled";
                return false;
            }

            return true;
        }

        private static bool IsPlayerRuntimeReady(int playerNum)
        {
            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum)
                return false;

            try
            {
                var player = PlayerController.GetPlayer(playerNum);
                if (player == null || player.gameObject == null || !player.gameObject.activeInHierarchy)
                    return false;
            }
            catch
            {
                return false;
            }

            try
            {
                return UseObject.GetPlayer(playerNum) != null && FishingController.Get(playerNum) != null && PlayerInventory.GetPlayer(playerNum) != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsFishingSessionActive(FishingController controller)
        {
            if (controller == null)
                return false;

            if (controller.fishing || IsControllerFishingCameraActive(controller))
                return true;

            try
            {
                var hook = Traverse.Create(controller).Field("fishingHook")?.GetValue() as FishingHook;
                return hook != null && hook.gameObject != null && hook.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }

        private static void TryAutoFishReel(int playerNum)
        {
            // Auto reel is driven through PlayerInputs.GetButtonDown patch.
        }

        static bool FishingCoroutineMoveNextPrefix(object __instance, MethodBase __originalMethod)
        {
            LogFishingCoroutineRuntimeDiscovery(__instance, __originalMethod);

            if (!IsAutoReelEnabled())
                return true;

            try
            {
                var controller = GetCoroutineController(__instance);
                var playerNum = controller?.playerNum ?? -1;
                var reason = GetAutoReelCoroutineBlockReason(playerNum, controller, __instance, out var startValue, out var fishingTimeValue);
                LogAutoReelCoroutineDiagnostic(playerNum, controller, __originalMethod, reason, startValue, fishingTimeValue);
                if (reason != "trigger")
                    return true;

                var startField = GetCoroutineBoolField(__instance, "startFishMinigame");
                if (startField != null)
                    startField.SetValue(__instance, true);

                var fishingTimeField = GetCoroutineBoolField(__instance, "fishingTime");
                if (fishingTimeField != null)
                    fishingTimeField.SetValue(__instance, true);

                if (playerNum >= 0 && playerNum < _autoFishReelUntilByPlayer.Length)
                    _autoFishReelUntilByPlayer[playerNum] = Time.realtimeSinceStartup + 2f;

                Log.LogInfo($"EASYFISHING_AUTO_REEL_COROUTINE_TRIGGER player={playerNum} coroutine={__originalMethod?.DeclaringType?.Name}");
            }
            catch (Exception ex)
            {
                LogThrottledDiagnostic($"Auto Reel coroutine prefix failed: {ex.GetType().Name}: {ex.Message}");
            }

            return true;
        }

        private static readonly Dictionary<string, float> _nextStartCoroutineDiscoveryLogAt = new Dictionary<string, float>();

        static bool MonoBehaviourStartCoroutinePrefix(MonoBehaviour __instance, IEnumerator __0, MethodBase __originalMethod)
        {
            if (_debugLogging?.Value != true || __instance == null || __0 == null)
                return true;

            var ownerType = __instance.GetType();
            var routineType = __0.GetType();
            var interesting = __instance is FishingController || __instance is FishingUI || ownerType.Name.IndexOf("Fishing", StringComparison.OrdinalIgnoreCase) >= 0 || routineType.FullName.IndexOf("Fishing", StringComparison.OrdinalIgnoreCase) >= 0 || routineType.FullName.IndexOf("Rod", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!interesting)
                return true;

            var key = ownerType.FullName + "|" + routineType.FullName;
            var now = Time.realtimeSinceStartup;
            if (_nextStartCoroutineDiscoveryLogAt.TryGetValue(key, out var next) && now < next)
                return true;
            _nextStartCoroutineDiscoveryLogAt[key] = now + 1.0f;

            DebugLog($"EASYFISHING_START_COROUTINE owner={ownerType.FullName} routine={routineType.FullName} fields={DescribeCoroutineFields(routineType)}");
            return true;
        }

        private static readonly Dictionary<string, float> _nextCoroutineDiscoveryLogAt = new Dictionary<string, float>();

        private static void LogFishingCoroutineRuntimeDiscovery(object coroutineInstance, MethodBase originalMethod)
        {
            if (_debugLogging?.Value != true)
                return;

            var typeName = originalMethod?.DeclaringType?.Name ?? coroutineInstance?.GetType().Name ?? "<unknown>";
            var now = Time.realtimeSinceStartup;
            if (_nextCoroutineDiscoveryLogAt.TryGetValue(typeName, out var next) && now < next)
                return;
            _nextCoroutineDiscoveryLogAt[typeName] = now + 1.5f;

            try
            {
                var controller = GetCoroutineController(coroutineInstance);
                var playerNum = controller?.playerNum ?? -1;
                var bools = DescribeCoroutineBoolValues(coroutineInstance);
                var ints = DescribeCoroutineIntValues(coroutineInstance);
                var floats = DescribeCoroutineFloatValues(coroutineInstance);
                DebugLog($"EASYFISHING_COROUTINE_MOVENEXT type={typeName} player={playerNum} controller={(controller != null)} fishing={controller?.fishing} camera={(controller != null && IsControllerFishingCameraActive(controller))} session={(controller != null && IsFishingSessionActive(controller))} bites={controller?.bitesList?.Count ?? -1} bools={bools} ints={ints} floats={floats}");
            }
            catch (Exception ex)
            {
                DebugLog($"EASYFISHING_COROUTINE_MOVENEXT_DIAG_FAILED type={typeName}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string GetAutoReelCoroutineBlockReason(int playerNum, FishingController controller, object coroutineInstance, out bool startValue, out bool fishingTimeValue)
        {
            startValue = GetCoroutineBoolValue(coroutineInstance, "startFishMinigame");
            fishingTimeValue = GetCoroutineBoolValue(coroutineInstance, "fishingTime");

            if (controller == null)
                return "controller_missing";
            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum)
                return "player_invalid";
            if (!IsFishingSessionActive(controller))
                return "session_inactive";
            if (startValue)
                return "already_started";

            var armed = playerNum < _autoReelInputUntilByPlayer.Length && Time.realtimeSinceStartup <= _autoReelInputUntilByPlayer[playerNum];
            var due = controller.bitesList != null && controller.bitesList.Count == 1 && Time.time >= controller.bitesList[0];
            if (!armed && !due)
                return "not_armed_or_due";

            return "trigger";
        }

        private static bool GetCoroutineBoolValue(object coroutineInstance, string contains)
        {
            var field = GetCoroutineBoolField(coroutineInstance, contains);
            if (field == null)
                return false;
            try
            {
                return field.GetValue(coroutineInstance) is bool value && value;
            }
            catch
            {
                return false;
            }
        }

        private static void LogAutoReelCoroutineDiagnostic(int playerNum, FishingController controller, MethodBase originalMethod, string reason, bool startValue, bool fishingTimeValue)
        {
            if (_debugLogging?.Value != true)
                return;
            var now = Time.realtimeSinceStartup;
            if (now < _nextAutoReelCoroutineDiagAt)
                return;
            _nextAutoReelCoroutineDiagAt = now + 1.0f;

            var count = controller?.bitesList?.Count ?? -1;
            var firstDelta = count > 0 ? controller.bitesList[0] - Time.time : float.NaN;
            var armedDelta = playerNum >= 0 && playerNum < _autoReelInputUntilByPlayer.Length ? _autoReelInputUntilByPlayer[playerNum] - now : float.NaN;
            DebugLog($"EASYFISHING_AUTO_REEL_COROUTINE_CHECK player={playerNum} coroutine={originalMethod?.DeclaringType?.Name} reason={reason} fishing={controller?.fishing} camera={(controller != null && IsControllerFishingCameraActive(controller))} session={(controller != null && IsFishingSessionActive(controller))} bites={count} firstDelta={firstDelta:0.000} armedDelta={armedDelta:0.000} start={startValue} fishingTime={fishingTimeValue}");
        }

        private static FishingController GetCoroutineController(object coroutineInstance)
        {
            if (coroutineInstance == null)
                return null;

            var field = coroutineInstance.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(f => f.FieldType == typeof(FishingController));
            return field?.GetValue(coroutineInstance) as FishingController;
        }

        private static FieldInfo GetCoroutineBoolField(object coroutineInstance, string contains)
        {
            if (coroutineInstance == null)
                return null;

            return coroutineInstance.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(f => f.FieldType == typeof(bool) && f.Name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string DescribeCoroutineFields(Type type)
        {
            if (type == null)
                return "<null>";

            try
            {
                return string.Join(",", type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(f => $"{f.FieldType.Name}:{f.Name}")
                    .Take(20));
            }
            catch (Exception ex)
            {
                return $"<failed:{ex.GetType().Name}>";
            }
        }

        private static string DescribeCoroutineBoolValues(object coroutineInstance)
        {
            return DescribeCoroutineValues(coroutineInstance, typeof(bool), value => value is bool b ? (b ? "1" : "0") : "?");
        }

        private static string DescribeCoroutineIntValues(object coroutineInstance)
        {
            return DescribeCoroutineValues(coroutineInstance, typeof(int), value => value?.ToString() ?? "null");
        }

        private static string DescribeCoroutineFloatValues(object coroutineInstance)
        {
            return DescribeCoroutineValues(coroutineInstance, typeof(float), value => value is float f ? f.ToString("0.000") : "?");
        }

        private static string DescribeCoroutineValues(object coroutineInstance, Type fieldType, Func<object, string> format)
        {
            if (coroutineInstance == null)
                return "<null>";

            try
            {
                return string.Join(",", coroutineInstance.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(f => f.FieldType == fieldType)
                    .Select(f => $"{f.Name}={format(f.GetValue(coroutineInstance))}")
                    .Take(12));
            }
            catch (Exception ex)
            {
                return $"<failed:{ex.GetType().Name}>";
            }
        }

        private static bool ShouldAutoReelCoroutine(int playerNum, FishingController controller)
        {
            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum)
                return false;

            if (controller == null || !IsFishingSessionActive(controller))
                return false;

            if (playerNum < _autoReelInputUntilByPlayer.Length && Time.realtimeSinceStartup <= _autoReelInputUntilByPlayer[playerNum])
                return true;

            if (controller.bitesList == null || controller.bitesList.Count != 1)
                return false;

            return Time.time >= controller.bitesList[0];
        }

        private static void PollAutoReel()
        {
            if (_lastAutoReelPollFrame == Time.frameCount)
                return;

            _lastAutoReelPollFrame = Time.frameCount;

            for (var playerNum = MinSafePlayerNum; playerNum <= MaxSafePlayerNum; playerNum++)
            {
                if (!IsPlayerRuntimeReady(playerNum))
                    continue;

                FishingController controller = null;
                try
                {
                    controller = FishingController.Get(playerNum);
                }
                catch
                {
                    continue;
                }

                if (!IsFishingSessionActive(controller))
                    continue;

                if (controller?.bitesList == null || controller.bitesList.Count != 1)
                    continue;

                if (Time.time >= controller.bitesList[0])
                {
                    ArmAutoReel(playerNum, "bitesList");
                    TryAutoReelDirectFinish(playerNum, controller);
                }
            }
        }

        private static void TryAutoReelDirectFinish(int playerNum, FishingController controller)
        {
            if (!IsAutoReelEnabled() || !_InstantCatch.Value)
                return;
            if (controller == null || playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum)
                return;

            var now = Time.realtimeSinceStartup;
            if (playerNum < _autoReelDirectFinishCooldownUntilByPlayer.Length && now < _autoReelDirectFinishCooldownUntilByPlayer[playerNum])
                return;

            try
            {
                var ui = FishingUI.Get(playerNum);
                if (ui == null)
                    return;

                var biteCount = controller.bitesList?.Count ?? -1;
                var firstDelta = biteCount > 0 ? controller.bitesList[0] - Time.time : 999f;
                var fishingAge = GetFishingStartAge(playerNum);
                var baitAgeForLog = playerNum >= MinSafePlayerNum && playerNum <= MaxSafePlayerNum && _lastHookSetBaitAtByPlayer[playerNum] > 0f
                    ? Time.realtimeSinceStartup - _lastHookSetBaitAtByPlayer[playerNum]
                    : -1f;
                DebugLog($"EASYFISHING_AUTO_REEL_DIRECT_CHECK player={playerNum} fishing={controller.fishing} camera={IsControllerFishingCameraActive(controller)} session={IsFishingSessionActive(controller)} bites={biteCount} firstDelta={firstDelta:0.000} fishingAge={fishingAge:0.000} baitAge={baitAgeForLog:0.000} uiOpen={ui.IsOpen()} uiFish={(ui.fish != null)}");

                if (_lastFinishFishingAtByPlayer[playerNum] > _lastFishingStartAtByPlayer[playerNum])
                {
                    DebugLog($"EASYFISHING_AUTO_REEL_DIRECT_DIAG player={playerNum} reason=post_finish_stale proceeding=True fishingAge={fishingAge:0.000}");
                }

                if (!HasFishingStartedSettled(playerNum, AutoReelDirectFinishMinBaitAge, out var settledAge, out var settledReason))
                {
                    DebugLog($"EASYFISHING_AUTO_REEL_DIRECT_DIAG player={playerNum} reason={settledReason} proceeding=True fishingAge={settledAge:0.000}");
                }

                if (!HasSettledBait(playerNum, AutoReelDirectFinishMinBaitAge, out var baitAge, out var baitReason))
                {
                    DebugLog($"EASYFISHING_AUTO_REEL_DIRECT_DIAG player={playerNum} reason={baitReason} proceeding=True baitAge={baitAge:0.000}");
                }

                EnsureFishingUiHasFish(playerNum, controller, ui);

                if (ui.fish == null)
                {
                    DebugLog($"EASYFISHING_AUTO_REEL_DIRECT_SKIP player={playerNum} reason=no_ui_fish");
                    return;
                }

                if (playerNum < _autoReelDirectFinishCooldownUntilByPlayer.Length)
                    _autoReelDirectFinishCooldownUntilByPlayer[playerNum] = now + 1.25f;

                if (!ui.IsOpen())
                    ui.OpenUI();

                CompleteFishingUiImmediately(ui, "AutoReelDirectFinish");
                controller.FinishFishing(true);
                ArmAutoRecastSessionIfEligible(playerNum, "auto_reel_direct_finish");
                ClearBiteList(controller, "AutoReelDirectFinish");
                if (playerNum < _lastAutoReelDirectFinishAtByPlayer.Length)
                    _lastAutoReelDirectFinishAtByPlayer[playerNum] = Time.realtimeSinceStartup;
                if (playerNum < _removeRecastDelayCleanupAtByPlayer.Length)
                    _removeRecastDelayCleanupAtByPlayer[playerNum] = Time.realtimeSinceStartup + 0.2f;
                Log.LogInfo($"EASYFISHING_AUTO_REEL_DIRECT_FINISH player={playerNum} fish={(ui.fish != null ? ui.fish.name : "<null>")}");
            }
            catch (Exception ex)
            {
                LogThrottledDiagnostic($"Auto Reel direct finish failed player={playerNum}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void EnsureFishingUiHasFish(int playerNum, FishingController controller, FishingUI ui)
        {
            if (ui == null || ui.fish != null)
                return;

            try
            {
                var rod = GetSelectedRodForPlayer(playerNum);
                if (rod != null)
                    ui.StartFishingGame(rod);
            }
            catch (Exception ex)
            {
                DebugLog($"EASYFISHING_AUTO_REEL_SELECT_FISH_FAILED player={playerNum}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static Rod GetSelectedRodForPlayer(int playerNum)
        {
            try
            {
                var inventory = PlayerInventory.GetPlayer(playerNum);
                var actionBar = Traverse.Create(inventory).Field("actionBarInventory")?.GetValue() as ActionBarInventory;
                if (actionBar == null)
                    return null;

                try
                {
                    if (actionBar.GetSelectedItem() is Rod rod)
                        return rod;
                }
                catch
                {
                }

                try
                {
                    var selectedInstance = actionBar.GetSelectedItemInstance();
                    var item = ResolveItemFromItemInstance(selectedInstance);
                    if (item is Rod rodFromInstance)
                        return rodFromInstance;
                }
                catch
                {
                }
            }
            catch
            {
            }

            return null;
        }

        private static Item ResolveItemFromItemInstance(object itemInstance)
        {
            if (itemInstance == null)
                return null;

            try
            {
                var item = Traverse.Create(itemInstance).Method("LHBPOPOIFLE")?.GetValue() as Item;
                if (item != null)
                    return item;
            }
            catch
            {
            }

            try
            {
                return Traverse.Create(itemInstance).Method("AFOACBIHNCL")?.GetValue() as Item;
            }
            catch
            {
                return null;
            }
        }

        private static void ArmAutoReel(int playerNum, string source)
        {
            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum)
                return;

            var now = Time.realtimeSinceStartup;
            _autoReelInputUntilByPlayer[playerNum] = now + 2.0f;
            if (now - _autoReelLastArmLogAtByPlayer[playerNum] >= 0.75f)
            {
                _autoReelLastArmLogAtByPlayer[playerNum] = now;
                Log.LogInfo($"EASYFISHING_AUTO_REEL_ARMED player={playerNum} source={source}");
            }
        }


        private static bool ShouldAutoReelNow(int playerNum)
        {
            if (!IsAutoReelEnabled())
                return false;

            if (!IsPlayerRuntimeReady(playerNum))
                return false;

            if (playerNum >= 0 && playerNum < _autoFishReelUntilByPlayer.Length && Time.realtimeSinceStartup < _autoFishReelUntilByPlayer[playerNum])
                return false;

            FishingController controller;
            try
            {
                controller = FishingController.Get(playerNum);
            }
            catch
            {
                return false;
            }

            if (!IsFishingSessionActive(controller))
                return false;

            if (controller == null || controller.bitesList == null || controller.bitesList.Count == 0)
                return false;

            var due = controller.bitesList.Count == 1 && Time.time >= controller.bitesList[0];
            var armed = playerNum >= 0 && playerNum < _autoReelInputUntilByPlayer.Length && Time.realtimeSinceStartup <= _autoReelInputUntilByPlayer[playerNum];
            if (!due && !armed)
                return false;

            try
            {
                var fishingUi = FishingUI.Get(playerNum);
                if (fishingUi != null && fishingUi.IsOpen())
                    return false;
            }
            catch
            {
            }

            return true;
        }

        static bool PlayerInputsButtonDownAnyPrefix(PlayerInputs __instance, string __0, ref bool __result, MethodBase __originalMethod)
        {
            if (_debugLogging?.Value != true || !IsRemoveRecastDelayEnabled())
                return true;

            var resolvedPlayer = TryResolvePlayerInputs(__instance, out var playerNum);
            var recentFinishWindow = resolvedPlayer && IsWithinRecentFinishWindow(playerNum, 5f);
            if (!recentFinishWindow && !MatchesInputKeyword(__0))
                return true;

            var key = $"{(resolvedPlayer ? playerNum.ToString() : "?")}:{__originalMethod?.Name}:{__0}";
            var now = Time.realtimeSinceStartup;
            if (_nextInputButtonDiagAtByPlayerButton.TryGetValue(key, out var nextAt) && now < nextAt)
                return true;
            _nextInputButtonDiagAtByPlayerButton[key] = now + 0.25f;

            try
            {
                var controller = resolvedPlayer ? GetFishingControllerSafe(playerNum) : null;
                var hook = controller == null ? null : Traverse.Create(controller).Field("fishingHook")?.GetValue() as FishingHook;
                var useObject = resolvedPlayer ? UseObject.GetPlayer(playerNum) : null;
                var buttonDown = SafeMemberReadAsString(useObject, "buttonDown");
                var actionToolDone = SafeMemberReadAsString(useObject, "actionToolDone");
                var uiOpen = false;
                var uiContentActive = false;
                var sinceFinish = -1f;
                var sinceCleanup = -1f;
                var mainUiOpen = false;

                if (resolvedPlayer)
                {
                    var fishingUi = FishingUI.Get(playerNum);
                    uiOpen = fishingUi != null && fishingUi.IsOpen();
                    uiContentActive = fishingUi?.content != null && fishingUi.content.activeInHierarchy;
                    sinceFinish = playerNum < _lastFinishFishingAtByPlayer.Length && _lastFinishFishingAtByPlayer[playerNum] > 0f ? now - _lastFinishFishingAtByPlayer[playerNum] : -1f;
                    sinceCleanup = playerNum < _lastRemoveRecastCleanupAtByPlayer.Length && _lastRemoveRecastCleanupAtByPlayer[playerNum] > 0f ? now - _lastRemoveRecastCleanupAtByPlayer[playerNum] : -1f;
                    mainUiOpen = SafeMainUiOpen(playerNum);
                }

                var inputKind = (__originalMethod?.Name?.IndexOf("Down", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ? "down" : "held";

                DebugLog(
                    $"EASYFISHING_INPUT_BUTTON player={(resolvedPlayer ? playerNum.ToString() : "unresolved")} method={__originalMethod?.Name} input={inputKind} button={__0} " +
                    $"sinceFinish={sinceFinish:0.000} sinceCleanup={sinceCleanup:0.000} " +
                    $"uiOpen={uiOpen} uiContentActive={uiContentActive} mainUiOpen={mainUiOpen} recentWindow={recentFinishWindow} " +
                    $"fishing={controller?.fishing} fishingCamera={(controller != null && IsControllerFishingCameraActive(controller))} " +
                    $"hookActive={(hook?.gameObject != null && hook.gameObject.activeInHierarchy)} fishInfoActive={(hook?.fishInfo != null && hook.fishInfo.activeInHierarchy)} " +
                    $"rodSelected={(resolvedPlayer && IsRodSelectedForPlayer(playerNum))} useButtonDown={buttonDown} useActionToolDone={actionToolDone}");
            }
            catch (Exception ex)
            {
                DebugLog($"EASYFISHING_INPUT_BUTTON_DIAG_FAILED player={(resolvedPlayer ? playerNum.ToString() : "unresolved")} button={__0}: {ex.GetType().Name}: {ex.Message}");
            }

            return true;
        }

        private static bool IsWithinRecentFinishWindow(int playerNum, float seconds)
        {
            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum)
                return false;

            var now = Time.realtimeSinceStartup;
            var sinceFinish = playerNum < _lastFinishFishingAtByPlayer.Length && _lastFinishFishingAtByPlayer[playerNum] > 0f ? now - _lastFinishFishingAtByPlayer[playerNum] : 999f;
            var sinceCleanup = playerNum < _lastRemoveRecastCleanupAtByPlayer.Length && _lastRemoveRecastCleanupAtByPlayer[playerNum] > 0f ? now - _lastRemoveRecastCleanupAtByPlayer[playerNum] : 999f;
            return sinceFinish <= seconds || sinceCleanup <= seconds;
        }

        private static bool MatchesInputKeyword(string buttonName)
        {
            if (string.IsNullOrEmpty(buttonName))
                return false;

            return buttonName.IndexOf("use", StringComparison.OrdinalIgnoreCase) >= 0
                   || buttonName.IndexOf("interact", StringComparison.OrdinalIgnoreCase) >= 0
                   || buttonName.IndexOf("action", StringComparison.OrdinalIgnoreCase) >= 0
                   || buttonName.IndexOf("mouse", StringComparison.OrdinalIgnoreCase) >= 0
                   || buttonName.IndexOf("tool", StringComparison.OrdinalIgnoreCase) >= 0
                   || buttonName.IndexOf("select", StringComparison.OrdinalIgnoreCase) >= 0
                   || buttonName.IndexOf("item", StringComparison.OrdinalIgnoreCase) >= 0
                   || buttonName.IndexOf("ui", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsFishingUseButtonName(string name)
        {
            return string.Equals(name, "Use", StringComparison.Ordinal) ||
                   string.Equals(name, "LeftMouseDetect", StringComparison.Ordinal) ||
                   string.Equals(name, "Interact", StringComparison.Ordinal) ||
                   string.Equals(name, "Action", StringComparison.Ordinal) ||
                   string.Equals(name, "UseTool", StringComparison.Ordinal);
        }

        private static bool TryResolvePlayerInputs(PlayerInputs inputs, out int playerNum)
        {
            playerNum = -1;
            if (inputs == null)
                return false;

            for (var i = MinSafePlayerNum; i <= MaxSafePlayerNum; i++)
            {
                try
                {
                    if (ReferenceEquals(PlayerInputs.GetPlayer(i), inputs))
                    {
                        playerNum = i;
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        private static void LogBuildProof(string source)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var location = assembly.Location;
                var sha256 = File.Exists(location) ? ComputeSha256(location) : "<missing>";
                var mvid = assembly.ManifestModule.ModuleVersionId.ToString();

                Log.LogInfo(
                    $"EASYFISHING_BUILD_PROOF source={source} stamp={BuildProofStamp} " +
                    $"guid={PluginInfo.PLUGIN_GUID} version={PluginInfo.PLUGIN_VERSION} " +
                    $"location=\"{location}\" sha256={sha256} mvid={mvid}");
            }
            catch (Exception ex)
            {
                Log.LogError($"EASYFISHING_BUILD_PROOF_FAILED source={source}: {ex}");
            }
        }

        private static void LogConfigProof()
        {
            try
            {
                Log.LogInfo(
                    $"EASYFISHING_CONFIG_PROOF stamp={BuildProofStamp} " +
                    $"QuickProgress={_FishBarQuickProgress?.Value} " +
                    $"QuickProgressAmount={_FishBarQuickProgressAmount?.Value} " +
                    $"QuickProgressOnMiss={_FishBarQuickProgressOnMiss?.Value} " +
                    $"NoBarDecrease={_FishBarNoDecrease?.Value} " +
                    $"QuickBites={_FishBarQuickBites?.Value} " +
                    $"InstantCatch={_InstantCatch?.Value} " +
                    $"DontUseBait={_dontUseBait?.Value} " +
                    $"AutoRecastRod={_autoFish?.Value} " +
                    $"AutoReel={_autoReel?.Value} " +
                    $"AutoRecastRodPlayer={AutoFishPlayer} " +
                    $"AutoRecastRodDelay={_autoFishRecastDelay?.Value} " +
                    $"RemoveRecastDelay={_removeRecastDelay?.Value} " +
                    $"DebugLogging={_debugLogging?.Value}");
            }
            catch (Exception ex)
            {
                Log.LogError($"EASYFISHING_CONFIG_PROOF_FAILED: {ex}");
            }
        }

        private void RemoveLegacyAutoRecastPlayerConfig()
        {
            try
            {
                var configPath = Config.ConfigFilePath;

                if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
                    return;

                var lines = File.ReadAllLines(configPath).ToList();
                var entryIndex = lines.FindIndex(line => line.TrimStart().StartsWith("Auto Recast Rod Player", StringComparison.Ordinal));

                if (entryIndex < 0)
                    return;

                var startIndex = entryIndex;
                while (startIndex > 0)
                {
                    var previousLine = lines[startIndex - 1];

                    if (previousLine.StartsWith("#", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(previousLine))
                    {
                        startIndex--;
                        continue;
                    }

                    break;
                }

                lines.RemoveRange(startIndex, entryIndex - startIndex + 1);
                File.WriteAllLines(configPath, lines);
                Log.LogInfo("Removed legacy config entry from file: General/Auto Recast Rod Player");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to remove legacy Auto Recast Rod Player config entry: {ex}");
            }
        }

        private static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    builder.Append(b.ToString("X2"));
                return builder.ToString();
            }
        }

        public static void DebugLog(string message)
        {
            // Log a message to console only if debug is enabled in console
            if (_debugLogging.Value) Log.LogInfo($"TravellersRestFishingTweaks: Debug: {message}");
        }

        //////////////////////////////////////////////////////////////////
        //  Fast Progress, No progress loss

        static void StartFishingGamePostfix(FishingUI __instance)
        {
            DebugLog("StartFishingGamePostfix");

            if (_InstantCatch?.Value == true)
            {
                CompleteFishingUiImmediately(__instance, "StartFishingGamePostfix");
                return;
            }

            if (!_FishBarQuickProgress.Value && !_FishBarNoDecrease.Value)
                return;

            var settings = DifficultySettingsField?.GetValue(__instance) as FishingManagerSettings.DifficultySettings;
            if (settings == null)
            {
                Log.LogError("Could not find FishingUI difficulty settings. Functionality disabled.");
                return;
            }

            if (_FishBarQuickProgress.Value)
            {
                settings.hitProgression = 0.5f; // default 0.05f
            }

            if (_FishBarNoDecrease.Value)
            {
                settings.hitReduction = 0.0002f; // default 0.02f
                settings.barReductionPerSecond = 0.0002f; // default 0.15f
            }

            // These control fish movement, type Vector2. I assume they are loaded from the fish type
            //__instance.movementDistanceMinMax =
            //__instance.movementTimeMinMax = 
            //__instance.stopTimeMinMax =
        }

        private static void CompleteFishingUiImmediately(FishingUI fishingUi, string source)
        {
            if (fishingUi == null)
                return;

            ForceFishingProgressComplete(fishingUi, source);

            try
            {
                Traverse.Create(fishingUi).Field("FLMLLMHPJJA").SetValue(true);
            }
            catch
            {
            }

            try
            {
                Traverse.Create(fishingUi).Field("OOGIFKPEKMA").SetValue(true);
            }
            catch
            {
            }

            try
            {
                if (fishingUi.IsOpen())
                {
                    DebugLog($"EASYFISHING_INSTANT_CATCH_UI_LEFT_OPEN source={source} player={fishingUi.JIIGOACEIKL}");
                }
            }
            catch (Exception ex)
            {
                LogThrottledDiagnostic($"TravellersRestFishingTweaks: {source}: failed checking FishingUI open state: {ex.GetType().Name}: {ex.Message}");
            }
        }


        //////////////////////////////////////////////////////////////////
        ///  quicker Bites
        static bool StartFishingAnyPrefix(FishingController __instance, MethodBase __originalMethod)
        {
            DebugLog($"StartFishingAnyPrefix source={__originalMethod?.Name}");
            var playerNum = __instance?.playerNum ?? -1;
            if (playerNum >= MinSafePlayerNum && playerNum <= MaxSafePlayerNum)
            {
                ClearPreservedFishResultVisual(playerNum, "fishing_start");
                _lastFishingStartAtByPlayer[playerNum] = Time.realtimeSinceStartup;
                _lastHookSetBaitAtByPlayer[playerNum] = 0f;
                ClearBiteList(__instance, $"fishing_start:{__originalMethod?.Name}");
                DebugLog($"EASYFISHING_FISHING_START player={playerNum} method={__originalMethod?.Name} fishing={__instance?.fishing} camera={(__instance != null && IsControllerFishingCameraActive(__instance))} bites={__instance?.bitesList?.Count ?? -1}");
            }
            MarkBaitProtection(playerNum, 8f);
            ApplyRemoveRecastDelaySettings(__instance, $"start:{__originalMethod?.Name}");
            ArmAutoRecastSessionIfEligible(playerNum, $"fishing_start:{__originalMethod?.Name}");
            if (_FishBarQuickBites.Value)
            {
                var settings = FishingControllerSettings.GetValue(__instance) as FishingManagerSettings;
                if (settings == null)
                {
                    Log.LogError("Could not find FishingManager settings. Functionality disabled.");
                    return true;
                }

                settings.timeBetweenBites = 0.1f;
                settings.totalTime = 1;
                settings.bitesNum.x = 1;
                settings.bitesNum.y = 1;
                DebugLog($"QuickBites start settings applied from {__originalMethod?.Name}");
            }

            return true;
        }

        static void CreateBitesListAnyPostfix(FishingController __instance, MethodBase __originalMethod)
        {
            if (_FishBarQuickBites?.Value != true)
                return;

            NormalizeQuickBites(__instance, $"builder:{__originalMethod?.Name}", 0.1f);
        }

        static void NormalizeQuickBites(FishingController controller, string source, float delay)
        {
            if (controller == null)
                return;

            var playerNum = controller.playerNum;
            var oldCount = controller.bitesList?.Count ?? 0;
            var oldFirstDelta = controller.bitesList != null && controller.bitesList.Count > 0 ? controller.bitesList[0] - Time.time : 999f;
            var fishingAge = GetFishingStartAge(playerNum);

            if (controller.bitesList == null)
                controller.bitesList = new List<float>();

            controller.bitesList.Clear();
            controller.bitesList.Add(Time.time + delay);
            DebugLog($"EASYFISHING_QUICK_BITES_NORMALIZE player={playerNum} source={source} fishingAge={fishingAge:0.000} oldCount={oldCount} oldFirstDelta={oldFirstDelta:0.000} newDelay={delay:0.000}");
        }

        static void PollQuickBitesFallback()
        {
            for (int playerNum = MinSafePlayerNum; playerNum <= MaxSafePlayerNum; playerNum++)
            {
                if (!IsPlayerRuntimeReady(playerNum))
                    continue;

                FishingController controller = null;
                try
                {
                    controller = FishingController.Get(playerNum);
                }
                catch
                {
                    continue;
                }

                if (controller?.bitesList == null || controller.bitesList.Count == 0)
                    continue;

                if (!IsFishingSessionActive(controller))
                {
                    LogQuickBitesDiagThrottled(playerNum, "session_not_ready", $"proceeding=True bites={controller.bitesList.Count}");
                }

                if (_lastFinishFishingAtByPlayer[playerNum] > _lastFishingStartAtByPlayer[playerNum])
                {
                    if (IsFishingSessionActive(controller))
                    {
                        _lastFishingStartAtByPlayer[playerNum] = Time.realtimeSinceStartup;
                        LogQuickBitesDiagThrottled(playerNum, "observed_new_session_after_finish", $"recovered=True bites={controller.bitesList.Count}");
                    }
                    else
                    {
                        LogQuickBitesDiagThrottled(playerNum, "post_finish_stale", $"sessionActive=False clearing=True bites={controller.bitesList.Count}");
                        ClearBiteList(controller, "QuickBitesPostFinishStaleNoSession");
                        continue;
                    }
                }

                if (!HasFishingStartedSettled(playerNum, QuickBitesMinFishingAge, out var fishingAge, out var fishingReason))
                {
                    LogQuickBitesDiagThrottled(playerNum, fishingReason, $"proceeding=True fishingAge={fishingAge:0.000} bites={controller.bitesList.Count}");
                }

                var shouldNormalize = controller.bitesList.Count > 1;
                if (!shouldNormalize && controller.bitesList.Count == 1)
                    shouldNormalize = controller.bitesList[0] > Time.time + 0.25f;

                if (shouldNormalize)
                    NormalizeQuickBites(controller, $"poll:p{playerNum}", 0.1f);
            }
        }

        static bool FishingHookSetFakePrefix(FishingHook __instance)
        {
            if (_FishBarQuickBites?.Value != true)
                return true;

            var controller = FindControllerForHook(__instance);
            if (controller == null)
            {
                DebugLog("QuickBites intercepted SetFake but controller not found");
                return true;
            }

            if (controller.bitesList == null)
                controller.bitesList = new List<float>();

            controller.bitesList.Clear();
            controller.bitesList.Add(Time.time);
            controller.bitesList.Add(Time.time + 0.05f);
            DebugLog("QuickBites intercepted SetFake");
            return false;
        }

        static void FishingHookSetBaitPostfix(FishingHook __instance, MethodBase __originalMethod)
        {
            var controller = FindControllerForHook(__instance);
            var playerNum = controller?.playerNum ?? -1;

            if (playerNum >= MinSafePlayerNum && playerNum <= MaxSafePlayerNum)
            {
                _lastHookSetBaitAtByPlayer[playerNum] = Time.realtimeSinceStartup;
                var sinceStart = _lastFishingStartAtByPlayer[playerNum] > 0f ? Time.realtimeSinceStartup - _lastFishingStartAtByPlayer[playerNum] : -1f;
                DebugLog($"EASYFISHING_HOOK_SET_BAIT player={playerNum} method={__originalMethod?.Name} sinceStart={sinceStart:0.000} fishing={controller?.fishing} camera={(controller != null && IsControllerFishingCameraActive(controller))} hookActive={(__instance?.gameObject != null && __instance.gameObject.activeInHierarchy)} bites={controller?.bitesList?.Count ?? -1}");
            }

            if (_FishBarQuickBites?.Value == true)
                DebugLog($"QuickBites observed SetBait player={playerNum}");

            if (IsAutoReelEnabled())
                ArmAutoReel(playerNum, "FishingHook.SetBait");
        }

        static FishingController FindControllerForHook(FishingHook hook)
        {
            if (hook == null)
                return null;

            try
            {
                var hookPlayerObj = Traverse.Create(hook).Field("playerNum")?.GetValue();
                if (hookPlayerObj is int hookPlayerNum)
                {
                    try
                    {
                        var byPlayer = FishingController.Get(hookPlayerNum);
                        if (byPlayer != null)
                            return byPlayer;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            for (int playerNum = MinSafePlayerNum; playerNum <= MaxSafePlayerNum; playerNum++)
            {
                FishingController controller = null;
                try
                {
                    controller = FishingController.Get(playerNum);
                }
                catch
                {
                    continue;
                }

                if (controller == null)
                    continue;

                try
                {
                    var controllerHook = Traverse.Create(controller).Field("fishingHook")?.GetValue() as FishingHook;
                    if (ReferenceEquals(controllerHook, hook))
                        return controller;
                }
                catch
                {
                }
            }

            return null;
        }

        private static IEnumerable<MethodBase> FindFishingCoroutineMoveNextMethods()
        {
            foreach (var nestedType in typeof(FishingController).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            {
                MethodInfo moveNext = null;
                try
                {
                    moveNext = AccessTools.Method(nestedType, "MoveNext", Type.EmptyTypes);
                }
                catch
                {
                }

                if (moveNext == null)
                    continue;

                var fields = nestedType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var hasController = fields.Any(f => f.FieldType == typeof(FishingController));
                if (hasController)
                    yield return moveNext;
            }
        }

        //////////////////////////////////////////////////////////////////
        ///  Runtime discovery / context hooks
        static bool RodActionPrefix(int __0, bool __1, MethodBase __originalMethod)
        {
            if (__1)
            {
                if (IsRemoveRecastDelayEnabled())
                    TryClearCompletedFishingHook(__0, "manual-recast-input");

                if (IsDuplicateRodAction(__0))
                {
                    if (!ShouldSuppressDuplicateRodActionLog(__0))
                        DebugLog($"EASYFISHING_DUPLICATE_ROD_ACTION_BLOCKED player={__0} method={__originalMethod?.Name} window={DuplicateRodActionWindow:0.000}");
                    return false;
                }

                MarkBaitProtection(__0, 8f);
            }

            LogRecastAttemptDiagnostics(__0, __originalMethod?.Name, "prefix");
            if (ShouldLogInputPipeline(__0, __1, -1))
                LogInputPipeline(__0, __originalMethod?.Name, "Rod.prefix", __1, -1, true);

            DebugLog($"Diag Rod.{__originalMethod?.Name} prefix player={__0} pressed={__1}");
            return true;
        }

        static void RodActionPostfix(int __0, bool __1, bool __result, MethodBase __originalMethod)
        {
            if (__result)
            {
                MarkBaitProtection(__0, 8f);
                if (__1)
                    MarkRodActionAccepted(__0);
            }

            if (!__result)
                LogRecastAttemptDiagnostics(__0, __originalMethod?.Name, "postfix-failed");

            if (ShouldLogInputPipeline(__0, __1, -1))
                LogInputPipeline(__0, __originalMethod?.Name, "Rod.postfix", __1, -1, __result);

            DebugLog($"Diag Rod.{__originalMethod?.Name} postfix player={__0} pressed={__1} result={__result}");
        }

        static void LogRecastAttemptDiagnostics(int playerNum, string methodName, string stage)
        {
            if (_debugLogging?.Value != true || !IsRemoveRecastDelayEnabled())
                return;
            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum)
                return;

            var now = Time.realtimeSinceStartup;
            var sinceFinish = playerNum < _lastFinishFishingAtByPlayer.Length && _lastFinishFishingAtByPlayer[playerNum] > 0f ? now - _lastFinishFishingAtByPlayer[playerNum] : -1f;
            var sinceCleanup = playerNum < _lastRemoveRecastCleanupAtByPlayer.Length && _lastRemoveRecastCleanupAtByPlayer[playerNum] > 0f ? now - _lastRemoveRecastCleanupAtByPlayer[playerNum] : -1f;
            if (sinceFinish > 8f && sinceCleanup > 8f)
                return;

            try
            {
                var controller = GetFishingControllerSafe(playerNum);
                var hook = controller == null ? null : Traverse.Create(controller).Field("fishingHook")?.GetValue() as FishingHook;
                var player = PlayerController.GetPlayer(playerNum);
                DebugLog($"EASYFISHING_RECAST_ATTEMPT player={playerNum} method={methodName} stage={stage} sinceFinish={sinceFinish:0.000} sinceCleanup={sinceCleanup:0.000} playerBusy={player?.NILLCIMMKJE} fishing={controller?.fishing} camera={(controller != null && IsControllerFishingCameraActive(controller))} hookActive={(hook?.gameObject != null && hook.gameObject.activeInHierarchy)} fishInfoActive={(hook?.fishInfo != null && hook.fishInfo.activeInHierarchy)} rodSelected={IsRodSelectedForPlayer(playerNum)}");
            }
            catch (Exception ex)
            {
                DebugLog($"EASYFISHING_RECAST_ATTEMPT_DIAG_FAILED player={playerNum}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        static void RodNbfbPostfix(int __0, bool __result)
        {
            DebugLog($"Diag Rod.NBFBPMNMBJG player={__0} result={__result}");
        }

        static void RodOfakPostfix(int __0, bool __result)
        {
            if (__result)
                MarkBaitProtection(__0, 8f);

            DebugLog($"Diag Rod.OFAKNHNLKGI player={__0} result={__result}");
        }

        static void RodAnimationStagePrefix(int __0, MethodBase __originalMethod)
        {
            MarkBaitProtection(__0, 8f);
            DebugLog($"Diag Rod.{__originalMethod?.Name} animation player={__0}");
        }

        static void CharacterAnimatorToolStagePrefix(MethodBase __originalMethod)
        {
            DebugLog($"Diag CharacterAnimator.{__originalMethod?.Name}");
        }

        static void FishingUiOpenClosePrefix(MethodBase __originalMethod, object[] __args)
        {
            DebugLog($"Diag FishingUI.{__originalMethod?.Name} args={(__args == null ? 0 : __args.Length)}");
        }

        static void SelectAFishPostfix(int __0, Fish __result)
        {
            DebugLog($"Diag FishingManager.SelectAFish player={__0} fishType={__result?.GetType().Name ?? "null"}");
        }

        static void UseSelectedItemPostfix(UseObject __instance, bool __0, bool __1, int __2, bool __result)
        {
            var playerNum = TryReadPlayerNum(__instance);
            if (__result)
                MarkBaitProtection(playerNum, 8f);

            if (_autoFish?.Value == true && __result && __2 == 1 && playerNum >= MinSafePlayerNum && playerNum <= MaxSafePlayerNum && IsRodSelectedForPlayer(playerNum))
            {
                if (_autoRecastCastingNowByPlayer[playerNum])
                    ContinueAutoRecastSession(playerNum, "auto_cast");
                else
                    StartAutoRecastSession(playerNum, "manual_cast");
            }

            if (ShouldLogInputPipeline(playerNum, __0, __2))
                LogInputPipeline(playerNum, "UseSelectedItem", "UseObject.postfix", __0, __2, __result);

            DebugLog($"Diag UseObject.UseSelectedItem player={playerNum} pressed={__0} allow={__1} actionIndex={__2} result={__result}");
        }

        static void ActionSelectedItemPostfix(ActionBarInventory __instance, int __0, bool __1, bool __2, bool __3, bool __4, int __5, bool __result)
        {
            if (__result)
                MarkBaitProtection(__0, 8f);

            if (__result && __5 == 1)
                ArmAutoRecastSessionIfEligible(__0, "action_selected_item");

            if (ShouldLogInputPipeline(__0, __1, __5))
                LogInputPipeline(__0, "ActionSelectedItem", "ActionBarInventory.postfix", __1, __5, __result);

            DebugLog($"Diag ActionBarInventory.ActionSelectedItem player={__0} pressed={__1} allow={__2} objectClick={__3} skip={__4} actionIndex={__5} result={__result}");
        }

        private static bool ShouldLogInputPipeline(int playerNum, bool pressed, int actionIndex)
        {
            if (_debugLogging?.Value != true || !IsRemoveRecastDelayEnabled())
                return false;

            if (pressed || actionIndex == 1)
                return true;

            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum)
                return false;

            var now = Time.realtimeSinceStartup;
            var sinceFinish = playerNum < _lastFinishFishingAtByPlayer.Length && _lastFinishFishingAtByPlayer[playerNum] > 0f ? now - _lastFinishFishingAtByPlayer[playerNum] : 999f;
            var sinceCleanup = playerNum < _lastRemoveRecastCleanupAtByPlayer.Length && _lastRemoveRecastCleanupAtByPlayer[playerNum] > 0f ? now - _lastRemoveRecastCleanupAtByPlayer[playerNum] : 999f;
            return sinceFinish <= 5f || sinceCleanup <= 5f;
        }

        private static void LogInputPipeline(int playerNum, string methodName, string source, bool pressed, int actionIndex, bool result)
        {
            try
            {
                var now = Time.realtimeSinceStartup;
                var controller = GetFishingControllerSafe(playerNum);
                var hook = controller == null ? null : Traverse.Create(controller).Field("fishingHook")?.GetValue() as FishingHook;
                var useObject = playerNum >= MinSafePlayerNum && playerNum <= MaxSafePlayerNum ? UseObject.GetPlayer(playerNum) : null;
                var buttonDown = SafeMemberReadAsString(useObject, "buttonDown");
                var actionToolDone = SafeMemberReadAsString(useObject, "actionToolDone");
                var sinceFinish = playerNum < _lastFinishFishingAtByPlayer.Length && _lastFinishFishingAtByPlayer[playerNum] > 0f ? now - _lastFinishFishingAtByPlayer[playerNum] : -1f;
                var sinceCleanup = playerNum < _lastRemoveRecastCleanupAtByPlayer.Length && _lastRemoveRecastCleanupAtByPlayer[playerNum] > 0f ? now - _lastRemoveRecastCleanupAtByPlayer[playerNum] : -1f;
                var uiOpen = false;
                var uiContentActive = false;
                try
                {
                    var fishingUi = FishingUI.Get(playerNum);
                    uiOpen = fishingUi != null && fishingUi.IsOpen();
                    uiContentActive = fishingUi?.content != null && fishingUi.content.activeInHierarchy;
                }
                catch
                {
                }

                DebugLog(
                    $"EASYFISHING_INPUT_PIPELINE source={source} player={playerNum} method={methodName} pressed={pressed} actionIndex={actionIndex} result={result} " +
                    $"sinceFinish={sinceFinish:0.000} sinceCleanup={sinceCleanup:0.000} uiOpen={uiOpen} uiContentActive={uiContentActive} mainUiOpen={SafeMainUiOpen(playerNum)} " +
                    $"fishing={controller?.fishing} fishingCamera={(controller != null && IsControllerFishingCameraActive(controller))} " +
                    $"hookActive={(hook?.gameObject != null && hook.gameObject.activeInHierarchy)} fishInfoActive={(hook?.fishInfo != null && hook.fishInfo.activeInHierarchy)} " +
                    $"rodSelected={IsRodSelectedForPlayer(playerNum)} useButtonDown={buttonDown} useActionToolDone={actionToolDone}");
            }
            catch (Exception ex)
            {
                DebugLog($"EASYFISHING_INPUT_PIPELINE_FAILED player={playerNum} method={methodName}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static void LogQuickBitesDiagThrottled(int playerNum, string reason, string details)
        {
            if (_debugLogging?.Value != true)
                return;

            var key = $"{playerNum}:{reason}";
            var now = Time.realtimeSinceStartup;
            if (_nextQuickBitesDiagAtByPlayerReason.TryGetValue(key, out var nextAt) && now < nextAt)
                return;

            _nextQuickBitesDiagAtByPlayerReason[key] = now + 1.0f;
            DebugLog($"EASYFISHING_QUICK_BITES_DIAG player={playerNum} reason={reason} {details}");
        }

        static void ActionBarMbohPostfix(ActionBarInventory __instance, object __result)
        {
            DebugLog($"Diag ActionBarInventory.MBOHNGNNCED resultType={__result?.GetType().FullName ?? "null"}");
        }

        static bool ActionBarAnyActionPrefix(ActionBarInventory __instance, int __0, bool __1, MethodBase __originalMethod)
        {
            if (__1)
                MarkBaitProtection(__0, 8f);

            DebugLog($"Diag ActionBarInventory.{__originalMethod?.Name} prefix player={__0} pressed={__1}");
            return true;
        }

        static int TryReadPlayerNum(object instance)
        {
            if (instance == null)
                return -1;

            foreach (var memberName in new[] { "JIIGOACEIKL", "playerNum", "_playerNum" })
            {
                try
                {
                    var fieldValue = Traverse.Create(instance).Field(memberName)?.GetValue();
                    if (fieldValue is int fieldInt)
                        return fieldInt;
                }
                catch
                {
                }

                try
                {
                    var propertyValue = Traverse.Create(instance).Property(memberName)?.GetValue();
                    if (propertyValue is int propertyInt)
                        return propertyInt;
                }
                catch
                {
                }
            }

            return -1;
        }


        //////////////////////////////////////////////////////////////////
        ///  Don't use bait
        ///  
        static void FinishFishingPrefix(FishingController __instance, bool __0)
        {
            DebugLog("FinishFishingPrefix");
            MarkBaitProtection(__instance?.playerNum ?? -1, 2f);
            ApplyRemoveRecastDelaySettings(__instance, "FinishFishing");
            var playerNum = __instance?.playerNum ?? -1;
            if (playerNum >= 0 && playerNum < _lastFinishFishingAtByPlayer.Length)
                _lastFinishFishingAtByPlayer[playerNum] = Time.realtimeSinceStartup;
            if (__0)
                ArmAutoRecastSessionIfEligible(playerNum, "finish_fishing_success");
        }

        static void ApplyRemoveRecastDelaySettings(FishingController controller, string source)
        {
            if (!IsRemoveRecastDelayEnabled() || controller == null)
                return;

            try
            {
                var settings = FishingControllerSettings.GetValue(controller) as FishingManagerSettings;
                if (settings == null)
                    return;

                var changed = Math.Abs(settings.rollUpWaitTime) > 0.0001f || Math.Abs(settings.rollUpTime - 0.01f) > 0.0001f;
                settings.rollUpWaitTime = 0f;
                settings.rollUpTime = 0.01f;
                if (changed)
                    DebugLog($"EASYFISHING_REMOVE_RECAST_DELAY_SETTINGS_APPLIED source={source} player={controller.playerNum}");
            }
            catch (Exception ex)
            {
                LogThrottledDiagnostic($"Remove Recast Delay settings failed source={source}: {ex.GetType().Name}: {ex.Message}");
            }

            // Zero out CommonReferences wait fields used in the FinishFishing coroutine.
            // The coroutine uses wait1_5 three times (4.5s total) plus potentially wait1, wait05, etc.
            // Replacing them all with near-zero makes the post-catch animation instant.
            try
            {
                if (!_commonReferencesWait1_5Zeroed)
                {
                    var waitFields = new[] { "wait1_5", "wait1", "wait05", "wait2", "wait2_5", "wait3", "wait3_5", "wait4", "wait5" };
                    var zeroed = new System.Collections.Generic.List<string>();
                    foreach (var fieldName in waitFields)
                    {
                        var f = typeof(CommonReferences).GetField(fieldName,
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (f != null)
                        {
                            f.SetValue(null, new WaitForSeconds(0.01f));
                            zeroed.Add(fieldName);
                        }
                    }
                    _commonReferencesWait1_5Zeroed = true;
                    Log.LogInfo($"EASYFISHING_REMOVE_RECAST_DELAY_WAITS_ZEROED source={source} fields={string.Join(",", zeroed)}");
                }
            }
            catch (Exception ex)
            {
                LogThrottledDiagnostic($"Remove Recast Delay wait zero failed source={source}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        static bool PlayerInventoryRemoveItemItemPrefix(PlayerInventory __instance, Item __0, ref Slot __result, MethodBase __originalMethod)
        {
            return TryHandleBaitRemovalBlock(__instance, __0, ref __result, __originalMethod);
        }

        static bool PlayerInventoryRemoveItemItemBoolPrefix(PlayerInventory __instance, Item __0, bool __1, ref Slot __result, MethodBase __originalMethod)
        {
            return TryHandleBaitRemovalBlock(__instance, __0, ref __result, __originalMethod);
        }

        static bool ContainerRemoveItemPrefix(Container __instance, Item __0, bool __1, ref Slot __result, MethodBase __originalMethod)
        {
            return TryHandleBaitRemovalBlock(__instance, __0, ref __result, __originalMethod);
        }

        static bool SlotConsumeOnePrefix(Slot __instance, bool __0, ref bool __result, MethodBase __originalMethod)
        {
            if (_dontUseBait?.Value != true)
                return true;

            if (_isRefundingBait)
                return true;

            if (__instance == null)
                return true;

            if (!TryGetSlotItemId(__instance, out var slotItemId) || !_baitItemIds.Contains(slotItemId))
                return true;

            var methodName = __originalMethod?.Name ?? "unknown";
            if (!TryResolvePlayerForSlot(__instance, out var playerNum))
            {
                DebugLog($"EASYFISHING_BAIT_REMOVE_ALLOW reason=slot-player-unresolved method={methodName} itemId={slotItemId} baitName={GetBaitName(slotItemId)} player=-1");
                return true;
            }

            if (!IsBaitProtectionActive(playerNum) && !IsFishingContext(playerNum))
                return true;

            if (!SelectedOrProtectedBaitMatches(playerNum, slotItemId))
                return true;

            __result = true;
            Log.LogInfo($"EASYFISHING_DONT_USE_BAIT_SLOT_BLOCKED method={methodName} player={playerNum} itemId={slotItemId} baitName={GetBaitName(slotItemId)}");
            return false;
        }

        static bool SlotSetStackPrefix(Slot __instance, ref int __0, bool __1, bool __2, MethodBase __originalMethod)
        {
            if (_dontUseBait?.Value != true || _isRefundingBait)
                return true;

            if (__instance == null)
                return true;

            var oldAmount = Mathf.Max(0, GetSlotAmount(__instance));
            var requested = Mathf.Max(0, __0);
            if (requested >= oldAmount)
                return true;

            if (!TryGetSlotItemId(__instance, out var itemId) || !_baitItemIds.Contains(itemId))
                return true;

            var methodName = __originalMethod?.Name ?? "unknown";
            if (!TryResolvePlayerForSlot(__instance, out var playerNum))
            {
                DebugLog($"EASYFISHING_BAIT_STACK_ALLOW reason=slot-player-unresolved method={methodName} itemId={itemId} baitName={GetBaitName(itemId)} old={oldAmount} requested={requested}");
                return true;
            }

            if (!ShouldProtectSelectedFishingBait(playerNum, itemId))
            {
                DebugLog($"EASYFISHING_BAIT_STACK_ALLOW reason=not-protected method={methodName} player={playerNum} itemId={itemId} baitName={GetBaitName(itemId)} old={oldAmount} requested={requested}");
                return true;
            }

            __0 = oldAmount;
            Log.LogInfo($"EASYFISHING_DONT_USE_BAIT_STACK_BLOCKED method={methodName} player={playerNum} itemId={itemId} baitName={GetBaitName(itemId)} old={oldAmount} requested={requested}");
            return true;
        }

        static bool TryHandleBaitRemovalBlock(object inventoryOrContainer, Item item, ref Slot result, MethodBase originalMethod)
        {
            if (_dontUseBait?.Value != true)
                return true;

            if (inventoryOrContainer == null || item == null)
                return true;

            if (!TryGetItemId(item, out var removedItemId))
                return true;

            if (!_baitItemIds.Contains(removedItemId))
                return true;

            var baitName = GetBaitName(removedItemId);
            var methodName = originalMethod?.Name ?? "unknown";
            var playerForSeen = -1;
            TryGetPlayerNumFromInventoryOrContainer(inventoryOrContainer, out playerForSeen);
            DebugLog($"EASYFISHING_BAIT_REMOVE_SEEN method={methodName} itemId={removedItemId} baitName={baitName} player={playerForSeen}");

            if (!TryGetPlayerNumFromInventoryOrContainer(inventoryOrContainer, out var playerNum))
            {
                DebugLog($"EASYFISHING_BAIT_REMOVE_ALLOW reason=player-unresolved method={methodName} itemId={removedItemId} baitName={baitName} player=-1");
                return true;
            }

            var controller = GetFishingControllerSafe(playerNum);
            if (controller == null)
            {
                DebugLog($"EASYFISHING_BAIT_REMOVE_ALLOW reason=controller-missing method={methodName} itemId={removedItemId} baitName={baitName} player={playerNum}");
                return true;
            }

            Item selectedBaitItem = null;
            try
            {
                selectedBaitItem = FishingManager.BaitItem(controller.baitSelected);
            }
            catch
            {
                DebugLog($"EASYFISHING_BAIT_REMOVE_ALLOW reason=bait-resolve-failed method={methodName} itemId={removedItemId} baitName={baitName} player={playerNum}");
                return true;
            }

            if (!TryGetItemId(selectedBaitItem, out var selectedBaitItemId))
            {
                DebugLog($"EASYFISHING_BAIT_REMOVE_ALLOW reason=selected-bait-id-missing method={methodName} itemId={removedItemId} baitName={baitName} player={playerNum}");
                return true;
            }

            if (selectedBaitItemId != removedItemId)
            {
                DebugLog($"EASYFISHING_BAIT_REMOVE_ALLOW reason=selected-bait-mismatch method={methodName} itemId={removedItemId} baitName={baitName} player={playerNum}");
                return true;
            }

            if (!controller.fishing && !IsBaitProtectionActive(playerNum))
            {
                DebugLog($"EASYFISHING_BAIT_REMOVE_ALLOW reason=no-fishing-context method={methodName} itemId={removedItemId} baitName={baitName} player={playerNum}");
                return true;
            }

            result = null;
            Log.LogInfo($"EASYFISHING_DONT_USE_BAIT_BLOCKED method={methodName} player={playerNum} baitType={controller.baitSelected} itemId={removedItemId} baitName={baitName}");
            return false;
        }

        static void MarkBaitProtection(int playerNum, float seconds)
        {
            if (_dontUseBait?.Value != true)
                return;

            if (playerNum < 0 || playerNum >= _baitProtectionUntilByPlayer.Length)
                return;

            try
            {
                var controller = GetFishingControllerSafe(playerNum);
                if (controller == null)
                {
                    DebugLog($"MarkBaitProtection: no controller for player={playerNum}");
                }
                else
                {
                    Item selectedBaitItem = null;
                    try
                    {
                        selectedBaitItem = FishingManager.BaitItem(controller.baitSelected);
                    }
                    catch
                    {
                        selectedBaitItem = null;
                    }

                    if (TryGetItemId(selectedBaitItem, out var baitId) && _baitItemIds.Contains(baitId))
                    {
                        var baselineCount = CountPlayerItem(playerNum, baitId);
                        _protectedBaitIdByPlayer[playerNum] = baitId;
                        _protectedBaitBaselineByPlayer[playerNum] = baselineCount;
                        Log.LogInfo($"EASYFISHING_BAIT_PROTECTION_MARKED player={playerNum} baitId={baitId} baitName={GetBaitName(baitId)} baseline={baselineCount} seconds={seconds:0.###}");
                    }
                    else
                    {
                        DebugLog($"MarkBaitProtection: selected bait unresolved player={playerNum}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"MarkBaitProtection failed player={playerNum}: {ex.GetType().Name}: {ex.Message}");
            }

            var duration = Mathf.Max(0f, seconds);
            var until = Time.realtimeSinceStartup + duration;
            if (until > _baitProtectionUntilByPlayer[playerNum])
                _baitProtectionUntilByPlayer[playerNum] = until;
        }

        static bool IsBaitProtectionActive(int playerNum)
        {
            if (playerNum < 0 || playerNum >= _baitProtectionUntilByPlayer.Length)
                return false;

            return Time.realtimeSinceStartup <= _baitProtectionUntilByPlayer[playerNum];
        }

        static void MarkBaitProtectionForAllPlayers(float seconds)
        {
            for (var playerNum = 0; playerNum < _baitProtectionUntilByPlayer.Length; playerNum++)
                MarkBaitProtection(playerNum, seconds);
        }

        static bool SelectedOrProtectedBaitMatches(int playerNum, int itemId)
        {
            if (playerNum < 0 || playerNum >= _protectedBaitIdByPlayer.Length)
                return false;

            if (_protectedBaitIdByPlayer[playerNum] == itemId)
                return true;

            var controller = GetFishingControllerSafe(playerNum);
            if (controller == null)
                return false;

            try
            {
                var selectedBaitItem = FishingManager.BaitItem(controller.baitSelected);
                return TryGetItemId(selectedBaitItem, out var selectedId) && selectedId == itemId;
            }
            catch
            {
                return false;
            }
        }

        static string GetBaitName(int itemId)
        {
            return _baitNamesByItemId.TryGetValue(itemId, out var name) ? name : "Unknown";
        }

        static bool TryGetItemId(Item item, out int itemId)
        {
            itemId = 0;
            if (item == null)
                return false;

            try
            {
                itemId = Traverse.Create(item).Field("id").GetValue<int>();
            }
            catch
            {
                itemId = 0;
            }

            return itemId != 0;
        }

        static FishingController GetFishingControllerSafe(int playerNum)
        {
            try
            {
                return FishingController.Get(playerNum);
            }
            catch
            {
                return null;
            }
        }

        static bool TryGetPlayerNumFromInventory(PlayerInventory inventory, out int playerNum)
        {
            playerNum = -1;
            if (inventory == null)
                return false;

            try
            {
                var reflected = Traverse.Create(inventory).Field("playerNum")?.GetValue();
                if (reflected is int p)
                {
                    playerNum = p;
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                var reflectedAlias = Traverse.Create(inventory).Field("JIIGOACEIKL")?.GetValue();
                if (reflectedAlias is int pAlias)
                {
                    playerNum = pAlias;
                    return true;
                }
            }
            catch
            {
            }

            for (var i = MinSafePlayerNum; i <= MaxSafePlayerNum; i++)
            {
                try
                {
                    var byPlayer = PlayerInventory.GetPlayer(i);
                    if (ReferenceEquals(byPlayer, inventory))
                    {
                        playerNum = i;
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        static void PlayerInventoryAddItemPostfix(PlayerInventory __instance, ItemInstance __0, bool __result, MethodBase __originalMethod)
        {
            if (!IsRemoveRecastDelayEnabled() || !__result)
                return;

            if (!TryGetPlayerNumFromInventory(__instance, out var playerNum))
                return;

            var controller = GetFishingControllerSafe(playerNum);
            if (controller == null)
                return;

            Log.LogInfo($"EASYFISHING_ADD_ITEM_SEEN player={playerNum} method={__originalMethod?.Name} result={__result} fishing={controller.fishing} camera={IsControllerFishingCameraActive(controller)} uiFish={HasFishingUiFish(playerNum)}");

            if (!LooksLikeFishingRewardContext(playerNum, controller))
                return;

            _removeRecastDelayCleanupAtByPlayer[playerNum] = Time.realtimeSinceStartup + 0.05f;
            Log.LogInfo($"EASYFISHING_REMOVE_RECAST_DELAY_REWARD_SEEN player={playerNum} method={__originalMethod?.Name}");
        }

        static bool LooksLikeFishingRewardContext(int playerNum, FishingController controller)
        {
            if (controller == null)
                return false;

            if (HasFishingUiFish(playerNum))
                return true;

            try
            {
                var hook = Traverse.Create(controller).Field("fishingHook")?.GetValue() as FishingHook;
                if (hook != null)
                {
                    if (hook.fishInfo != null && hook.fishInfo.activeInHierarchy)
                        return true;
                    if (hook.gameObject != null && hook.gameObject.activeInHierarchy && (controller.fishing || IsControllerFishingCameraActive(controller)))
                        return true;
                }
            }
            catch
            {
            }

            return controller.fishing || IsControllerFishingCameraActive(controller);
        }

        static bool HasFishingUiFish(int playerNum)
        {
            try
            {
                var ui = FishingUI.Get(playerNum);
                return ui?.fish != null;
            }
            catch
            {
                return false;
            }
        }

        static void CloseFishingUiForRemoveRecastDelay(int playerNum, string source)
        {
            try
            {
                var ui = FishingUI.Get(playerNum);
                if (ui == null)
                    return;

                var wasOpen = false;
                var contentActive = false;
                try
                {
                    wasOpen = ui.IsOpen();
                    contentActive = ui.content != null && ui.content.activeInHierarchy;
                }
                catch
                {
                }

                ui.CloseUI();
                ui.fish = null;
                Log.LogInfo($"EASYFISHING_REMOVE_RECAST_DELAY_UI_CLOSED player={playerNum} source={source} wasOpen={wasOpen} contentActive={contentActive}");
            }
            catch (Exception ex)
            {
                LogThrottledDiagnostic($"EASYFISHING_REMOVE_RECAST_DELAY_UI_CLOSE_FAILED player={playerNum} source={source}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        static bool IsRemoveRecastGameplayCleanupSource(string source)
        {
            return source == "reward-seen"
                || source == "completed-state"
                || source == "auto-recast-gate";
        }

        static void EnsureObservedCompletedFishingSession(int playerNum, string source, bool fishInfoActive, bool hookActive, FishingController controller)
        {
            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum)
                return;
            if (controller == null)
                return;
            if (!(source == "completed-state" || source == "reward-seen" || source == "manual-recast-input"))
                return;
            if (controller.fishing || IsControllerFishingCameraActive(controller))
                return;

            var hasActiveResult = fishInfoActive || hookActive;
            var hasUiFish = HasFishingUiFish(playerNum);

            if (source == "completed-state" && !hasActiveResult)
                return;
            if ((source == "reward-seen" || source == "manual-recast-input") && !hasActiveResult && !hasUiFish)
                return;

            var now = Time.realtimeSinceStartup;
            var changed = false;
            var mode = string.Empty;
            var finishAt = playerNum < _lastFinishFishingAtByPlayer.Length ? _lastFinishFishingAtByPlayer[playerNum] : 0f;
            var startAt = playerNum < _lastFishingStartAtByPlayer.Length ? _lastFishingStartAtByPlayer[playerNum] : 0f;

            if (hasActiveResult)
            {
                var missingFinish = finishAt <= 0f;
                var staleFinish = !missingFinish && now - finishAt >= FishResultDisplayMinTime;
                if (missingFinish || staleFinish)
                {
                    finishAt = now;
                    _lastFinishFishingAtByPlayer[playerNum] = finishAt;
                    _lastFishingStartAtByPlayer[playerNum] = finishAt - 0.001f;
                    changed = true;
                    mode = staleFinish ? "refresh" : "seed";
                }
            }
            else if (finishAt <= 0f)
            {
                finishAt = now;
                _lastFinishFishingAtByPlayer[playerNum] = finishAt;
                startAt = finishAt - 0.001f;
                _lastFishingStartAtByPlayer[playerNum] = startAt;
                changed = true;
                mode = "seed";
            }

            if (!changed && finishAt > 0f && startAt <= 0f)
            {
                startAt = finishAt - 0.001f;
                _lastFishingStartAtByPlayer[playerNum] = startAt;
                changed = true;
                mode = "seed";
            }

            if (!changed)
                return;

            var key = $"{playerNum}:{source}";
            if (!_nextObservedCompletedSessionDiagAtByPlayerSource.TryGetValue(key, out var nextAt) || now >= nextAt)
            {
                _nextObservedCompletedSessionDiagAtByPlayerSource[key] = now + 2f;
                DebugLog($"EASYFISHING_OBSERVED_COMPLETED_SESSION player={playerNum} source={source} mode={mode} finishAt={finishAt:0.000} startAt={startAt:0.000} hookActive={hookActive} fishInfoActive={fishInfoActive}");
            }
        }

        static bool TryEndCompletedFishingSelectedUse(int playerNum, string source, float finishAt)
        {
            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum || playerNum >= _lastEndSelectedUseAtByPlayer.Length)
                return false;
            if (finishAt <= 0f)
                return false;
            if (_lastEndSelectedUseFinishAtByPlayer[playerNum] == finishAt)
                return false;

            try
            {
                var useObject = UseObject.GetPlayer(playerNum);
                useObject?.EndSelectedItem(1);
                var rodActionEndRan = TryEndCompletedFishingRodAction(playerNum, source, out var rodActionEndMethodName);
                ForceUseObjectActionReady(useObject, playerNum, source);
                var buttonDown = SafeMemberReadAsString(useObject, "buttonDown");
                var actionToolDone = SafeMemberReadAsString(useObject, "actionToolDone");
                _lastEndSelectedUseAtByPlayer[playerNum] = Time.realtimeSinceStartup;
                _lastEndSelectedUseFinishAtByPlayer[playerNum] = finishAt;
                DebugLog($"EASYFISHING_REMOVE_RECAST_DELAY_END_USE player={playerNum} source={source} finishAt={finishAt:0.000} rodActionEnd={rodActionEndRan} rodActionEndMethod={rodActionEndMethodName} buttonDown={buttonDown} actionToolDone={actionToolDone}");
                return true;
            }
            catch (Exception ex)
            {
                DebugLog($"EASYFISHING_REMOVE_RECAST_DELAY_END_USE_FAILED player={playerNum} source={source}: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        static void ForceUseObjectActionReady(UseObject useObject, int playerNum, string source)
        {
            if (useObject == null)
                return;
            if (!IsRemoveRecastDelayEnabled())
                return;
            if (!(source == "completed-state" || source == "reward-seen" || source == "manual-recast-input" || source == "unlock-window"))
                return;

            var beforeButtonDown = SafeMemberReadAsString(useObject, "buttonDown");
            var beforeActionToolDone = SafeMemberReadAsString(useObject, "actionToolDone");
            var buttonChanged = false;
            var actionToolDoneChanged = false;

            try
            {
                var traverse = Traverse.Create(useObject);
                var buttonField = traverse.Field("buttonDown");
                if (buttonField != null)
                {
                    var value = buttonField.GetValue();
                    if (value is int)
                    {
                        buttonField.SetValue(0);
                        buttonChanged = true;
                    }
                    else if (value is bool)
                    {
                        buttonField.SetValue(false);
                        buttonChanged = true;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"EASYFISHING_REMOVE_RECAST_DELAY_USEOBJECT_BUTTON_READY_FAILED player={playerNum} source={source}: {ex.GetType().Name}: {ex.Message}");
            }

            try
            {
                var actionToolDoneField = Traverse.Create(useObject).Field("actionToolDone");
                if (actionToolDoneField != null)
                {
                    actionToolDoneField.SetValue(true);
                    actionToolDoneChanged = true;
                }
            }
            catch (Exception ex)
            {
                DebugLog($"EASYFISHING_REMOVE_RECAST_DELAY_USEOBJECT_ACTION_READY_FAILED player={playerNum} source={source}: {ex.GetType().Name}: {ex.Message}");
            }

            var afterButtonDown = SafeMemberReadAsString(useObject, "buttonDown");
            var afterActionToolDone = SafeMemberReadAsString(useObject, "actionToolDone");
            Log.LogInfo($"EASYFISHING_REMOVE_RECAST_DELAY_USEOBJECT_READY player={playerNum} source={source} beforeButtonDown={beforeButtonDown} afterButtonDown={afterButtonDown} beforeActionToolDone={beforeActionToolDone} afterActionToolDone={afterActionToolDone} buttonChanged={buttonChanged} actionToolDoneChanged={actionToolDoneChanged}");
        }

        static bool TryEndCompletedFishingRodAction(int playerNum, string source, out string methodName)
        {
            methodName = "<none>";
            if (!IsRemoveRecastDelayEnabled())
                return false;
            if (playerNum < MinSafePlayerNum || playerNum > MaxSafePlayerNum)
                return false;

            var rod = GetSelectedRodForPlayer(playerNum);
            if (rod == null)
            {
                DebugLog($"EASYFISHING_REMOVE_RECAST_DELAY_ROD_ACTION_END_SKIPPED player={playerNum} source={source} reason=rod_unresolved");
                return false;
            }

            var method = _rodActionEndMethod;
            var fallback = false;
            if (method == null)
            {
                method = ResolveMethod(typeof(Rod), "ActionEnd", new[] { typeof(int) });
                _rodActionEndMethod = method;
            }
            if (method == null)
            {
                method = _rodActionEndFallbackMethod;
                fallback = method != null;
            }

            if (method == null)
            {
                DebugLog($"EASYFISHING_REMOVE_RECAST_DELAY_ROD_ACTION_END_SKIPPED player={playerNum} source={source} reason=method_missing");
                return false;
            }

            try
            {
                method.Invoke(rod, new object[] { playerNum });
                methodName = method.Name;
                DebugLog($"EASYFISHING_REMOVE_RECAST_DELAY_ROD_ACTION_END player={playerNum} source={source} method={method.Name} fallback={fallback}");
                return true;
            }
            catch (Exception ex)
            {
                methodName = method.Name;
                DebugLog($"EASYFISHING_REMOVE_RECAST_DELAY_ROD_ACTION_END_FAILED player={playerNum} source={source} method={method.Name}: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        static Transform ResolvePreservedResultParent(FishingHook hook)
        {
            try
            {
                if (_preservedFishResultRoot == null)
                {
                    _preservedFishResultRoot = new GameObject("EasyFishingPreservedResults");
                    UnityEngine.Object.DontDestroyOnLoad(_preservedFishResultRoot);
                }

                return _preservedFishResultRoot.transform;
            }
            catch
            {
                return null;
            }
        }

        static void MakePreservedResultNonBlocking(GameObject visual)
        {
            if (visual == null)
                return;

            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (var collider in visual.GetComponentsInChildren<Collider2D>(true))
                collider.enabled = false;
            foreach (var graphic in visual.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            foreach (var canvasGroup in visual.GetComponentsInChildren<CanvasGroup>(true))
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            foreach (var selectable in visual.GetComponentsInChildren<Selectable>(true))
                selectable.interactable = false;
        }

        static void ClearPreservedFishResultVisual(int playerNum, string source)
        {
            if (playerNum < 0 || playerNum >= _preservedFishResultVisualByPlayer.Length)
                return;

            var visual = _preservedFishResultVisualByPlayer[playerNum];
            _preservedFishResultVisualByPlayer[playerNum] = null;
            _preservedFishResultVisualUntilByPlayer[playerNum] = 0f;

            if (visual == null)
                return;

            try
            {
                UnityEngine.Object.Destroy(visual);
                DebugLog($"EASYFISHING_REMOVE_RECAST_DELAY_RESULT_SNAPSHOT_CLEARED player={playerNum} source={source}");
            }
            catch (Exception ex)
            {
                LogThrottledDiagnostic($"Failed clearing preserved fish result visual player={playerNum} source={source}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        static bool TryPreserveFishResultVisualSnapshot(int playerNum, FishingHook hook, string source)
        {
            if (playerNum < 0 || playerNum >= _preservedFishResultVisualByPlayer.Length)
                return false;
            if (hook?.fishInfo == null || !hook.fishInfo.activeInHierarchy)
                return false;
            if (hook.fishIconInfo == null || hook.fishIconInfo.sprite == null)
                return false;

            try
            {
                ClearPreservedFishResultVisual(playerNum, $"replace:{source}");

                var parent = ResolvePreservedResultParent(hook);
                var clone = new GameObject(hook.fishInfo.name + PreservedFishResultVisualSuffix);
                if (parent != null)
                    clone.transform.SetParent(parent, false);
                clone.transform.position = hook.fishIconInfo.transform.position;
                clone.transform.rotation = hook.fishIconInfo.transform.rotation;
                clone.transform.localScale = hook.fishIconInfo.transform.lossyScale;

                var spriteRenderer = clone.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = hook.fishIconInfo.sprite;
                spriteRenderer.flipX = hook.fishIconInfo.flipX;
                spriteRenderer.flipY = hook.fishIconInfo.flipY;
                spriteRenderer.color = hook.fishIconInfo.color;
                spriteRenderer.sortingLayerID = hook.fishIconInfo.sortingLayerID;
                spriteRenderer.sortingOrder = hook.fishIconInfo.sortingOrder + 1;
                MakePreservedResultNonBlocking(clone);

                _preservedFishResultVisualByPlayer[playerNum] = clone;
                _preservedFishResultVisualUntilByPlayer[playerNum] = Time.realtimeSinceStartup + FishResultDisplayMinTime;

                Log.LogInfo($"EASYFISHING_REMOVE_RECAST_DELAY_RESULT_SNAPSHOT player={playerNum} source={source} mode=inert-icon parent={(parent != null ? parent.name : "<world>")}");
                return true;
            }
            catch (Exception ex)
            {
                LogThrottledDiagnostic($"Failed preserving fish result visual snapshot player={playerNum} source={source}: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        static void PollPreservedFishResultVisuals()
        {
            var now = Time.realtimeSinceStartup;
            for (var playerNum = 0; playerNum < _preservedFishResultVisualByPlayer.Length; playerNum++)
            {
                if (_preservedFishResultVisualByPlayer[playerNum] == null)
                    continue;
                if (_preservedFishResultVisualUntilByPlayer[playerNum] > 0f && now >= _preservedFishResultVisualUntilByPlayer[playerNum])
                    ClearPreservedFishResultVisual(playerNum, "expired");
            }
        }

        static void ArmRecastUnlockWindow(int playerNum, float finishAt)
        {
            if (playerNum < 0 || playerNum >= _recastUnlockWindowUntilByPlayer.Length)
                return;
            var until = Time.realtimeSinceStartup + RecastUnlockWindowDuration;
            _recastUnlockWindowUntilByPlayer[playerNum] = until;
            _recastUnlockWindowFinishAtByPlayer[playerNum] = finishAt;
            DebugLog($"EASYFISHING_RECAST_UNLOCK_WINDOW_ARMED player={playerNum} until={until:0.000} finishAt={finishAt:0.000}");
        }

        static void PollRecastUnlockWindow(int playerNum, FishingController controller)
        {
            if (playerNum < 0 || playerNum >= _recastUnlockWindowUntilByPlayer.Length)
                return;

            var now = Time.realtimeSinceStartup;
            var until = _recastUnlockWindowUntilByPlayer[playerNum];
            var hasPending = playerNum < _pendingRecastByPlayer.Length && _pendingRecastByPlayer[playerNum];

            if (until <= 0f && !hasPending)
                return;
            if (until > 0f && now > until && !hasPending)
            {
                _recastUnlockWindowUntilByPlayer[playerNum] = 0f;
                return;
            }

            // If the game started a new fishing session, disarm immediately — do NOT interfere
            if (controller != null && controller.fishing)
            {
                DebugLog($"EASYFISHING_RECAST_UNLOCK_WINDOW_DISARMED player={playerNum} reason=new_session_detected");
                _recastUnlockWindowUntilByPlayer[playerNum] = 0f;
                if (playerNum < _pendingRecastByPlayer.Length)
                    _pendingRecastByPlayer[playerNum] = false;
                return;
            }

            // Only clear busy/UseObject state — do NOT touch controller.fishing here
            // (forcing it false caused instant reel-back on new casts)
            try
            {
                var player = PlayerController.GetPlayer(playerNum);
                if (player != null)
                    player.NILLCIMMKJE = false;
            }
            catch { }

            var useObject = UseObject.GetPlayer(playerNum);
            if (useObject != null)
                ForceUseObjectActionReady(useObject, playerNum, "unlock-window");

            // Detect raw recast input — use GetMouseButton(0) too to catch held clicks
            var mouseDown = Input.GetMouseButtonDown(0) || (hasPending && Input.GetMouseButton(0));
            var rodSelected = GetSelectedRodForPlayer(playerNum) != null;
            var mainUiOpen = false;
            try { mainUiOpen = SafeMainUiOpen(playerNum); } catch { }

            // Set pending if input seen
            if ((mouseDown || hasPending) && rodSelected)
            {
                if (playerNum < _pendingRecastByPlayer.Length)
                    _pendingRecastByPlayer[playerNum] = true;
                hasPending = true;
            }

            var diagKey = playerNum;
            var shouldDiag = now >= _nextRecastUnlockDiagAtByPlayer[diagKey];
            if (shouldDiag)
            {
                _nextRecastUnlockDiagAtByPlayer[diagKey] = now + 0.5f;
                bool fishingUiOpen = false;
                bool fishingUiContent = false;
                bool playerBusy = false;
                int buttonDown = -1;
                bool actionToolDone = false;
                try
                {
                    var ui = FishingUI.Get(playerNum);
                    if (ui != null)
                    {
                        fishingUiOpen = ui.IsOpen();
                        fishingUiContent = ui.content != null && ui.content.activeInHierarchy;
                    }
                } catch { }
                try { playerBusy = PlayerController.GetPlayer(playerNum)?.NILLCIMMKJE ?? false; } catch { }
                try
                {
                    var uo = UseObject.GetPlayer(playerNum);
                    if (uo != null)
                    {
                        buttonDown = uo.buttonDown;
                        actionToolDone = uo.actionToolDone;
                    }
                } catch { }
                DebugLog($"EASYFISHING_RECAST_UNLOCK_WINDOW_POLL player={playerNum} remaining={(until > 0f ? (until - now) : 0f):0.000} mouseDown={mouseDown} rodSelected={rodSelected} pending={hasPending} " +
                    $"mainUiOpen={mainUiOpen} fishingUiOpen={fishingUiOpen} fishingUiContent={fishingUiContent} " +
                    $"playerBusy={playerBusy} buttonDown={buttonDown} actionToolDone={actionToolDone} controllerFishing={controller?.fishing}");
            }

            if (!hasPending || !rodSelected)
                return;

            // Defer cast while any UI is open — close it if we have pending recast input
            if (mainUiOpen)
            {
                if (until > 0f && until - now < 0.6f)
                    _recastUnlockWindowUntilByPlayer[playerNum] = now + 0.6f;

                // Force-close the reward/result UI so the cast can proceed
                try
                {
                    MainUI.CloseAllUIWindows(false);
                    DebugLog($"EASYFISHING_RECAST_UNLOCK_WINDOW_CLOSE_MAIN_UI player={playerNum}");
                }
                catch (Exception ex)
                {
                    DebugLog($"EASYFISHING_RECAST_UNLOCK_WINDOW_CLOSE_MAIN_UI_FAILED player={playerNum}: {ex.GetType().Name}: {ex.Message}");
                }
                return;
            }

            // UI closed and pending input — attempt cast
            DebugLog($"EASYFISHING_RECAST_UNLOCK_WINDOW_RAW_INPUT player={playerNum} attempting UseSelectedItem bypass");

            try
            {
                var result = UseObject.GetPlayer(playerNum)?.UseSelectedItem(true, true, 1) ?? false;
                DebugLog($"EASYFISHING_RECAST_UNLOCK_WINDOW_BYPASS player={playerNum} UseSelectedItem result={result}");
                if (result)
                {
                    _recastUnlockWindowUntilByPlayer[playerNum] = 0f;
                    if (playerNum < _pendingRecastByPlayer.Length)
                        _pendingRecastByPlayer[playerNum] = false;
                    return;
                }

                // Fallback: ActionSelectedItem
                var actionBar = Traverse.Create(PlayerInventory.GetPlayer(playerNum))?.Field("actionBarInventory")?.GetValue();
                if (actionBar != null)
                {
                    var actionBarMethod = ResolveMethod(actionBar.GetType(), "ActionSelectedItem",
                        new[] { typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(int) });
                    if (actionBarMethod != null)
                    {
                        var abResult = actionBarMethod.Invoke(actionBar, new object[] { playerNum, true, true, false, false, 1 });
                        DebugLog($"EASYFISHING_RECAST_UNLOCK_WINDOW_BYPASS_AB player={playerNum} ActionSelectedItem result={abResult}");
                        if (abResult is bool abBool && abBool)
                        {
                            _recastUnlockWindowUntilByPlayer[playerNum] = 0f;
                            if (playerNum < _pendingRecastByPlayer.Length)
                                _pendingRecastByPlayer[playerNum] = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"EASYFISHING_RECAST_UNLOCK_WINDOW_BYPASS_FAILED player={playerNum}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        static void PollRemoveRecastDelay()
        {
            if (_lastRemoveRecastDelayPollFrame == Time.frameCount)
                return;

            _lastRemoveRecastDelayPollFrame = Time.frameCount;
            var now = Time.realtimeSinceStartup;
            PollPreservedFishResultVisuals();

            for (var playerNum = MinSafePlayerNum; playerNum <= MaxSafePlayerNum; playerNum++)
            {
                if (!IsPlayerRuntimeReady(playerNum))
                    continue;

                var controller = GetFishingControllerSafe(playerNum);
                if (controller == null)
                    continue;

                ApplyRemoveRecastDelaySettings(controller, "poll");

                // Poll the post-cleanup unlock window (continuous busy-clear + raw input bypass)
                if (IsRemoveRecastDelayEnabled())
                    PollRecastUnlockWindow(playerNum, controller);

                if (playerNum >= 0 && playerNum < _removeRecastDelayCleanupAtByPlayer.Length && _removeRecastDelayCleanupAtByPlayer[playerNum] > 0f && now >= _removeRecastDelayCleanupAtByPlayer[playerNum])
                {
                    TryClearCompletedFishingHook(playerNum, "reward-seen");
                    _removeRecastDelayCleanupAtByPlayer[playerNum] = 0f;
                    continue;
                }

                if (!controller.fishing && !IsControllerFishingCameraActive(controller))
                    TryClearCompletedFishingHook(playerNum, "completed-state");
            }
        }

        static void TryClearCompletedFishingHook(int playerNum, string source)
        {
            var controller = GetFishingControllerSafe(playerNum);
            if (controller == null)
                return;

            try
            {
                var hook = Traverse.Create(controller).Field("fishingHook")?.GetValue() as FishingHook;
                if (hook == null)
                    return;

                var fishInfoActive = hook.fishInfo != null && hook.fishInfo.activeInHierarchy;
                var hookActive = hook.gameObject != null && hook.gameObject.activeInHierarchy;

                if (source == "completed-state" && !fishInfoActive && !hookActive)
                    return;

                EnsureObservedCompletedFishingSession(playerNum, source, fishInfoActive, hookActive, controller);

                var isTrustedGameplayCleanupSource = IsRemoveRecastGameplayCleanupSource(source);
                var isManualRecastInput = source == "manual-recast-input";
                var lastFinishAt = playerNum >= 0 && playerNum < _lastFinishFishingAtByPlayer.Length ? _lastFinishFishingAtByPlayer[playerNum] : 0f;
                var lastStartAt = playerNum >= 0 && playerNum < _lastFishingStartAtByPlayer.Length ? _lastFishingStartAtByPlayer[playerNum] : 0f;
                var finishedCurrentSession = lastFinishAt > 0f && lastFinishAt >= lastStartAt;
                var recentFinish = finishedCurrentSession && Time.realtimeSinceStartup - lastFinishAt < FishResultDisplayMinTime;
                var manualCompletedCleanup = isManualRecastInput && recentFinish && (fishInfoActive || hookActive || HasFishingUiFish(playerNum));
                var isGameplayCleanupSource = isTrustedGameplayCleanupSource || manualCompletedCleanup;
                var endUseAttempted = false;
                var endUseRan = false;

                if (!isGameplayCleanupSource && (controller.fishing || IsControllerFishingCameraActive(controller)))
                    return;

                if (!fishInfoActive && !hookActive && !isGameplayCleanupSource)
                    return;

                var preserveResultVisual = isGameplayCleanupSource && fishInfoActive && finishedCurrentSession && recentFinish && !IsRemoveRecastDelayEnabled();

                var preservedSnapshot = preserveResultVisual && TryPreserveFishResultVisualSnapshot(playerNum, hook, source);

                try
                {
                    var player = PlayerController.GetPlayer(playerNum);
                    if (player != null)
                        player.NILLCIMMKJE = false;
                }
                catch
                {
                }

                if (isGameplayCleanupSource)
                {
                    if (finishedCurrentSession && recentFinish)
                    {
                        endUseAttempted = true;
                        endUseRan = TryEndCompletedFishingSelectedUse(playerNum, source, lastFinishAt);
                    }

                    controller.fishing = false;
                    ClearBiteList(controller, $"RemoveRecastDelay:{source}");
                    try
                    {
                        Traverse.Create(controller).Field("fishingCamera").SetValue(false);
                    }
                    catch
                    {
                    }
                }

                if (source == "completed-state" && (fishInfoActive || hookActive))
                {
                    var now = Time.realtimeSinceStartup;
                    if (playerNum >= 0 && playerNum < _nextCompletedStateHeartbeatAtByPlayer.Length && now >= _nextCompletedStateHeartbeatAtByPlayer[playerNum])
                    {
                        _nextCompletedStateHeartbeatAtByPlayer[playerNum] = now + 0.75f;
                        var endUseState = endUseAttempted ? (endUseRan ? "ran" : "attempted_not_run") : "skipped_not_recent_or_not_finished";
                        DebugLog(
                            $"EASYFISHING_REMOVE_RECAST_DELAY_HEARTBEAT player={playerNum} source={source} " +
                            $"hookActive={hookActive} fishInfoActive={fishInfoActive} lastFinishAt={lastFinishAt:0.000} lastStartAt={lastStartAt:0.000} " +
                            $"finishedCurrentSession={finishedCurrentSession} recentFinish={recentFinish} endUse={endUseState}");
                    }
                }

                if (fishInfoActive || hookActive)
                {
                    if (hook.fishInfo != null)
                        hook.fishInfo.SetActive(false);
                    if (hook.gameObject != null)
                        hook.gameObject.SetActive(false);

                    if (isGameplayCleanupSource)
                        CloseFishingUiForRemoveRecastDelay(playerNum, source);

                    if (preservedSnapshot)
                    {
                        Log.LogInfo($"EASYFISHING_REMOVE_RECAST_DELAY_DETACHED_RESULT_CLEARED player={playerNum} source={source} hookWasActive={hookActive} fishInfoWasActive={fishInfoActive}");
                    }
                    else
                    {
                        Log.LogInfo($"EASYFISHING_REMOVE_RECAST_DELAY_CLEARED player={playerNum} source={source} hookWasActive={hookActive} fishInfoWasActive={fishInfoActive} preserveRequested={preserveResultVisual}");
                    }

                    // Arm the post-cleanup unlock window so we can continuously re-clear busy state
                    // and intercept raw recast input during the native result-display lock period
                    if (IsRemoveRecastDelayEnabled() && isGameplayCleanupSource)
                        ArmRecastUnlockWindow(playerNum, lastFinishAt);
                }
                else if (isGameplayCleanupSource)
                {
                    var key = $"{playerNum}:{source}";
                    var now = Time.realtimeSinceStartup;
                    if (!_nextStateOnlyCleanupDiagAtByPlayerSource.TryGetValue(key, out var nextAt) || now >= nextAt)
                    {
                        _nextStateOnlyCleanupDiagAtByPlayerSource[key] = now + 2f;
                        DebugLog($"EASYFISHING_REMOVE_RECAST_DELAY_STATE_ONLY_CLEARED player={playerNum} source={source} hookWasActive={hookActive} fishInfoWasActive={fishInfoActive}");
                    }
                }

                if (playerNum >= 0 && playerNum < _lastRemoveRecastCleanupAtByPlayer.Length)
                    _lastRemoveRecastCleanupAtByPlayer[playerNum] = Time.realtimeSinceStartup;
            }
            catch (Exception ex)
            {
                LogThrottledDiagnostic($"Remove Recast Delay cleanup failed player={playerNum} source={source}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        static bool TryGetPlayerNumFromInventoryOrContainer(object inventoryOrContainer, out int playerNum)
        {
            playerNum = -1;
            if (inventoryOrContainer == null)
                return false;

            if (inventoryOrContainer is PlayerInventory playerInventory)
                return TryGetPlayerNumFromInventory(playerInventory, out playerNum);

            if (TryGetPlayerNumFromObjectField(inventoryOrContainer, out playerNum))
                return true;

            for (var i = MinSafePlayerNum; i <= MaxSafePlayerNum; i++)
            {
                try
                {
                    var playerInventoryCandidate = PlayerInventory.GetPlayer(i);
                    if (playerInventoryCandidate == null)
                        continue;

                    var actionBar = Traverse.Create(playerInventoryCandidate).Field("actionBarInventory")?.GetValue();
                    if (ReferenceEquals(actionBar, inventoryOrContainer))
                    {
                        playerNum = i;
                        return true;
                    }

                    var inventory = Traverse.Create(playerInventoryCandidate).Field("inventory")?.GetValue();
                    if (ReferenceEquals(inventory, inventoryOrContainer))
                    {
                        playerNum = i;
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        static bool TryGetPlayerNumFromObjectField(object instance, out int playerNum)
        {
            playerNum = -1;
            if (instance == null)
                return false;

            foreach (var fieldName in new[] { "playerNum", "JIIGOACEIKL" })
            {
                try
                {
                    var reflected = Traverse.Create(instance).Field(fieldName)?.GetValue();
                    if (reflected is int p)
                    {
                        playerNum = p;
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        static void PollDontUseBaitRefundFallback()
        {
            for (var playerNum = MinSafePlayerNum; playerNum <= MaxSafePlayerNum; playerNum++)
            {
                if (playerNum < 0 || playerNum >= _protectedBaitIdByPlayer.Length)
                    continue;

                var protectedBaitId = _protectedBaitIdByPlayer[playerNum];
                if (protectedBaitId == 0)
                    continue;

                if (!IsBaitProtectionActive(playerNum) && !IsFishingContext(playerNum))
                    continue;

                var baseline = _protectedBaitBaselineByPlayer[playerNum];
                var current = CountPlayerItem(playerNum, protectedBaitId);
                if (current >= baseline)
                    continue;

                var missing = baseline - current;
                if (missing <= 0)
                    continue;

                AddBait(playerNum, protectedBaitId, missing);
                Log.LogInfo($"EASYFISHING_DONT_USE_BAIT_REFUNDED player={playerNum} itemId={protectedBaitId} baitName={GetBaitName(protectedBaitId)} missing={missing} baseline={baseline} currentBefore={current}");
            }
        }

        static void PollDontUseBaitGlobalMonitor()
        {
            if (_lastDontUseBaitMonitorFrame == Time.frameCount)
                return;

            _lastDontUseBaitMonitorFrame = Time.frameCount;
            var now = Time.realtimeSinceStartup;

            for (var playerNum = MinSafePlayerNum; playerNum <= MaxSafePlayerNum; playerNum++)
            {
                if (!IsPlayerRuntimeReady(playerNum))
                    continue;

                var selectedBaitId = GetSelectedFishingBaitItemId(playerNum);
                var rodSelected = IsRodSelectedForPlayer(playerNum);
                var fishingContext = IsFishingContext(playerNum);

                if (rodSelected)
                    _rodSelectedUntilByPlayer[playerNum] = now + 3f;
                if (fishingContext)
                    _fishingContextUntilByPlayer[playerNum] = now + 10f;

                if (selectedBaitId != 0 && (rodSelected || fishingContext))
                {
                    _lastSelectedBaitIdByPlayer[playerNum] = selectedBaitId;
                    _lastSelectedBaitSeenAtByPlayer[playerNum] = now;
                }

                if (!_baitMonitorInitializedByPlayer[playerNum])
                {
                    for (var baitIndex = 0; baitIndex < BaitIds.Length; baitIndex++)
                    {
                        _lastBaitCountsByPlayer[playerNum, baitIndex] = CountPlayerItem(playerNum, BaitIds[baitIndex]);
                    }

                    _baitMonitorInitializedByPlayer[playerNum] = true;
                    DebugLog($"EASYFISHING_DONT_USE_BAIT_MONITOR_READY player={playerNum} selectedBaitId={selectedBaitId} rodSelected={rodSelected} fishingContext={fishingContext}");
                    continue;
                }

                for (var baitIndex = 0; baitIndex < BaitIds.Length; baitIndex++)
                {
                    var baitId = BaitIds[baitIndex];
                    var previous = _lastBaitCountsByPlayer[playerNum, baitIndex];
                    var current = CountPlayerItem(playerNum, baitId);
                    if (current < previous)
                    {
                        var missing = previous - current;
                        if (ShouldProtectSelectedFishingBait(playerNum, baitId))
                        {
                            try
                            {
                                _isRefundingBait = true;
                                AddBait(playerNum, baitId, missing);
                            }
                            finally
                            {
                                _isRefundingBait = false;
                            }

                            var after = CountPlayerItem(playerNum, baitId);
                            Log.LogInfo($"EASYFISHING_DONT_USE_BAIT_MONITOR_REFUNDED player={playerNum} itemId={baitId} baitName={GetBaitName(baitId)} missing={missing} previous={previous} currentBefore={current} currentAfter={after} selectedBaitId={selectedBaitId} rodSelected={rodSelected} fishingContext={fishingContext}");
                            current = after;
                        }
                        else
                        {
                            DebugLog($"EASYFISHING_DONT_USE_BAIT_MONITOR_DECREASE_IGNORED player={playerNum} itemId={baitId} baitName={GetBaitName(baitId)} previous={previous} current={current} selectedBaitId={selectedBaitId} rodSelected={rodSelected} fishingContext={fishingContext}");
                        }
                    }

                    _lastBaitCountsByPlayer[playerNum, baitIndex] = current;
                }
            }
        }

        static bool ShouldProtectSelectedFishingBait(int playerNum, int baitItemId)
        {
            if (playerNum < 0 || playerNum > 4)
                return false;

            if (!_baitItemIds.Contains(baitItemId))
                return false;

            var now = Time.realtimeSinceStartup;
            var selectedBaitId = GetSelectedFishingBaitItemId(playerNum);
            var baitMatches = baitItemId == selectedBaitId ||
                              (baitItemId == _lastSelectedBaitIdByPlayer[playerNum] && now - _lastSelectedBaitSeenAtByPlayer[playerNum] <= 10f);

            var context = IsFishingContext(playerNum) ||
                          IsRodSelectedForPlayer(playerNum) ||
                          now <= _rodSelectedUntilByPlayer[playerNum] ||
                          now <= _fishingContextUntilByPlayer[playerNum];

            return baitMatches && context;
        }

        static int GetSelectedFishingBaitItemId(int playerNum)
        {
            try
            {
                var controller = FishingController.Get(playerNum);
                if (controller == null)
                    return 0;

                var baitItem = FishingManager.BaitItem(controller.baitSelected);
                if (!TryGetItemId(baitItem, out var baitId))
                    return 0;

                return _baitItemIds.Contains(baitId) ? baitId : 0;
            }
            catch
            {
                return 0;
            }
        }

        static bool IsRodSelectedForPlayer(int playerNum)
        {
            try
            {
                var playerInventory = PlayerInventory.GetPlayer(playerNum);
                if (playerInventory == null)
                    return false;

                var actionBar = Traverse.Create(playerInventory).Field("actionBarInventory")?.GetValue() as ActionBarInventory;
                if (actionBar == null)
                    return false;

                var selectedItemObj = actionBar.GetSelectedItem();
                var selectedInstanceObj = actionBar.GetSelectedItemInstance();
                var selectedType = selectedItemObj?.GetType().Name ?? string.Empty;
                var instanceType = selectedInstanceObj?.GetType().Name ?? string.Empty;
                if (selectedType.Contains("Rod") || instanceType.Contains("Rod"))
                    return true;

                object instanceItem = null;
                try
                {
                    instanceItem = Traverse.Create(selectedInstanceObj).Method("LHBPOPOIFLE")?.GetValue();
                }
                catch
                {
                }

                if (instanceItem == null)
                {
                    try
                    {
                        instanceItem = Traverse.Create(selectedInstanceObj).Method("AFOACBIHNCL")?.GetValue();
                    }
                    catch
                    {
                    }
                }

                var instanceItemType = instanceItem?.GetType().Name ?? string.Empty;
                return instanceItemType.Contains("Rod");
            }
            catch
            {
                return false;
            }
        }

        static int CountPlayerItem(int playerNum, int itemId)
        {
            if (itemId == 0)
                return 0;

            PlayerInventory playerInventory = null;
            try
            {
                playerInventory = PlayerInventory.GetPlayer(playerNum);
            }
            catch
            {
                return 0;
            }

            if (playerInventory == null)
                return 0;

            int total = 0;
            try
            {
                var allSlotsObj = Traverse.Create(playerInventory).Method("GetAllSlots")?.GetValue();
                if (allSlotsObj is IEnumerable<Slot> directSlots)
                {
                    foreach (var slot in directSlots)
                    {
                        if (slot == null || !TryGetSlotItemId(slot, out var slotItemId) || slotItemId != itemId)
                            continue;

                        total += Mathf.Max(0, GetSlotAmount(slot));
                    }

                    return total;
                }
            }
            catch
            {
            }

            foreach (var fieldName in new[] { "actionBarInventory", "inventory" })
            {
                try
                {
                    var container = Traverse.Create(playerInventory).Field(fieldName)?.GetValue();
                    var slotsObj = Traverse.Create(container).Field("slots")?.GetValue();
                    if (slotsObj is IEnumerable<Slot> slots)
                    {
                        foreach (var slot in slots)
                        {
                            if (slot == null || !TryGetSlotItemId(slot, out var slotItemId) || slotItemId != itemId)
                                continue;

                            total += Mathf.Max(0, GetSlotAmount(slot));
                        }
                    }
                }
                catch
                {
                }
            }

            return total;
        }

        static void AddBait(int playerNum, int itemId, int amount)
        {
            if (amount <= 0 || itemId == 0)
                return;

            PlayerInventory inventory = null;
            try
            {
                inventory = PlayerInventory.GetPlayer(playerNum);
            }
            catch
            {
                return;
            }

            if (inventory == null)
                return;

            for (var i = 0; i < amount; i++)
            {
                try
                {
                    var dbItem = ItemDatabaseAccessor.GetItem(itemId, false, true);
                    if (dbItem == null)
                        return;
                    var instance = new ItemInstance(dbItem);
                    inventory.AddItem(instance, false, true, false, true);
                }
                catch
                {
                    return;
                }
            }
        }

        static int GetSlotAmount(Slot slot)
        {
            if (slot == null)
                return 0;

            foreach (var fieldName in new[] { "amount", "stack", "quantity", "count" })
            {
                try
                {
                    var value = Traverse.Create(slot).Field(fieldName)?.GetValue();
                    if (value is int i)
                        return i;
                }
                catch
                {
                }
            }

            foreach (var propertyName in new[] { "amount", "stack", "quantity", "count" })
            {
                try
                {
                    var value = Traverse.Create(slot).Property(propertyName)?.GetValue();
                    if (value is int i)
                        return i;
                }
                catch
                {
                }
            }

            return 1;
        }

        static bool IsFishingContext(int playerNum)
        {
            try
            {
                var controller = FishingController.Get(playerNum);
                if (controller != null)
                {
                    if (controller.fishing)
                        return true;

                    if (IsControllerFishingCameraActive(controller))
                        return true;

                    var hook = Traverse.Create(controller).Field("fishingHook")?.GetValue() as FishingHook;
                    if (hook != null)
                    {
                        if (hook.gameObject != null && hook.gameObject.activeInHierarchy)
                            return true;
                        if (hook.enabled && hook.gameObject != null && hook.gameObject.activeInHierarchy)
                            return true;
                    }
                }
            }
            catch
            {
            }

            try
            {
                var ui = FishingUI.Get(playerNum);
                if (ui?.content != null && ui.content.activeInHierarchy)
                    return true;
            }
            catch
            {
            }

            return false;
        }

        static bool TryGetSlotItemId(Slot slot, out int itemId)
        {
            itemId = 0;
            if (slot == null)
                return false;

            object itemObj = null;
            try
            {
                itemObj = slot.itemInstance?.LHBPOPOIFLE();
            }
            catch
            {
            }

            if (itemObj == null)
            {
                try
                {
                    itemObj = slot.itemInstance?.AFOACBIHNCL();
                }
                catch
                {
                }
            }

            return itemObj is Item item && TryGetItemId(item, out itemId);
        }

        static bool TryResolvePlayerForSlot(Slot slot, out int playerNum)
        {
            playerNum = -1;
            if (slot == null)
                return false;

            for (var i = MinSafePlayerNum; i <= MaxSafePlayerNum; i++)
            {
                PlayerInventory inventory = null;
                try
                {
                    inventory = PlayerInventory.GetPlayer(i);
                }
                catch
                {
                    continue;
                }

                if (inventory == null)
                    continue;

                try
                {
                    var allSlotsObj = Traverse.Create(inventory).Method("GetAllSlots")?.GetValue();
                    if (allSlotsObj is IEnumerable<Slot> allSlots)
                    {
                        foreach (var s in allSlots)
                        {
                            if (ReferenceEquals(s, slot))
                            {
                                playerNum = i;
                                return true;
                            }
                        }
                    }
                }
                catch
                {
                }

                foreach (var fieldName in new[] { "actionBarInventory", "inventory" })
                {
                    try
                    {
                        var container = Traverse.Create(inventory).Field(fieldName)?.GetValue();
                        var slotsObj = Traverse.Create(container).Field("slots")?.GetValue();
                        if (slotsObj is IEnumerable<Slot> slots)
                        {
                            foreach (var s in slots)
                            {
                                if (ReferenceEquals(s, slot))
                                {
                                    playerNum = i;
                                    return true;
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return false;
        }

        //////////////////////////////////////////////////////////////////
        ///  Instant Catch
        static bool LateUpdatePrefix(FishingUI __instance)
        {
            if (!_InstantCatch.Value) return true;
            if (!__instance.content.activeInHierarchy) return true;

            if (!_loggedFishingUiActive)
            {
                _loggedFishingUiActive = true;
                DebugLog("LateUpdatePrefix: FishingUI content is active; applying instant catch");
            }

            // Set progress slider value to 1.0 for instant completion.
            ForceFishingProgressComplete(__instance, "LateUpdatePrefix");

            return true;
        }

    }
}
