using UnityEngine;

/// <summary>
/// Device-side Test Mode switch (saved in PlayerPrefs). Turned on/off with the hidden
/// long-press + code on the settings gear (see SecretTestModeTrigger).
/// </summary>
public static class TestMode
{
    private const string Key = "brutus_test_mode";

    public static event System.Action<bool> Changed;

    public static bool Enabled
    {
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        get => PlayerPrefs.GetInt(Key, 0) == 1;
#else
        get => false;   // store (release) builds: test mode can never be on
#endif
        set
        {
            PlayerPrefs.SetInt(Key, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke(value);
        }
    }
}
