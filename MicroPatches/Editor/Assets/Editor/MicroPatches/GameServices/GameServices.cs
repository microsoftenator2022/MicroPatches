using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

using HarmonyLib;

using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Base;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.JsonSystem.Converters;
using Kingmaker.Blueprints.JsonSystem.EditorDatabase.ResourceReplacementProvider;
using Kingmaker.BundlesLoading;
using Kingmaker.Utility.EditorPreferences;
using Kingmaker.Utility.UnityExtensions;

using MicroPatches;
using MicroPatches.Editor;

using MicroUtils.Transpiler;

using Newtonsoft.Json;

using Owlcat.Runtime.Core.Utility.Locator;

using UnityEditor;

using UnityEngine;

public static class GameServices
{
    const string rtAppData = @"%LocalAppData%Low\Owlcat Games\Warhammer 40000 Rogue Trader";
    static readonly Regex gamePathRegex = new Regex(@"^Mono path\[0\] = '(.*?)/WH40KRT_Data/Managed'$");
    
    static string GamePath
    {
        get
        {
            var gamePath = EditorPreferences.Instance.ModsGameBuildPath;
            if (!string.IsNullOrWhiteSpace(gamePath) && Directory.Exists(gamePath))
                return gamePath;
            
            var playerLogPath = Path.Join(Environment.ExpandEnvironmentVariables(rtAppData), "Player.log");
            if (File.Exists(playerLogPath))
            {
                var f = File.OpenText(playerLogPath);
                var line = f.ReadLine();
                while (line != null)
                {
                    var match = gamePathRegex.Match(line);
                    if (match.Success)
                    {
                        gamePath = match.Groups[1].Value;
                        break;
                    }

                    line = f.ReadLine();
                }
            }

            if (gamePath != EditorPreferences.Instance.ModsGameBuildPath)
                EditorPreferences.Instance.ModsGameBuildPath = gamePath;

            return gamePath;
        }
    }

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

    [MenuItem("MicroPatches/Game services/Start")]
    public static void StartGameServices()
    {
        //if (loadCommonBundles != null)
        if (Started)
            return;

        if (Starting)
        {
            Reset();
        }

        Starting = true;
        Canceled = false;

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

        Debug.Log("Init paths");
        ApplicationPaths.Init();

        Debug.Log("Starting default services");

        Services.RegisterDefaultServices();

        //Debug.Log("Starting OwlcatModificationsManager service");

        Debug.Log("Starting BundlesLoadService");

        if (Services.GetInstance<BundlesLoadService>() == null)
        {
            Services.RegisterServiceInstance<BundlesLoadService>(new BundlesLoadService(new ResourceReplacementProviderStub()));
        }

        var p = Progress.Start("Loading bundles", options: Progress.Options.Indefinite);

        //var loadedBundlesGameObject = GameObject.Find(LoadedBundlesGameObjectName);

        //if (loadedBundlesGameObject == null)
            EditorCoroutine.Start(LoadCommonBundlesCoroutine(() => Progress.Finish(p, Canceled ? Progress.Status.Failed : Progress.Status.Succeeded)));
        //else
        //{
        //    try
        //    {
        //        RestoreLoadedBundles(loadedBundlesGameObject.GetComponent<LoadedBundlesList>());
        //        UnityEngine.Object.Destroy(loadedBundlesGameObject);
        //        StartGameLoader.LoadDirectReferencesList();
        //    }
        //    finally
        //    {
        //        Progress.Finish(p);
        //    }
        //}
    }

    public static bool Canceled = false;
    static IEnumerator LoadCommonBundlesCoroutine(Action onFinish)
    {
        try
        {
            IEnumerator coroutine = null;
            try
            {
                coroutine = BundlesLoadService.Instance.RequestCommonBundlesCoroutine();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Canceled = true;
            }

            if (Canceled)
                yield break;

            yield return null;

            var running = true;
            
            while (running && !Canceled)
            {
                try
                {
                    EditorUtility.DisplayProgressBar("Loading bundles", "Loading common bundles", 0);

                    running = coroutine.MoveNext();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    Canceled = true;
                    yield break;
                }

                yield return null;
            }

            PFLog.Mods.DebugLog("Loaded bundles:");
            foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                PFLog.Mods.DebugLog($"  {bundle.name}");
            }

            if (!Canceled)
            {
                EditorUtility.ClearProgressBar();
                yield return null;
                EditorUtility.DisplayProgressBar("Loading bundles", "Loading direct references", 0.5f);
                yield return null;
                StartGameLoader.LoadDirectReferencesList();
            }
        }
        finally
        {
            Starting = false;
            Started = !Canceled;

            EditorUtility.ClearProgressBar();

            onFinish();
        }
    }

    //class LoadedBundlesList : MonoBehaviour
    //{
    //    public string[] bundleNames;
        
    //    [SerializeReference]
    //    public AssetBundle[] bundles;
        
    //    public int[] requestCounts;
    //}

    //const string LoadedBundlesGameObjectName = "MicroPatches_LoadedBundles";

    //static void SaveLoadedBundles()
    //{
    //    var go = new GameObject(LoadedBundlesGameObjectName, typeof(LoadedBundlesList));

    //    var loadedBundles = Util.GetLoadedBundles();

    //    var lbl = go.GetComponent<LoadedBundlesList>();
    //    lbl.bundleNames = new string[loadedBundles.Length];
    //    lbl.bundles = new AssetBundle[loadedBundles.Length];
    //    lbl.requestCounts = new int[loadedBundles.Length];

    //    for (var i = 0; i < loadedBundles.Length; i++)
    //    {
    //        lbl.bundleNames[i] = loadedBundles[i].name;
    //        lbl.bundles[i] = loadedBundles[i].bundle;
    //        lbl.requestCounts[i] = loadedBundles[i].requestCount;
    //    }
    //}

    //static void RestoreLoadedBundles(LoadedBundlesList lbl)
    //{
    //    Util.ReloadBundlesLoadServiceLists();

    //    foreach (var (name, bundle, requestCount) in lbl.bundleNames
    //        .Zip(lbl.bundles, (name, bundle) => (name, bundle))
    //        .Zip(lbl.requestCounts, (nb, requestCount) => (nb.name, nb.bundle, requestCount)))
    //    {
    //        Debug.Log($"Restoring {name} ({requestCount})");
    //        Util.AddToBundlesLoadService(name, bundle, requestCount);
    //    }
    //}

    public static void Reset(bool unloadBundles)
    {
        Canceled = true;

        Services.ResetAllRegistrations();

        //if (unloadBundles)
            AssetBundle.UnloadAllAssetBundles(true);
        //else
        //    SaveLoadedBundles();

        Started = false;
        Starting = false;
    }

    [MenuItem("MicroPatches/Game services/Reset")]
    public static void Reset() => Reset(true);
}
