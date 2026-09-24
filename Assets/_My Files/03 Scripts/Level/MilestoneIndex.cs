using UnityEngine;

/// <summary>
/// Marks a white milestone band with the number it should show (set by EndlessLevel = chunk index),
/// so numbering stays correct even when chunks are generated ahead or skipped by a checkpoint start.
/// </summary>
public class MilestoneIndex : MonoBehaviour
{
    public int value;
}
