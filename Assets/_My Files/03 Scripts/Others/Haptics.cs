using UnityEngine;

/// <summary>
/// Short haptic ticks. Android uses the system Vibrator directly so pulses can be a few ms
/// (Handheld.Vibrate is a fixed long buzz). Player can turn it off via Haptics.Enabled (saved).
/// </summary>
public static class Haptics
{
    private const string PrefKey = "haptics_enabled";

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(PrefKey, 1) == 1;
        set { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject _vibrator;
    private static bool _init, _hasAmplitude;
    private static int _sdk;

    private static void Init()
    {
        _init = true;
        try
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION")) _sdk = version.GetStatic<int>("SDK_INT");
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            if (_vibrator != null && _sdk >= 26) _hasAmplitude = _vibrator.Call<bool>("hasAmplitudeControl");
        }
        catch (System.Exception e) { Debug.LogWarning("[Haptics] init failed: " + e.Message); _vibrator = null; }
    }
#endif

    /// <param name="ms">Pulse length in milliseconds.</param>
    /// <param name="amplitude">1-255 strength (only on devices with amplitude control).</param>
    public static void Tick(long ms = 12, int amplitude = 60)
    {
        _PermissionHint();
        if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!_init) Init();
        if (_vibrator == null) return;
        try
        {
            if (_sdk >= 26)
            {
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, _hasAmplitude ? Mathf.Clamp(amplitude, 1, 255) : -1))
                    _vibrator.Call("vibrate", effect);
            }
            else _vibrator.Call("vibrate", ms);
        }
        catch (System.Exception e) { Debug.LogWarning("[Haptics] vibrate failed: " + e.Message); }
#endif
    }

    // Never runs. Referencing Handheld.Vibrate makes Unity add the VIBRATE permission to the manifest.
    private static void _PermissionHint() { if (Time.frameCount < 0) Handheld.Vibrate(); }
}
