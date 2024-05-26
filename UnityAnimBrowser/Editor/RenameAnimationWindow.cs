using UnityEngine;
using UnityEditor;

public class RenameAnimationWindow : EditorWindow
{
    private AnimationClip animationClip;
    private string newName;
    private System.Action onRename;

    public static void ShowWindow(AnimationClip anim, System.Action onRenameCallback)
    {
        var window = new RenameAnimationWindow ();
        window.titleContent = new GUIContent("Rename Animation ?");
        window.minSize = new Vector2(200, 80);
        window.maxSize = window.minSize;
        window.animationClip = anim;
        window.newName = anim.name;
        window.onRename = onRenameCallback;
        window.ShowUtility();
    }

    private void OnGUI()
    {
        GUILayout.Label("New Name:", EditorStyles.boldLabel);
        newName = EditorGUILayout.TextField("", newName);

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Rename"))
        {
            RenameAnimation();
        }
        if (GUILayout.Button("Cancel"))
        {
            Close();
        }
        GUILayout.EndHorizontal();
    }

    private void RenameAnimation()
    {
        if (!string.IsNullOrEmpty(newName) && newName != animationClip.name)
        {
            string path = AssetDatabase.GetAssetPath(animationClip);
            AssetDatabase.RenameAsset(path, newName);
            AssetDatabase.SaveAssets();
            onRename.Invoke();
        }
        Close();
    }
}
