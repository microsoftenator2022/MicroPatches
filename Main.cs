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

        public static UnityModManager.ModEntry.ModLogger Logger => Instance.ModEntry.Logger;

        readonly Lazy<(Type t, PatchClassProcessor pc)[]> PatchClasses = new(() =>
            AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly())
                .Select(t => (t, pc: HarmonyInstance.CreateClassProcessor(t)))
                .Where(tuple => tuple.pc.HasPatchAttribute())
                .ToArray());

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            Instance = new(modEntry);

            Instance.RunPatches();

            return true;
        }

        static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            HarmonyInstance.UnpatchAll(modEntry.Info.Id);
            Instance = null!;
            return true;
        }

        readonly Dictionary<Type, bool?> AppliedPatches = new();

        static bool IsExperimental(PatchClassProcessor pc) => pc.GetCategory() == "Experimental";

        void RunPatches()
        {
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