using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;

using HarmonyLib;

using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Base;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.JsonSystem.Converters;
using Kingmaker.Blueprints.JsonSystem.EditorDatabase.ResourceReplacementProvider;
using Kingmaker.BundlesLoading;
using Kingmaker.Modding;
using Kingmaker.Utility.EditorPreferences;
using Kingmaker.Utility.UnityExtensions;

using MicroPatches.Editor;

using MicroUtils.Transpiler;

using Newtonsoft.Json;

using Owlcat.Runtime.Core.Utility.Locator;

using UnityEditor;

using UnityEngine;

public static class GameServices
{
    static string GamePath => EditorPreferences.Instance.ModsGameBuildPath;
    static string AppDataPath => Path.Combine(GamePath, "WH40KRT_Data");
    static string GetBundlesFolder() => "Bundles";
    static string BundlesPath(string filename) => Path.Combine(AppDataPath, "..", "Bundles", filename);

    [HarmonyPatch]
    static class Patches
    {
        [HarmonyPatch(typeof(BundlesLoadService), nameof(BundlesLoadService.BundlesPath))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> BundlesPath_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            //Debug.Log($"Patching {nameof(BundlesLoadService)}.{nameof(BundlesLoadService.BundlesPath)}");

            var match = instructions.FindInstructionsIndexed(new Func<CodeInstruction, bool>[]
            {
                ci => ci.Calls(AccessTools.PropertyGetter(typeof(Application), nameof(Application.dataPath))),
                ci => ci.opcode == OpCodes.Ldstr,
                ci => ci.Calls(AccessTools.Method(typeof(AssetBundleNames), nameof(AssetBundleNames.GetBundlesFolder))),
                ci => ci.opcode == OpCodes.Ldarg_0,
                ci => ci.Calls(AccessTools.Method(typeof(Path), nameof(Path.Combine),
                    new [] { typeof(string), typeof(string), typeof(string), typeof(string)}))
            }).ToArray();

            if (match.Length != 5)
                throw new Exception("Could not find target instructions");

            match[0].instruction.operand = AccessTools.PropertyGetter(typeof(GameServices), nameof(GameServices.AppDataPath));
            match[2].instruction.operand = AccessTools.Method(typeof(GameServices), nameof(GameServices.GetBundlesFolder));

            return instructions;
        }

        [HarmonyPatch(typeof(BundlesUsageProvider), nameof(BundlesUsageProvider.UseBundles), MethodType.Getter)]
        [HarmonyPostfix]
        static bool BundlesUsageProvider_UseBundles_Postfix(bool _) => EditorPreferences.Instance.LoadAssetsFromBundles;

        [HarmonyPatch(typeof(UnityObjectConverter), nameof(UnityObjectConverter.WriteJson))]
        [HarmonyPrefix]
        static bool UnityObjectConverter_WriteJson_Prefix(JsonWriter writer, object value)
        {
            var @object = value as UnityEngine.Object;

            if (@object == null || UnityObjectConverter.AssetList == null)
                return true;

            if (UnityObjectConverter.AssetList.GetAssetId(@object) is not (string guid, long fileid))
                return true;

            writer.WriteStartObject();
            writer.WritePropertyName("guid");
            writer.WriteValue(guid);
            writer.WritePropertyName("fileid");
            writer.WriteValue(fileid);
            writer.WriteEndObject();

            return false;
        }
    }

    public static LocationList LocationList =>
        BundlesLoadService.Instance is { } bls ?
        AccessTools.Field(typeof(BundlesLoadService), "m_LocationList").GetValue(bls) as LocationList :
        null;

    class ResourceReplacementProviderStub : IResourceReplacementProvider
    {
        public string GetBundleNameForAsset(string guid) => null;
        public DependencyData GetDependenciesForBundle(string bundleName) => null;
        public object OnResourceLoaded(object resource, string guid) => null;
        public AssetBundle TryLoadBundle(string bundleName) => null;
    }
    
    public static bool Starting { get; private set; } = false;

    public static bool Started { get; private set; } = false;

    [MenuItem("Game services/Start BundlesLoadService")]
    public static void StartBundlesLoadService()
    {
        if (loadCommonBundles != null)
        {
            Reset();
        }

        if (Starting || Started)
            return;

        Starting = true;

        Debug.Log($"Bundles path: {BundlesPath("")}");

        if (!EditorPreferences.Instance.LoadAssetsFromBundles || !EditorPreferences.Instance.LoadBlueprintsAsInBuild)
        {
            Debug.LogWarning("Setting editor preferences to load from bundles");
            EditorPreferences.Instance.LoadAssetsFromBundles = true;
            EditorPreferences.Instance.LoadBlueprintsAsInBuild = true;
            EditorPreferences.Instance.Save();
        }

        if (!ResourcesLibrary.UseBundles)
        {
            Debug.LogError("UseBundles is false");
            return;
        }

        Debug.Log("Starting default services");

        Services.RegisterDefaultServices();

        //Debug.Log("Starting OwlcatModificationsManager service");

        Debug.Log("Starting BundlesLoadService");

        if (Services.GetInstance<BundlesLoadService>() == null)
        {
            Services.RegisterServiceInstance<BundlesLoadService>(new BundlesLoadService(new ResourceReplacementProviderStub()));
        }

        BundlesLoadService.Instance.ReadListsForEditor();

        var ll = LocationList;

        if (ll == null)
        {
            Debug.LogError("locationlist is null");

            Debug.Log($"Path: {BundlesLoadService.BundlesPath("locationlist.json")}");
        }

        if (progressId == 0)
        {
            try
            {
                loadCommonBundles = EditorCoroutine.Start(LoadCommonBundlesCoroutine());
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    static EditorCoroutine loadCommonBundles;

    static int progressId = 0;

    static IEnumerator LoadCommonBundlesCoroutine()
    {
        if (progressId == 0)
        {
            progressId = Progress.Start("Loading common bundles");
        }
        try
        {
            IEnumerator coroutine;
            try
            {
                coroutine = BundlesLoadService.Instance.RequestCommonBundlesCoroutine();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                yield break;
            }

            yield return null;

            var running = true;

            while(running)
            {
                try
                {
                    running = coroutine.MoveNext();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    yield break;
                }

                yield return null;
            }
    
            Debug.Log("Loaded common bundles");
        }
        finally
        {
            Progress.Finish(progressId);
            progressId = 0;
        }

        Debug.Log("Load Direct References List");

        StartGameLoader.LoadDirectReferencesList();

        Started = true;
        Starting = false;
    }

    [MenuItem("Game services/Reset")]
    public static void Reset()
    {
        if (loadCommonBundles != null)
        {
            loadCommonBundles.Stop();
            loadCommonBundles = null;
        }

        if (progressId > 0)
        {
            Progress.Finish(progressId);
            progressId = 0;
        }

        Started = false;
        Starting = false;

        Services.ResetAllRegistrations();
        AssetBundle.UnloadAllAssetBundles(true);
    }
}
