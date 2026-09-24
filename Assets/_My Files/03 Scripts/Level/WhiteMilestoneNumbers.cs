using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Numbers the white milestone bands 00, 01, 02... in climbing order, in the score's font.
/// Uses the (previously hidden) TextMeshPro inside each band's Milestones group.
/// Works for the hand-built level and the endless level (new chunks are picked up as they spawn).
/// </summary>
public class WhiteMilestoneNumbers : MonoBehaviour
{
    [Tooltip("Parent of the stacks / chunks (Manual Testing Platforms).")]
    public Transform container;
    [Tooltip("The score text whose font, material and colour the numbers copy.")]
    public TMP_Text scoreText;
    [Tooltip("Colour of the numbers (warm cream #FFF1D2). Untick to use the score text's colour.")]
    public bool useOwnColor = true;
    public Color numberColor = new Color(1f, 241f / 255f, 210f / 255f, 1f);   // #FFF1D2

    [Tooltip("Number shown on the first white floor.")]
    public int firstNumber = 0;
    [Tooltip("Digits shown (2 = 00, 01 ...). Bigger numbers simply grow.")]
    public int digits = 2;
    [Tooltip("Text size in world units (the band is about 2.5 tall).")]
    public float fontSize = 20f;
    [Tooltip("Draw above the band and stones, below Brutus and eggs.")]
    public int sortingOrder = 11;

    public enum Side { Left, Right, AwayFromTorch }
    [Tooltip("Which end of the band the number sits at. AwayFromTorch picks the end with no torch on that row (left if both are clear).")]
    public Side side = Side.AwayFromTorch;
    [Tooltip("Gap between the number and the end of the band.")]
    public float edgePadding = 0.8f;

    private readonly HashSet<Transform> _seen = new HashSet<Transform>();
    private int _next;

    private void Start()
    {
        _next = firstNumber;
        Scan();
        InvokeRepeating(nameof(Scan), 0.25f, 0.25f);
    }

    private void Scan()
    {
        if (container == null) return;
        var bands = new List<SpriteRenderer>();
        foreach (Transform stack in container)
        {
            if (_seen.Contains(stack) || !stack.gameObject.activeInHierarchy) continue;
            _seen.Add(stack);
            foreach (var sr in stack.GetComponentsInChildren<SpriteRenderer>(false))
                if (IsWhiteBand(sr)) bands.Add(sr);
        }
        bands.Sort((a, b) => a.bounds.center.y.CompareTo(b.bounds.center.y));
        foreach (var band in bands)
        {
            // the endless level tags each band with its chunk number; otherwise number in climbing order
            var mi = band.GetComponentInParent<MilestoneIndex>();
            if (mi != null) Label(band, firstNumber + mi.value);
            else if (EndlessLevel.Instance == null) Label(band, _next++);   // endless: only chunk-top milestones are numbered
        }
    }

    /// <summary>Is a torch showing on this band's row, on the given wall?</summary>
    private static bool TorchOn(SpriteRenderer band, bool left)
    {
        Transform plat = band.transform;
        while (plat != null && !plat.name.StartsWith("Platform")) plat = plat.parent;
        if (plat == null) return false;
        string wallName = left ? "Left Wall" : "Right Wall";
        foreach (Transform c in plat)
        {
            if (c.name.Trim() != wallName || !c.gameObject.activeInHierarchy) continue;
            foreach (Transform t in c)
                if (t.name.StartsWith("Torch"))
                    foreach (var r in t.GetComponentsInChildren<SpriteRenderer>(false)) if (r.enabled) return true;
        }
        return false;
    }

    private static bool IsWhiteBand(SpriteRenderer sr)
    {
        if (sr == null || !sr.enabled || sr.sprite == null || sr.sprite.name != "Square") return false;
        var c = sr.color;
        return c.r > 0.9f && c.g > 0.9f && c.b > 0.9f && c.a < 0.6f;
    }

    private void Label(SpriteRenderer band, int number)
    {
        var group = band.transform.parent;
        TextMeshPro tmp = group != null ? group.GetComponentInChildren<TextMeshPro>(true) : null;
        if (tmp == null)
        {
            var go = new GameObject("Milestone Number", typeof(TextMeshPro));
            go.transform.SetParent(group != null ? group : band.transform, false);
            tmp = go.GetComponent<TextMeshPro>();
        }
        tmp.gameObject.SetActive(true);
        if (scoreText != null)
        {
            tmp.font = scoreText.font;
            tmp.fontSharedMaterial = scoreText.fontSharedMaterial;
            tmp.color = scoreText.color;
        }
        if (useOwnColor) tmp.color = numberColor;
        tmp.text = number.ToString(new string('0', Mathf.Max(1, digits)));
        tmp.margin = Vector4.zero;                 // the original text was parked to the side with a big margin
        tmp.enableAutoSizing = false;
        tmp.fontSize = fontSize;
        bool right = side == Side.Right || (side == Side.AwayFromTorch && TorchOn(band, left: true) && !TorchOn(band, left: false));
        tmp.alignment = right ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
        tmp.sortingOrder = sortingOrder;
        var b = band.bounds;
        tmp.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, b.size.x - 2f * edgePadding), b.size.y);
        tmp.transform.rotation = Quaternion.identity;
        tmp.transform.localScale = Vector3.one;
        tmp.transform.position = new Vector3(b.center.x, b.center.y, b.center.z - 0.01f);
    }
}
