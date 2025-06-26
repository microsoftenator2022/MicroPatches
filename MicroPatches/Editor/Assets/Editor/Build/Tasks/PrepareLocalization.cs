using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Kingmaker;
using Kingmaker.Localization;
using Kingmaker.Localization.Enums;
using Kingmaker.Localization.Shared;
using Kingmaker.Utility.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using OwlcatModification.Editor.Build.Context;

using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;

namespace OwlcatModification.Editor.Build.Tasks
{
    public class PrepareLocalization : IBuildTask
    {
#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        private IBuildParameters m_BuildParameters;

        [InjectContext(ContextUsage.In)]
        private IModificationParameters m_ModificationParameters;
#pragma warning restore 649

        public int Version
            => 1;

        #region MicroPatches
        void AddLocalizedStringsFromBlueprints()
        {
            var strings = new List<LocalizedStringData>();

            foreach (var f in Directory.EnumerateFiles(m_ModificationParameters.BlueprintsPath, "*.*", SearchOption.AllDirectories)
                .Where(p => Path.GetExtension(p) is ".jbp" or ".patch" or ".jbp_patch"))
            {
                var text = File.ReadAllText(f);
                var root = JObject.Parse(text);

                foreach (var node in root.DescendantsAndSelf().OfType<JObject>())
                {
                    if (node["m_Key"]?.ToString() is string key && node["m_JsonPath"]?.ToString() is string path)
                    {
                        PFLog.Build.Log($"Found localized string {node}");

                        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(path))
                            continue;

                        strings.Add(JsonConvert.DeserializeObject<LocalizedStringData>(File.ReadAllText(path), LocalizedString.Settings));
                    }
                }
            }

            var serializer = JsonSerializer.Create(new JsonSerializerSettings((JsonConvert.DefaultSettings ?? (() => new JsonSerializerSettings()))())
            { Formatting = Formatting.Indented });

            PFLog.Build.Log($"{strings.Count} strings");
            //var serializer = new JsonSerializer();

            foreach (var locale in Enum.GetValues(typeof(Locale)).Cast<Locale>())
            {
                if (!strings.Any(s => s.GetLocale(locale) is not null))
                    continue;

                var pack = new LocalizationPack();

                foreach (var s in strings)
                {
                    if (!s.TryGetText(locale, out string text))
                        continue;

                    pack.PutString(s.Key, text);
                }

                var path = Path.Combine(m_BuildParameters.GetOutputFilePathForIdentifier(BuilderConsts.OutputLocalization), Enum.GetName(typeof(Locale), locale) + ".json");

                if (File.Exists(path))
                {
                    pack.AddStrings(serializer.DeserializeObject<LocalizationPack>(File.ReadAllText(path)));
                }

                File.WriteAllText(path, serializer.SerializeObject(pack));
            }
        }
        #endregion

        public ReturnCode Run()
        {
            string[] localeFiles = Enum.GetNames(typeof(Locale)).Select(i => i + ".json").ToArray();

            string originDirectory = m_ModificationParameters.LocalizationPath;
            string destinationDirectory = m_BuildParameters.GetOutputFilePathForIdentifier(BuilderConsts.OutputLocalization);

            BuilderUtils.CopyFilesWithFoldersStructure(
                originDirectory, destinationDirectory, SearchOption.TopDirectoryOnly, i =>
                {
                    string filename = Path.GetFileName(i);
                    return localeFiles.Contains(filename);
                });

            #region MicroPatches
            AddLocalizedStringsFromBlueprints();
            #endregion

            return ReturnCode.Success;
        }
    }
}