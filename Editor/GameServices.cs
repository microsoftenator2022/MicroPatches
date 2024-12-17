using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using HarmonyLib;

using Kingmaker;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.JsonSystem.EditorDatabase.ResourceReplacementProvider;
using Kingmaker.BundlesLoading;
using Kingmaker.Editor.Utility;
using Kingmaker.Utility.UnityExtensions;

using Owlcat.Runtime.Core.Utility.Locator;

using UnityEditor;

using UnityEngine;

public static class GameServices
{
    const string BundlesPath = @"D:\SteamLibrary\steamapps\common\Warhammer 40,000 Rogue Trader\Bundles";

    [HarmonyPatch]
    [HarmonyPatchCategory(MicroPatchesDomainReloadHandler.HaromnyPatchCategoryName)]
    static class Patches
    {
        [HarmonyPatch(typeof(BundlesLoadService), nameof(BundlesLoadService.BundlesPath))]
        [HarmonyPostfix]
        static string BundlesPath_Postfix(string __result, string fileName)
        {
            if (!string.IsNullOrEmpty(__result))
                return __result;

            Debug.Log(nameof(BundlesPath_Postfix));
            
            var path = Path.Combine(BundlesPath, fileName);

            Debug.Log(path);

            if (File.Exists(path))
                return path;

            return __result;
        }
    }

    class ResourceReplacementProviderStub : IResourceReplacementProvider
    {
        public string GetBundleNameForAsset(string guid) => null;
        public DependencyData GetDependenciesForBundle(string bundleName) => null;
        public object OnResourceLoaded(object resource, string guid) => null;
        public AssetBundle TryLoadBundle(string bundleName) => null;
    }

    static bool Started = false;

    [MenuItem("Game services/Start BundlesLoadService")]
    public static void StartBundlesLoadService()
    {
        if (Started)
            return;

        Started = true;
        
        MicroPatchesDomainReloadHandler.BeforeAssemblyReload += Reset;

        Debug.Log("Starting default services");

        Services.RegisterDefaultServices();

        Debug.Log("Starting BundlesLoadService");

        if (Services.GetInstance<BundlesLoadService>() == null)
        {
            Services.RegisterServiceInstance<BundlesLoadService>(new BundlesLoadService(new ResourceReplacementProviderStub()));
        }

        Debug.Log("Loading common bundles");

        EditorCoroutine.Start(BundlesLoadService.Instance.RequestCommonBundlesCoroutine());
    }

    [MenuItem("Game services/Reset")]
    static void Reset()
    {
        if (!Started)
            return;

        Services.ResetAllRegistrations();
        MicroPatchesDomainReloadHandler.BeforeAssemblyReload -= Reset;
        Started = false;
        AssetBundle.UnloadAllAssetBundles(false);
    }
}
