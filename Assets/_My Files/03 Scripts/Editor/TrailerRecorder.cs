#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;

/// <summary>
/// Tools > Brutus > Record 10s Trailer
/// Starts the endless run at a checkpoint (floor 200, right before the twin towers), lets the Autopilot
/// play, records 10 s at 1080x1920 / 60 fps to Recordings/, then restores everything.
/// </summary>
[InitializeOnLoad]
public static class TrailerRecorder
{
    private const string Pending = "BrutusTrailer.pending", SavedBest = "BrutusTrailer.savedBest", SavedBg = "BrutusTrailer.savedBg";
    public static float StartDelay = 1.0f, Duration = 10f;
    public static int StartBest = 400;             // checkpoint start = half of this = floor 200
    public static float CloseCallChance = 0.5f;
    public static float FailAfter = 8.6f;          // seconds after the first tap: the deliberate miss (the clip ends on the fall)
    private static RecorderController _controller;

    static TrailerRecorder() { EditorApplication.playModeStateChanged += OnPlayMode; }

    [MenuItem("Tools/Brutus/Record 10s Trailer")]
    public static void Record()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[Trailer] Stop Play mode first."); return; }
        SessionState.SetInt(SavedBest, PlayerPrefs.GetInt("endless_best_floor", 0));
        SessionState.SetBool(SavedBg, PlayerSettings.runInBackground);
        SessionState.SetInt("BrutusTrailer.savedTest", PlayerPrefs.GetInt("brutus_test_mode", 0));
        PlayerPrefs.SetInt("brutus_test_mode", 0);   // no auto rocket-revive: the clip should end on the fall
        PlayerPrefs.SetInt("endless_best_floor", StartBest);
        PlayerPrefs.SetInt("endless_first_run_done", 1);
        PlayerPrefs.Save();
        PlayerSettings.runInBackground = true;          // keep playing while the editor is not focused
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayMode(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
        {
            var ap = new GameObject("Autopilot (recording)").AddComponent<Autopilot>();
            ap.closeCallChance = CloseCallChance;
            ap.failAfter = FailAfter;   // misses a jump near the end of the clip
            StartRecording();
            EditorApplication.update += WatchForEnd;
        }
        else if (s == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending, false))
        {
            SessionState.SetBool(Pending, false);
            PlayerPrefs.SetInt("endless_best_floor", SessionState.GetInt(SavedBest, 0));
            PlayerPrefs.SetInt("brutus_test_mode", SessionState.GetInt("BrutusTrailer.savedTest", 0));
            PlayerPrefs.Save();
            PlayerSettings.runInBackground = SessionState.GetBool(SavedBg, false);
            Debug.Log("[Trailer] Done. Video saved in the project's Recordings folder.");
        }
    }

    private static void StartRecording()
    {
        var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = "Brutus Trailer";
        movie.Enabled = true;
        movie.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
        movie.VideoBitRateMode = VideoBitrateMode.High;
        movie.ImageInputSettings = new CameraInputSettings
        {
            Source = ImageSource.MainCamera,
            OutputWidth = 1080,
            OutputHeight = 1920,
            CaptureUI = true
        };
        movie.AudioInputSettings.PreserveAudio = true;
        movie.OutputFile = "Recordings/Brutus_Trailer_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        settings.AddRecorderSettings(movie);
        settings.SetRecordModeToTimeInterval(StartDelay, StartDelay + Duration);
        settings.FrameRate = 60f;
        settings.FrameRatePlayback = FrameRatePlayback.Constant;
        settings.CapFrameRate = true;
        _controller = new RecorderController(settings);
        _controller.PrepareRecording();
        _controller.StartRecording();
        Debug.Log("[Trailer] Recording " + Duration + " s to " + movie.OutputFile + ".mp4");
    }

    private static void WatchForEnd()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= WatchForEnd; return; }
        if (Time.time > StartDelay + Duration + 0.5f && (_controller == null || !_controller.IsRecording()))
        {
            EditorApplication.update -= WatchForEnd;
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
