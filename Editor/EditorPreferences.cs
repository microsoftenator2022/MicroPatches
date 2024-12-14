using System;
using System.Collections;
using System.Collections.Generic;

using Owlcat.Editor.Core.Utility;

using UnityEditor;

using UnityEngine;

public class MicroPatchesEditorPreferences
{

    static MicroPatchesEditorPreferences instance;

    public static MicroPatchesEditorPreferences Instance => instance ?? new();

    const string UseMicroPatchModeSettingsKey = "Template/UseMicroPatches";
    public bool UseMicroPatchMode { get; private set; } = true;

    private MicroPatchesEditorPreferences()
    {
        this.UseMicroPatchMode = EditorPrefs.GetBool(UseMicroPatchModeSettingsKey, true);
    }

    public void Save()
    {
        EditorPrefs.SetBool(UseMicroPatchModeSettingsKey, this.UseMicroPatchMode);
    }

    [SettingsProvider]
    public static SettingsProvider CreateMyCustomSettingsProvider()
    {
        var provider = new SettingsProvider("Preferences/MicroPatches", SettingsScope.User, new[] { "MicroPatches "})
        {
            guiHandler = _ => {
                using (GuiScopes.LabelWidth(300))
                    Instance.UseMicroPatchMode = EditorGUILayout.Toggle("Use MicroPatches mode for 'Save As Patch'", Instance.UseMicroPatchMode);

                if (GUI.changed)
                    Instance.Save();
            }
        };

        return provider;
    }
}
