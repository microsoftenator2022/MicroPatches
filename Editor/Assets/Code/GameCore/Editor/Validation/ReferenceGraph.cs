using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Kingmaker.AreaLogic.Cutscenes;
using Kingmaker.AreaLogic.Etudes;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Area;
using Kingmaker.Blueprints.JsonSystem.EditorDatabase;
using Kingmaker.Blueprints.JsonSystem.PropertyUtility;
using Kingmaker.Blueprints.Quests;
using Owlcat.QA.Validation;
using Kingmaker.DialogSystem.Blueprints;
using Kingmaker.Utility.EditorPreferences;
using Owlcat.Runtime.Core.Utility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kingmaker.Editor.Validation
{
    [InitializeOnLoad]
    public class ReferenceGraph
    {
        public enum ValidationStateType
        {
            Normal,
            Warning,
            Error
        }

        public class Entry
        {
            public string ObjectGuid;
            public string ObjectName;
            public string ObjectType;
            public string OwnerName;

            public List<Ref> References;

            public int FullReferencesMask => References == null || References.Count == 0
                ? 0
                : References.Select(r => r.ReferenceTypeMask).Aggregate((a, b) => a | b);

            public string ValidationResult;
            public ValidationStateType ValidationState;
        }

        public class SceneEntity
        {
            public string GUID;
            public List<Ref> Refs = new List<Ref>();
        }

        public class Ref
        {
            public string AssetPath;
            public string AssetType;
            public string ReferencingObjectName;
            public string ReferencingObjectType;
            //public string TransformPath; // for scene references, path to referencing obj transform
            public int ReferenceTypeMask;
            public string RefChasingAssetGuid;
            public bool IsScene => AssetPath.EndsWith(".unity");

            public string AssetGuid
                => AssetPath.StartsWith("Assets")
                    ? AssetDatabase.AssetPathToGUID(AssetPath)
                    : BlueprintsDatabase.PathToId(AssetPath);
        }

        public class EntityRef
        {
            public string AssetPath;
            public string AssetName;
            public string UsagesType;
        }

        public readonly List<Entry> Entries = new List<Entry>();
        public readonly List<SceneEntity> SceneEntitys = new List<SceneEntity>();
        public static bool IsReferenceTrackingSuppressed { get; set; }
        private List<string> m_ReferencingBlueprintPaths;
        private List<string> m_ReferencingScenesPaths;
        private Dictionary<string, Entry> m_EntriesByGuid;
        private Dictionary<string, SceneEntity> m_SceneObjectRefs;
        private readonly Dictionary<string, string> m_TypeNamesByGuid = new Dictionary<string, string>();

        class RelevantTypeData
        {
            public Type Type;
            public ReferenceAnalyzerBase Analyzer;
            public ReferenceValidatorBase Validator;
            public Func<int, string> MaskDecoder;
        }

        private static List<RelevantTypeData> s_RelevantTypes = new List<RelevantTypeData>
        {
            new RelevantTypeData
            {
                Type = typeof(BlueprintSummonPool),
                Analyzer = new ReferenceAnalyzerSummonPool(),
                Validator = new ReferenceValidatorSummonPool(),
                MaskDecoder = i => ((SummonPoolReferenceType)i).ToString(),
            },
            new RelevantTypeData
            {
                Type = typeof(BlueprintUnlockableFlag),
                Analyzer = new ReferenceAnalyzerUnlock(),
                Validator = new ReferenceValidatorUnlocks(),
                MaskDecoder = i => ((UnlockableFlagReferenceType)i).ToString(),
            },
            new RelevantTypeData
            {
                Type = typeof(BlueprintQuest),
                Analyzer = new ReferenceAnalyzerQuest(),
                Validator = new ReferenceValidatorQuests(),
                MaskDecoder = i => ((QuestReferenceType)i).ToString(),
            },
            new RelevantTypeData
            {
                Type = typeof(BlueprintQuestObjective),
                Analyzer = new ReferenceAnalyzerQuestObjective(),
                Validator = new ReferenceValidatorQuestObjectives(),
                MaskDecoder = i => ((QuestObjectiveReferenceType)i).ToString(),
            },
            new RelevantTypeData
            {
                Type = typeof(BlueprintDialog),
                Analyzer = new ReferenceAnalyzerDialog(),
                Validator = new ReferenceValidatorDialog(),
                MaskDecoder = i => ((DialogReferenceType)i).ToString(),
            },
            new RelevantTypeData
            {
                Type = typeof(Cutscene),
                Analyzer = new ReferenceAnalyzerCutscene(),
                Validator = new ReferenceValidatorCutscene(),
                MaskDecoder = i => i > 0 ? "Played" : "None",
            },
            new RelevantTypeData
            {
                Type = typeof(BlueprintAreaEnterPoint),
                Analyzer = new ReferenceAnalyzerEnterPoint(),
                Validator = new ReferenceValidatorEnterPoint(),
                MaskDecoder = i => i > 0 ? "Used" : "None",
            },
            new RelevantTypeData
            {
                Type = typeof(BlueprintEtude),
                Analyzer = new ReferenceAnalyzerEtude(),
                Validator = new ReferenceValidatorEtude(),
                MaskDecoder = i => ((EtudeReferenceType)i).ToString(),
            }
        };

        static ReferenceGraph m_ReferenceGraph;

        #region MicroPatches
        //static ReferenceGraph()
        //{
        //    BlueprintsDatabase.OnPreSave += id => Graph?.CleanReferencesInBlueprintWithId(id);
        //    BlueprintsDatabase.OnSavedId += id => Graph?.ParseFileWithId(id);
        //    IsReferenceTrackingSuppressed = EditorPreferences.Instance.SuppressReferenceTracking;
        //}

        static ReferenceGraph()
        {
	        BlueprintsDatabase.OnPreSave += id => Graph?.CleanReferencesInBlueprintWithId(id);
	        BlueprintsDatabase.OnSavedId += id => Graph?.ParseFileWithId(id);
            IsReferenceTrackingSuppressed = EditorPreferences.Instance.SuppressReferenceTracking;
        }

        [InitializeOnLoadMethod]
        static void InitGraph()
        {
            if (!File.Exists("references.xml"))
            {
                Debug.LogWarning("references.xml does not exist. Creating reference graph.");
                CollectMenu();
            }
        }
        #endregion

        public static ReferenceGraph Graph
        {
            get
            {
                if (m_ReferenceGraph == null)
                {
                    m_ReferenceGraph = Load();
                }

                return m_ReferenceGraph;
            }
        }

        [MenuItem("Tools/DREAMTOOL/Collect reference data")]
        public static void CollectMenu()
        {
            try
            {
                var rg = new ReferenceGraph();
                rg.CollectRelevantObjects();
                EditorUtility.DisplayProgressBar("Collecting potential referencing paths", "Blueprints", 0);
                rg.CollectPotentialBlueprints();
                EditorUtility.DisplayProgressBar("Collecting potential referencing paths", "Scenes", 0);
                rg.CollectPotentialScenes();

                rg.m_EntriesByGuid = rg.Entries.ToDictionary(e => e.ObjectGuid);

                rg.CollectBlueprintReferencesPaths();
                rg.CollectSceneReferencesPaths();

                rg.FixupScriptGuids();
                EditorUtility.DisplayProgressBar("Saving ref graph", "references.xml", 0);
                rg.Save();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Tools/DREAMTOOL/Analyze references in blueprints")]
        public static void AnalyzeRefs()
        {
            try
            {
                var rg = Load();
                rg.AnalyzeReferencesInBlueprints();
                EditorUtility.DisplayProgressBar("Saving ref graph", "references.xml", 0);
                rg.Save();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Tools/DREAMTOOL/Analyze references in scenes")]
        public static void AnalyzeRefsScenes()
        {
            try
            {
                var rg = Load();
                rg.AnalyzeReferencesInScenes();
                EditorUtility.DisplayProgressBar("Saving ref graph", "references.xml", 0);
                rg.Save();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Tools/DREAMTOOL/FindOwners")]
        public static void FindAssetOwners()
        {
            try
            {
                var rg = Load();
                foreach (var bap in BlueprintsDatabase.LoadAllOfType<BlueprintArea>())
                {
                    BlueprintToOwnerMatcher.Instance.AddAreaToSceneCache(bap);
                }

                BlueprintToOwnerMatcher.Instance.ReferenceGraph = rg;

                for (int ii = 0; ii < rg.Entries.Count; ii++)
                {
                    var entry = rg.Entries[ii];
                    if (ii % 20 == 0)
                        EditorUtility.DisplayProgressBar("Matching assets", entry.ObjectName, (float)ii / rg.Entries.Count);

                    Debug.Log($"Load {entry.ObjectType}: {entry.ObjectName}");
                    var asset = BlueprintsDatabase.LoadById<BlueprintScriptableObject>(entry.ObjectGuid);
                    if (asset)
                    {
                        entry.OwnerName = BlueprintToOwnerMatcher.Instance.TryMatchToOwner(asset);
                    }
                }

                EditorUtility.DisplayProgressBar("Saving ref graph", "references.xml", 0);
                BlueprintToOwnerMatcher.Instance.SaveCache();

                rg.Save();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static string EntryRefMaskToString(Entry entry, int mask)
        {
            return GetTypeData(entry)?.MaskDecoder?.Invoke(mask) ?? "<unknown type>";
        }

        public void Save()
        {
            using (var sw = new StreamWriter("references.xml"))
            {
                var xs = new XmlSerializer(typeof(ReferenceGraph));
                xs.Serialize(sw, this);
            }
        }

        public static ReferenceGraph Reload()
        {
            m_ReferenceGraph = Load();
            return m_ReferenceGraph;
        }

        static ReferenceGraph Load()
        {
            using (var sr = new StreamReader("references.xml"))
            {
                var xs = new XmlSerializer(typeof(ReferenceGraph));
                return xs.Deserialize(sr) as ReferenceGraph;
            }
        }

        public void CollectRelevantObjects()
        {
            Entries.Clear();
            try
            {
                for (int ii = 0; ii < s_RelevantTypes.Count; ii++)
                {
                    var data = s_RelevantTypes[ii];
                    EditorUtility.DisplayProgressBar("Collecting relevant objects", data.Type.Name, (float)ii / s_RelevantTypes.Count);

                    if (data.Type.IsSubclassOf(typeof(UnityEngine.Object)))
                    {
                        string filter = " t:" + data.Type.Name;
                        var guids = AssetDatabase.FindAssets(filter);
                        foreach (var guid in guids)
                        {
                            Entries.Add(
                                new Entry
                                {
                                    ObjectGuid = guid,
                                    ObjectName = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)),
                                    ObjectType = data.Type.Name,
                                    References = new List<Ref>()
                                });
                        }
                    }
                    else
                    {
                        var search = BlueprintsDatabase.SearchByType(data.Type);
                        foreach (var pair in search)
                        {
                            Entries.Add(
                                new Entry
                                {
                                    ObjectGuid = pair.Item1,
                                    ObjectName = Path.GetFileNameWithoutExtension(pair.Item2),
                                    ObjectType = data.Type.Name,
                                    References = new List<Ref>()
                                });
                        }
                    }


                    // also add blueprints
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public void CollectPotentialBlueprints()
        {
            // todo: maybe just FindFiles will be better?
            m_ReferencingBlueprintPaths = BlueprintsDatabase.SearchByType(typeof(SimpleBlueprint)).Select(p=>p.Item2).ToList();
        }
        
        public void CollectPotentialScenes()
        {
            try
            {
                m_ReferencingScenesPaths = new List<string>();
                foreach (var bap in BlueprintsDatabase.LoadAllOfType<BlueprintAreaPart>())
                {
                    if (bap)
                    {
                        m_ReferencingScenesPaths.Add(bap.DynamicScene?.ScenePath);
                    }

                    if (bap is BlueprintArea area)
                    {
                        BlueprintToOwnerMatcher.Instance.AddAreaToSceneCache(area);
                    }
                }
                foreach (var bap in BlueprintsDatabase.LoadAllOfType<BlueprintAreaMechanics>())
                {
                    if (bap)
                    {
                        m_ReferencingScenesPaths.Add(bap.Scene?.ScenePath);
                    }
                }
                
                m_ReferencingScenesPaths.RemoveAll(p => p == null);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public void MatchToOwners()
        {
            BlueprintToOwnerMatcher.Instance.ReferenceGraph = this;
            try
            {
                for (int ii = 0; ii < Entries.Count; ii++)
                {
                    var entry = Entries[ii];
                    if (ii % 20 == 0)
                        EditorUtility.DisplayProgressBar("Matching assets to owners", entry.ObjectName, (float)ii / Entries.Count);
                    
                    var asset = BlueprintsDatabase.LoadById<BlueprintScriptableObject>(entry.ObjectGuid);
                    if (asset)
                    {
                        entry.OwnerName = BlueprintToOwnerMatcher.Instance.TryMatchToOwner(asset);
                    }
                }
            }
            finally
            {
                BlueprintToOwnerMatcher.Instance.SaveCache();
                EditorUtility.ClearProgressBar();
            }
        }

        private int m_Counter;

        public void CollectBlueprintReferencesPaths()
        {
            try
            {
                m_EntriesByGuid = m_EntriesByGuid ?? Entries.ToDictionary(e => e.ObjectGuid);
                RunParallel(
                    8,
                    () => CollectReferencesPathsFromBluerpintJob(m_ReferencingBlueprintPaths),
                    "Collecting referencing blueprints",
                    m_ReferencingBlueprintPaths.Count);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public void CollectSceneReferencesPaths()
        {
            try
            {
                m_EntriesByGuid = m_EntriesByGuid ?? Entries.ToDictionary(e => e.ObjectGuid);
                RunParallel(
                    8,
                    () => CollectReferencesPathsJob(m_ReferencingScenesPaths),
                    "Collecting referencing scenes",
                    m_ReferencingScenesPaths.Count);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        void RunParallel(int threads, Action task, string desc, int length)
        {
            var ts = new Task[threads];
            m_Counter = 0;
            for (int ii = 0; ii < threads; ii++)
            {
                ts[ii] = new Task(task);
                ts[ii].Start();
            }
            while (ts.Any(t => !t.IsCompleted))
            {
                EditorUtility.DisplayProgressBar(desc, $"Done: {m_Counter}/{length}", (float)m_Counter / length);
                Thread.Sleep(500);
            }
            for (int ii = 0; ii < threads; ii++)
            {
                ts[ii].Wait();
            }
        }

        void CollectReferencesPathsJob(List<string> paths)
        {
            m_SceneObjectRefs = m_SceneObjectRefs ?? SceneEntitys.ToDictionary(x => x.GUID);
            var parser = new ReferenceFileParser();
            while (m_Counter < paths.Count)
            {
                var ii = Interlocked.Increment(ref m_Counter) - 1;
                if (ii >= paths.Count)
                    return;

                var assetPath = paths[ii];

                var assetType = assetPath.EndsWith(".unity") ? "Scene" : null;

                parser.ParseFile(assetPath,
                    () =>
                    {
                        var objType = parser.ScriptGuid;
                        assetType = assetType ?? objType; // if we're in an asset, first encountered script type is the asset type
                        foreach (var obj in parser.Refs)
                        {
                            var objRef = new Ref
                            {
                                AssetPath = assetPath,
                                AssetType = assetType,
                                ReferencingObjectName = parser.ObjectName,
                                ReferencingObjectType = objType
                            };

                            ProcessRef(obj, objRef);
                        }
                    });
            }
        }

        void CollectReferencesPathsFromBluerpintJob(List<string> paths)
        {
            m_SceneObjectRefs = m_SceneObjectRefs ?? SceneEntitys.ToDictionary(x => x.GUID);
            var parser = new JsonReferenceExtractor();
            while (m_Counter < paths.Count)
            {
                var ii = Interlocked.Increment(ref m_Counter) - 1;
                if (ii >= paths.Count)
                    return;

                var assetPath = paths[ii];
                ParseFile(parser, assetPath);
            }
        }

        private void ParseFile(JsonReferenceExtractor parser, string assetPath)
        {
            parser.ParseFile(assetPath,
                () =>
                {
                    var objType = parser.ScriptGuid;
                    foreach (var obj in parser.Refs)
                    {
                        var objRef = new Ref
                        {
                            AssetPath = assetPath,
                            AssetType = objType,
                            ReferencingObjectName = parser.ObjectName,
                            ReferencingObjectType = objType
                        };

                        ProcessRef(obj, objRef);
                    }
                }, false);
        }
        
        private void ParseFileWithId(string id)
        {
	        if (IsReferenceTrackingSuppressed)
	        {
		        return;
	        }
	        
            var parser = new JsonReferenceExtractor();
            ParseFile(parser, BlueprintsDatabase.IdToPath(id));
            Graph.Save();
        }

        public void CleanReferencesInBlueprintWithId(string id)
        {
	        if (IsReferenceTrackingSuppressed)
	        {
		        return;
	        }
	        
	        var assetPath = BlueprintsDatabase.IdToPath(id);
            var parser = new JsonReferenceExtractor();
            parser.ParseFile(assetPath,
                () =>
                {
                    var objType = parser.ScriptGuid;
                    foreach (var obj in parser.Refs)
                    {
                        var objRef = new Ref
                        {
                            AssetPath = assetPath,
                            AssetType = objType,
                            ReferencingObjectName = parser.ObjectName,
                            ReferencingObjectType = objType
                        };

                        CleanRef(obj, objRef);
                    }
                }, false);
        }

        void ProcessRef(ObjRef obj, Ref objRef)
		{
            switch (obj.RefType)
			{
                case RefType.Asset:
                    AddBlueprintRef(obj, objRef);
                    break;

                case RefType.SceneObject:
                    AddSceneObjectRef(obj, objRef);
                    break;

                default:
                    throw new Exception("Undefined ref type");
			}
		}

        void AddBlueprintRef(ObjRef obj, Ref objRef)
        {
            m_EntriesByGuid = m_EntriesByGuid ?? Entries.ToDictionary(e => e.ObjectGuid);
            if (!m_EntriesByGuid.TryGetValue(obj.Guid, out var entry))
                return;

            lock (entry.References)
            {
                entry.References.Add(objRef);
            }
        }

        void AddSceneObjectRef(ObjRef obj, Ref objRef)
        {
            m_SceneObjectRefs = m_SceneObjectRefs ?? SceneEntitys.ToDictionary(x => x.GUID);
            lock (m_SceneObjectRefs)
            {
                if (!m_SceneObjectRefs.TryGetValue(obj.Guid, out var sceneEntity))
                {
                    sceneEntity = new SceneEntity() { GUID = obj.Guid };
                    m_SceneObjectRefs.Add(obj.Guid, sceneEntity);
                    SceneEntitys.Add(sceneEntity);
                }

                lock (sceneEntity.Refs)
                {
                    sceneEntity.Refs.Add(objRef);
                }
            }
        }
        
        private void CleanRef(ObjRef obj, Ref objRef)
        {
            switch (obj.RefType)
            {
                case RefType.Asset:
                    RemoveBlueprintRef(obj, objRef);
                    break;

                case RefType.SceneObject:
                    RemoveSceneObjectRef(obj, objRef);
                    break;

                default:
                    throw new Exception("Undefined ref type");
            }
        }
        
        void RemoveBlueprintRef(ObjRef obj, Ref objRef)
        {
            m_EntriesByGuid = m_EntriesByGuid ?? Entries.ToDictionary(e => e.ObjectGuid);
            if (!m_EntriesByGuid.TryGetValue(obj.Guid, out var entry))
                return;

            lock (entry.References)
            {
                entry.References.Remove(objRef);
            }
        }

        void RemoveSceneObjectRef(ObjRef obj, Ref objRef)
        {
            m_SceneObjectRefs = m_SceneObjectRefs ?? SceneEntitys.ToDictionary(x => x.GUID);
            lock (m_SceneObjectRefs)
            {
                if (m_SceneObjectRefs.TryGetValue(obj.Guid, out var sceneEntity))
                {
                    lock (sceneEntity.Refs)
                    {
                        var existingRef2 = sceneEntity.Refs.Find(r => r.AssetGuid == objRef.AssetGuid);
                        sceneEntity.Refs.Remove(existingRef2);
                    }
                }
            }
        }

        string ScriptGiudToTypeName(string guid)
        {
            string res;
            if (!m_TypeNamesByGuid.TryGetValue(guid, out res))
            {
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                res = ms ? ms.name : "<Unknown:" + guid + ">";
                m_TypeNamesByGuid[guid] = res;
            }
            return res;
        }

        public void FixupScriptGuids()
        {
            try
            {
                for (int ii = 0; ii < Entries.Count; ii++)
                {
                    if (ii % 20 == 0)
                        EditorUtility.DisplayProgressBar(
                            "Type names fixup",
                            Entries[ii].ObjectName,
                            (float)ii / Entries.Count);

                    var entry = Entries[ii];
                    foreach (var refData in entry.References)
                    {
                        if (!refData.IsScene)
                        {
                            refData.AssetType = ScriptGiudToTypeName(refData.AssetType);
                        }
                        refData.ReferencingObjectType = ScriptGiudToTypeName(refData.ReferencingObjectType);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public void SetupChasingLinks()
        {
            try
            {
                var chaseCache = new Dictionary<string, string>();
                for (int ii = 0; ii < Entries.Count; ii++)
                {
                    var entry = Entries[ii];

                    if (ii % 20 == 0)
                        EditorUtility.DisplayProgressBar("Analyzing reference chasing", "To " + entry.ObjectName, (float)ii / Entries.Count);


                    foreach (var refData in entry.References)
                    {
                        if (refData.IsScene)
                            continue;

                        if (s_RelevantTypes.Any(t => t.Type.Name == refData.AssetType))
                        {
                            refData.RefChasingAssetGuid = refData.AssetGuid;
                        }
                        else
                        {
                            var directoryName = Path.GetDirectoryName(refData.AssetPath);
                            if (IsCutsceneCommand(refData.AssetType))
                            {
                                string baseAssetGuid;
                                if (!chaseCache.TryGetValue(directoryName, out baseAssetGuid))
                                {
                                    chaseCache[directoryName] = baseAssetGuid = BlueprintsDatabase
                                        .SearchByFolder(directoryName)
                                        .FirstOrDefault(
                                            p => BlueprintsDatabase.GetTypeById(p.Item1) == typeof(Cutscene))
                                        .Item1;
                                }
                                refData.RefChasingAssetGuid = baseAssetGuid;
                            }
                            else if (IsDialogCue(refData.AssetType))
                            {
                                string baseAssetGuid;
                                if (!chaseCache.TryGetValue(directoryName, out baseAssetGuid))
                                {
                                    chaseCache[directoryName] = baseAssetGuid = BlueprintsDatabase
                                        .SearchByFolder(directoryName)
                                        .FirstOrDefault(
                                            p => BlueprintsDatabase.GetTypeById(p.Item1) == typeof(BlueprintDialog))
                                        .Item1;
                                }
                                refData.RefChasingAssetGuid = baseAssetGuid;
                            }
                        }
                    }
                }

            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private bool IsCutsceneCommand(string assetType)
        {
            return TypeCache.GetTypesDerivedFrom(typeof(CommandBase))
                .Where(t => !t.IsAbstract)
                .Any(s => s.Name == assetType);
        }

        private bool IsDialogCue(string assetType)
        {
            return TypeCache.GetTypesDerivedFrom(typeof(BlueprintCueBase)).Where(t => !t.IsAbstract)
                       .Any(s => s.Name == assetType)
                   || TypeCache.GetTypesDerivedFrom(typeof(BlueprintAnswerBase)).Where(t => !t.IsAbstract)
                       .Any(s => s.Name == assetType);
        }

        public void AnalyzeReferencesInBlueprints()
        {
            try
            {
                for (int ii = 0; ii < Entries.Count; ii++)
                {
                    var entry = Entries[ii];

                    var analyzer = GetTypeData(entry)?.Analyzer;
                    if (analyzer == null)
                    {
                        continue;
                    }

                    if (ii % 20 == 0)
                        EditorUtility.DisplayProgressBar("Analyzing blueprint refs", "To " + entry.ObjectName, (float)ii / Entries.Count);

                    analyzer.Target = BlueprintsDatabase.LoadById<BlueprintScriptableObject>(entry.ObjectGuid);
                    foreach (var refData in entry.References)
                    {
                        if (refData.IsScene)
                            continue;

                        var asset =
                            ExtractNamedSubobjectFromBlueprint(
                                BlueprintsDatabase.LoadAtPath<SimpleBlueprint>(refData.AssetPath),
                                refData.ReferencingObjectName);
                        if (asset != null)
                        {
                            analyzer.AnalyzeBlueprintReference(asset, refData);
                        }
                        // todo: add error message?
                    }
                }

            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
        
        public static object ExtractNamedSubobjectFromBlueprint(SimpleBlueprint bp, string name)
        {
            var w = BlueprintEditorWrapper.Wrap(bp);
            var so = new SerializedObject(w);
            var p = so.GetIterator();
            var enter = true;
            while (p.Next(enter))
            {
                enter = p.propertyType == SerializedPropertyType.Generic ||
                        p.propertyType == SerializedPropertyType.ManagedReference;

                if (p.propertyType == SerializedPropertyType.ManagedReference)
                {
                    var np = p.FindPropertyRelative("name");
                    if (np?.stringValue == name)
                    {
                        return FieldFromProperty.GetFieldValue(p);
                    }
                }
            }

            return null;
        }
        
        private static RelevantTypeData GetTypeData(Entry entry)
        {
            return s_RelevantTypes.FirstOrDefault(t => t.Type.Name == entry.ObjectType);
        }

        public void ValidateReferences()
        {
            try
            {
                for (int ii = 0; ii < Entries.Count; ii++)
                {
                    var entry = Entries[ii];

                    var validator = GetTypeData(entry)?.Validator;
                    if (validator == null)
                    {
                        continue;
                    }

                    if (ii % 100 == 0)
                    {
                        EditorUtility.DisplayProgressBar("Validating data", Entries[ii].ObjectName, (float)ii / Entries.Count);
                    }
                    validator.ValidateEntry(Entries[ii]);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public void ValidateObjects()
        {
            try
            {
                for (int ii = 0; ii < Entries.Count; ii++)
                {
                    var entry = Entries[ii];
                    if (ii % 100 == 0)
                    {
                        EditorUtility.DisplayProgressBar("Validating data", Entries[ii].ObjectName, (float)ii / Entries.Count);
                    }

                    var asset = BlueprintsDatabase.LoadById<BlueprintScriptableObject>(entry.ObjectGuid);

                    using var vc = AssetValidator.CreateContext(asset.name);

                    if (vc.HasErrors)
                    {
                        AssetValidator.ValidateAsset(asset, vc);
                        if (vc.HasErrors)
                        {
                            entry.ValidationResult += vc.Errors.Aggregate((a, b) => a + "\n" + b);
                            entry.ValidationState = ValidationStateType.Error;
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public void AnalyzeReferencesInScenes()
        {
            try
            {
                var sceneList = Entries.SelectMany(e => e.References).Where(r => r.AssetType == "Scene")
                    .Select(r => r.AssetPath).Distinct().ToList();

                for (int ii = 0; ii < sceneList.Count; ii++)
                {
                    EditorUtility.DisplayProgressBar("Analyzing scene refs", "In " + Path.GetFileNameWithoutExtension(sceneList[ii]), (float)ii / sceneList.Count);

                    EditorSceneManager.OpenScene(sceneList[ii], OpenSceneMode.Single);
                    var gameObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();

                    for (int jj = 0; jj < Entries.Count; jj++)
                    {
                        var entry = Entries[jj];

                        if (!entry.References.Any(r => r.AssetPath == sceneList[ii]))
                            continue;

                        var analyzer = GetTypeData(entry)?.Analyzer;
                        if (analyzer == null)
                        {
                            continue;
                        }
                        
                        analyzer.Target = BlueprintsDatabase.LoadById<BlueprintScriptableObject>(entry.ObjectGuid);
                        
                        foreach (var refData in entry.References)
                        {
                            if (refData.AssetPath != sceneList[ii])
                                continue;

                            analyzer.AnalyzeSceneReferences(gameObjects, refData);
                        }
                    }
                }

            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static IEnumerable<Type> GetRelevantTypes()
        {
            return s_RelevantTypes.Select(t => t.Type);
        }

        public Entry FindEntryByGuid(string guid)
        {
            m_EntriesByGuid = m_EntriesByGuid ?? Entries.ToDictionary(e => e.ObjectGuid);

            Entry res;
            m_EntriesByGuid.TryGetValue(guid, out res);
            return res;
        }

        public bool TryFindRefsOnEntityByGuid(string guid, out List<Ref> refs)
        {
            m_SceneObjectRefs = m_SceneObjectRefs ?? SceneEntitys.ToDictionary(x => x.GUID);
            if (m_SceneObjectRefs.TryGetValue(guid, out var entity))
            {
                refs = entity.Refs;
                return true;
            }
            else
            {
                refs = new List<Ref>(0);
                return false;
            }
        }
    }
}
