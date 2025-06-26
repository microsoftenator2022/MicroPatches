using Kingmaker;
using Kingmaker.Localization;
using Kingmaker.Utility.Serialization;
using Newtonsoft.Json;
using OwlcatModification.Editor.Utility;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Code.GameCore.Editor.Mods
{
    public static class SharedStringAssetRepair
    {
        private static string OldSharedStringScriptGuid;
        private static long OldSharedStringScriptFileId;
        
        [MenuItem("Modification Tools/Repair SharedString configs")]
        public static void Repair()
        {
            #if OWLCAT_MODS
            ReadScriptData();
            
            if(string.IsNullOrEmpty(OldSharedStringScriptGuid))
                return;
            
            RepairInternal();
            AssetDatabase.Refresh();

            #endif 
        }

        private static void ReadScriptData()
        {
            const string cacheFileName = "SharedStringScriptData";
            var guids = AssetDatabase.FindAssets(cacheFileName);
            if (guids == null || guids.Length != 1)
            {
                Debug.Log($"Error while trying to repair so configs. Couldn't find cache file {cacheFileName}.json");
                return;
            }
            
            var cacheFileGuid = guids[0];
            var cacheFilePath = AssetDatabase.GUIDToAssetPath(cacheFileGuid);
            PFLog.Mods.Log($"{cacheFilePath}");

            if (!File.Exists(cacheFilePath))
            {
                PFLog.Mods.Log($"Cache file not found at path: {cacheFilePath};");
                return;
            }
            
            var cacheFileContent = File.ReadAllText(cacheFilePath);

            if (string.IsNullOrEmpty(cacheFileContent))
            {
                PFLog.Mods.Log($"Error while reading data from {cacheFilePath}");
                return;
            }
            
            JsonSerializer serializer = JsonSerializer.Create
            (new JsonSerializerSettings
                {Formatting = Formatting.Indented}
            );

            var scriptData = serializer.DeserializeObject<ScriptableObjectScriptData>(cacheFileContent);
            OldSharedStringScriptGuid = scriptData.Guid;
            OldSharedStringScriptFileId = scriptData.FileId;
        }

        private static void RepairInternal()
        {
            var sharedStringAssetsFolder = Path.Combine(Application.dataPath, "Mechanics", "Blueprints");
            
            var files = Directory.EnumerateFiles(sharedStringAssetsFolder, "*.asset", SearchOption.AllDirectories)
                .ToList();

            PFLog.Mods.Log($"Found {files.Count} SharedStringAsset files in project.");

            var newGuid = ScriptsGuidUtil.GetScriptGuid(typeof(SharedStringAsset));
            var newFileId = FileIDUtil.Compute(typeof(SharedStringAsset));

            #region MicroPatches
            //var oldMetaString = $"fileID: {OldSharedStringScriptFileId}, guid: {OldSharedStringScriptGuid}";
            //var newMetaString = $"fileID: {newFileId}, guid: {newGuid}";

            PFLog.Mods.Log($"New SharedStringAssets guid {newGuid}, fileId {newFileId}");

            AssetDatabase.ReleaseCachedFileHandles();
            AssetDatabase.StartAssetEditing();

            var progressid = Progress.Start("Repairing SharedStringAssets");

            try
            {
                var count = 0;

                Parallel.ForEach(files,
                    file =>
                    {
                        //RepairConfig(file, oldMetaString, newMetaString);
                        RepairConfig(file, newFileId.ToString(), newGuid);
                        Interlocked.Increment(ref count);
                        Progress.Report(progressid, ((float)count) / ((float)files.Count));
                    });
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                Progress.Finish(progressid);
            }

            AssetDatabase.Refresh();

            if (UnknownGuids.Count > 0)
            {
                //PFLog.Mods.Error("Unknown asset guids:\n" + string.Join("\n", UnknownGuids.Select(t => t.Item1).Distinct()));
                var dict = new Dictionary<string, string[]>();

                foreach (var guid in UnknownGuids.GroupBy(t => t.Item1))
                {
                    dict.Add(guid.Key, guid.Select(t => Path.GetRelativePath(Path.GetFullPath("."), t.Item2).Replace(@"\", "/")).ToArray());
                }

                File.WriteAllText("Assets/Mechanics/Blueprints/UnknownGUIDs.json", JsonConvert.SerializeObject(dict, Formatting.Indented));
            }
            #endregion
        }
        #region MicroPatches
        static ConcurrentBag<(string, string)> UnknownGuids = new();

        //private static void RepairConfig(string filePath, string oldMeta, string newMeta)
        //{
        //    var contents = File.ReadAllText(filePath);
        //    if (string.IsNullOrEmpty(contents))
        //    {
        //        PFLog.Mods.Error($"Error while reading contents of {filePath}");
        //        return;
        //    }

        //    contents = contents.Replace(oldMeta, newMeta);
        //    File.WriteAllText(filePath, contents);
        //}

        static readonly string[] KnownSharedStringAssetGuids = new[]
        {
            "e91caa81884d28a438cca4933d34ec1e",
            "36baaa8bdcb9d8b49b9199833965d2c3"
        };

        static readonly Regex MonoScriptPropertyString = new Regex(@"m_Script:\s+\{fileID:\s+(?<fileID>\-?\d+)\s*,\s+guid:\s+(?<guid>[0-9a-f]{32})\b.*\}");

        private static void RepairConfig(string filePath, string newFileID, string newGuid)
        {
            filePath = Path.GetFullPath(filePath);

            if (filePath.Length > 260 && !filePath.StartsWith(@"\\?\"))
                filePath = $@"\\?\{filePath}";

            var contents = File.ReadAllText(filePath);
            if (string.IsNullOrEmpty(contents))
            {
                PFLog.Mods.Error($"Error while reading contents of {filePath}");
                return;
            }

            var matches = MonoScriptPropertyString.Matches(contents);

            if (matches.Count != 1)
                return;

            var match = matches[0];

            if (match.Groups["fileID"].Value != "11500000")
                return;

            var guid = match.Groups["guid"].Value;

            if (!KnownSharedStringAssetGuids.Contains(guid))
            {
                //PFLog.Mods.Error($"Unkown MonoScript guid '{guid}'");
                UnknownGuids.Add((guid, filePath));
                return;
            }

            static (int index, int length) location(Group group) => group.Success ? (group.Index, group.Length) : default;
            static string replaceRange(string source, int index, int length, string replacement)
            {
                var left = source.Substring(0, index);
                var right = source.Substring(index + length);

                return left + replacement + right;
            }

            var fileIDLocation = location(match.Groups["fileID"]);
            var guidLocation = location(match.Groups["guid"]);

            contents = replaceRange(contents, guidLocation.index, guidLocation.length, newGuid);
            contents = replaceRange(contents, fileIDLocation.index, fileIDLocation.length, newFileID);

            File.WriteAllText(filePath, contents);
        }
        #endregion
    }
}