using System;
using System.Linq;
using JetBrains.Annotations;
using Kingmaker.Code.Editor.Utility;
using Kingmaker.Editor.UIElements.Custom.Properties;
using Kingmaker.Editor.UIElements.ValuePicker;
using Kingmaker.Editor.Utility;
using Kingmaker.ResourceLinks;
using Owlcat.Editor.Core.Utility;
using Owlcat.QA.Validation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace Kingmaker.Editor.Blueprints
{
	public class WeakLinkDrawer<TAsset> : PropertyDrawer where TAsset : Object
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var idProperty = property.FindPropertyRelative(nameof(WeakResourceLink.AssetId));
			bool notNull = property.GetAttributes()?.Any(x => x is NotNullAttribute or ValidateNotNullAttribute) ?? false;
			var idPropertySafe = new RobustSerializedProperty(idProperty);
			var currentValue = GenericWeakLinkDrawer.GetAsset(idProperty.hasMultipleDifferentValues ? 
				null : idProperty.stringValue, typeof(TAsset));

            #region MicroPatches
            if (currentValue == null)
            {
                var link = property.GetTargetObjectOfProperty() as WeakResourceLink<TAsset>;

                if (link != null)
                {
                    currentValue = link.Load();
                }
            }
			#endregion

			Action<Object> pickCallback = o =>
				{
					var p = idPropertySafe.Property;
					using (GuiScopes.UpdateObject(p.serializedObject))
					{
						idPropertySafe.Property.stringValue = GenericWeakLinkDrawer.GetGuid(o, typeof(TAsset));
					}
				};

			var prevColor = GUI.color;
			if (notNull && currentValue == null)
				GUI.color = Color.red;
			
			AssetPicker.ShowPropertyField(
				position, property, fieldInfo,
				currentValue, pickCallback, 
				label, typeof(TAsset), Filter
			);
			GUI.color = prevColor;
		}

		protected virtual bool Filter(AssetPicker.HierarchyEntry entry) => true; 
		
		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			return new WeakLinkProperty(property, typeof(TAsset), fieldInfo, Filter);
		}	
	}

	[CustomPropertyDrawer(typeof(Texture2DLink))]
	public class Texture2DLinkDrawer : WeakLinkDrawer<Texture2D>
	{
	}
	
	[CustomPropertyDrawer(typeof(SpriteLink))]
	public class SpriteLinkDrawer : WeakLinkDrawer<Sprite>
	{
	}
	
	[CustomPropertyDrawer(typeof(MaterialLink))]
	public class MaterialLinkDrawer : WeakLinkDrawer<Material>
	{
	}
	
	[CustomPropertyDrawer(typeof(VideoLink))]
	public class VideoLinkDrawer : WeakLinkDrawer<VideoClip>
	{
	}
}