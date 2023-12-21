using HarmonyLib;

using Kingmaker.Modding;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IO;
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
        internal static class Category
        {
            internal const string Experimental = "Experimental";
            internal const string Hidden = "Hidden";
            internal const string Optional = "Optional";
        }
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

        static readonly Lazy<(Type t, PatchClassProcessor pc)[]> patchClasses = new(() =>
            AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly())
                .Select(t => (t, pc: HarmonyInstance.CreateClassProcessor(t)))
                .Where(tuple => tuple.pc.HasPatchAttribute())
                .ToArray());

        public static IEnumerable<(Type t, PatchClassProcessor pc)> PatchClasses => patchClasses.Value;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            Instance = new(modEntry);

            Instance.PrePatchTests();
            Instance.RunPatches();
            Instance.PostPatchTests();

            if (Instance.AppliedPatches.Any(p => p.Value is true))
                CreateUI();

            return true;
        }

        static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            HarmonyInstance.UnpatchAll(modEntry.Info.Id);
            Instance = null!;
            return true;
        }

        public readonly Dictionary<Type, bool?> AppliedPatches = new();

        public static bool IsExperimental(PatchClassProcessor pc) => pc.GetCategory() is Category.Experimental;
        public static bool IsHidden(PatchClassProcessor pc) => pc.GetCategory() is Category.Hidden;
        public static bool IsOptional(PatchClassProcessor pc) =>
#if DEBUG
            false;
#else
            pc.GetCategory() is Category.Optional or Category.Experimental;
#endif

        void RunPatches()
        {
            foreach (var pc in PatchClasses)
            {
                if (pc.t.GetCustomAttribute<MicroPatchAttribute>() is not { } attr && !IsHidden(pc.pc))
                    Logger.Warning($"Missing MicroPatch attribute for patch {pc.t.Name}");
            }

            RunPatches(PatchClasses.Where(tuple => !IsExperimental(tuple.pc)));

            Logger.Log("Running experimental patches");
            try
            {
                RunPatches(PatchClasses.Where(tuple => IsExperimental(tuple.pc)
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

        static Assembly GetHarmonyAss() => AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(ass => ass.GetName().Name == "0Harmony");

        static Version HarmonyVersion => GetHarmonyAss().GetName().Version;

        static string EnabledPatchesFilePath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "enabledPatches.json");

        Dictionary<string, bool>? enabledPatches;
        Dictionary<string, bool> EnabledPatches
        {
            get
            {
                if (enabledPatches is null && File.Exists(EnabledPatchesFilePath))
                {
                    try
                    {
                        enabledPatches = JsonConvert.DeserializeObject<Dictionary<string, bool>>(File.ReadAllText(EnabledPatchesFilePath));
                    }
                    catch (Exception e)
                    {
                        Logger.LogException(e);
                    }
                }

                return enabledPatches ??= new();
            }

            set
            {
                enabledPatches = value;

                File.WriteAllText(EnabledPatchesFilePath, JsonConvert.SerializeObject(enabledPatches));
            }
        }

        public void SetPatchEnabled(string name, bool enabled)
        {
            EnabledPatches[name] = enabled;
            EnabledPatches = EnabledPatches;
        }

        public bool GetPatchEnabled((Type, PatchClassProcessor) patch)
        {
            var (t, pc) = patch;

            if (EnabledPatches.TryGetValue(t.Name, out var enabled))
            {
                return enabled;
            }

            if (Main.IsExperimental(pc))
                return false;

            if (Main.IsOptional(pc))
                return true;

            return true;
        }
    }
}