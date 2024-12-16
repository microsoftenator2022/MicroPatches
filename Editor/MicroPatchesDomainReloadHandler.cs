using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using HarmonyLib;

using MicroPatches;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using UnityEditor;

using UnityEngine;

[InitializeOnLoad]
public static class MicroPatchesDomainReloadHandler
{
    public const string HaromnyPatchCategoryName = "MicroPatches.EditorPatches";

    public static Harmony Harmony { get; private set; }

    static MicroPatchesDomainReloadHandler()
    {
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
    }

    static void OnBeforeAssemblyReload()
    {
        if (Harmony != null)
        {
            Debug.Log("Unpatching");
            Harmony.UnpatchCategory(HaromnyPatchCategoryName);
            Harmony = null;
        }
    }

    static void OnAfterAssemblyReload()
    {
        if (Harmony == null)
        {
            Harmony = new(Assembly.GetExecutingAssembly().GetName().Name);
            Debug.Log("Patching");
            Harmony.PatchCategory(HaromnyPatchCategoryName);
        }
    }
}
