using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kingmaker.Modding;
using OwlcatModification.Editor.Build.Context;
using OwlcatModification.Editor.Build.Tasks;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Pipeline.Utilities;
using System.Text.RegularExpressions;
using UnityEditor.Build.Player;
using UnityEngine;
using Kingmaker.Utility.NewtonsoftJson;
using Newtonsoft.Json;
using System.IO.Compression;
using Kingmaker;

namespace OwlcatModification.Editor.Build
{
    public static class Builder
    {

        private class WriteableSettingsData
        {
            [JsonProperty]
            public List<string> SourceDirectories = new();

            [JsonProperty]
            public List<string> EnabledModifications = new();
        }

        public static ReturnCode Build(Modification modification)
        {
            string sourcePath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(modification));
            return Build(
                modification.Manifest,
                modification.Settings,
                sourcePath,
                BuilderConsts.DefaultBuildFolder,
                null);
        }

        public static ReturnCode BuildAndOpen(Modification modification)
        {
            var returnCode = Build(modification);
            if (returnCode == 0)
            {
                EditorUtility.RevealInFinder(modification.GetFinalBuildPath());
            }
            return returnCode;
        }

        public static string GetFinalBuildPath(this Modification modification)
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars());
            var invalidCharsRegex = new Regex($"[{Regex.Escape(regexSearch) + "\\s"}]");
            var targetFolderName = invalidCharsRegex.Replace(modification.Manifest.UniqueName, "");
            return Path.Combine(Application.dataPath, "..", "Build", targetFolderName);
        }

        public static ReturnCode BuildAndInstall(Modification modification)
        {
            var returnCode = Build(modification);
            if (returnCode == 0)
            {
                var editorPath = Application.persistentDataPath;
                // In Editor the persistentDataPath directory points to OwlcatGames\OwlcatModification; The path needed is Owlcat Games\Warhammer 40000 Rogue Trader\Modifications
                // I can't test on Linux platforms. The Steam version seems to use Proton (Path: ~/.steam/steam/steamapps/compatdata/2186680/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat\ Games/Warhammer\ 40000\ Rogue\ Trader/),
                // so it might work there too as long as Unity Editor doesn't behave differently
                var lastIndex = editorPath.LastIndexOf("OwlcatGames");
                if (lastIndex != -1)
                {
                    string replacedString = editorPath.Substring(0, lastIndex) + "Owlcat Games" + editorPath.Substring(lastIndex + "OwlcatGames".Length);
                    DirectoryInfo directory = new DirectoryInfo(replacedString);
                    string userProfileDir = Path.Combine(directory.Parent.FullName, "Warhammer 40000 Rogue Trader");
                    string targetDir = Path.Combine(userProfileDir, "Modifications");
                    var zipFilePath = modification.GetFinalBuildPath() + ".zip";
                    var targetModDir = Path.Combine(targetDir, modification.Manifest.UniqueName);
                    Directory.CreateDirectory(targetModDir);
                    using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            if (entry.FullName == "/") continue;
                            string entryTargetPath = Path.Combine(targetModDir, entry.FullName);
                            if (entry.FullName.EndsWith("/"))
                            {
                                // This entry is a directory
                                Directory.CreateDirectory(Path.GetDirectoryName(entryTargetPath));
                            }
                            else
                            {
                                // Ensure the directory structure exists
                                Directory.CreateDirectory(Path.GetDirectoryName(entryTargetPath));
                                #region MicroPatches
                                try
                                {
                                    // Extract the entry to the target path
                                    entry.ExtractToFile(entryTargetPath, true);
                                }
                                catch (Exception ex)
                                {
                                    PFLog.Build.Exception(ex);
                                }
                                #endregion
                            }
                        }
                    }

                    var SettingsFilePath = Path.Combine(userProfileDir, OwlcatModificationsManager.SettingsFileName);
                    WriteableSettingsData settings = NewtonsoftJsonHelper.DeserializeFromFile<WriteableSettingsData>(SettingsFilePath) ?? new();
                    var tmpList = settings.EnabledModifications ?? new();
                    if (!tmpList.Contains(modification.Manifest.UniqueName))
                    {
                        tmpList.Add(modification.Manifest.UniqueName);
                        settings.EnabledModifications = tmpList;
                        File.WriteAllText(SettingsFilePath, JsonUtility.ToJson(settings, true));
                    }
                    EditorUtility.RevealInFinder(Path.Combine(targetDir, modification.Manifest.UniqueName));
                }
                else
                {
                    Debug.LogError($"Can't install locally Unexpected Persistent Data Path: {editorPath}");

                }
            }
            return returnCode;
        }

        public static ReturnCode BuildAndPublish(Modification modification)
        {
            var returnCode = Build(modification);
            if (returnCode == ReturnCode.Success || returnCode == ReturnCode.SuccessCached || returnCode == ReturnCode.SuccessNotRun)
            {
                returnCode = PublishToWorkshop.Publish(modification);
            }
            return returnCode;
        }

        public static ReturnCode Build(
            OwlcatModificationManifest manifest,
            Modification.SettingsData settings,
            string sourceFolder,
            string targetFolder,
            params IContextObject[] contextObjects)
        {
            try
            {
                return BuildInternal(manifest, settings, sourceFolder, targetFolder, contextObjects);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error!", $"{e.Message}\n\n{e.StackTrace}", "Close");
                return ReturnCode.Exception;
            }
        }

        private static ReturnCode BuildInternal(
            OwlcatModificationManifest manifest,
            Modification.SettingsData settings,
            string sourceFolder,
            string targetFolder,
            params IContextObject[] contextObjects)
        {
            if (!Path.IsPathRooted(targetFolder))
            {
                targetFolder = Path.Combine(Path.Combine(Application.dataPath, ".."), targetFolder);
            }

            string intermediateBuildFolder = Path.Combine(targetFolder, BuilderConsts.Intermediate);
            if (Directory.Exists(targetFolder))
            {
                Directory.Delete(targetFolder, true);
            }

            Directory.CreateDirectory(intermediateBuildFolder);

            string logFilepath = Path.Combine(targetFolder, "build.log");
            var defaultBuildTarget = EditorUserBuildSettings.activeBuildTarget;
            var defaultBuildTargetGroup = BuildTargetGroup.Standalone;
            var defaultBuildOptions = EditorUserBuildSettings.development
                ? ScriptCompilationOptions.DevelopmentBuild
                : ScriptCompilationOptions.None;

            UglySBPHacks.ThreadingManager_WaitForOutstandingTasks();
            AssetDatabase.SaveAssets();

            var buildContext = new BuildContext(contextObjects);
            var buildParameters = buildContext.EnsureContextObject<IBundleBuildParameters>(
                () => new BundleBuildParameters(defaultBuildTarget, defaultBuildTargetGroup, intermediateBuildFolder)
                {
                    BundleCompression = BuildCompression.LZ4,
                    ScriptOptions = defaultBuildOptions,
                    UseCache = false,
                });

            contextObjects = (contextObjects ?? new IContextObject[0]).Concat(new IContextObject[]
            {
                buildParameters,
                buildContext.EnsureContextObject<IBundleLayoutManager>(() => new DefaultBundleLayoutManager()),
                buildContext.EnsureContextObject<IModificationRuntimeSettings>(() => new DefaultModificationRuntimeSettings()),
                buildContext.EnsureContextObject(() => new BuildInterfacesWrapper()),
                buildContext.EnsureContextObject<IBuildLogger>(() => new BuildLoggerFile(logFilepath)),
                buildContext.EnsureContextObject<IProgressTracker>(() => new ProgressLoggingTracker()),
                buildContext.EnsureContextObject<IDependencyData>(() => new BuildDependencyData()),
                buildContext.EnsureContextObject<IBundleWriteData>(() => new BundleWriteData()),
                buildContext.EnsureContextObject<IBundleBuildResults>(() => new BundleBuildResults()),
                buildContext.EnsureContextObject<IDeterministicIdentifiers>(() => new Unity5PackedIdentifiers()),
                buildContext.EnsureContextObject<IBundleBuildContent>(()
                    => new BundleBuildContent(Enumerable.Empty<AssetBundleBuild>())),
                buildContext.EnsureContextObject<IBuildCache>(
                    () => new BuildCache(buildParameters.CacheServerHost, buildParameters.CacheServerPort)),
                buildContext.EnsureContextObject<IModificationParameters>(
                    () => new DefaultModificationParameters(manifest, settings, sourceFolder)),
            }).ToArray();

            var tasksList = GetTasks().ToArray();
            try
            {
                return RunTasks(tasksList, buildContext);
            }
            finally
            {
                Dispose(contextObjects, tasksList);
            }
        }

        private static ReturnCode RunTasks(IList<IBuildTask> tasksList, IBuildContext context)
        {
            var validationResult = BuildTasksRunner.Validate(tasksList, context);
            if (validationResult < ReturnCode.Success)
            {
                return validationResult;
            }

            return BuildTasksRunner.Run(tasksList, context);
        }

        private static void Dispose(IEnumerable<IContextObject> contextObjects, IEnumerable<IBuildTask> tasks)
        {
            foreach (var disposable in contextObjects.OfType<IDisposable>())
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception e)
                {
                    BuildLogger.LogException(e);
                }
            }

            // ReSharper disable once SuspiciousTypeConversion.Global
            foreach (var disposable in tasks.OfType<IDisposable>())
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception e)
                {
                    BuildLogger.LogException(e);
                }
            }
        }

        private static T EnsureContextObject<T>(this BuildContext context, Func<T> createDefaultObject) where T : IContextObject
        {
            if (!context.ContainsContextObject<T>())
            {
                var obj = createDefaultObject.Invoke();
                context.SetContextObject(obj);
                return obj;
            }

            return context.GetContextObject<T>();
        }

        private static IEnumerable<IBuildTask> GetTasks()
        {
            yield return new SwitchToBuildPlatform();

            yield return new PrepareBuild();

            yield return new BuildAssemblies();

            yield return new PrepareBlueprints();

            yield return new ExtractBlueprintDirectReferences();
            yield return new PrepareBundles();

            yield return new PrepareLocalization();

            yield return new CheckAssetsValidity();

            yield return new CalculateSceneDependencyData();
            yield return new CalculateCustomDependencyData();
            yield return new CalculateAssetDependencyData();

            yield return new GenerateBundlePacking();
            yield return new UpdateBundleObjectLayout();
            yield return new GenerateBundleCommands();
            yield return new GenerateSubAssetPathMaps();
            yield return new GenerateBundleMaps();

            yield return new WriteSerializedFiles();
            yield return new ArchiveAndCompressBundles();

            yield return new CreateManifestAndSettings();

            yield return new PrepareArtifacts();
            yield return new PackArtifacts();
        }
    }
}