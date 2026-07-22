using UnityEngine;
using TMPro;

/// <summary>
/// Same exact steps that already worked for stack 1, now applied to every
/// stack under Manual Platforms:
///
///   For each stack (in order):
///     1. Go to its LAST child (its 10th platform)
///     2. Go inside "Milestones"
///     3. Set its text to (stack position * 10)
///
/// Stack 1 -> "10", Stack 2 -> "20", Stack 3 -> "30", ... and so on.
/// </summary>
public class TestSingleStackLabel : MonoBehaviour
{
    [Tooltip("Drag 'Manual Platforms' here.")]
    public Transform manualPlatforms;

    [Tooltip("If true, runs automatically 1 second after Play starts.")]
    public bool runAutomaticallyOnPlay = true;

    void Start()
    {
        if (runAutomaticallyOnPlay)
        {
            Invoke(nameof(Run), 1f);
        }
    }

    [ContextMenu("Label Every Stack")]
    public void Run()
    {
        if (manualPlatforms == null)
        {
            Debug.LogError("TestSingleStackLabel: 'Manual Platforms' not assigned.");
            return;
        }

        int labeled = 0;

        for (int i = 0; i < manualPlatforms.childCount; i++)
        {
            Transform stack = manualPlatforms.GetChild(i);
            int milestoneValue = (i + 1) * 10;

            if (stack.childCount == 0)
            {
                Debug.LogWarning($"TestSingleStackLabel: '{stack.name}' has no children, skipping.");
                continue;
            }

            // Go to this stack's LAST child (its 10th platform).
            Transform lastPlatform = stack.GetChild(stack.childCount - 1);

            // Go inside "Milestones".
            Transform milestones = lastPlatform.Find("Milestones");
            if (milestones == null)
            {
                Debug.LogWarning($"TestSingleStackLabel: no 'Milestones' found inside '{stack.name}' -> '{lastPlatform.name}', skipping.");
                continue;
            }

            // Set the text.
            TMP_Text label = milestones.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                Debug.LogWarning($"TestSingleStackLabel: no text found inside 'Milestones' for '{stack.name}', skipping.");
                continue;
            }

            label.text = milestoneValue.ToString();
            labeled++;

            Debug.Log($"TestSingleStackLabel: stack '{stack.name}' (position {i + 1}) -> milestone set to {milestoneValue}");
        }

        Debug.Log($"TestSingleStackLabel: done. Labeled {labeled} of {manualPlatforms.childCount} stacks.");
    }
}