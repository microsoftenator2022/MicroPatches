using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;

using HarmonyLib;

using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.JsonSystem.EditorDatabase.ResourceReplacementProvider;
using Kingmaker.BundlesLoading;
using Kingmaker.Editor.Utility;
using Kingmaker.Modding;
using Kingmaker.Utility.UnityExtensions;

using Owlcat.Runtime.Core.Utility.Locator;

using UnityEditor;

using UnityEngine;

public static class GameServices
{
    const string BundlesPath = @"D:\SteamLibrary\steamapps\common\Warhammer 40,000 Rogue Trader\Bundles";
    const string GameDataPath = @"D:\SteamLibrary\steamapps\common\Warhammer 40,000 Rogue Trader\WH40KRT_Data";
    //const string HarmonyPatchCategoryName = "MicroPatches.EditorPatches";
    [HarmonyPatch]
    //[HarmonyPatchCategory(HarmonyPatchCategoryName)]
    static class Patches
    {
        //[HarmonyPatch(typeof(BundlesLoadService), nameof(BundlesLoadService.BundlesPath))]
        //[HarmonyPostfix]
        //static string BundlesPath_Postfix(string __result, string fileName)
        //{
        //    if (!string.IsNullOrEmpty(__result))
        //        return __result;

        //    Debug.Log(nameof(BundlesPath_Postfix));

        //    var path = Path.Combine(BundlesPath, fileName);

        //    Debug.Log(path);

        //    if (File.Exists(path))
        //        return path;

        //    return __result;
        //}

        [HarmonyPatch(typeof(BundlesLoadService), nameof(BundlesLoadService.BundlesPath))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> BundlesPath_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var i in instructions)
            {
                if (i.Calls(AccessTools.PropertyGetter(typeof(Application), nameof(Application.dataPath))))
                {
                    i.opcode = OpCodes.Ldstr;
                    i.operand = GameDataPath;
                }

                yield return i;
            }
        }

        [HarmonyPatch(typeof(AssetBundleNames), nameof(AssetBundleNames.GetBundlesFolder))]
        [HarmonyPostfix]
        static string GetBundlesFolder_Postfix(string _) => "Bundles";

        [HarmonyPatch(typeof(ResourcesLibrary), nameof(ResourcesLibrary.UseBundles), MethodType.Getter)]
        [HarmonyPostfix]
        static bool UseBundles_Postfix(bool _) => true;
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

    public static bool Started { get; private set; } = false;

    [MenuItem("Game services/Start BundlesLoadService")]
    public static void StartBundlesLoadService()
    {
        if (Started)
            return;

        Started = true;

        Debug.Log("Starting default services");

        Services.RegisterDefaultServices();

        Debug.Log("Starting OwlcatModificationsManager service");

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

        if (loadCommonBundles != null)
        {
            Reset();
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

        Services.ResetAllRegistrations();
        Started = false;
        AssetBundle.UnloadAllAssetBundles(false);
    }
}
