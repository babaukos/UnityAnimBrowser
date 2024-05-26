using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class AnimationBrowser : EditorWindow
{
    private string searchFilter = "";
    private List<AnimationClip> animations = new List<AnimationClip>();
    private Vector2 scrollPos;

    private enum SortType { Name, Path, Size }
    private SortType currentSort = SortType.Name;
    private bool ascending = true;

    private AnimationClip selectedClip;
    private AnimationClip draggedClip;

    private GUIStyle selectedStyle;
    private GUIStyle normalStyle;
    private GUIStyle headerStyle;

    [MenuItem("Window/Animation Browser")]
    public static void ShowWindow()
    {
        GetWindow<AnimationBrowser>("Animation Browser");
    }

    #region Internal Methods
    private void OnEnable()
    {
        LoadAnimations();
        SetupStyles();
    }

    private void OnGUI()
    {
        Event e = Event.current;

        #region Toolbar
        GUILayout.BeginHorizontal(GUI.skin.FindStyle("Toolbar"));
        searchFilter = GUILayout.TextField(searchFilter, GUI.skin.FindStyle("ToolbarSeachTextField"));
        if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSeachCancelButton")))
        {
            searchFilter = "";
            GUI.FocusControl(null);
        }
        GUILayout.EndHorizontal();
        #endregion

        #region Header
        GUILayout.BeginHorizontal(GUI.skin.FindStyle("Toolbar"));
        if (GUILayout.Button("Name", headerStyle, GUILayout.Width(200)))
        {
            SortAnimations(SortType.Name);
        }
        if (GUILayout.Button("Path", headerStyle, GUILayout.Width(400)))
        {
            SortAnimations(SortType.Path);
        }
        if (GUILayout.Button("Size (KB)", headerStyle, GUILayout.Width(100)))
        {
            SortAnimations(SortType.Size);
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        #endregion

        scrollPos = GUILayout.BeginScrollView(scrollPos);

        #region Animation List
        foreach (var anim in animations)
        {
            if (string.IsNullOrEmpty(searchFilter) || anim.name.ToLower().Contains(searchFilter.ToLower()))
            {
                Rect rect = EditorGUILayout.BeginHorizontal();
                if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
                {
                    selectedClip = anim;
                    Repaint();
                }

                GUIStyle style = anim == selectedClip ? selectedStyle : normalStyle;
                string path = AssetDatabase.GetAssetPath(anim);
                Texture iconContent = EditorGUIUtility.ObjectContent(anim, typeof(AnimationClip)).image;
                Texture iconContent2 = AssetPreview.GetAssetPreview(anim);
                Texture resultAnimImage = iconContent2 == null ? iconContent : iconContent2;

                string tooltip = string.Format("Name: {0}\nPath: {1}\nSize: {2} KB", anim.name, path, (new FileInfo(path).Length / 1024f).ToString("F2"));

                GUILayout.Label(new GUIContent(resultAnimImage), GUILayout.Width(18), GUILayout.Height(20));

                GUILayout.Label(new GUIContent(anim.name, tooltip), style, GUILayout.Width(178));
                GUILayout.Label(new GUIContent(path, tooltip), style, GUILayout.Width(396));
                FileInfo fileInfo = new FileInfo(path);
                GUILayout.Label(new GUIContent((fileInfo.Length / 1024f).ToString("F2"), tooltip), style, GUILayout.Width(97));  // Size in KB

                EditorGUILayout.EndHorizontal();

                // Drag and Drop
                Rect dragRect = GUILayoutUtility.GetLastRect();
                if (dragRect.Contains(e.mousePosition) && e.type == EventType.MouseDrag)
                {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new Object[] { anim };
                    DragAndDrop.StartDrag("Dragging Animation");
                    e.Use();
                }

                // Context Menu
                if (e.type == EventType.ContextClick && rect.Contains(e.mousePosition))
                {
                    ShowContextMenu(anim);
                    e.Use();
                }
            }
        }
        #endregion

        GUILayout.EndScrollView();

        // Deselect if clicked on empty space
        if (e.type == EventType.MouseDown && e.button == 0 && !GUILayoutUtility.GetLastRect().Contains(e.mousePosition))
        {
            selectedClip = null;
            Repaint();
        }

        HandleDragAndDrop();
    }
    #endregion

    #region Load and Sort Animations
    private void LoadAnimations()
    {
        animations.Clear();
        var guids = AssetDatabase.FindAssets("t:AnimationClip");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var animation = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (animation != null)
            {
                animations.Add(animation);
            }
        }
        SortAnimations(currentSort);
    }

    private void SortAnimations(SortType sortType)
    {
        if (currentSort == sortType)
            ascending = !ascending;
        else
        {
            currentSort = sortType;
            ascending = true;
        }

        switch (sortType)
        {
            case SortType.Name:
                animations = ascending ? animations.OrderBy(anim => anim.name).ToList() : animations.OrderByDescending(anim => anim.name).ToList();
                break;
            case SortType.Path:
                animations = ascending ? animations.OrderBy(anim => AssetDatabase.GetAssetPath(anim)).ToList() : animations.OrderByDescending(anim => AssetDatabase.GetAssetPath(anim)).ToList();
                break;
            case SortType.Size:
                animations = ascending ? animations.OrderBy(anim => new FileInfo(AssetDatabase.GetAssetPath(anim)).Length).ToList() : animations.OrderByDescending(anim => new FileInfo(AssetDatabase.GetAssetPath(anim)).Length).ToList();
                break;
        }
    }
    #endregion

    #region Drag and Drop
    private void HandleDragAndDrop()
    {
        Event evt = Event.current;
        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                foreach (Object obj in DragAndDrop.objectReferences)
                {
                    if (obj is AnimationClip)
                    {
                        AnimationClip clip = obj as AnimationClip;
                        // Handle dropping animation clip to Animator
                        AnimatorController animatorController = Selection.activeObject as AnimatorController;
                        if (animatorController != null)
                        {
                            AnimatorStateMachine rootStateMachine = animatorController.layers[0].stateMachine;
                            AddAnimationClipToStateMachine(clip, rootStateMachine);
                        }
                        // Handle dropping animation clip to Animation component
                        else if (Selection.activeGameObject != null)
                        {
                            Animation animation = Selection.activeGameObject.GetComponent<Animation>();
                            if (animation != null)
                            {
                                animation.AddClip(clip, clip.name);
                            }
                        }
                    }
                }
                draggedClip = null;
                Event.current.Use();
            }
        }
    }

    private void AddAnimationClipToStateMachine(AnimationClip clip, AnimatorStateMachine stateMachine)
    {
        AnimatorState state = stateMachine.AddState(clip.name);
        state.motion = clip;
    }
    #endregion

    #region Context Menu
    private void ShowContextMenu(AnimationClip anim)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Highlight in Project"), false, () => HighlightAnimationInProject(anim));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Rename"), false, () => RenameAnimation(anim));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Delete"), false, () => DeleteAnimation(anim));

        menu.ShowAsContext();
    }

    private void RenameAnimation(AnimationClip anim)
    {
        RenameAnimationWindow.ShowWindow(anim, LoadAnimations);
    }

    private void DeleteAnimation(AnimationClip anim)
    {
        if (EditorUtility.DisplayDialog("Delete Animation", "Are you sure you want to delete " + anim.name + "?", "Delete", "Cancel"))
        {
            string path = AssetDatabase.GetAssetPath(anim);
            AssetDatabase.DeleteAsset(path);
            LoadAnimations();
            Repaint();
        }
    }

    private void HighlightAnimationInProject(AnimationClip anim)
    {
        EditorGUIUtility.PingObject(anim);
    }
    #endregion

#region Setup Styles
    private void SetupStyles()
    {
        // Creating styles for different editor skins
        selectedStyle = new GUIStyle("Label");
        selectedStyle.normal.background = MakeTex(2, 2, new Color(0.24f, 0.49f, 0.91f, 1.0f));
        selectedStyle.normal.textColor = Color.white;

        normalStyle = new GUIStyle("Label");

        headerStyle = new GUIStyle("ToolbarButton");
        headerStyle.alignment = TextAnchor.MiddleLeft;
        headerStyle.stretchWidth = false;
        headerStyle.fixedHeight = 18;
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Texture2D result = new Texture2D(width, height);
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
        {
            pix[i] = col;
        }

        result.SetPixels(pix);
        result.Apply();
        return result;
    }
    #endregion
}
