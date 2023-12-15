using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityModManagerNet;

namespace MicroPatches
{
#if DEBUG
    [EnableReloading]
#endif
    partial class Main
    {
        internal const string ExperimentalCategory = "Experimental";

        internal static Main Instance = null!;

        internal Main(UnityModManager.ModEntry modEntry)
        {
            ModEntry = modEntry;
            this.Harmony = new Harmony(modEntry.Info.Id);

            modEntry.OnUnload = OnUnload;
            modEntry.OnGUI = OnGUI;
        }

        readonly Harmony Harmony;

        public static Harmony HarmonyInstance => Instance.Harmony;

        readonly UnityModManager.ModEntry ModEntry;

        private static UnityModManager.ModEntry.ModLogger Logger => Instance.ModEntry.Logger;

        public static void PatchLog(string patchName, string message) => Logger.Log($"[MicroPatch {patchName}] {message}");
        public static void PatchError(string patchName, string message) => Logger.Error($"[MicroPatch {patchName}] {message}");
        public static void PatchWarning(string patchName, string message) => Logger.Warning($"[MicroPatch {patchName}] {message}");
        public static void PatchLogException(Exception ex) => Logger.LogException(ex);

        readonly Lazy<(Type t, PatchClassProcessor pc)[]> PatchClasses = new(() =>
            AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly())
                .Select(t => (t, pc: HarmonyInstance.CreateClassProcessor(t)))
                .Where(tuple => tuple.pc.HasPatchAttribute())
                .ToArray());

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            Instance = new(modEntry);

            Instance.RunPatches();

            //try
            //{
            //    AccessTools.Method(typeof(UnityModManager), "CheckModUpdates")?.Invoke(null, []);
            //}
            //catch (Exception e)
            //{
            //    Logger.LogException(e);
            //}

            return true;
        }

        static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            HarmonyInstance.UnpatchAll(modEntry.Info.Id);
            Instance = null!;
            return true;
        }

        readonly Dictionary<Type, bool?> AppliedPatches = new();

        static bool IsExperimental(PatchClassProcessor pc) => pc.GetCategory() == ExperimentalCategory;

        void RunPatches()
        {
            foreach (var pc in PatchClasses.Value)
            {

                if (pc.t.GetCustomAttribute<MicroPatchAttribute>() is not { } attr)
                    Logger.Warning($"Missing MicroPatch attribute for patch {pc.t.Name}");

            }

            RunPatches(PatchClasses.Value.Where(tuple => !IsExperimental(tuple.pc)));

            Logger.Log("Running experimental patches");
            try
            {
                RunPatches(PatchClasses.Value.Where(tuple => IsExperimental(tuple.pc)
#if !DEBUG
                && EnabledPatches.TryGetValue(tuple.t.Name, out var enabled) && enabled
#endif
                ));
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        void RunPatches(IEnumerable<(Type, PatchClassProcessor)> typesAndPatches)
        {
            foreach (var (t, pc) in typesAndPatches)
            {
                try
                {
                    Logger.Log($"Running patch class {t.Name}");
                    pc.Patch();

                    AppliedPatches[t] = true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Exception in patch class {t.Name}");
                    Logger.LogException(ex);

                    AppliedPatches[t] = false;
                }
            }
        }
    }
}