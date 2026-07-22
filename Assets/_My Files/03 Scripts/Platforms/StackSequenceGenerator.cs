using UnityEngine;
using TMPro;

/// <summary>
/// Generates stacks (VerticalStack / HorizontalStack / ScaffoldStack prefabs)
/// and labels each one's milestone text the moment it's created -- all in one
/// place, one script.
///
/// Stack 1  -> milestone text = "10"
/// Stack 2  -> milestone text = "20"
/// Stack 3  -> milestone text = "30"
/// ...
/// Stack N  -> milestone text = N * 10
/// </summary>
public class StackSequenceGenerator : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject verticalStackPrefab;
    public GameObject horizontalStackPrefab;
    public GameObject scaffoldStackPrefab;

    [Header("Settings")]
    public int stackCount = 30;
    public int seed = 42;
    public Transform container;

    [Header("Confirmed real spacing")]
    public float startY = -15f;
    public float stackHeight = 30f;
    public float horizontalToNextGap = 21f;
    public float horizontalToNextXShift = -123f;
    public float scaffoldToNextGap = 42.6f;
    public float scaffoldToNextXShift = 0f;
    public float scaffoldExitGap = 18.81f;
    public float scaffoldExitXShift = 0f;
    public float verticalStackZ = -156.2f;
    public float horizontalStackZ = -6.2f;

    private System.Random rng;

    private enum StackType { Vertical, Horizontal, Scaffold }

    void Start()
    {
        Generate();
    }

    [ContextMenu("Generate Stacks")]
    public void Generate()
    {
        if (verticalStackPrefab == null)
        {
            Debug.LogError("StackSequenceGenerator: Vertical Stack Prefab not assigned.");
            return;
        }

        rng = new System.Random(seed);
        Transform parent = container != null ? container : transform;

        int count = Mathf.Clamp(stackCount, 1, 30);

        float currentY = startY;
        float currentX = 0f;
        bool previousWasHorizontal = false;
        bool previousWasScaffold = false;

        for (int i = 0; i < count; i++)
        {
            int stackNumber = i + 1;

            StackType type = PickStackType(stackNumber, count);
            GameObject prefabToUse = ResolvePrefab(type);

            bool isNonVertical = (type != StackType.Vertical);

            float y, x;
            if (i == 0)
            {
                y = startY;
                x = 0f;
            }
            else if (type == StackType.Scaffold)
            {
                // Entering a Scaffold stack always uses its own dedicated gap,
                // regardless of what the previous stack type was.
                y = currentY + scaffoldToNextGap;
                x = currentX + scaffoldToNextXShift;
            }
            else if (previousWasScaffold)
            {
                // Leaving a Scaffold stack also uses its own dedicated gap,
                // regardless of what the next stack type is.
                y = currentY + scaffoldExitGap;
                x = currentX + scaffoldExitXShift;
            }
            else
            {
                y = currentY + (previousWasHorizontal ? horizontalToNextGap : stackHeight);
                x = currentX + (previousWasHorizontal ? horizontalToNextXShift : 0f);
            }

            float z = isNonVertical ? horizontalStackZ : verticalStackZ;

            // Create the stack.
            GameObject instance = Instantiate(prefabToUse, parent);
            instance.transform.localPosition = new Vector3(x, y, z);
            instance.transform.localRotation = Quaternion.identity;
            instance.name = stackNumber.ToString();

            // Set difficulty per platform inside this stack, using the real
            // PlatformStackResizer component -- easier early, harder later.
            PlatformStackResizer resizer = instance.GetComponent<PlatformStackResizer>();
            if (resizer == null)
            {
                resizer = instance.AddComponent<PlatformStackResizer>();
            }
            resizer.globalDifficulty = PlatformStackResizer.Difficulty.None; // use per-platform array below
            int firstFloorOfStack = (stackNumber - 1) * 10 + 1;
            resizer.platformDifficulties = BuildDifficultyArray(firstFloorOfStack);

            // Label it immediately -- stack 1 = "10", stack 2 = "20", etc.
            int milestoneNumber = stackNumber * 10;
            SetMilestoneLabel(instance, milestoneNumber);

            currentY = y;
            currentX = x;
            previousWasHorizontal = (type == StackType.Horizontal);
            previousWasScaffold = (type == StackType.Scaffold);
        }

        Debug.Log($"StackSequenceGenerator: generated and labeled {count} stacks.");
    }

    /// <summary>
    /// Builds the 10-element Difficulty array for one stack, given the global
    /// floor number of its first platform. Index 9 (the milestone floor, always
    /// "Platform 10") is always Full -- a milestone should never also be a
    /// precision landing.
    /// </summary>
    PlatformStackResizer.Difficulty[] BuildDifficultyArray(int firstFloorOfStack)
    {
        var result = new PlatformStackResizer.Difficulty[10];

        for (int j = 0; j < 10; j++)
        {
            int floorNumber = firstFloorOfStack + j;
            bool isMilestone = (j == 9);

            result[j] = isMilestone
                ? PlatformStackResizer.Difficulty.Full
                : PickDifficultyForFloor(floorNumber);
        }

        return result;
    }

    PlatformStackResizer.Difficulty PickDifficultyForFloor(int floorNumber)
    {
        // Weights per band: {Full, Medium, Small}
        float wFull, wMedium, wSmall;

        if (floorNumber <= 10)                            // Tutorial
        {
            wFull = 1.0f; wMedium = 0.0f; wSmall = 0.0f;
        }
        else if (floorNumber <= 40)                        // Foundation
        {
            wFull = 0.70f; wMedium = 0.30f; wSmall = 0.0f;
        }
        else if (floorNumber <= 90)                        // Gap
        {
            wFull = 0.40f; wMedium = 0.40f; wSmall = 0.20f;
        }
        else if (floorNumber <= 150)                       // Precision
        {
            wFull = 0.20f; wMedium = 0.45f; wSmall = 0.35f;
        }
        else if (floorNumber <= 220)                       // High Risk
        {
            wFull = 0.10f; wMedium = 0.35f; wSmall = 0.55f;
        }
        else                                                // Expert
        {
            wFull = 0.05f; wMedium = 0.25f; wSmall = 0.70f;
        }

        float roll = (float)rng.NextDouble() * (wFull + wMedium + wSmall);

        if (roll < wFull) return PlatformStackResizer.Difficulty.Full;
        if (roll < wFull + wMedium) return PlatformStackResizer.Difficulty.Medium;
        return PlatformStackResizer.Difficulty.Small;
    }

    void SetMilestoneLabel(GameObject stack, int number)
    {
        Transform milestones = null;
        foreach (Transform t in stack.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Milestones")
            {
                milestones = t;
                break;
            }
        }

        if (milestones == null)
        {
            Debug.LogWarning($"StackSequenceGenerator: no 'Milestones' found inside '{stack.name}'.");
            return;
        }

        TMP_Text label = milestones.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            Debug.LogWarning($"StackSequenceGenerator: 'Milestones' found in '{stack.name}' but no text inside it.");
            return;
        }

        label.text = number.ToString();
    }

    /// <summary>
    /// Picks Vertical, Horizontal, or Scaffold for this stack. Stack 1 is
    /// always Vertical. Otherwise the same probability curve used to govern
    /// "non-vertical" chance is split evenly between Horizontal and Scaffold.
    /// </summary>
    StackType PickStackType(int stackNumber, int totalStacks)
    {
        if (stackNumber == 1) return StackType.Vertical;

        float progress = (float)(stackNumber - 1) / Mathf.Max(1, totalStacks - 1);
        float nonVerticalChance = Mathf.Lerp(0.15f, 0.25f, progress);

        float roll = (float)rng.NextDouble();
        if (roll >= nonVerticalChance)
        {
            return StackType.Vertical;
        }

        // Within the "non-vertical" slice, split 50/50 between Horizontal and Scaffold.
        float subRoll = (float)rng.NextDouble();
        return subRoll < 0.5f ? StackType.Horizontal : StackType.Scaffold;
    }

    GameObject ResolvePrefab(StackType type)
    {
        switch (type)
        {
            case StackType.Horizontal:
                if (horizontalStackPrefab != null) return horizontalStackPrefab;
                break;
            case StackType.Scaffold:
                if (scaffoldStackPrefab != null) return scaffoldStackPrefab;
                break;
        }

        // Fallback to Vertical if the requested prefab isn't assigned.
        return verticalStackPrefab;
    }
}