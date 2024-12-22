using System;
using System.Collections;
using System.Collections.Generic;

using Kingmaker.Utility.EditorPreferences;

using Owlcat.Editor.Core.Utility;

using UnityEditor;

using UnityEngine;

public class MicroPatchesEditorPreferences
{

    static MicroPatchesEditorPreferences instance;

    public static MicroPatchesEditorPreferences Instance => instance ?? new();

    const string UseMicroPatchModeSettingsKey = "Template/UseMicroPatches";
    public bool UseMicroPatchMode { get; private set; } = true;
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
        this.UseMicroPatchMode = EditorPrefs.GetBool(UseMicroPatchModeSettingsKey, true);
    }

    public void Save()
    {
        EditorPrefs.SetBool(UseMicroPatchModeSettingsKey, this.UseMicroPatchMode);
    }

    [SettingsProvider]
    public static SettingsProvider CreateSettingsProvider()
    {
        var provider = new SettingsProvider("Preferences/MicroPatches", SettingsScope.User, new[] { "MicroPatches "})
        {
            guiHandler = _ => {
                using (GuiScopes.LabelWidth(300))
                {
                    Instance.UseMicroPatchMode = EditorGUILayout.Toggle("Use MicroPatches mode for 'Save As Patch'", Instance.UseMicroPatchMode);
                    Instance.LoadBlueprintsAsInBuild = EditorGUILayout.Toggle("(Owlcat) Load blueprints as in build", Instance.LoadBlueprintsAsInBuild);
                    Instance.LoadAssetsFromBundles = EditorGUILayout.Toggle("(Owlcat) Load assets from bundles", Instance.LoadAssetsFromBundles);
                }
                if (GUI.changed)
                {
                    Instance.Save();
                    EditorPreferences.Instance.Save();
                }
            }
        };

        return provider;
    }
}
