using System;
using System.Collections.Generic;
using System.IO;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem.EditorDatabase;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Kingmaker.Editor.Validation
{
    public class JsonReferenceExtractor
    {
        private string m_ScriptGuid;
        private string m_ObjectName;
        private List<ObjRef> m_Refs;
        
        public string ScriptGuid => m_ScriptGuid;

        public string ObjectName => m_ObjectName;

        public List<ObjRef> Refs => m_Refs;

        public void ParseFile(string path, Action onAssetParsed, bool checkWeakResourceLinks)
        {
            #region Micropatches
			var longPath = @"\\?\" + Path.GetFullPath(path);
			var jo = JObject.Parse(File.ReadAllText(longPath));
			//var jo = JObject.Parse(File.ReadAllText(path));
			#endregion
            var root = (JObject)jo["Data"];
            m_ScriptGuid = root["$type"].Value<string>();
            m_ScriptGuid = m_ScriptGuid.Substring(0, 32); // value is <guid>, <typename> - cut that last part
            m_ObjectName = Path.GetFileNameWithoutExtension(path);
            m_Refs = new List<ObjRef>();
            ParseJsonObject(root, onAssetParsed, checkWeakResourceLinks);
            onAssetParsed?.Invoke();
        }

        private void ParseJsonObject(JObject root, Action onAssetParsed, bool checkWeakResourceLinks)
        {
            // if we have name and type, this is an internal object (component, element)
            var np = root["name"];
            var tp = root["$type"];

            var isNestedAsset = np != null && tp != null;
            var oldScript = m_ScriptGuid;
            var oldName = m_ObjectName;
            var oldRefs = m_Refs;
            
            if (isNestedAsset)
            {
                m_ScriptGuid = tp.Value<string>().Substring(0, 32);
                m_ObjectName = np.Value<string>();
                m_Refs = new List<ObjRef>();
            }

            foreach (var prop in root)
            {
                ParseValue(prop.Key, prop.Value, onAssetParsed, checkWeakResourceLinks);
            }

            if (isNestedAsset)
            {
                onAssetParsed?.Invoke();
                m_ScriptGuid = oldScript;
                m_ObjectName = oldName;
                m_Refs = oldRefs;
            }
        }
        
        private void ParseValue(string propName, JToken value, Action onAssetParsed, bool checkWeakResourceLinks)
        {
            switch (value)
            {
                case JValue v:
                {
                    var s = v.Value<string>();
                
                    if(string.IsNullOrEmpty(s))
                        break;
                
                    if (s.StartsWith("!bp_"))
                    {
                        // this is a blueprint link
                        Refs.Add(new ObjRef(RefType.Asset, s.Substring(4)));
                    }
                    else if (propName == "_entity_id")
                    {
                        // this is a scene obj link
                        Refs.Add(new ObjRef(RefType.SceneObject, s));
                    }

                    break;
                }
                case JObject jo:
                    ParseJsonObject(jo, onAssetParsed, checkWeakResourceLinks);
                    break;
                case JArray ja:
                {
                    foreach (var token in ja)
                    {
                        ParseValue("", token, onAssetParsed, checkWeakResourceLinks);
                    }

                    break;
                }
            }
        }
        
        [MenuItem("BP/Test refparse")]
        public static void Test()
        {
            var bp = BlueprintEditorWrapper.Unwrap<SimpleBlueprint>(Selection.activeObject);
            if (bp != null)
            {
                var rf = new JsonReferenceExtractor();
                rf.ParseFile(BlueprintsDatabase.GetAssetPath(bp), () => 
                {
                    PFLog.Default.Log($"Parsed {rf.ObjectName}: {rf.ScriptGuid}");
                    foreach (var objRef in rf.Refs)
                    {
                        PFLog.Default.Log($"referenced: {objRef.Guid} ({Path.GetFileNameWithoutExtension(BlueprintsDatabase.IdToPath(objRef.Guid))}), {objRef.RefType}");
                    }
                }, false);
            }
        }
    }
}