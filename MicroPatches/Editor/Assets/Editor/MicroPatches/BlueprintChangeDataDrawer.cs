using System.Collections;
using System.Collections.Generic;

using Kingmaker.Modding;

using OwlcatModification.Editor;

using UnityEditor;

using UnityEngine;

using static Kingmaker.Modding.OwlcatModificationSettings;

[CustomPropertyDrawer(typeof(BlueprintChangeData))]
public class BlueprintChangeDataDrawer : PropertyDrawer
{
    public enum JsonPatchType
    {
        Replace,
        Edit,
        Micro
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        (EditorGUIUtility.singleLineHeight * 3) + (EditorGUIUtility.standardVerticalSpacing * 2);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var x = position.x;
        var y = position.y;
        var width = position.width;
        float totalheight = 0;

        var guidRect = new Rect(x, y, width, EditorGUIUtility.singleLineHeight);

        totalheight += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        var nameRect = new Rect(x, y + totalheight, width, EditorGUIUtility.singleLineHeight);

        totalheight += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        var patchTypeRect = new Rect(x, y + totalheight, width, EditorGUIUtility.singleLineHeight);


        var guid = property.FindPropertyRelative(nameof(BlueprintChangeData.Guid));
        EditorGUI.PropertyField(guidRect, guid);

        var filename = property.FindPropertyRelative(nameof(BlueprintChangeData.Filename));
        EditorGUI.PropertyField(nameRect, filename);
        
        var patchType = property.FindPropertyRelative(nameof(BlueprintChangeData.PatchType));
        patchType.intValue = (int)(JsonPatchType)EditorGUI.EnumPopup(patchTypeRect, "Patch type", (JsonPatchType)patchType.intValue);
        //EditorGUI.PropertyField(patchTypeRect, patchType);
    }
}
