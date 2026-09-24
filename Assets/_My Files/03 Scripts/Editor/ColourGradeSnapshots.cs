#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Saves / applies named colour-grade snapshots (SpriteRenderer.color only).
/// Covers: sprites under the Main Camera, the stack prefabs, and scene renderers under
/// "Manual Testing Platforms" that are either not prefab-linked or carry a colour override.
/// Menu: Tools > Colour Grade.
/// </summary>
public static class ColourGradeSnapshots
{
    public const string Folder = "Assets/_My Files/ColourGrades";
    public static readonly string[] Prefabs = {
        "Assets/_My Files/05 Prefabs/VerticalStack.prefab",
        "Assets/_My Files/05 Prefabs/HorizontalStack.prefab",
        "Assets/_My Files/05 Prefabs/Platforms/Dragon/ScafoldSystem 1.prefab",
    };

    [System.Serializable] public class Entry { public string scope; public string path; public Color color; }
    [System.Serializable] public class Snapshot { public string name; public string created; public List<Entry> entries = new List<Entry>(); }

    // Unique path using sibling indices, so duplicate names never collide.
    static string IdxPath(Transform t, Transform stop)
    {
        var parts = new List<string>();
        while (t != null && t != stop) { parts.Add(t.GetSiblingIndex() + ":" + t.name); t = t.parent; }
        parts.Reverse();
        return string.Join("/", parts);
    }

    static IEnumerable<SpriteRenderer> SceneTargets()
    {
        var cam = Camera.main;
        if (cam != null) foreach (var sr in cam.GetComponentsInChildren<SpriteRenderer>(true)) yield return sr;
        var mtp = GameObject.Find("Manual Testing Platforms");
        if (mtp == null) yield break;
        foreach (var sr in mtp.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(sr)) { yield return sr; continue; }
            var p = new SerializedObject(sr).FindProperty("m_Color");
            if (p != null && p.prefabOverride) yield return sr;
        }
    }

    public static string Save(string name)
    {
        var snap = new Snapshot { name = name, created = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
        foreach (var path in Prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                snap.entries.Add(new Entry { scope = path, path = IdxPath(sr.transform, root.transform.parent), color = sr.color });
            PrefabUtility.UnloadPrefabContents(root);
        }
        foreach (var sr in SceneTargets())
            snap.entries.Add(new Entry { scope = "SCENE", path = IdxPath(sr.transform, null), color = sr.color });

        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_My Files", "ColourGrades");
        string file = Folder + "/" + name + ".json";
        File.WriteAllText(file, JsonUtility.ToJson(snap, true));
        AssetDatabase.ImportAsset(file);
        return file + " (" + snap.entries.Count + " renderers)";
    }

    public static string Apply(string name)
    {
        string file = Folder + "/" + name + ".json";
        if (!File.Exists(file)) return "Snapshot not found: " + file;
        var snap = JsonUtility.FromJson<Snapshot>(File.ReadAllText(file));
        int applied = 0, missing = 0;

        foreach (var path in Prefabs)
        {
            var map = snap.entries.Where(e => e.scope == path).ToDictionary(e => e.path, e => e.color);
            if (map.Count == 0) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Color c;
                if (map.TryGetValue(IdxPath(sr.transform, root.transform.parent), out c)) { sr.color = c; applied++; } else missing++;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        var sceneMap = snap.entries.Where(e => e.scope == "SCENE").ToDictionary(e => e.path, e => e.color);
        foreach (var sr in SceneTargets())
        {
            Color c;
            if (sceneMap.TryGetValue(IdxPath(sr.transform, null), out c)) { Undo.RecordObject(sr, "Apply colour grade"); sr.color = c; applied++; }
            else missing++;
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        return "Applied '" + name + "': " + applied + " renderers" + (missing > 0 ? ", " + missing + " not in snapshot (left as-is)" : "");
    }

    [MenuItem("Tools/Colour Grade/Apply Original")]      static void A0() { Debug.Log(Apply("Original")); }
    [MenuItem("Tools/Colour Grade/Apply Claude Style")]  static void A1() { Debug.Log(Apply("Claude_Style")); }
    [MenuItem("Tools/Colour Grade/Apply Spec Hierarchy")] static void A2() { Debug.Log(Apply("Spec_Hierarchy")); }
    [MenuItem("Tools/Colour Grade/Save Current As...")]
    static void SaveAs()
    {
        string p = EditorUtility.SaveFilePanelInProject("Save colour grade", "MyGrade", "json", "Name this grade", Folder);
        if (!string.IsNullOrEmpty(p)) Debug.Log(Save(Path.GetFileNameWithoutExtension(p)));
    }
}
#endif
