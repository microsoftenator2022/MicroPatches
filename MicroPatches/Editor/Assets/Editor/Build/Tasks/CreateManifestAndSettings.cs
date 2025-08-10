using Code.GameCore.Blueprints.BlueprintPatcher;
using Kingmaker;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Modding;
using Kingmaker.Utility.UnityExtensions;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using OwlcatModification.Editor.Build.Context;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;

namespace OwlcatModification.Editor.Build.Tasks
{
    public class CreateManifestAndSettings : IBuildTask
    {
#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        private IBuildParameters m_BuildParameters;

        [InjectContext(ContextUsage.In)]
        private IModificationParameters m_ModificationParameters;

        [InjectContext(ContextUsage.In)]
        private IModificationRuntimeSettings m_ModificationSettings;
        
        #region MicroPatches
        [InjectContext(ContextUsage.In)]
        private IBundleBuildResults m_BundleBuildResults;
        #endregion
#pragma warning restore 649

        public int Version
            => 1;

        public ReturnCode Run()
        {
            string buildFolderPath = m_BuildParameters.GetOutputFilePathForIdentifier("");

            var blueprintPatches =
                AssetDatabase.FindAssets($"t:{nameof(BlueprintPatches)}", new[] {m_ModificationParameters.SourcePath})
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<BlueprintPatches>)
                    .FirstOrDefault();

            string manifestJsonFilePath = Path.Combine(buildFolderPath, Kingmaker.Modding.OwlcatModification.ManifestFileName);
            string manifestJsonContent = JsonUtility.ToJson(m_ModificationParameters.Manifest, true);
            File.WriteAllText(manifestJsonFilePath, manifestJsonContent);

            var patchUpdatedData = ChangePatchesNamesForInnerFolders(blueprintPatches);

            if (patchUpdatedData != null)
            {
                PFLog.Build.Log($"Adding BlueprintPatches info to ModificationSettings");
                m_ModificationSettings.Settings.BlueprintPatches = patchUpdatedData;
            }

            #region MicroPatches
            if (m_BundleBuildResults?.BundleInfos.Count > 0)
                foreach (var bi in m_BundleBuildResults.BundleInfos)
                {
                    static string removeBundlesPrefix(string path) => path.StartsWith(@"Bundles\") ? path.Remove(0, 8) : path;

                    if (bi.Key.StartsWith(@"Bundles\"))
                        m_ModificationSettings.Settings.BundleDependencies.BundleToDependencies.Add(
                            removeBundlesPrefix(bi.Key),
                            bi.Value.Dependencies.Select(removeBundlesPrefix).ToList());
                }
            #endregion

            string settingsJsonFilePath = Path.Combine(buildFolderPath, Kingmaker.Modding.OwlcatModification.SettingsFileName);
            string settingsJsonContent = JsonUtility.ToJson(m_ModificationSettings.Settings, true);
            File.WriteAllText(settingsJsonFilePath, settingsJsonContent);


            return ReturnCode.Success;
        }

        /// <summary>
        /// Change paths to .jbp_patch files adding part of a path relative to Blueprints folder,
        /// which helps to support inner folders inside Blueprints folder.
        /// </summary>
        /// <param name="blueprintPatches"></param>
        private List<OwlcatModificationSettings.BlueprintChangeData> ChangePatchesNamesForInnerFolders(BlueprintPatches blueprintPatches)
        {
            List<OwlcatModificationSettings.BlueprintChangeData> patchEntries = null;
            var blueprintsFolderPath = Path.Combine(m_ModificationParameters.SourcePath, "Blueprints");
            PFLog.Build.Log($"Blueprints folder source path: {m_ModificationParameters.SourcePath}");

            if (blueprintPatches != null)
            {
                patchEntries = blueprintPatches.Entries.ToList();
                foreach (var entry in patchEntries)
                {
                    #region MicroPatches
                    if (entry.PatchType is not OwlcatModificationSettings.BlueprintPatchType.Edit)
                        continue;
                    #endregion

                    var patchSourceFiles =
                        Directory.EnumerateFiles(blueprintsFolderPath, $"{entry.Filename}.jbp_patch", SearchOption.AllDirectories);
                    string sourcePath = null;
                    foreach (var file in patchSourceFiles)
                    {
                        BlueprintPatch patch = null;
                        string fileContents = File.ReadAllText(file);
                        using (var sr = new StringReader(fileContents))
                        using (var jr = new JsonTextReader(sr))
                            try
                            {
                                patch = Json.Serializer.Deserialize<BlueprintPatch>(jr);
                            }
                            catch (Exception ex)
                            {
                                PFLog.Build.Error($"Exception occured reading patch: {ex}");
                            }
                        if (patch == null)
                        {
                            PFLog.Build.Error($"Failed to read BlueprintPatch from file : {file}");
                            continue;
                        }
                        if (!string.Equals(patch.TargetGuid, entry.Guid))
                            continue;

                        sourcePath = file.Replace(".jbp_patch", "").Replace(blueprintsFolderPath, "")[1..];
                        PFLog.Build.Log($"Changing patch path to {sourcePath}");

                        entry.Filename = sourcePath;
                        break;
                    }

                    if (sourcePath.IsNullOrEmpty())
                    {
                        PFLog.Build.Error($"Patch file for {entry.Filename} : {entry.Guid} not found.");
                    }
                }
            }

            return patchEntries;
        }

    }
}