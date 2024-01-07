using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using HarmonyLib;

using Kingmaker.Modding;

using MicroPatches.Patches;

using MicroUtils.Linq;

using Newtonsoft.Json;

using UnityModManagerNet;

namespace MicroPatches
{
#if DEBUG
    [EnableReloading]
#endif
    partial class Main
    {
        public const bool IsDebug =
#if DEBUG
            true;
#else
            false;
#endif

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

        internal readonly UnityModManager.ModEntry ModEntry;

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

        static readonly Lazy<MicroPatch[]> patches = new(() => patchClasses.Value.Select(p => MicroPatch.FromType(p.pc, p.t)).ToArray());
        public static IEnumerable<MicroPatch> Patches => patches.Value;

        public static IEnumerable<(MicroPatch.IPatchGroup group, MicroPatch[] patches)> PatchGroups => Patches
            .GroupBy(p => p.Group)
            .Select(g => (g.Key, g.ToArray()));

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            Instance = new(modEntry);

            MicroPatch.Logger = Logger;

            Instance.PrePatchTests();
            Instance.RunPatches();
            Instance.PostPatchTests();

            if (Patches.Any(p => p.IsEnabled() &&
                Instance.AppliedPatches.TryGetValue(p.PatchClass, out var applied) &&
                (applied is false)))
                CreateUI();

            return true;
        }

        static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            HarmonyInstance.UnpatchAll(modEntry.Info.Id);
            Instance = null!;
            return true;
        }

        public readonly Dictionary<Type, bool> AppliedPatches = new();

        void RunPatches()
        {
            var enabledPatches = Patches.Where(p => p.IsEnabled());

            RunPatches(enabledPatches.Where(p => !p.IsExperimental));

            Logger.Log("Running experimental patches");
            try
            {
                RunPatches(enabledPatches.Where(p => p.IsExperimental));
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        void RunPatches(IEnumerable<MicroPatch> patches)
        {
            foreach (var p in patches)
            {
                try
                {
                    Logger.Log($"Running patch class {p.PatchClass.Name}");
                    p.Patch.Patch();

                    AppliedPatches[p.PatchClass] = true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Exception in patch class {p.PatchClass.Name}");
                    Logger.LogException(ex);

                    AppliedPatches[p.PatchClass] = false;
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

        public bool GetPatchEnabled(MicroPatch patch)
        {
            if (EnabledPatches.TryGetValue(patch.PatchClass.Name, out var enabled))
            {
                return enabled;
            }

            if (patch.IsExperimental && !IsDebug)
                return false;
            
            return true;
        }

        public bool GetPatchApplied(MicroPatch patch) => AppliedPatches.TryGetValue(patch.PatchClass, out var applied) && applied;
    }
}