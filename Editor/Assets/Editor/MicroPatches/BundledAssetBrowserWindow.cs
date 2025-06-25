using System;
using System.Collections;
using System.Collections.Generic;

using HarmonyLib;

using Kingmaker.Blueprints.JsonSystem.Converters;

using RogueTrader.SharedTypes;

using UnityEditor;

using UnityEngine;

public class BundledAssetBrowserWindow : EditorWindow
{
    static BlueprintReferencedAssets directReferencedAssets;
    static readonly HashSet<(string, long)> m_EntryKeys = new();
    
    void OnEnable()
    {
        if (!GameServices.Started)
            GameServices.StartGameServices();

        directReferencedAssets = UnityObjectConverter.AssetList;

        var assetListEntry = typeof(BlueprintReferencedAssets).GetNestedType("Entry", AccessTools.all);
        var assetIdField = AccessTools.Field(assetListEntry, "AssetId");
        var fileIdField = AccessTools.Field(assetListEntry, "FileID");

        var entries = (IEnumerable)assetListEntry.GetField("m_Entries").GetValue(directReferencedAssets);

        foreach (var entry in entries)
        {
            var assetId = (string)assetIdField.GetValue(entry);
            var fileId = (long)fileIdField.GetValue(entry);

            m_EntryKeys.Add((assetId, fileId));
        }
    }
}
