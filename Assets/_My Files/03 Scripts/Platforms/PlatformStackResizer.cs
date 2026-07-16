using UnityEngine;

public class PlatformStackResizer : MonoBehaviour
{
    public enum Difficulty { None, Full, Medium, Small }

    [Tooltip("Set to override ALL platforms with the same difficulty. None = use per-platform settings below.")]
    public Difficulty globalDifficulty = Difficulty.None;

    [Tooltip("Index 0 = Platform 1, Index 1 = Platform 2, etc. Used only if Global Difficulty is None.")]
    public Difficulty[] platformDifficulties = new Difficulty[10];

    private const float LEFT_WALL_MEDIUM = -7.78f;
    private const float RIGHT_WALL_MEDIUM = 7.57f;
    private const float LEFT_WALL_SMALL = -4.75f;
    private const float RIGHT_WALL_SMALL = 2.76f;

    private const float BG3_MEDIUM_SCALE = 2.901951f;
    private const float BG3_SMALL_SCALE = 1.4128f;

    void Awake()
    {
        for (int i = 0; i < 10; i++)
        {
            Transform platform = transform.Find("Platform " + (i + 1));
            if (platform == null) continue;

            Difficulty diff = (globalDifficulty != Difficulty.None)
                ? globalDifficulty
                : (i < platformDifficulties.Length ? platformDifficulties[i] : Difficulty.Full);

            ApplyDifficulty(platform, diff);
        }
    }

    void ApplyDifficulty(Transform platform, Difficulty difficulty)
    {
        if (difficulty == Difficulty.Full || difficulty == Difficulty.None) return;

        string[] mediumDisable = { "Floor_Tile_01", "Floor_Tile_09" };
        string[] smallDisable = { "Floor_Tile_01", "Floor_Tile_02", "Floor_Tile_03",
                                   "Floor_Tile_07", "Floor_Tile_08", "Floor_Tile_09" };

        foreach (string tileName in (difficulty == Difficulty.Medium ? mediumDisable : smallDisable))
        {
            Transform tile = platform.Find(tileName);
            if (tile != null) tile.gameObject.SetActive(false);
        }

        Transform leftWall = FindChildByTag(platform, "Left Wall");
        Transform rightWall = FindChildByTag(platform, "Right Wall");

        if (leftWall != null)
        {
            var p = leftWall.localPosition;
            p.x = (difficulty == Difficulty.Medium) ? LEFT_WALL_MEDIUM : LEFT_WALL_SMALL;
            leftWall.localPosition = p;
        }

        if (rightWall != null)
        {
            var p = rightWall.localPosition;
            p.x = (difficulty == Difficulty.Medium) ? RIGHT_WALL_MEDIUM : RIGHT_WALL_SMALL;
            rightWall.localPosition = p;
        }

        Transform bg3 = FindDeepChild(platform, "Background_gameplay (3)");
        if (bg3 != null)
        {
            var scale = bg3.localScale;
            scale.x = (difficulty == Difficulty.Medium) ? BG3_MEDIUM_SCALE : BG3_SMALL_SCALE;
            bg3.localScale = scale;
        }
    }

    Transform FindChildByTag(Transform parent, string tag)
    {
        foreach (Transform child in parent)
            if (child.CompareTag(tag)) return child;
        return null;
    }

    Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform result = FindDeepChild(child, childName);
            if (result != null) return result;
        }
        return null;
    }
}