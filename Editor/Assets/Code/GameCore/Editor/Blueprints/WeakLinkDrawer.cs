using System;
using JetBrains.Annotations;
using Kingmaker.Editor.Utility;
using Kingmaker.ResourceLinks;
#region MicroPatches
using Kingmaker.Code.Editor.Utility;
#endregion
using Owlcat.Editor.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace Kingmaker.Editor.Blueprints
{
	public class WeakLinkDrawer<TAsset> : PropertyDrawer 
		where TAsset : Object
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var idProperty = property.FindPropertyRelative(nameof(WeakResourceLink.AssetId));
			var idPropertySafe = new RobustSerializedProperty(idProperty);
			var currentValue = GetAsset(idProperty.hasMultipleDifferentValues?null:idProperty.stringValue);

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

            Action<Object> pickCallback =
				o =>
				{
					var p = idPropertySafe.Property;
					using (GuiScopes.UpdateObject(p.serializedObject))
					{
						idPropertySafe.Property.stringValue = GetGuid(o);
					}
				};

			AssetPicker.ShowPropertyField(
				position, property, fieldInfo,
				currentValue, pickCallback, 
				label, typeof(TAsset)
			);
		}

		[CanBeNull]
		private static Object GetAsset(string guid)
		{
			if (string.IsNullOrEmpty(guid))
				return null;

			var assetPath = AssetDatabase.GUIDToAssetPath(guid);
			if (string.IsNullOrEmpty(assetPath))
				return null;

			return AssetDatabase.LoadAssetAtPath<TAsset>(assetPath);
		}

		private static string GetGuid(Object asset)
		{
			if (typeof(MonoBehaviour).IsAssignableFrom(typeof(TAsset)))
			{
                if (!(asset is TAsset))
                {
                    var go = asset as GameObject;
                    if (go == null)
                    {
                        return "";
                    }

                    if (go.GetComponent<TAsset>() == null)
                    {
                        return "";
                    }
                }
            }

			var assetPath = AssetDatabase.GetAssetPath(asset);
			return AssetDatabase.AssetPathToGUID(assetPath);
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