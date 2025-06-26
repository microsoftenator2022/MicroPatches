using UnityEngine;

using UnityModManagerNet;

namespace MicroPatches
{
    partial class Main
    {
        void OnGUI(UnityModManager.ModEntry _)
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Configure"))
            {
                if (Main.UIWindow == null)
                    CreateUI();

                UnityModManager.UI.Instance.ToggleWindow(false);
            }

            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();
        }
    }
}
