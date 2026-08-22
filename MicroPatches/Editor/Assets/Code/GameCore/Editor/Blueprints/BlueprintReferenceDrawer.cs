#if UNITY_EDITOR
using System;
using Code.Framework.Utility.DotNetExtensions;
using Code.Utility.Attributes;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem.EditorDatabase;
using Kingmaker.Blueprints.JsonSystem.PropertyUtility;
using Kingmaker.Code.Editor.Utility;
using Kingmaker.Editor.UIElements.Custom.Properties;
using Kingmaker.Editor.Utility;
using Kingmaker.Utility.CodeTimer;
using Owlcat.Editor.Utility;
using Owlcat.Runtime.Core.Utility;
using RectEx;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
#region MicroPatches
using Kingmaker.Blueprints.Base;

namespace Kingmaker.Editor.Blueprints
{
	//[CustomPropertyDrawer(typeof(BlueprintReference<>), true)]
	[CustomPropertyDrawer(typeof(IReferenceBase), true)]
#endregion
	public class BlueprintReferenceDrawer : PropertyDrawer
	{
		public override VisualElement CreatePropertyGUI(SerializedProperty property)
    	{
             bool inline = fieldInfo.HasAttribute<InlineBlueprintAttribute>();
             if (!inline && property.IsArrayElement())
             {
                 var parent = property.GetParent();
                 if (parent != null)
                 {
                     var parentField = FieldFromProperty.GetFieldInfo(parent);
                     inline = parentField?.HasAttribute<InlineBlueprintAttribute>() ?? false;
                 }
             }
             
             return new BlueprintReferenceProperty(property, fieldInfo, inline);
         }

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return EditorGUIUtility.singleLineHeight;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);
			Type referencedType;

			var fieldType = fieldInfo.FieldType;
			if ((fieldInfo.FieldType.IsGenericType && 
			     fieldInfo.FieldType.GetGenericTypeDefinition() == typeof(BlueprintReference<>)) || 
			    fieldType.IsArrayOfType(typeof(BlueprintReference<>)))
			{
				var genericType = fieldType;
				while (true)
				{
					if (genericType.IsArray)
					{
						var elementType = genericType.GetElementType();
						if (elementType != null)
						{
							genericType = elementType;
							continue;
						}
					}

					if (genericType.IsList())
					{
						var elementType = genericType.GenericTypeArguments[0];
						if (elementType != null)
						{
							genericType = elementType;
							continue;
						}
					}

					break;
				}

				referencedType = genericType.IsGenericType &&
				                 genericType.GetGenericTypeDefinition() == typeof(BlueprintReference<>) ? 
					genericType.GenericTypeArguments[0] : genericType;
			}
			else
			{
				var type = BlueprintLinkDrawer.GetElementType(fieldInfo?.FieldType) ?? fieldInfo?.FieldType;

				referencedType = type?.BaseType?.GetGenericArguments()[0];
			}

			/*var idProperty = property.FindPropertyRelative(nameof(WeakResourceLink.AssetId));
			var idPropertySafe = new RobustSerializedProperty(idProperty);*/
			
			//var type = BlueprintLinkDrawer.GetElementType(fieldInfo?.FieldType) ?? fieldInfo?.FieldType;
			//
			// var referencedType = type?.BaseType?.GetGenericArguments()[0];

			if (referencedType == null)
			{
				EditorGUI.LabelField(position, property.displayName, "[Cannot determine reference type]");
				return;
			}
			
			GUILayout.BeginHorizontal();

			var guidProperty = property.FindPropertyRelative("guid");
			if (guidProperty != null)
			{
				var guidProp = new RobustSerializedProperty(guidProperty);
				string g = guidProp.Property.stringValue;
				var bp = BlueprintsDatabase.LoadById<BlueprintScriptableObject>(g);
				var chunkPositions = position.Row(new[] { 5f, 0.1f });

				using (ProfileScope.New("ShowObjectField"))
				{
					BlueprintPicker.ShowObjectField(chunkPositions[0], bp, g, bp2 =>
					{
						guidProp.Property.stringValue = bp2?.AssetGuid ?? "";
						guidProp.Property.serializedObject.ApplyModifiedProperties();
					}, label, referencedType, fieldInfo, referencedType);
				}

				if (guidProp.Property.boxedValue.ToString() != "" && 
				    GUI.Button(chunkPositions[1], "", OwlcatEditorStyles.Instance.OpenButton))
				{
					BlueprintInspectorWindow.OpenFor(bp);
				}
			}
			else
			{
				if (fieldInfo.HasAttribute<SerializeReference>())
				{
					EditorGUI.LabelField(position, label, new GUIContent("DON'T USE [SerializeReference]"));
					PFLog.Default.Error("Don't use [SerializeReference] for BlueprintRef<>!");
				}
			}

			GUILayout.EndHorizontal();
			EditorGUI.EndProperty();
		}
    }
}
#endif