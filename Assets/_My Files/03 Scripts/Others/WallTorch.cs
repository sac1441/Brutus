using UnityEngine;

/// <summary>
/// Lives on a Torch nested inside a platform's Left/Right Wall in the VerticalStack prefab.
/// Walks up the stack from the platform above spawn: lights a torch, skips 0-1 platforms,
/// mostly alternates sides (occasional same-side repeat). Seeded from the stack's world
/// position, so every stack instance differs but the layout is stable between runs.
/// If the chosen side's wall is disabled (short platform) it falls back to the other wall.
/// </summary>
[ExecuteAlways]
public class WallTorch : MonoBehaviour
{
    public enum Side { Left = 0, Right = 1 }
    public Side side = Side.Left;

    [Tooltip("First platform index that can carry a torch (0 = spawn platform).")]
    public int startIndex = 1;
    [Tooltip("Max platforms skipped between torches (0 = can be on every platform).")]
    public int maxGap = 1;
    [Range(0, 100)]
    [Tooltip("Chance (%) the next torch repeats the same side instead of alternating.")]
    public int sameSideChance = 25;

    private void OnEnable() { Refresh(); }
    private void Start() { Refresh(); }
#if UNITY_EDITOR
    private void OnValidate() { Refresh(); }
    private void Update() { if (!Application.isPlaying) Refresh(); }
#endif

    public void Refresh()
    {
        Transform wall = transform.parent;
        Transform platform = wall != null ? wall.parent : null;
        Transform stack = platform != null ? platform.parent : null;
        if (stack == null) return;

        int myIdx = platform.GetSiblingIndex();
        bool show = ResolveSide(stack, myIdx) == (int)side;

        foreach (var r in GetComponentsInChildren<Renderer>(true)) if (r.enabled != show) r.enabled = show;
        foreach (var a in GetComponentsInChildren<Animator>(true)) if (a.enabled != show) a.enabled = show;
    }

    /// <summary>Returns 0 (left), 1 (right) or -1 (no torch) for the platform at sibling index target.</summary>
    private int ResolveSide(Transform stack, int target)
    {
        uint h = Hash(stack, 0);
        int s = (int)(h & 1u);
        int i = startIndex;
        int n = 0;
        while (i <= target && i < stack.childCount)
        {
            if (i == target)
            {
                Transform plat = stack.GetChild(i);
                if (WallActive(plat, s)) return s;
                if (WallActive(plat, 1 - s)) return 1 - s;
                return -1;
            }
            n++;
            h = Hash(stack, n);
            i += 1 + (int)(h % (uint)(maxGap + 1));
            if ((h >> 8) % 100u >= (uint)sameSideChance) s = 1 - s;
        }
        return -1;
    }

    private static bool WallActive(Transform plat, int s)
    {
        if (!plat.name.StartsWith("Platform") || !plat.gameObject.activeSelf) return false;
        if (s == 0 && plat.parent != null)
        {
            // Keep torches off the stretch of left wall a snake is wrapped around
            var snake = plat.parent.Find("Snake");
            var idle = snake != null ? snake.GetComponent<SnakeIdle>() : null;
            if (idle != null && snake.gameObject.activeInHierarchy && idle.Covers(plat.position.y)) return false;
        }
        string want = s == 0 ? "Left Wall" : "Right Wall";
        foreach (Transform c in plat)
            if (c.name.Trim() == want) return c.gameObject.activeSelf;
        return false;
    }

    private static uint Hash(Transform stack, int i)
    {
        unchecked
        {
            uint h = 2166136261u;
            h = (h ^ (uint)Mathf.RoundToInt(stack.position.x * 10f)) * 16777619u;
            h = (h ^ (uint)Mathf.RoundToInt(stack.position.y * 10f)) * 16777619u;
            h = (h ^ (uint)(i * 7919 + 17)) * 16777619u;
            h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
            return h;
        }
    }
}
