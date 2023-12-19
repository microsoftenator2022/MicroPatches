#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using UnityEngine;

using UnityModManagerNet;

namespace MicroPatches
{
    partial class Main
    {
        void OnGUI(UnityModManager.ModEntry _)
        {
            GUILayout.BeginHorizontal();
            //            {
            //                GUILayout.BeginVertical();
            //                {
            //                    GUILayout.Label("Patches Status");

            //                    var font = GUI.skin.label.font;

            //                    foreach (var (t, pc) in PatchClasses)
            //                    {
            //                        var name = t.Name;

            //                        AppliedPatches.TryGetValue(t, out var applied);

            //                        if (t.GetCustomAttribute<MicroPatchAttribute>() is { } attr)
            //                            name = attr.Name;

            //                        GUILayout.BeginHorizontal();
            //                        {
            //                            GUILayout.Label((applied ?? false) ? "OK" : "KO");

            //                            if (IsExperimental(pc))
            //                            {
            //                                var enabled = GetPatchEnabled(t.Name);
            //                                var toggle = GUILayout.Toggle(
            //#if DEBUG
            //                                    true,
            //#else
            //                                    enabled,
            //#endif
            //                                    $"{name} (Experimental)");
            //#if !DEBUG
            //                                if (toggle != enabled)
            //                                    SetPatchEnabled(t.Name, toggle);
            //#endif
            //                            }
            //                            else
            //                            {
            //                                GUILayout.Label(name);
            //                            }

            //                            GUILayout.FlexibleSpace();
            //                        }
            //                        GUILayout.EndHorizontal();
            //                    }
            //                }
            //                GUILayout.EndVertical();

            //                GUILayout.FlexibleSpace();

            //                GUILayout.BeginVertical();
            //                {
            //                    //static bool IsVersionMisMatch() => HarmonyVersion < UmmHarmonyVersion;

            //                    GUILayout.BeginHorizontal();
            //                    {
            //                        GUILayout.Label($"Harmony version: {HarmonyVersion}");

            //                        //if (IsVersionMisMatch() && ModEntry!.Active)
            //                        //{
            //                        //    if (GUILayout.Button("Update"))
            //                        //    {
            //                        //        ModEntry!.OnToggle = (_, value) => !value;
            //                        //        ModEntry!.Active = false;
            //                        //        ModEntry!.Info.DisplayName = $"{ModEntry!.Info.DisplayName} - RESTART REQUIRED";

            //                        //        ReplaceHarmony(GetHarmonyAss().Location, UmmHarmonyPath);
            //                        //    }
            //                        //}
            //                    }
            //                    GUILayout.EndHorizontal();

            //                    //GUILayout.Label($"UMM Harmony Version: {UmmHarmonyVersion}");
            if (GUILayout.Button("Configure"))
            {
                if (Main.UIWindow == null)
                    CreateUI();

                UnityModManager.UI.Instance.ToggleWindow(false);
            }
            //    }
            //    GUILayout.EndVertical();
            //}

            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();
        }
    }
}
