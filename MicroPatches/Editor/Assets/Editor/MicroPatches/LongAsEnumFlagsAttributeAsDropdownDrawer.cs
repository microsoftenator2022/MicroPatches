using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Kingmaker.Utility
{
    [CustomPropertyDrawer(typeof(LongAsEnumFlagsAttribute))]
    public class LongAsEnumFlagsAttributeAsDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.LabelField(new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height), label);

            string text = property.hasMultipleDifferentValues ? "-multiple-  " : property.longValue == 0 ? "None  " : "";
            var enumType = (attribute as LongAsEnumFlagsAttribute).EnumType;
            var names = System.Enum.GetNames(enumType);

            foreach (string name in names)
            {
                long value = (long)System.Enum.Parse(enumType, name);
                if ((property.longValue & value) == value) text += string.Format("{0}, ", name);
            }
            text = text.Remove(text.Length - 2, 2);
            Rect popupRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y, position.width - EditorGUIUtility.labelWidth, position.height);
            if (GUI.Button(popupRect, new GUIContent(text), (GUIStyle)"miniPopup"))
            {
                GenericMenu menu = new GenericMenu();
                foreach (var name in names)
                {
                    long value = (long)System.Enum.Parse(enumType, name);
                    bool has = (property.longValue & value) == value;
                    menu.AddItem(new GUIContent("None"), property.longValue == 0, () =>
                    {
                        property.longValue = 0;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                    menu.AddItem(new GUIContent(name), has, () =>
                    {
                        if (has) property.longValue ^= value;
                        else property.longValue |= value;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                }
                menu.DropDown(popupRect);
            }
        }
    }
}
