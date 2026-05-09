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
using UnityEngine;
using UnityEngine.UI;


namespace NepEasyFishing
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
        private static bool _loggedFishingUiActive;
        private static bool _loggedPollingFallbackActive;
        private static bool _loggedEasyMinigameFallbackActive;
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
        private static readonly HashSet<int> _baitItemIds = new HashSet<int> { 1444, 1445, 1446, 1447, 1448 };
        private static readonly Dictionary<int, string> _baitNamesByItemId = new Dictionary<int, string>
        {
            { 1444, "Worm" },
            { 1445, "Larva" },
            { 1446, "Meat Bait" },
            { 1447, "Seafood Bait" },
            { 1448, "Lure" }
        };

        private const string BuildProofStamp = "20260510-003000";

        private static readonly FieldInfo FishingControllerSettings =
            AccessTools.Field(typeof(FishingController), "settings");

        private static readonly FieldInfo FishingUISettings = AccessTools.Field(typeof(FishingUI), "settings");
        private static readonly FieldInfo FishingUIFishIcon = AccessTools.Field(typeof(FishingUI), "fishIcon");
        private static readonly FieldInfo FishingUIBox = AccessTools.Field(typeof(FishingUI), "box");
        private static readonly FieldInfo FishingUIHitThreshold = AccessTools.Field(typeof(FishingUI), "DNNFOPAGBPD");

        private static FieldInfo DifficultySettingsField;

        public Plugin()
        {
            // bind to config settings
            _FishBarQuickProgress = Config.Bind("General", "Quick Progress", true,
                "Fishing minigame progress fills very quickly when fish is clicked on");
            _FishBarQuickProgressAmount = Config.Bind("General", "Quick Progress Amount", 0.15f,
                "Amount added to the fishing minigame progress bar per second while Quick Progress is enabled");
            _FishBarQuickProgressOnMiss = Config.Bind("General", "Quick Progress On Miss", false,
                "If true, Quick Progress still increases while holding input even when the fish is outside the bar/box");
            _FishBarNoDecrease = Config.Bind("General", "No Bar Decrease", true,
                "Fishing minigame progress does not decrease");
            _FishBarQuickBites =
                Config.Bind("General", "Quick Bites", true, "Reduced time before bites, no fake bites");
            _debugLogging = Config.Bind("Debug", "Debug Logging", false, "Logs additional information to console");
            _InstantCatch = Config.Bind("General", "Instant Catch", true,
                "Instantly catch fish once hooked instead of starting the minigame");
            _dontUseBait = Config.Bind("General", "Dont use bait", false, "Don't consume bait when fishing");
        }


        private void Awake()
        {
            // Plugin startup logic
            Log = Logger;
            Logger.LogInfo($"NepEasyFishing: Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            LogBuildProof("Awake");
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
            var containerRemoveItemMethod = ResolveMethod(typeof(Container), "RemoveItem", new[] { typeof(Item), typeof(bool) });
            var slotConsumeOneMethod = ResolveMethod(typeof(Slot), "MEODNPFJDMH", new[] { typeof(bool) });
            var slotConsumeOneAliasMethod = ResolveMethod(typeof(Slot), "MBCIJPPOGJG", new[] { typeof(bool) });
            var slotSetStackMethod = ResolveMethod(typeof(Slot), "BGJPNGLONLP", new[] { typeof(int), typeof(bool), typeof(bool) });

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
            DebugLog($"Target resolution: Container.RemoveItem(Item, bool) => {(containerRemoveItemMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: Slot.MEODNPFJDMH(bool) => {(slotConsumeOneMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: Slot.MBCIJPPOGJG(bool) => {(slotConsumeOneAliasMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: Slot.BGJPNGLONLP(int,bool,bool) => {(slotSetStackMethod != null ? "FOUND" : "MISSING")}");

            try
            {
                var startFishingGamePostfix = AccessTools.Method(typeof(Plugin), nameof(StartFishingGamePostfix), new[] { typeof(FishingUI) });
                var startFishingCoroutinePrefix = AccessTools.Method(typeof(Plugin), nameof(StartFishingAnyPrefix), new[] { typeof(FishingController), typeof(MethodBase) });
                var createBitesListPostfix = AccessTools.Method(typeof(Plugin), nameof(CreateBitesListAnyPostfix), new[] { typeof(FishingController), typeof(MethodBase) });
                var finishFishingPrefix = AccessTools.Method(typeof(Plugin), nameof(FinishFishingPrefix), new[] { typeof(FishingController) });
                var playerInventoryRemoveItemItemPrefix = AccessTools.Method(typeof(Plugin), nameof(PlayerInventoryRemoveItemItemPrefix), new[] { typeof(PlayerInventory), typeof(Item), typeof(Slot).MakeByRefType(), typeof(MethodBase) });
                var playerInventoryRemoveItemItemBoolPrefix = AccessTools.Method(typeof(Plugin), nameof(PlayerInventoryRemoveItemItemBoolPrefix), new[] { typeof(PlayerInventory), typeof(Item), typeof(bool), typeof(Slot).MakeByRefType(), typeof(MethodBase) });
                var containerRemoveItemPrefix = AccessTools.Method(typeof(Plugin), nameof(ContainerRemoveItemPrefix), new[] { typeof(Container), typeof(Item), typeof(bool), typeof(Slot).MakeByRefType(), typeof(MethodBase) });
                var slotConsumeOnePrefix = AccessTools.Method(typeof(Plugin), nameof(SlotConsumeOnePrefix), new[] { typeof(Slot), typeof(bool), typeof(bool).MakeByRefType(), typeof(MethodBase) });
                var slotSetStackPrefix = AccessTools.Method(typeof(Plugin), nameof(SlotSetStackPrefix), new[] { typeof(Slot), typeof(int).MakeByRefType(), typeof(bool), typeof(bool), typeof(MethodBase) });
                var lateUpdatePrefix = AccessTools.Method(typeof(Plugin), nameof(LateUpdatePrefix), new[] { typeof(FishingUI) });
                var fishingHookSetFakePrefix = AccessTools.Method(typeof(Plugin), nameof(FishingHookSetFakePrefix), new[] { typeof(FishingHook) });
                var fishingHookSetBaitPostfix = AccessTools.Method(typeof(Plugin), nameof(FishingHookSetBaitPostfix), new[] { typeof(MethodBase) });

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
                PatchWithLogging("Container.RemoveItem(Item, bool)", containerRemoveItemMethod, containerRemoveItemPrefix, isPrefix: true);
                PatchWithLogging("Slot.MEODNPFJDMH(bool)", slotConsumeOneMethod, slotConsumeOnePrefix, isPrefix: true);
                PatchWithLogging("Slot.MBCIJPPOGJG(bool)", slotConsumeOneAliasMethod, slotConsumeOnePrefix, isPrefix: true);
                PatchWithLogging("Slot.BGJPNGLONLP(int,bool,bool)", slotSetStackMethod, slotSetStackPrefix, isPrefix: true);
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
                LogPatchInfo("Container.RemoveItem(Item, bool)", containerRemoveItemMethod);
                LogPatchInfo("Slot.MEODNPFJDMH(bool)", slotConsumeOneMethod);
                LogPatchInfo("Slot.MBCIJPPOGJG(bool)", slotConsumeOneAliasMethod);
                LogPatchInfo("Slot.BGJPNGLONLP(int,bool,bool)", slotSetStackMethod);
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

            if (_dontUseBait?.Value == true)
            {
                PollDontUseBaitGlobalMonitor();
            }

            if (_debugLogging?.Value == true && Input.GetKeyDown(KeyCode.F8))
            {
                DumpFishingState("F8");
            }
        }

        private static void InstallTickerFallback()
        {
            try
            {
                var tickerObject = new GameObject("NepEasyFishing.UpdateTicker");
                UnityEngine.Object.DontDestroyOnLoad(tickerObject);
                tickerObject.hideFlags = HideFlags.HideAndDontSave;
                tickerObject.AddComponent<EasyFishingTicker>();
                Log.LogInfo($"EASYFISHING_TICKER_INSTALLED stamp={BuildProofStamp}");
            }
            catch (Exception ex)
            {
                Log.LogError($"EASYFISHING_TICKER_INSTALL_FAILED stamp={BuildProofStamp}: {ex}");
            }
        }

        private class EasyFishingTicker : MonoBehaviour
        {
            private bool _loggedTickerUpdateProof;

            private void Update()
            {
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

                if (_dontUseBait?.Value == true)
                {
                    PollDontUseBaitGlobalMonitor();
                }

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

            for (int playerNum = 0; playerNum <= 4; playerNum++)
            {
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
                    LogThrottledDiagnostic($"NepEasyFishing: easy minigame fallback player {playerNum}: FishingUI progress slider is null");
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
                LogThrottledDiagnostic($"NepEasyFishing: easy minigame fallback player {playerNum} failed: {ex.GetType().Name}: {ex.Message}");
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
                LogThrottledDiagnostic($"NepEasyFishing: failed to read fish-in-box state: {ex.GetType().Name}: {ex.Message}");
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
                    LogThrottledDiagnostic($"NepEasyFishing: {source}: FishingUI progress slider is null");
                    return;
                }

                reflectedSlider.value = 1.0f;
            }
            catch (Exception ex)
            {
                LogThrottledDiagnostic($"NepEasyFishing: {source}: failed to force FishingUI progress: {ex.GetType().Name}: {ex.Message}");
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
            Log.LogInfo($"NepEasyFishing: Debug dump requested ({reason})");

            for (int playerNum = 0; playerNum <= 4; playerNum++)
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

                    Log.LogInfo($"NepEasyFishing: Debug dump player={playerNum}, fishingUiActive={uiActive}, progress={progress}, controllerFishing={fishing}, baitSelected={baitSelected}");
                }
                catch (Exception ex)
                {
                    DebugLog($"Debug dump player={playerNum} unavailable: {ex.GetType().Name}: {ex.Message}");
                }
            }
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
                    $"DebugLogging={_debugLogging?.Value}");
            }
            catch (Exception ex)
            {
                Log.LogError($"EASYFISHING_CONFIG_PROOF_FAILED: {ex}");
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
            if (_debugLogging.Value) Log.LogInfo($"NepEasyFishing: Debug: {message}");
        }

        //////////////////////////////////////////////////////////////////
        //  Fast Progress, No progress loss

        static void StartFishingGamePostfix(FishingUI __instance)
        {
            DebugLog("StartFishingGamePostfix");

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


        //////////////////////////////////////////////////////////////////
        ///  quicker Bites
        static bool StartFishingAnyPrefix(FishingController __instance, MethodBase __originalMethod)
        {
            DebugLog($"StartFishingAnyPrefix source={__originalMethod?.Name}");
            MarkBaitProtection(__instance?.playerNum ?? -1, 8f);
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

            if (controller.bitesList == null)
                controller.bitesList = new List<float>();

            controller.bitesList.Clear();
            controller.bitesList.Add(Time.time + delay);
            DebugLog($"QuickBites normalized bitesList from {source}");
        }

        static void PollQuickBitesFallback()
        {
            for (int playerNum = 0; playerNum <= 4; playerNum++)
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

                if (controller?.bitesList == null || controller.bitesList.Count == 0)
                    continue;

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

        static void FishingHookSetBaitPostfix(MethodBase __originalMethod)
        {
            if (_FishBarQuickBites?.Value != true)
                return;

            DebugLog("QuickBites observed SetBait");
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

            for (int playerNum = 0; playerNum <= 4; playerNum++)
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


        //////////////////////////////////////////////////////////////////
        ///  Don't use bait
        ///  
        static void FinishFishingPrefix(FishingController __instance)
        {
            DebugLog("FinishFishingPrefix");
            MarkBaitProtection(__instance?.playerNum ?? -1, 2f);
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

            for (var i = 0; i <= 4; i++)
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

        static bool TryGetPlayerNumFromInventoryOrContainer(object inventoryOrContainer, out int playerNum)
        {
            playerNum = -1;
            if (inventoryOrContainer == null)
                return false;

            if (inventoryOrContainer is PlayerInventory playerInventory)
                return TryGetPlayerNumFromInventory(playerInventory, out playerNum);

            if (TryGetPlayerNumFromObjectField(inventoryOrContainer, out playerNum))
                return true;

            for (var i = 0; i <= 4; i++)
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
            for (var playerNum = 0; playerNum <= 4; playerNum++)
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

            for (var playerNum = 0; playerNum <= 4; playerNum++)
            {
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

                    try
                    {
                        var fishingCameraObj = Traverse.Create(controller).Field("fishingCamera")?.GetValue();
                        if (fishingCameraObj is Camera fishingCamera && fishingCamera != null)
                            return true;
                        if (fishingCameraObj is GameObject cameraGo && cameraGo.activeInHierarchy)
                            return true;
                    }
                    catch
                    {
                    }

                    var hook = Traverse.Create(controller).Field("fishingHook")?.GetValue() as FishingHook;
                    if (hook != null)
                    {
                        if (hook.gameObject != null && hook.gameObject.activeInHierarchy)
                            return true;
                        if (hook.enabled)
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

            for (var i = 0; i <= 4; i++)
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

        //////////////////////////////////////////////////////////////////
        ///  Discovery diagnostics
        static void RodActionPrefix(int __0, bool __1, MethodBase __originalMethod)
        {
            if (_dontUseBait?.Value == true && __1)
            {
                MarkBaitProtection(__0, 12f);
                MarkBaitProtectionForAllPlayers(2f);
                DebugLog($"EASYFISHING_BAIT_PROTECTION_MARKED source=Rod.{__originalMethod?.Name} playerNum={__0} canAct={__1}");
            }
        }

        static void ActionBarAnyActionPrefix(ActionBarInventory __instance, int __0, bool __1, MethodBase __originalMethod)
        {
            if (_dontUseBait?.Value != true)
                return;

            if (!__1)
                return;

            bool isRod = false;
            try
            {
                var selectedItemObj = __instance?.GetSelectedItem();
                var selectedInstanceObj = __instance?.GetSelectedItemInstance();
                var selectedType = selectedItemObj?.GetType().Name ?? "";
                var instanceType = selectedInstanceObj?.GetType().Name ?? "";
                isRod = selectedType.Contains("Rod") || instanceType.Contains("Rod");
            }
            catch
            {
            }

            if (!isRod)
                return;

            MarkBaitProtection(__0, 12f);
            DebugLog($"EASYFISHING_BAIT_PROTECTION_MARKED source=ActionBarInventory.{__originalMethod?.Name} playerNum={__0} canAct={__1}");
        }

        static void RodActionPostfix(int __0, bool __1, bool __result, MethodBase __originalMethod)
        {
            var controller = FishingController.Get(__0);
            var fishing = SafeMemberReadAsString(controller, "fishing");
            var baitSelected = SafeMemberReadAsString(controller, "baitSelected");
            DebugLog($"Diag Rod.{__originalMethod?.Name}: playerNum={__0}, canAct={__1}, result={__result}, fishing={fishing}, baitSelected={baitSelected}");
        }

        static void RodNbfbPostfix(int __0, bool __result)
        {
            DebugLog($"Diag Rod.NBFBPMNMBJG: playerNum={__0}, result={__result}");
        }

        static void RodOfakPostfix(int __0, bool __result)
        {
            DebugLog($"Diag Rod.OFAKNHNLKGI: playerNum={__0}, result={__result}");
        }

        static void RodAnimationStagePrefix(int __0, MethodBase __originalMethod)
        {
            DebugLog($"Diag Rod.{__originalMethod?.Name}: playerNum={__0}");
        }

        static void CharacterAnimatorToolStagePrefix(MethodBase __originalMethod)
        {
            DebugLog($"Diag CharacterAnimator.{__originalMethod?.Name}");
        }

        static void FishingUiOpenClosePrefix(MethodBase __originalMethod, object[] __args)
        {
            string playerNum = "n/a";
            if (__args != null && __args.Length > 0 && __args[0] is int p)
                playerNum = p.ToString();

            DebugLog($"Diag FishingUI.{__originalMethod?.Name}: playerNum={playerNum}");
        }

        static void SelectAFishPostfix(int __0, Fish __result)
        {
            var isNull = __result == null;
            var fishInfo = DescribeFishResult(__result);
            DebugLog($"Diag FishingManager.SelectAFish: playerNum={__0}, isNull={isNull}, fish={fishInfo}");
        }

        static void UseSelectedItemPostfix(UseObject __instance, bool __0, bool __1, int __2, bool __result)
        {
            var playerNum = SafeMemberReadAsString(__instance, "JIIGOACEIKL");
            DebugLog($"Diag UseObject.UseSelectedItem: playerNum={playerNum}, canAct={__0}, allowSelectedAction={__1}, actionIndex={__2}, result={__result}");
        }

        static void ActionSelectedItemPostfix(ActionBarInventory __instance, int __0, bool __1, bool __2, bool __3, bool __4, int __5, bool __result)
        {
            string selectedState;
            try
            {
                var selectedItemObj = __instance.GetSelectedItem();
                var selectedItemType = selectedItemObj?.GetType().Name ?? "null";
                var selectedInstanceObj = __instance.GetSelectedItemInstance();
                var selectedInstanceType = selectedInstanceObj?.GetType().Name ?? "null";
                var currentSlot = __instance.GetCurrentSlotSelected().ToString();
                var selectedItemActionable = selectedItemObj is IActionable;
                var selectedInstanceActionable = selectedInstanceObj is IActionable;
                selectedState = $"selectedItemType={selectedItemType}, selectedInstanceType={selectedInstanceType}, currentSlot={currentSlot}, selectedItemActionable={selectedItemActionable}, selectedInstanceActionable={selectedInstanceActionable}";
            }
            catch
            {
                selectedState = "selectedItemType=n/a, selectedInstanceType=n/a, currentSlot=n/a, selectedItemActionable=n/a, selectedInstanceActionable=n/a";
            }

            DebugLog($"Diag ActionBarInventory.ActionSelectedItem: playerNum={__0}, canAct={__1}, allowSelectedAction={__2}, objectClick={__3}, skipActionable={__4}, actionIndex={__5}, result={__result}, {selectedState}");
        }

        static void ActionBarMbohPostfix(ActionBarInventory __instance, object __result)
        {
            var actionableType = __result?.GetType().Name ?? "null";
            string selectedState;
            try
            {
                var selectedItemObj = __instance.GetSelectedItem();
                var selectedItemType = selectedItemObj?.GetType().Name ?? "null";
                var selectedInstanceObj = __instance.GetSelectedItemInstance();
                var selectedInstanceType = selectedInstanceObj?.GetType().Name ?? "null";
                var selectedItemActionable = selectedItemObj is IActionable;
                var selectedInstanceActionable = selectedInstanceObj is IActionable;
                selectedState = $"selectedItemType={selectedItemType}, selectedInstanceType={selectedInstanceType}, selectedItemActionable={selectedItemActionable}, selectedInstanceActionable={selectedInstanceActionable}";
            }
            catch
            {
                selectedState = "selectedItemType=n/a, selectedInstanceType=n/a, selectedItemActionable=n/a, selectedInstanceActionable=n/a";
            }

            DebugLog($"Diag ActionBarInventory.MBOHNGNNCED: actionableType={actionableType}, {selectedState}");
        }
    }
}
