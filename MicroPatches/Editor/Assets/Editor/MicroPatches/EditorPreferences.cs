using System;
using System.Collections;
using System.Collections.Generic;

using Kingmaker.Utility.EditorPreferences;

using Owlcat.Editor.Core.Utility;

using UnityEditor;

using UnityEngine;

public class MicroPatchesEditorPreferences
{
    public static MicroPatchesEditorPreferences Instance { get; } = new();

    const string UseMicroPatchModeSettingsKey = "Template/UseMicroPatches";
    const string GameServicesAutoStartSettingsKey = "Template/GameServicesAutoStart";

    public bool UseMicroPatchMode { get; private set; }
    public bool GameServicesAutoStart { get; private set; }

    public bool LoadBlueprintsAsInBuild
    {
        get => EditorPreferences.Instance.LoadBlueprintsAsInBuild;
        set => EditorPreferences.Instance.LoadBlueprintsAsInBuild = value;
    }
    public bool LoadAssetsFromBundles
    {
        get => EditorPreferences.Instance.LoadAssetsFromBundles;
        set => EditorPreferences.Instance.LoadAssetsFromBundles = value;
    }

    private MicroPatchesEditorPreferences()
    {
        this.UseMicroPatchMode = EditorPrefs.GetBool(UseMicroPatchModeSettingsKey, false);
        this.GameServicesAutoStart = EditorPrefs.GetBool(GameServicesAutoStartSettingsKey, true);
    }

    public void Save()
    {
        EditorPrefs.SetBool(UseMicroPatchModeSettingsKey, this.UseMicroPatchMode);
        EditorPrefs.SetBool(GameServicesAutoStartSettingsKey, this.GameServicesAutoStart);
    }

    private static void GUIHandler()
    {
        using (GuiScopes.LabelWidth(400))
        {
            Instance.UseMicroPatchMode = EditorGUILayout.Toggle("Use MicroPatches mode for 'Save As Patch' (experimental)", Instance.UseMicroPatchMode);
            Instance.GameServicesAutoStart = EditorGUILayout.Toggle("Autostart game services", Instance.GameServicesAutoStart);
            
            EditorGUILayout.Separator();
            
            EditorGUILayout.LabelField("Required (and enabled by) Game Services:");

            using (GuiScopes.Indent())
            {
                GUI.enabled = !Instance.GameServicesAutoStart;
                Instance.LoadBlueprintsAsInBuild = EditorGUILayout.Toggle("(Owlcat) Load blueprints as in build",
                    Instance.GameServicesAutoStart || Instance.LoadBlueprintsAsInBuild);

                Instance.LoadAssetsFromBundles = EditorGUILayout.Toggle("(Owlcat) Load assets from bundles",
                    Instance.GameServicesAutoStart || Instance.LoadAssetsFromBundles);
            }
            GUI.enabled = true;   
        }
        if (GUI.changed)
        {
            Instance.Save();
            EditorPreferences.Instance.Save();
        }
    }

    [SettingsProvider]
    public static SettingsProvider CreateSettingsProvider()
    {
        var provider = new SettingsProvider("Preferences/MicroPatches", SettingsScope.User, new[] { "MicroPatches "})
        {
            guiHandler = static _ => { GUIHandler(); }
        };

        return provider;
    }
}
