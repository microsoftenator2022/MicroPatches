using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Code.GameCore.Blueprints.BlueprintPatcher;

using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem.EditorDatabase;
using Kingmaker.Modding;
using Kingmaker.Utility.EditorPreferences;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using OwlcatModification.Editor;

using SharpCompress.Archives.Tar;

using UnityEditor;

using UnityEngine;

public static class RefreshBlueprints
{
    static readonly Regex EntryPathRegex = new(@"^(?:WhRtModificationTemplate/)(.*)");

    [MenuItem("MicroPatches/Refresh blueprints")]
    public static void RefreshBlueprintsFromArchive()
    {
        var templateArchivePath = Path.Combine(EditorPreferences.Instance.ModsGameBuildPath, "Modding", "WhRtModificationTemplate.tar");

        if (!File.Exists(templateArchivePath))
        {
            Debug.LogError($"Template archive does not exist at {templateArchivePath}");
            return;
        }

        BlueprintsDatabase.InvalidateAllCache();

        //using var _ = BlueprintsDatabase.PauseIndexing();

        EditorUtility.DisplayCancelableProgressBar("Refreshing blueprints", "Reading archive index", 0);

        using var tarFile = TarArchive.Open(templateArchivePath);

        var blueprintEntries = tarFile.Entries
            .Select(entry => (entry, path: EntryPathRegex.Match(entry.Key).Groups[1].Value))
            .Where(e => e.path.StartsWith("Blueprints") || e.path.StartsWith("Strings"))
            .ToArray();

        static void deleteExisting(TarArchiveEntry entry)
        {
            using var s = entry.OpenEntryStream();

            using var tr = new StreamReader(s);
            using var jr = new JsonTextReader(tr);
            
            var assetId = JToken.ReadFrom(jr)["AssetId"].ToString();

            if (assetId is null)
                return;

            var path = BlueprintsDatabase.IdToPath(assetId);
            
            if (!string.IsNullOrEmpty(path))
                File.Delete(path);
        }

        static void writeFile(TarArchiveEntry entry, string path)
        {
            var length = (int)entry.Size;
            using var s = entry.OpenEntryStream();
            var arr = System.Buffers.ArrayPool<byte>.Shared.Rent(length);
            var buffer = new Span<byte>(arr, 0, length);
            s.Read(buffer);

            using var f = File.Create(path);
            f.Write(buffer);

            System.Buffers.ArrayPool<byte>.Shared.Return(arr);
        }

        for (var i = 0; i < blueprintEntries.Length; i++)
        {
            var (entry, path) = blueprintEntries[i];
        
            if (entry.IsDirectory)
            {
                if (Directory.Exists(path))
                    continue;

                Directory.CreateDirectory(path);
                continue;
            }

            if (EditorUtility.DisplayCancelableProgressBar("Refreshing blueprints", $"({i}/{blueprintEntries.Length}) {entry.Key.Remove(0, "WhRtModificationTemplate/".Length)}", ((float)i) / ((float)blueprintEntries.Length)))
            {
                break;
            }
            
            if (Path.GetExtension(path) == ".jpb")
                deleteExisting(entry);

            writeFile(entry, path);
        }

        BlueprintsDatabase.InvalidateAllCache();

        EditorUtility.ClearProgressBar();
    }

    const string RestoredModBlueprintsDirectoryName = "ModBlueprints";
    static readonly string RestoredModBlueprintsPath =
        Path.Combine(BlueprintsDatabase.DbPathPrefix, RestoredModBlueprintsDirectoryName);

    static void RestoreBlueprintsFromModDirectories()
    {
        if (!Directory.Exists(RestoredModBlueprintsPath))
            Directory.CreateDirectory(RestoredModBlueprintsPath);

        foreach (var modDir in Directory.EnumerateDirectories(@"Assets\Modifications"))
        {
            var blueprintsDir = Path.Join(modDir, "Blueprints");

            if (!Directory.Exists(blueprintsDir))
                continue;

            foreach (var blueprint in Directory.EnumerateFiles(blueprintsDir, "*.jbp", SearchOption.AllDirectories))
            {
                try
                {
                    var json = JObject.Parse(File.ReadAllText(blueprint));

                    if (!string.IsNullOrEmpty(BlueprintsDatabase.IdToPath(json["AssetId"].ToString())))
                        continue;

                    var blueprintRelativePath = Path.GetRelativePath(blueprintsDir, blueprint);

                    var destination = Path.Join(
                        RestoredModBlueprintsPath,
                        Path.GetFileName(modDir),
                        blueprintRelativePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(destination));

                    File.Copy(blueprint, destination, false);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }

    static readonly JsonSerializer BlueprintPatchSerializer = JsonSerializer.Create(BlueprintPatcher.Settings);

    static void CreateInheritedBlueprintFromPatch(
        string targetAssetId,
        string path,
        BlueprintChangeDataDrawer.JsonPatchType patchType)
    {
        using var patchFile = File.OpenText(path);

        var blueprint = BlueprintsDatabase.DuplicateAsset(BlueprintsDatabase.LoadById<SimpleBlueprint>(targetAssetId));
        var newGuid = blueprint.AssetGuid;
        var targetPath = BlueprintsDatabase.IdToPath(newGuid);
        var wrapper = BlueprintsDatabase.LoadWrapperAtPath(targetPath);

        var json = JObject.Parse(File.ReadAllText(targetPath));
        var overrides = new JArray();

        switch (patchType)
        {
            case BlueprintChangeDataDrawer.JsonPatchType.Edit:
            {
                using var jr = new JsonTextReader(patchFile);
                var patch = BlueprintPatchSerializer.Deserialize<BlueprintPatch>(jr);

                patch.TargetGuid = newGuid;

                wrapper.Data = BlueprintPatcher.TryPatchBlueprint(patch, wrapper.Data, newGuid);
                wrapper.Save(targetPath);

                foreach (var n in patch.FieldOverrides.Select(fo => fo.FieldName))
                    overrides.Add(n);

                foreach (var n in patch.ArrayPatches.Select(ap => ap.FieldName))
                    overrides.Add(n);

                foreach (var cp in patch.ComponentsPatches)
                    overrides.Add(
                        JsonConvert.DeserializeObject<BlueprintComponentPatchData>(cp.FieldValue, BlueprintPatcher.Settings)
                            .ComponentValue.name);
                break;
            }
            case BlueprintChangeDataDrawer.JsonPatchType.Micro:
                var patchJson = JObject.Parse(patchFile.ReadToEnd());
                File.WriteAllText(targetPath, MicroPatches.JsonPatch.ApplyPatch(json, patchJson).ToString());

                // TODO: Generate list for m_Overrides. How?

                break;

            default:
                throw new NotImplementedException();
        }

        json["Data"]["m_Overrides"] = overrides;
        File.WriteAllText(targetPath, json.ToString());
    }
}
