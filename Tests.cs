using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using HarmonyLib;

using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem.Helpers;
using Kingmaker.Blueprints.Root;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Entities.Base;
using Kingmaker.Modding;
using Kingmaker.UI.Canvases;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Parts;

using MicroPatches.UGUI;

using Owlcat.Runtime.Core.Logging;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace MicroPatches
{
    internal partial class Main
    {
        void PrePatchTests()
        {
#if DEBUG

#endif
        }

        void PostPatchTests()
        {
#if DEBUG
            //try
            //{
            //    AccessTools.Method(typeof(UnityModManager), "CheckModUpdates")?.Invoke(null, []);
            //}
            //catch (Exception e)
            //{
            //    Logger.LogException(e);
            //}

            //Logger.Log($"{Patches.OwlcatModification_LoadAssemblies_Patch.TypeToGuidCache.Value?.Count} guids");
            //Logger.Log($"{Patches.OwlcatModification_LoadAssemblies_Patch.GuidToTypeCache.Value?.Count} types");

            CreateUI();
#endif
        }

        //[HarmonyPatch]
        //static class TestLoadOrder
        //{
        //    [HarmonyPatch(typeof(GuidClassBinder), nameof(GuidClassBinder.StartWarmingUp))]
        //    [HarmonyPostfix]
        //    static void GuidClassBinder_StartWarmingUp_Postfix()
        //    {
        //        Logger.Log(nameof(GuidClassBinder_StartWarmingUp_Postfix));
        //    }

        //    [HarmonyPatch(typeof(OwlcatModificationsManager), nameof(OwlcatModificationsManager.ApplyModifications))]
        //    [HarmonyPrefix]
        //    static void OwlcatModificationsManager_ApplyModifications_Prefix()
        //    {
        //        Logger.Log(nameof(OwlcatModificationsManager_ApplyModifications_Prefix));
        //    }

        //    [HarmonyPatch(typeof(GameMainMenu), nameof(GameMainMenu.Awake))]
        //    [HarmonyPostfix]
        //    static void MainMenu_Awake_Postfix()
        //    {

        //        Logger.Log($"{(new GuidClassBinder()).BindToType(Assembly.GetExecutingAssembly().GetName().Name, "186f54c7f41448c3a8c497d1df4b6bd8")}");
        //    }
        //}
    }
}

//[TypeId("186f54c7f41448c3a8c497d1df4b6bd8")]
//class MyComponent : BlueprintComponent
//{

//}

//[TypeId("acf7ee13d7b143129c589dad4d2e3e1a")]
//class MyAction : GameAction
//{
//    public override string GetCaption() => "My game action";
//    public override void RunAction() { }
//}

//class ClassesWithGuid
//{
//    public static List<(Type, string)> Classes = new List<(Type, string)>()
//    {
//        (typeof(MyComponent), "186f54c7f41448c3a8c497d1df4b6bd8")
//    };
//}
