using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shifts the backdrop's colour mood every 50 floors of the endless level, blending over the
/// last 10 floors of each band, so height is visible (like High Risers' sky).
/// Multiplies the colours the sprites start with, so the scene's colour grade stays the base.
/// Put it on the camera's Background object.
/// </summary>
public class BackdropStages : MonoBehaviour
{
    [Tooltip("Tint per 50-floor stage. The last one holds for all higher floors.")]
    public Color[] stageTints =
    {
        new Color(1.00f, 1.00f, 1.00f),   //   0-50  as designed
        new Color(1.00f, 0.92f, 0.84f),   //  50-100 warm late afternoon
        new Color(1.00f, 0.84f, 0.86f),   // 100-150 rosy sunset
        new Color(0.88f, 0.80f, 0.98f),   // 150-200 violet dusk
        new Color(0.74f, 0.74f, 0.92f),   // 200-250 blue evening
        new Color(0.62f, 0.66f, 0.88f),   // 250+    moonlit
    };
    public int floorsPerStage = 50;
    public int blendFloors = 10;

    private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
    private readonly List<Color> _base = new List<Color>();
    private Color _applied = Color.white;

    private void Start()
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true)) { _renderers.Add(sr); _base.Add(sr.color); }
    }

    private void LateUpdate()
    {
        var level = EndlessLevel.Instance;
        if (level == null || stageTints.Length == 0) return;

        float f = level.CurrentFloor;
        int stage = Mathf.FloorToInt(f / floorsPerStage);
        float into = f - stage * floorsPerStage;
        Color a = stageTints[Mathf.Min(stage, stageTints.Length - 1)];
        Color b = stageTints[Mathf.Min(stage + 1, stageTints.Length - 1)];
        float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(floorsPerStage - blendFloors, floorsPerStage, into));
        Color target = Color.Lerp(a, b, t);

        _applied = Color.Lerp(_applied, target, 1f - Mathf.Exp(-3f * Time.deltaTime));   // never snaps (e.g. after a revive)
        for (int i = 0; i < _renderers.Count; i++)
        {
            if (_renderers[i] == null) continue;
            Color c = _base[i];
            _renderers[i].color = new Color(c.r * _applied.r, c.g * _applied.g, c.b * _applied.b, c.a);
        }
    }
}
