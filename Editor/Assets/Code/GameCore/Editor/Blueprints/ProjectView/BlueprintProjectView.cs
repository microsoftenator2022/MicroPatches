using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem.EditorDatabase;
using Kingmaker.Editor;
using Kingmaker.Editor.Blueprints;
using Kingmaker.Editor.Blueprints.ProjectView;
using Owlcat.Editor.Core.Utility;
using Owlcat.Editor.Utility;
using Owlcat.Runtime.Core.Utility;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Kingmaker.Utility.DotNetExtensions;
using RogueTrader.Editor.Blueprints.ProjectView;
using System.Linq;
using Kingmaker.Utility.EditorPreferences;

namespace Assets.Editor
{
    public class BlueprintProjectView: EditorWindow, IHasCustomMenu
    {
        [SerializeField]
        private TreeViewState m_TreeViewState;
        FolderTreeView m_TreeView;

        [SerializeField]
        private TreeViewState m_TreeViewStateRight;
        FileListView m_FilesView;
        
        SearchField m_SearchField;

        [SerializeField]
        private float m_SplitWidth = 100;
        bool m_IsResizing = false;
        private Exception m_OnEnableException;
        [SerializeField]
        private bool m_IsLocked;

        private static bool s_ReloadAllScheduled;

        public static event Action<Rect, FileListItem> OnItemGUI;
        
        [MenuItem("Window/General/Blueprints %&5",priority = 5)]
        static void ShowWindow()
        {
            var window = GetWindow<BlueprintProjectView>(typeof(EditorGUI).Assembly.GetType("UnityEditor.ProjectBrowser"));
            window.Show();
        }
        
        static BlueprintProjectView()
        {
            BlueprintEditorUtility.OnPing += Ping;
            BlueprintsDatabase.OnInvalidated += ScheduleReloadAll;
        }

        public override IEnumerable<Type> GetExtraPaneTypes()
        {
            return base.GetExtraPaneTypes().Concat(typeof(BlueprintProjectView));
        }

        void OnEnable()
        {
            try
            {
                if (DatabaseServerConnector.ClientInstance == null)
                {
                    // do not enable window if we did not have a chance to connect to the service yet
                    EditorApplication.delayCall += OnEnable;
                    return;
                }
                
                if (m_TreeViewState == null)
                    m_TreeViewState = new TreeViewState();
                if (m_TreeViewStateRight == null)
                    m_TreeViewStateRight = new TreeViewState();
                m_TreeViewState.searchString = "";

                m_TreeView = new FolderTreeView(m_TreeViewState);
                m_TreeView.Reload();

                m_FilesView = new FileListView(m_TreeViewStateRight);
                m_FilesView.RootPath = m_TreeView.GetSelectedPath();
                m_FilesView.SearchPattern = m_TreeViewStateRight.searchString;
                m_FilesView.Owner = this;

                m_TreeView.OnSelectionChanged += path => m_FilesView.RootPath = path;
                m_FilesView.OnFolderSelected += path => m_TreeView.OpenPath(path, true);

                m_FilesView.Reload();
                m_FilesView.RefreshSelection();

                m_SearchField = new SearchField();
                m_SearchField.downOrUpArrowKeyPressed += m_FilesView.SetFocusAndEnsureSelectedItem;

                // needed by ElementCopyAndPasteController
                wantsMouseMove = true;
                wantsMouseEnterLeaveWindow = true;
                titleContent = new GUIContent("Blueprints");
            }
            catch(Exception x)
            {
                m_OnEnableException = x;
            }
        }

        public static void Ping(SimpleBlueprint bp)
        {
            if(bp == null || Event.current == null)
                return;

            if (Event.current.control)
            {
                Selection.activeObject = BlueprintEditorWrapper.Wrap(bp);
            }
            else
            {
                foreach (var view in Resources.FindObjectsOfTypeAll<BlueprintProjectView>())
                {
                    if (!view.m_IsLocked)
                    {
                        view.Select(bp.AssetGuid, true);
                        view.Repaint();
                    }
                }
            }
        }

