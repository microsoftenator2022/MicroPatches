using System.IO;
using System.Reflection;

using Kingmaker.Visual.CharacterSystem;

using UnityEditor;

using UnityEngine;

static class EquipmentEntityMenuItem
{
    [MenuItem("Assets/Create/Character System/EquipmentEntity")]
    static void CreateEquipmentEntity()
    {
        var args = new object[1];
        typeof(ProjectWindowUtil).GetMethod("TryGetActiveFolderPath", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        var path = (string)args[0];

        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<EquipmentEntity>(), Path.Combine(path, "NewEquipmentEntity.asset"));
    }
}