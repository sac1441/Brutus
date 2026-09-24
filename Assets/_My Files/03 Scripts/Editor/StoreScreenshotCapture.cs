using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Tools > Brutus > Capture Store Screenshots
/// Starts a run at floor 200 (just below the twin towers), lets the Autopilot play cleanly
/// (no close calls), and saves 8 PNGs of the Game view to the project's Screenshots/ folder.
/// Restores best floor and runInBackground afterwards. Keep the Game view in front while it runs.
/// Tip: set the Game view to a 1080x1920 portrait resolution first for store-ready sizes.
/// </summary>
[InitializeOnLoad]
public static class StoreScreenshotCapture
{
    private const string Pending = "BrutusShots.pending", SavedBest = "BrutusShots.savedBest", SavedBg = "BrutusShots.savedBg";

    public static int   StartBest  = 380;   // checkpoint start = floor 190, runs into the twin towers at 200
    public static float StartDelay = 2.0f;
    public static float Interval   = 2.6f;  // spread shots across different sections
    public static int   ShotCount  = 8;     // Google Play max

    private static int _taken;
    private static double _nextAt;

    static StoreScreenshotCapture() { EditorApplication.playModeStateChanged += OnPlayMode; }

    private static string Folder => Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");

    [MenuItem("Tools/Brutus/Capture Store Screenshots")]
    public static void Capture()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[Shots] Stop Play mode first."); return; }
        SessionState.SetInt(SavedBest, PlayerPrefs.GetInt("endless_best_floor", 0));
        SessionState.SetBool(SavedBg, PlayerSettings.runInBackground);
        PlayerPrefs.SetInt("endless_best_floor", StartBest);
        PlayerPrefs.SetInt("endless_first_run_done", 1);
        PlayerPrefs.Save();
        PlayerSettings.runInBackground = true;
        Directory.CreateDirectory(Folder);
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayMode(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
        {
            var ap = new GameObject("Autopilot (screenshots)").AddComponent<Autopilot>();
            ap.closeCallChance = 0f;   // clean, confident play for store shots
            ap.failAfter = -1f;
            new GameObject("Screenshot Zoom").AddComponent<ScreenshotZoom>();   // bigger Brutus in store shots
            _taken = 0;
            _nextAt = EditorApplication.timeSinceStartup + StartDelay;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
        else if (s == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending, false))
        {
            SessionState.SetBool(Pending, false);
            EditorApplication.update -= Tick;
            PlayerPrefs.SetInt("endless_best_floor", SessionState.GetInt(SavedBest, 0));
            PlayerPrefs.Save();
            PlayerSettings.runInBackground = SessionState.GetBool(SavedBg, false);
            Debug.Log($"[Shots] Done. Screenshots saved in {Folder}");
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        if (Time.timeScale == 0f) { _nextAt = EditorApplication.timeSinceStartup + 0.5; return; } // skip death/revive screens
        if (_taken >= ShotCount) { EditorApplication.isPlaying = false; return; }
        if (EditorApplication.timeSinceStartup < _nextAt) return;
        _nextAt = EditorApplication.timeSinceStartup + Interval;

        string file = Path.Combine(Folder, $"brutus_shot_{System.DateTime.Now:HHmmss}_{_taken:00}.png");
        ScreenCapture.CaptureScreenshot(file);
        _taken++;
        Debug.Log($"[Shots] Captured {_taken}/{ShotCount}: {file}");
    }
}
