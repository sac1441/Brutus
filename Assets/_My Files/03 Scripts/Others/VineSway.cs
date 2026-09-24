using UnityEngine;

/// <summary>
/// Gentle wind sway for hanging vines. Rotates around the sprite's top pivot.
/// Each instance gets a random phase and speed so vines drift independently.
/// </summary>
public class VineSway : MonoBehaviour
{
    [Tooltip("Max lean angle in degrees.")]
    public float amplitude = 2.5f;
    [Tooltip("Base sway speed. Randomised +/-30% per vine.")]
    public float speed = 0.7f;
    [Tooltip("Occasional gust strength added on top of the base sway.")]
    public float gustAmplitude = 1.5f;

    private float _phase;
    private float _speedMul;
    private float _gustSeed;
    private float _baseZ;

    private void Awake()
    {
        _phase = Random.Range(0f, Mathf.PI * 2f);
        _speedMul = Random.Range(0.7f, 1.3f);
        _gustSeed = Random.Range(0f, 100f);
        _baseZ = transform.localEulerAngles.z;
    }

    private void Update()
    {
        float t = Time.time * speed * _speedMul + _phase;
        float sway = Mathf.Sin(t) * amplitude + Mathf.Sin(t * 2.3f + 1.7f) * amplitude * 0.35f;
        float gust = (Mathf.PerlinNoise(_gustSeed, Time.time * 0.15f) - 0.5f) * 2f * gustAmplitude;
        transform.localRotation = Quaternion.Euler(0f, 0f, _baseZ + sway + gust);
    }
}
