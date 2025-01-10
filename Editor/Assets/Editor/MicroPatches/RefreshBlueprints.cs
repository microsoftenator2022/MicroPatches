using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Kingmaker.Blueprints.JsonSystem.EditorDatabase;
using Kingmaker.Utility.EditorPreferences;

using Owlcat.Runtime.Core.Utility.Buffers;

using SharpCompress.Archives.Tar;
using SharpCompress.Readers;

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

        EditorUtility.DisplayCancelableProgressBar("Refreshing blueprints", "Reading archive index", 0);

        using var tarFile = TarArchive.Open(templateArchivePath);

        var blueprintEntries = tarFile.Entries
            .Select(entry => (entry, path: EntryPathRegex.Match(entry.Key).Groups[1].Value))
            .Where(e => e.path.StartsWith("Blueprints") || e.path.StartsWith("Strings"))
            .ToArray();

        static void writeFile(TarArchiveEntry entry, string path)
        {
            var length = (int)entry.Size;
            using var s = entry.OpenEntryStream();
            var arr = System.Buffers.ArrayPool<byte>.Shared.Rent(length);
            var buffer = new Span<byte>(arr, 0, length);
            s.Read(buffer);

            File.WriteAllBytes(path, buffer.ToArray());
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
            
            //if (Path.GetExtension(path) != ".jbp")
            //    continue;

            if (EditorUtility.DisplayCancelableProgressBar("Refreshing blueprints", $"({i}/{blueprintEntries.Length}) {entry.Key.Remove(0, "WhRtModificationTemplate/".Length)}", ((float)i) / ((float)blueprintEntries.Length)))
            {
                break;
            }

            writeFile(entry, path);

            //var length = (int)entry.Size;
            //using var s = entry.OpenEntryStream();
            //var arr = System.Buffers.ArrayPool<byte>.Shared.Rent(length);
            //var buffer = new Span<byte>(arr, 0, length);
            //s.Read(buffer);

            //File.WriteAllBytes(path, buffer.ToArray());
        }

        BlueprintsDatabase.InvalidateAllCache();

        EditorUtility.ClearProgressBar();
    }
}
