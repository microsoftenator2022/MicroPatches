using System.Collections.Generic;
using System.IO;
using System.Linq;

using Kingmaker;
using Kingmaker.Modding;

using OwlcatModification.Editor.Build.Context;

using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;

using UnityEngine;

namespace OwlcatModification.Editor.Build.Tasks
{
    public class CheckAssetsValidity : IBuildTask
    {
        [InjectContext(ContextUsage.In)]
        private IModificationParameters m_ModificationParameters;

        public int Version
            => 1;

        #region MicroPatches
        IEnumerable<string> MissingBlueprintPatchFiles()
        {
            List<string> missingPatchFiles = new();

            if (AssetDatabase.FindAssets($"t:{nameof(BlueprintPatches)}", new[] { m_ModificationParameters.SourcePath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BlueprintPatches>)
                .FirstOrDefault()
                is { } blueprintPatches)
            {
                var blueprintFiles = Directory.EnumerateFiles(Path.Combine(m_ModificationParameters.SourcePath, "Blueprints"), "*.*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(Path.Combine(m_ModificationParameters.SourcePath, "Blueprints"), path))
                    .ToArray();

                foreach (var entry in blueprintPatches.Entries)
                {
                    if (blueprintFiles.Contains(entry.Filename))
                        continue;

                    // CreateManifestAndSettings task may fix patches in nested folders
                    if (entry.PatchType is OwlcatModificationSettings.BlueprintPatchType.Edit)
                        continue;

                    else if (blueprintFiles.Contains($"{entry.Filename}.patch"))
                    {
                        Debug.LogWarning($"{entry.Filename} is missing .patch extension");
                        continue;
                    }

                    missingPatchFiles.Add(entry.Filename);
                }
            }

            return missingPatchFiles;
        }

        public ReturnCode Run()
        {

            var missingBpPatchFiles = MissingBlueprintPatchFiles();
            if (missingBpPatchFiles.Any())
            {
                var errorMessage = "Missing patch files:\n" + string.Join("\n", missingBpPatchFiles);

                //PFLog.Build.Error(errorMessage);
                Debug.LogError(errorMessage);

                return ReturnCode.Error;
            }

            return ReturnCode.Success;
        }
        #endregion
    }
}