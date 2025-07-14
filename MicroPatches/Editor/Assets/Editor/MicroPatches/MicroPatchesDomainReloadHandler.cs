using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using HarmonyLib;

using Kingmaker.Blueprints.JsonSystem.EditorDatabase;

using MicroPatches;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using UnityEditor;

using UnityEngine;

[InitializeOnLoad]
public static class MicroPatchesDomainReloadHandler
{
    //public const string HarmonyPatchCategoryName = "MicroPatches.EditorPatches";

    public static Harmony Harmony { get; private set; }

    static MicroPatchesDomainReloadHandler()
    {
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
    }

    public static event Action BeforeAssemblyReload;

    static void OnBeforeAssemblyReload()
    {
        try
        {
            try
            {
                BeforeAssemblyReload?.Invoke();

                BlueprintsDatabase.InvalidateAllCache();
            }
            finally
            {
                GameServices.Reset(true);
            }
        }
        finally
        {
            if (Harmony != null)
            {
                //Debug.Log("Unpatching");
                Harmony.UnpatchAll(Harmony.Id);
                Harmony = null;
            }
        }
    }

    public static event Action AfterAssemblyReload;

    static void OnAfterAssemblyReload()
    {
        if (Harmony == null)
        {
            Harmony = new(Assembly.GetExecutingAssembly().GetName().Name);
            //Debug.Log("Patching");
            Harmony.PatchAll();
            Harmony.PatchAll(typeof(GameServices).Assembly);
            PatchRunner.RunPatches(Harmony);
        }

        if (MicroPatchesEditorPreferences.Instance.GameServicesAutoStart &&
            !GameServices.Started &&
            !GameServices.Starting &&
            !GameServices.Canceled)
            GameServices.StartGameServices();

        AfterAssemblyReload?.Invoke();
    }
}