        public static void ScheduleReloadAll()
        {
            if(s_ReloadAllScheduled)
                return;
            s_ReloadAllScheduled = true;
            EditorApplication.delayCall += ReloadAll;
        }
        public static void ReloadAll()
        {
            s_ReloadAllScheduled = false;
            foreach (var view in Resources.FindObjectsOfTypeAll<BlueprintProjectView>())
            {
                view.m_TreeView?.Reload();
                view.m_FilesView?.Reload();
            }
        }

        void OnSelectionChange()
        {
            if(m_IsLocked)
                return;
            
            if(Selection.activeObject is BlueprintEditorWrapper bew)
            {
                Select(bew.Blueprint.AssetGuid);
            }
            Repaint();
        }

        private void Select(string id, bool ping = false)
        {
            if(m_FilesView.HasSearch)
                return;
            
            if (m_FilesView.IsShowing(id))
            {
                m_FilesView.Select(id, ping);
            }
            else
            {
                var path = BlueprintsDatabase.IdToPath(id);
                m_TreeView.OpenPath(BlueprintsDatabase.RelativeToFullPath(path));
                m_FilesView.Reload();
                m_FilesView.Select(id, ping);
            }
        }

        void OnGUI()
        {
            if (DatabaseServerConnector.ClientInstance == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Box("No connection to indexing server", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                if (GUILayout.Button("Reconnect"))
                {
                    DatabaseServerConnector.Connect();
                }
                if (GUILayout.Button("Try restarting server"))
                {
                    DatabaseServerConnector.RestartAndConnect();
                }
                GUILayout.FlexibleSpace();
                return;
            }

            if (m_OnEnableException != null)
            {
                EditorGUILayout.HelpBox(m_OnEnableException.ToString(), MessageType.Error);
            }

            DoNotifications();
            DoToolbar();
            DoSplit();
        }

        void DoSplit()
        {
            GUILayout.BeginHorizontal();
            var leftRect = GUILayoutUtility.GetRect(m_SplitWidth, m_SplitWidth, 0, 10000);
            m_TreeView?.OnGUI(leftRect);

            ResizeSplit();

            var rightRect = GUILayoutUtility.GetRect(0, position.width - m_SplitWidth - 4, 0, 10000);
            m_FilesView?.OnGUI(rightRect);

            ShowContextMenu(leftRect, rightRect);

            GUILayout.EndHorizontal();
        }

        private void ShowContextMenu(Rect leftRect, Rect rightRect)
        {
            if (Event.current.type == EventType.ContextClick || m_TreeView.HasFocus() || m_FilesView.HasFocus())
            {
                if (leftRect.Contains(Event.current.mousePosition) || m_TreeView.HasFocus())
                {
                    if (m_TreeView.HandleGUICommand())
                    {
                        ReloadAll();
                    }
                }
                else if (rightRect.Contains(Event.current.mousePosition) || (m_FilesView != null && m_FilesView.HasFocus()))
                {
                    if (m_FilesView.HandleGUICommand())
                    {
                        ReloadAll();
                    };
                }
            }
        }

        private void ResizeSplit()
        {
            var splitControlRect = GUILayoutUtility.GetRect(4, 4, 0, 10000); ;
            GUI.Box(splitControlRect, "", GUIStyle.none);
            var cid = GUIUtility.GetControlID(8846532, FocusType.Passive, splitControlRect);
            EditorGUIUtility.AddCursorRect(splitControlRect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && splitControlRect.Contains(Event.current.mousePosition))
            {
                m_IsResizing = true;
                Event.current.Use();
                GUIUtility.hotControl = cid;
            }
            if (m_IsResizing)
            {
                m_SplitWidth = Event.current.mousePosition.x;
                Repaint();
            }
            if (Event.current.GetTypeForControl(cid) == EventType.MouseUp && GUIUtility.hotControl==cid)
            {
                m_IsResizing = false;
                GUIUtility.hotControl = 0;
            }
        }


        void DoToolbar()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            {
                var cdc = EditorGUIUtility.IconContent("CreateAddNew");
                Rect rect = GUILayoutUtility.GetRect(cdc, "ToolbarCreateAddNewDropDown");
                if (EditorGUI.DropdownButton(rect, cdc, FocusType.Passive, "ToolbarCreateAddNewDropDown"))
                {
                    ShowBlueprintCreation(rect);
                }
                
                var nc = new GUIContent(OwlcatEditorStyles.Instance.NicolayIcon);
                rect = GUILayoutUtility.GetRect(nc, EditorStyles.toolbarButton);
                if (EditorGUI.DropdownButton(rect, nc, FocusType.Passive, EditorStyles.toolbarButton))
                {
                    GUIUtility.hotControl = 0;
                    NewAssetWindow.ShowAssetWindowNew();
                }
            }

            GUILayout.FlexibleSpace();
            if (m_TreeView != null && m_SearchField != null)
            {
                var rect = GUILayoutUtility.GetRect(100, 200, 16, 16);
                m_FilesView.SearchPattern = m_SearchField.OnGUI(rect, m_FilesView.SearchPattern);
                
                var btc = EditorGUIUtility.TrIconContent("FilterByType", "Search by Type");
                rect = GUILayoutUtility.GetRect(btc, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
                if (EditorGUI.DropdownButton(rect, btc, FocusType.Passive, EditorStyles.toolbarButton))
                {
                    TypePicker.Show(rect,
                        "",
                        () => TypeCache.GetTypesDerivedFrom(typeof(SimpleBlueprint))
                            .Where(t => !t.IsAbstract)
                            .OrderBy(t => t.Name),
                        SetTypeFilter);
                }
            }
            if (m_IsLocked && GUILayout.Button(EditorGUIUtility.IconContent("InspectorLock"), EditorStyles.toolbarButton))
            {
                m_IsLocked = false;
            }

            //GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DoNotifications()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            if (BlueprintsDatabase.DuplicatedIds?.Length > 0)
            {
                using (GuiScopes.Color(Color.red))
                {
                    if (GUILayout.Button("Errors!", GUILayout.ExpandWidth(false)))
                    {
                        GetWindow<FixDuplicatesWindow>();
                    }
                }
            }

            if (BlueprintsDatabase.ListOfContainsShadowDeletedBlueprintWithoutIsShadowDeleted.Count > 0)
            {
                using (GuiScopes.Color(Color.yellow))
                {
                    if (GUILayout.Button("Using shadow deleted!", GUILayout.ExpandWidth(false)))
                    {
                        GetWindow<UsingShadowDeletedWindow>();
                    }
                }
            }
            
            if (BlueprintsDatabase.ListOfContainsRemoveBlueprints.Count > 0)
            {
                using (GuiScopes.Color(Color.yellow))
                {
                    if (GUILayout.Button("Using remove!", GUILayout.ExpandWidth(false)))
                    {
                        GetWindow<UsingRemoveWindow>();
                    }
                }
            }
            
            GUILayout.EndHorizontal();
        }

        public void ShowBlueprintCreation(Rect rect)
        {
            GUIUtility.hotControl = 0;
            TypePicker.Show(rect,
                "",
                () => TypeCache.GetTypesDerivedFrom(typeof(SimpleBlueprint))
                    .Where(t => !t.IsAbstract)
                    .OrderBy(t => t.Name),
                CreateNewBlueprint);
        }

        private void CreateNewBlueprint(Type obj)
        {
            var path = BlueprintsDatabase.FullToRelativePath(m_FilesView.RootPath);
            var bp = BlueprintsDatabase.CreateAsset(obj, path, "New" + obj.Name);
            EditorApplication.delayCall += () => // need time for the indexing server to update
            {
                m_FilesView.Reload();
                m_FilesView.SelectAndRename(bp.AssetGuid);
            };
            
            if (bp is BlueprintScriptableObject bpScriptable)
            {
                bpScriptable.Author = EditorPreferences.Instance.NewBlueprintAuthor;
                bpScriptable.SetDirty();
                BlueprintsDatabase.Save(bpScriptable.AssetGuid);
            }
        }

        private void SetTypeFilter(Type obj)
        {
            // todo: if we have non-type search elements, do not delete them
            m_FilesView.SearchPattern = "t:" + obj.Name;
        }

        public static void RaiseOnItemGUI(Rect rect, FileListItem fileItem)
        {
            OnItemGUI?.Invoke(rect, fileItem);
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Lock"), m_IsLocked, ()=>m_IsLocked=!m_IsLocked);
        }
    }
}