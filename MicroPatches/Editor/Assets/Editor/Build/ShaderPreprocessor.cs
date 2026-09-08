using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
#region MicroPatches
using UnityEditor;
#endregion

namespace OwlcatModification.Editor.Build
{
	public class ShaderPreprocessor : IPreprocessShaders
	{
		public int callbackOrder
			=> 0;

		public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
		{
			#region MicroPatches
            var path = AssetDatabase.GetAssetPath(shader);
            var name = shader.name;

            if (name != null && !name.Contains("Hidden/VFX"))
                {
                    data.Clear();
                }
				else
				{
					Debug.Log($"Found VFX shader {name} ({path}), compiling");
				}
			#endregion
		}
	}
}