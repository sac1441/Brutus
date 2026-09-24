using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hidden Test Mode switch. Put on the settings gear button.
/// Hold the gear for holdSeconds -> a small code pad opens (game paused, pause menu not opened).
/// Correct code toggles TestMode.Enabled; a wrong code silently clears. While Test Mode is on,
/// a tiny dot shows in the bottom-left corner.
/// </summary>
public class SecretTestModeTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("Seconds the gear must be held.")]
    public float holdSeconds = 3f;
    [Tooltip("Code that toggles Test Mode. Change it before publishing.")]
    public string code = "0000";

    private Button _button;
    private bool _holding, _fired;
    private float _heldFor;

    private GameObject _canvas, _pad, _dot;
    private Text _display;
    private string _entered = "";
    private float _prevTimeScale = 1f;
    private bool _prevInputLocked;

    private void Awake()
    {
        _button = GetComponent<Button>();
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        enabled = false;   // store (release) builds: no hidden code pad, no dot
        return;
#endif
        BuildUI();
        TestMode.Changed += OnChanged;
        OnChanged(TestMode.Enabled);
    }

    private void OnDestroy()
    {
        TestMode.Changed -= OnChanged;
        if (_canvas != null) Destroy(_canvas);
    }

    // --------------------------------------------------------------- long press
    public void OnPointerDown(PointerEventData e) { _holding = true; _fired = false; _heldFor = 0f; }
    public void OnPointerExit(PointerEventData e) { _holding = false; }
    public void OnPointerUp(PointerEventData e)
    {
        _holding = false;
        if (_fired) StartCoroutine(ReenableButtonNextFrame());   // swallow the click that follows a long press
    }

    private void Update()
    {
        if (!_holding || _fired) return;
        _heldFor += Time.unscaledDeltaTime;
        if (_heldFor < holdSeconds) return;
        _fired = true;
        if (_button != null) _button.enabled = false;             // so releasing doesn't open the pause menu
        OpenPad();
    }

    private IEnumerator ReenableButtonNextFrame()
    {
        yield return null;
        if (_button != null) _button.enabled = true;
    }

    // --------------------------------------------------------------- code pad
    private void OpenPad()
    {
        _entered = ""; UpdateDisplay();
        _prevTimeScale = Time.timeScale; Time.timeScale = 0f;
        var pc = FindFirstObjectByType<TarodevController.PlayerController>();
        if (pc != null) { _prevInputLocked = pc.InputLocked; pc.InputLocked = true; }
        _pad.SetActive(true);
    }

    private void ClosePad()
    {
        _pad.SetActive(false);
        _entered = "";
        Time.timeScale = _prevTimeScale;
        var pc = FindFirstObjectByType<TarodevController.PlayerController>();
        if (pc != null) StartCoroutine(RestoreInputNextFrame(pc));  // the closing tap must not become a jump
    }

    private IEnumerator RestoreInputNextFrame(TarodevController.PlayerController pc)
    {
        yield return null;
        pc.InputLocked = _prevInputLocked;
    }

    private void Press(string key)
    {
        if (key == "C") { _entered = ""; UpdateDisplay(); return; }
        if (key == "OK")
        {
            if (_entered == code) { TestMode.Enabled = !TestMode.Enabled; ClosePad(); }
            else { _entered = ""; UpdateDisplay(); }                  // wrong code: silently clear
            return;
        }
        if (_entered.Length < 8) { _entered += key; UpdateDisplay(); }
    }

    private void UpdateDisplay() { if (_display != null) _display.text = _entered.Length == 0 ? "- - - -" : new string('*', _entered.Length); }

    private void OnChanged(bool on) { if (_dot != null) _dot.SetActive(on); }

    // --------------------------------------------------------------- UI built in code
    private void BuildUI()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _canvas = new GameObject("SecretTestModeUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var cv = _canvas.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 500;
        var sc = _canvas.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1080, 1920); sc.matchWidthOrHeight = 0.5f;

        // tiny corner dot
        _dot = Box(_canvas.transform, "TestModeDot", new Color(1f, 0.3f, 0.3f, 0.75f));
        var dr = (RectTransform)_dot.transform; dr.anchorMin = dr.anchorMax = dr.pivot = new Vector2(0f, 0f); dr.anchoredPosition = new Vector2(28f, 28f); dr.sizeDelta = new Vector2(16f, 16f);
        _dot.GetComponent<Image>().raycastTarget = false;

        // pad
        _pad = Box(_canvas.transform, "CodePad", new Color(0f, 0f, 0f, 0.6f));      // full-screen dim, blocks taps
        var pr = (RectTransform)_pad.transform; pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero;
        var panel = Box(_pad.transform, "Panel", new Color(0.12f, 0.1f, 0.12f, 0.95f));
        var pnr = (RectTransform)panel.transform; pnr.sizeDelta = new Vector2(620f, 860f);

        _display = Label(panel.transform, "Display", font, 64, new Vector2(0f, 330f), new Vector2(560f, 110f));
        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "OK" };
        for (int i = 0; i < keys.Length; i++)
        {
            int r = i / 3, c = i % 3;
            string k = keys[i];
            var b = Box(panel.transform, "Key " + k, new Color(0.3f, 0.26f, 0.28f, 1f));
            var br = (RectTransform)b.transform; br.sizeDelta = new Vector2(170f, 130f); br.anchoredPosition = new Vector2((c - 1) * 190f, 170f - r * 150f);
            b.AddComponent<Button>().onClick.AddListener(() => Press(k));
            var t = Label(b.transform, "Text", font, 52, Vector2.zero, br.sizeDelta); t.text = k;
        }
        var close = Box(panel.transform, "Close", new Color(0.45f, 0.2f, 0.2f, 1f));
        var cr = (RectTransform)close.transform; cr.sizeDelta = new Vector2(560f, 90f); cr.anchoredPosition = new Vector2(0f, -375f);
        close.AddComponent<Button>().onClick.AddListener(ClosePad);
        var ct = Label(close.transform, "Text", font, 40, Vector2.zero, cr.sizeDelta); ct.text = "Close";
        _pad.SetActive(false);
        UpdateDisplay();
    }

    private static GameObject Box(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static Text Label(Transform parent, string name, Font font, int size, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform; rt.anchoredPosition = pos; rt.sizeDelta = box;
        var t = go.GetComponent<Text>(); t.font = font; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
        return t;
    }
}
