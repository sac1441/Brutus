#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility to fix VerticalStack and ScaffoldSystem prefabs.
/// Resets Z to 0, Rotation X to 0, and spaces platforms 3 units apart in Y starting from 0.
/// Place this script in an Editor folder: Assets/_My Files/Editor/PrefabFixer.cs
/// </summary>
public class PrefabFixer : EditorWindow
{
    [MenuItem("Tools/Fix Prefabs")]
    public static void ShowWindow()
    {
        GetWindow<PrefabFixer>("Fix Prefabs");
    }

    private GameObject verticalStackPrefab;
    private GameObject scaffoldPrefab;
    private float ySpacing = 3f;
    private string platformPrefix = "Platform"; // children whose name starts with this get repositioned

    private void OnGUI()
    {
        GUILayout.Label("Prefab Fixer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        verticalStackPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Vertical Stack Prefab", verticalStackPrefab, typeof(GameObject), false);

        scaffoldPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Scaffold Prefab", scaffoldPrefab, typeof(GameObject), false);

        ySpacing = EditorGUILayout.FloatField("Y Spacing Between Platforms", ySpacing);
        platformPrefix = EditorGUILayout.TextField("Platform Name Prefix", platformPrefix);

        EditorGUILayout.Space();

        if (GUILayout.Button("Fix Both Prefabs"))
        {
            if (verticalStackPrefab != null) FixPrefab(verticalStackPrefab);
            if (scaffoldPrefab != null) FixPrefab(scaffoldPrefab);
            Debug.Log("Prefab fixing complete!");
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Fix Vertical Stack Only"))
        {
            if (verticalStackPrefab != null) FixPrefab(verticalStackPrefab);
        }

        if (GUILayout.Button("Fix Scaffold Only"))
        {
            if (scaffoldPrefab != null) FixPrefab(scaffoldPrefab);
        }
    }

    private void FixPrefab(GameObject prefab)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        int platformIndex = 0;

        foreach (Transform child in root.transform)
        {
            // Reset Z position for ALL children
            Vector3 pos = child.localPosition;
            child.localPosition = new Vector3(pos.x, pos.y, 0f);

            // Reset X rotation for ALL children
            Vector3 rot = child.localEulerAngles;
            child.localEulerAngles = new Vector3(0f, rot.y, rot.z);

            // Reposition platforms equidistantly in Y
            if (child.name.StartsWith(platformPrefix))
            {
                child.localPosition = new Vector3(
                    child.localPosition.x,
                    platformIndex * ySpacing,
                    0f
                );
                platformIndex++;

                // Also reset Z and rotation X on platform children
                foreach (Transform platformChild in child)
                {
                    Vector3 cPos = platformChild.localPosition;
                    platformChild.localPosition = new Vector3(cPos.x, cPos.y, 0f);

                    Vector3 cRot = platformChild.localEulerAngles;
                    platformChild.localEulerAngles = new Vector3(0f, cRot.y, cRot.z);
                }
            }
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);

        Debug.Log($"Fixed prefab: {prefab.name} — {platformIndex} platforms repositioned");
    }
}
#endif