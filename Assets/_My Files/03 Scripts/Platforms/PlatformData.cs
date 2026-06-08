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
}

[System.Serializable]
public class PlatformData
{
    public List<PlatformConfig> platforms;
}