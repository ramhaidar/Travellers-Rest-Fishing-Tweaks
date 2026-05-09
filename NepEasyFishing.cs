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

        private const string BuildProofStamp = "20260509-224500";

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
            var lateUpdateMethod = AccessTools.Method(typeof(FishingUI), "LateUpdate", Type.EmptyTypes);
            var fishingHookSetFakeMethod = ResolveMethod(typeof(FishingHook), "SetFake", Type.EmptyTypes);
            var fishingHookSetBaitMethod = ResolveMethod(typeof(FishingHook), "SetBait", Type.EmptyTypes);

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
            DebugLog($"Target resolution: FishingUI.LateUpdate() => {(lateUpdateMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingHook.SetFake() => {(fishingHookSetFakeMethod != null ? "FOUND" : "MISSING")}");
            DebugLog($"Target resolution: FishingHook.SetBait() => {(fishingHookSetBaitMethod != null ? "FOUND" : "MISSING")}");

            try
            {
                var startFishingGamePostfix = AccessTools.Method(typeof(Plugin), nameof(StartFishingGamePostfix), new[] { typeof(FishingUI) });
                var startFishingCoroutinePrefix = AccessTools.Method(typeof(Plugin), nameof(StartFishingAnyPrefix), new[] { typeof(FishingController), typeof(MethodBase) });
                var createBitesListPostfix = AccessTools.Method(typeof(Plugin), nameof(CreateBitesListAnyPostfix), new[] { typeof(FishingController), typeof(MethodBase) });
                var finishFishingPrefix = AccessTools.Method(typeof(Plugin), nameof(FinishFishingPrefix), new[] { typeof(FishingController) });
                var lateUpdatePrefix = AccessTools.Method(typeof(Plugin), nameof(LateUpdatePrefix), new[] { typeof(FishingUI) });
                var fishingHookSetFakePrefix = AccessTools.Method(typeof(Plugin), nameof(FishingHookSetFakePrefix), new[] { typeof(FishingHook) });
                var fishingHookSetBaitPostfix = AccessTools.Method(typeof(Plugin), nameof(FishingHookSetBaitPostfix), new[] { typeof(MethodBase) });

                var rodActionMethod = ResolveMethod(typeof(Rod), "Action", new[] { typeof(int), typeof(bool) });
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

                var rodActionPostfix = AccessTools.Method(typeof(Plugin), nameof(RodActionPostfix), new[] { typeof(int), typeof(bool), typeof(bool) });
                var rodNbfbPostfix = AccessTools.Method(typeof(Plugin), nameof(RodNbfbPostfix), new[] { typeof(int), typeof(bool) });
                var rodOfakPostfix = AccessTools.Method(typeof(Plugin), nameof(RodOfakPostfix), new[] { typeof(int), typeof(bool) });
                var rodAnimStagePrefix = AccessTools.Method(typeof(Plugin), nameof(RodAnimationStagePrefix), new[] { typeof(int), typeof(MethodBase) });
                var animatorToolStagePrefix = AccessTools.Method(typeof(Plugin), nameof(CharacterAnimatorToolStagePrefix), new[] { typeof(MethodBase) });
                var fishingUiOpenClosePrefix = AccessTools.Method(typeof(Plugin), nameof(FishingUiOpenClosePrefix), new[] { typeof(MethodBase), typeof(object[]) });
                var selectFishPostfix = AccessTools.Method(typeof(Plugin), nameof(SelectAFishPostfix), new[] { typeof(int), typeof(Fish) });
                var useSelectedItemPostfix = AccessTools.Method(typeof(Plugin), nameof(UseSelectedItemPostfix), new[] { typeof(UseObject), typeof(bool), typeof(bool), typeof(int), typeof(bool) });
                var actionSelectedItemPostfix = AccessTools.Method(typeof(Plugin), nameof(ActionSelectedItemPostfix), new[] { typeof(ActionBarInventory), typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(int), typeof(bool) });
                var actionBarMbohPostfix = AccessTools.Method(typeof(Plugin), nameof(ActionBarMbohPostfix), new[] { typeof(ActionBarInventory), typeof(object) });

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
                PatchWithLogging("FishingUI.LateUpdate()", lateUpdateMethod, lateUpdatePrefix, isPrefix: true);
                PatchWithLogging("FishingHook.SetFake()", fishingHookSetFakeMethod, fishingHookSetFakePrefix, isPrefix: true);
                PatchWithLogging("FishingHook.SetBait()", fishingHookSetBaitMethod, fishingHookSetBaitPostfix, isPrefix: false);
                PatchWithLogging("Rod.Action(int, bool)", rodActionMethod, rodActionPostfix, isPrefix: false);
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
                LogPatchInfo("FishingUI.LateUpdate()", lateUpdateMethod);
                LogPatchInfo("FishingHook.SetFake()", fishingHookSetFakeMethod);
                LogPatchInfo("FishingHook.SetBait()", fishingHookSetBaitMethod);
                LogPatchInfo("Rod.Action(int, bool)", rodActionMethod);
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

            return AccessTools.Method(type, methodName);
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

            if (_dontUseBait.Value)
            {
                //We're going to add an extra piece of the selected bait to the players inventory right before the code that (among other things) removes the bait.

                // If we can get the numeric id of the bait Item we can make an ItemInstance of it with "new ItemInstance" 
                //__instance.baitSelected is an enum 
                // FishingManager.BaitItem() returns an Item when given the bait enum
                Item baitItem = FishingManager.BaitItem(__instance.baitSelected);
                if (baitItem == null)
                {
                    DebugLog("FinishFishing Prefix: no bait selected");
                    return;
                }

                //Then use reflection to get the id from the private field
                int reflectedItemID = 0;
                reflectedItemID = Traverse.Create(baitItem).Field("id").GetValue<int>();
                if (reflectedItemID != 0)
                {
                    DebugLog($"FinishFishing Prefix: bait itemID {reflectedItemID}");
                    ItemInstance baitItemInstance =
                        new ItemInstance(ItemDatabaseAccessor.GetItem(reflectedItemID, false, true));
                    PlayerInventory.GetPlayer(__instance.playerNum)
                        .AddItem(baitItemInstance); //AddItem wants an item instance, not an item
                }
                else
                    DebugLog("FinishFishing Prefix: failed to get itemId for bait");
            }
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
        static void RodActionPostfix(int __0, bool __1, bool __result)
        {
            var controller = FishingController.Get(__0);
            var fishing = SafeMemberReadAsString(controller, "fishing");
            var baitSelected = SafeMemberReadAsString(controller, "baitSelected");
            DebugLog($"Diag Rod.Action: playerNum={__0}, canAct={__1}, result={__result}, fishing={fishing}, baitSelected={baitSelected}");
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
