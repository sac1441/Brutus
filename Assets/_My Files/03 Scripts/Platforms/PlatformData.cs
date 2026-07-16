using System.Collections.Generic;

[System.Serializable]
public class PlatformConfig
{
    public float y;

    public int[] floors;

    public bool leftWall;

    public bool rightWall;

    public int[] coins;

    public int[] traps;

    // NEW

    public bool showBackground;

    public bool showLabel;

    public string labelText;

    // NEW - scaffold/dragon obstacle anchor. When true, both Dragon Left and
    // Dragon Right prefabs are spawned at this floor's position. Each prefab
    // already contains its own fixed run of vine pairs (cleared progressively
    // by ScaffoldController on player collision), so this is placed once at
    // the bottom of a scaffold section, not per-floor.
    public bool hasScaffold;
}

[System.Serializable]
public class PlatformData
{
    public List<PlatformConfig> platforms;
}