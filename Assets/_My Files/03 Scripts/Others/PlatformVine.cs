using UnityEngine;

/// <summary>
/// Sits on a Vine child inside a platform prefab. Shows the vine only when the
/// parent platform is full width (all floor tiles active); hides it on short platforms.
/// Runs in editor too so prefab instances look right without pressing Play.
/// </summary>
[ExecuteAlways]
public class PlatformVine : MonoBehaviour
{
    [Tooltip("Minimum active Floor_Tile children on the parent platform for this vine to show.")]
    public int minTiles = 9;

    private void OnEnable() { Refresh(); }
    private void Start() { Refresh(); }
#if UNITY_EDITOR
    private void OnValidate() { Refresh(); }
    private void Update() { if (!Application.isPlaying) Refresh(); }
#endif

    public void Refresh()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null || transform.parent == null) return;
        int tiles = 0;
        foreach (Transform t in transform.parent)
            if (t.name.StartsWith("Floor_Tile") && t.gameObject.activeSelf) tiles++;
        bool show = tiles >= minTiles;
        if (sr.enabled != show) sr.enabled = show;
        var sway = GetComponent<VineSway>();
        if (sway != null && sway.enabled != show) sway.enabled = show;
    }
}
