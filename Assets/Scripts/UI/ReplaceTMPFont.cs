#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

public class ReplaceTMPFont : EditorWindow
{
    private TMP_FontAsset newFont;

    [MenuItem("Tools/Replace All TMP Fonts")]
    static void Init() => GetWindow<ReplaceTMPFont>("Replace TMP Fonts").Show();

    void OnGUI()
    {
        newFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
            "New Font", newFont, typeof(TMP_FontAsset), false);

        GUI.enabled = newFont != null;

        if (GUILayout.Button("Replace in Open Scenes"))
            ReplaceInScenes();

        if (GUILayout.Button("Replace in All Prefabs"))
            ReplaceInPrefabs();

        if (GUILayout.Button("Replace in All Scenes (build settings)"))
            ReplaceInAllScenes();
    }

    void ReplaceInScenes()
    {
        int count = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            foreach (var root in scene.GetRootGameObjects())
                count += ReplaceInGameObject(root);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        Debug.Log($"Заменено в открытых сценах: {count}");
    }

    void ReplaceInAllScenes()
    {
        int total = 0;
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (!s.enabled) continue;
            var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
                count += ReplaceInGameObject(root);
            if (count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            total += count;
        }
        Debug.Log($"Заменено во всех сценах: {total}");
    }

    void ReplaceInPrefabs()
    {
        int count = 0;
        var guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = PrefabUtility.LoadPrefabContents(path);
            int c = ReplaceInGameObject(prefab);
            if (c > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
                count += c;
            }
            PrefabUtility.UnloadPrefabContents(prefab);
        }
        Debug.Log($"Заменено в префабах: {count}");
    }

    int ReplaceInGameObject(GameObject root)
    {
        int count = 0;
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
        {
            Undo.RecordObject(t, "Replace TMP Font");
            t.font = newFont;
            EditorUtility.SetDirty(t);
            count++;
        }
        return count;
    }
}
#endif