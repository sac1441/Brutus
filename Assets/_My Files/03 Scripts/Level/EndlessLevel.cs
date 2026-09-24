using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Endless level: clones the hand-built stacks as chunks and chains them upward forever.
///
/// Fundamentals it never breaks:
///  - every join is a reachable jump (next entry floor = previous exit floor + joinGap, centred over it)
///  - only proven type joins are used: V->V, V->S, V->H, S->V, S->H, H->V
///  - walls on/off are left exactly as designed; narrowing moves existing walls inward with the floor ends
///  - colliders always match the art (tiles are switched off, never just hidden)
///  - eggs are re-seated on reachable floor after any change
///  - chunks behind Brutus stay alive long enough for a revive rewind
///
/// Rhythm: 1 chunk = 10 floors. 5 chunks = 1 band: intro, practice, practice, peak, breather.
/// Bands: 0 vertical only | 1 +scaffolds | 2 +horizontal | 3 +staggered verticals | 4+ remix.
/// Floors narrow gradually with height (like High Risers): 9 tiles -> 8 (floor 75) -> 7 (150) -> 6 (250).
/// </summary>
public class EndlessLevel : MonoBehaviour
{
    public enum ChunkType { Vertical, Scaffold, Horizontal, Diagonal, Twin }
    public enum Tier { Easy, Medium, Hard }

    [Header("Scene references")]
    [Tooltip("Parent of the hand-built stacks (they become the templates). Clones are spawned here too.")]
    public Transform container;
    [Tooltip("Parent of the hand-placed eggs (named Egg_<stack>).")]
    public Transform eggsRoot;
    public Transform player;

    [Header("Streaming")]
    public int chunksAhead = 3;
    [Tooltip("Keep this many chunks below Brutus alive (revive rewinds go back up to a few seconds).")]
    public int chunksBehind = 3;
    [Tooltip("Height of the jump from one chunk's top floor to the next chunk's first floor.")]
    public float joinGap = 3.0f;

    [Header("Run")]
    [Tooltip("Name of the stack every run starts on (Brutus spawns on it).")]
    public string startStack = "1";
    [Tooltip("The very first run of a new player always uses this seed, so it is the same gentle run.")]
    public int firstRunSeed = 12345;
    public bool logPlan = true;

    [Header("Narrowing with height (tiles per full-width floor)")]
    public int narrowTo8AtFloor = 30;
    public int narrowTo7AtFloor = 60;
    public int narrowTo6AtFloor = 110;
    public int narrowTo5AtFloor = 170;

    [Header("Sawtooth (tight peaks, relief breathers)")]
    [Tooltip("Peak chunks are this many tiles narrower than the current level (never below 4).")]
    public int peakExtraNarrow = 1;
    [Tooltip("Breather chunks are this many tiles wider than the current level (never above 9).")]
    public int breatherRelief = 1;

    [Header("Scaffold narrowing (plank pieces kept)")]
    public int scaffold4PiecesAtFloor = 60;
    public int scaffold3PiecesAtFloor = 120;
    [Tooltip("Scaffolds normally stay 3 planks wide so they can be randomized; set a floor here to allow 2-plank scaffolds.")]
    public int scaffold2PiecesAtFloor = 100000;

    [Header("Diagonal climb (generated)")]
    [Tooltip("Floor tiles per diagonal platform (4 = about 8.7 units).")]
    public int diagonalTiles = 4;
    [Tooltip("Floor tiles per diagonal platform from diagonalHardFromBand on.")]
    public int diagonalTilesHard = 3;
    public int diagonalHardFromBand = 2;
    [Tooltip("Sideways step per floor. Keep overlap between floors >= 2 units.")]
    public float diagonalShift = 4f;
    public int diagonalFloors = 10;

    [Header("Twin towers (generated)")]
    [Tooltip("Floor tiles per tower floor.")]
    public int twinTiles = 4;
    [Tooltip("Gap between the two towers. Brutus reaches ~1.2 units sideways in a one-floor jump.")]
    public float twinGap = 0.9f;
    [Tooltip("Band in which twin towers are introduced (4 = floor 200).")]
    public int twinFromBand = 4;
    private Template _twin;

    [Header("Checkpoint start (like High Risers)")]
    [Tooltip("Start each run at the milestone at half the best floor reached (rounded down to 10).")]
    public bool checkpointStart = true;
    [Tooltip("Checkpoint starts only once the best floor reaches this.")]
    public int checkpointMinBest = 40;
    private const string BestKey = "endless_best_floor";
    private int _best, _bestSaved;
    public int BestFloor => _best;

    [Header("Scaffold randomization (High Risers style)")]
    [Tooltip("From this floor, scaffold floors get random lengths/alignments.")]
    public int scaffoldRandomFromFloor = 60;
    [Range(0f, 1f)] public float scaffoldRandomChance = 0.6f;

    [Header("Milestones")]
    [Tooltip("Give every chunk a white milestone band on its top floor (vertical stacks already have one).")]
    public bool milestoneOnEveryChunk = true;
    private Sprite _bandSprite;

    private Template _diagonal;
    private Transform _diagSrcFirst, _diagSrcMid;
    private float _diagSpacing = 3f;
    private Vector2 _diagFirstOff, _diagMidOff;   // floor (centre x, top y) relative to the platform's own position
    private GameObject _eggSource;
    private int _lastDiagDir;
    private bool _firstRun;

    /// <summary>Approximate floor Brutus is on (chunk index * 10 + floor inside chunk).</summary>
    public int CurrentFloor { get; private set; }
    public static EndlessLevel Instance { get; private set; }

    // ------------------------------------------------------------------
    private class Template
    {
        public Transform root;
        public ChunkType type;
        public Tier tier;
        public Vector2 entryLocal, exitLocal;       // (centre x, top y) of lowest / highest floor, relative to root
        public readonly List<Transform> eggs = new List<Transform>();
        public readonly List<Vector3> eggOffsets = new List<Vector3>();
    }

    private class Chunk
    {
        public int index;
        public Template template;
        public Transform root;
        public float entryTop, exitTop, exitCentreX;
    }

    private readonly List<Template> _templates = new List<Template>();
    private readonly List<Chunk> _chunks = new List<Chunk>();
    private Transform _templateHolder;
    private System.Random _rng;
    private const string FirstRunKey = "endless_first_run_done";
    private readonly List<Template> _recent = new List<Template>();
    [Tooltip("A stack is not reused within this many chunks (when alternatives exist).")]
    public int avoidRepeatWithin = 3;

    // ------------------------------------------------------------------
    private void Awake()
    {
        Instance = this;
        if (container == null) { Debug.LogError("[Endless] No container set"); enabled = false; return; }

        bool firstRun = PlayerPrefs.GetInt(FirstRunKey, 0) == 0;
        _firstRun = firstRun;
        _rng = new System.Random(firstRun ? firstRunSeed : System.Environment.TickCount);
        if (firstRun) { PlayerPrefs.SetInt(FirstRunKey, 1); PlayerPrefs.Save(); }

        _best = _bestSaved = PlayerPrefs.GetInt(BestKey, 0);
        BuildTemplates();
    }

    private void Start()
    {
        var start = _templates.Find(t => t.root.name == startStack) ?? _templates.Find(t => t.type == ChunkType.Vertical && t.tier == Tier.Easy);
        SpawnChunk(start, start.root.position);          // chunk 0 exactly where Brutus spawns

        int startFloor = checkpointStart && _best >= checkpointMinBest ? (_best / 2) / 10 * 10 : 0;
        if (startFloor >= 10 && player != null)
        {
            int startChunk = startFloor / 10;
            GenerateUpTo(startChunk + chunksAhead);       // the chunks below are built too, so rhythm and numbering match a full climb
            var from = _chunks.Find(k => k.index == startChunk - 1);
            var pc = player.GetComponent<TarodevController.PlayerController>();
            if (from != null && pc != null)
            {
                pc.PlaceAt(from.exitCentreX, from.exitTop);   // on that chunk's top milestone
                var sm = FindFirstObjectByType<ScoreManager>();
                if (sm != null) sm.AddPoints(startFloor);
                Debug.Log($"[Endless] Checkpoint start at floor {startFloor} (best {_best})");
            }
        }
        EnsureAhead(0);
    }

    private void Update()
    {
        if (player == null || _chunks.Count == 0) return;
        int cur = CurrentChunkIndex();
        var c = _chunks.Find(k => k.index == cur);
        if (c != null)
        {
            float t = Mathf.InverseLerp(c.entryTop, c.exitTop, player.position.y);
            CurrentFloor = cur * 10 + Mathf.Clamp(Mathf.FloorToInt(t * 9f) + 1, 1, 10);
        }
        EnsureAhead(cur);
        TrimBehind(cur);

        if (CurrentFloor > _best) _best = CurrentFloor;
        if (_best / 10 != _bestSaved / 10) { PlayerPrefs.SetInt(BestKey, _best); PlayerPrefs.Save(); _bestSaved = _best; }
    }

    private int CurrentChunkIndex()
    {
        int idx = _chunks[0].index;
        foreach (var c in _chunks) if (player.position.y >= c.entryTop - 1f) idx = Mathf.Max(idx, c.index);
        return idx;
    }

    private void EnsureAhead(int cur)
    {
        while (_chunks[_chunks.Count - 1].index < cur + chunksAhead)
        {
            var prev = _chunks[_chunks.Count - 1];
            int i = prev.index + 1;
            var tpl = Choose(i, prev.template);
            if (tpl.type == ChunkType.Diagonal) { SpawnDiagonal(prev, i); continue; }
            if (tpl.type == ChunkType.Twin) { SpawnTwin(prev, i); continue; }
            // place: entry floor centred over previous exit floor, joinGap above it
            Vector3 pos = new Vector3(prev.exitCentreX - tpl.entryLocal.x, prev.exitTop + joinGap - tpl.entryLocal.y, tpl.root.position.z);
            SpawnChunk(tpl, pos);
        }
    }

    private void TrimBehind(int cur)
    {
        while (_chunks.Count > 0 && _chunks[0].index < cur - chunksBehind)
        {
            if (_chunks[0].root != null) Destroy(_chunks[0].root.gameObject);
            _chunks.RemoveAt(0);
        }
    }

    // ------------------------------------------------------------------
    // Templates
    private void BuildTemplates()
    {
        var horizontalStacks = new HashSet<Transform>();
        foreach (var trig in container.GetComponentsInChildren<StackExitTrigger>(true))
            if (trig.TriggerMode == CameraFollow.Mode.Horizontal && trig.Stack != null) horizontalStacks.Add(trig.Stack);

        var stacks = new List<Transform>();
        foreach (Transform s in container) if (s.gameObject.activeInHierarchy) stacks.Add(s);

        foreach (var s in stacks)
        {
            var t = new Template { root = s };
            t.type = horizontalStacks.Contains(s) ? ChunkType.Horizontal : (s.name.StartsWith("ScafoldSystem") ? ChunkType.Scaffold : ChunkType.Vertical);
            if (!MeasureFloors(s, out var entry, out var exit)) continue;
            t.entryLocal = entry - (Vector2)s.position;
            t.exitLocal = exit - (Vector2)s.position;
            t.tier = t.type == ChunkType.Vertical ? RateVertical(s) : Tier.Medium;
            if (eggsRoot != null)
                foreach (Transform e in eggsRoot)
                    if (e.name == "Egg_" + s.name) { t.eggs.Add(e); t.eggOffsets.Add(e.position - s.position); }
            _templates.Add(t);
        }

        // Diagonal climbs are built from platforms of an easy vertical stack
        var srcStack = _templates.Find(x => x.type == ChunkType.Vertical && x.tier == Tier.Easy && x.root.Find("Platform 1") != null && x.root.Find("Platform 2") != null && x.root.Find("Platform 5") != null);
        if (srcStack != null)
        {
            _diagSrcFirst = srcStack.root.Find("Platform 1");
            _diagSrcMid = srcStack.root.Find("Platform 5");
            float t1 = FloorTop(_diagSrcFirst), t2 = FloorTop(srcStack.root.Find("Platform 2"));
            if (t2 > t1) _diagSpacing = t2 - t1;
            _diagFirstOff = FloorCentreTop(_diagSrcFirst) - (Vector2)_diagSrcFirst.position;
            _diagMidOff = FloorCentreTop(_diagSrcMid) - (Vector2)_diagSrcMid.position;
            _diagonal = new Template { root = null, type = ChunkType.Diagonal, tier = Tier.Medium };
            _twin = new Template { root = null, type = ChunkType.Twin, tier = Tier.Medium };
        }
        foreach (var x in _templates) if (x.eggs.Count > 0) { _eggSource = x.eggs[0].gameObject; break; }
        foreach (var x in _templates)
        {
            foreach (var sr in x.root.GetComponentsInChildren<SpriteRenderer>(false)) if (IsWhiteBand(sr)) { _bandSprite = sr.sprite; break; }
            if (_bandSprite != null) break;
        }

        // Park the originals: they are only templates now
        _templateHolder = new GameObject("Chunk Templates (inactive)").transform;
        _templateHolder.gameObject.SetActive(false);
        foreach (var t in _templates)
        {
            t.root.SetParent(_templateHolder, true);
            foreach (var e in t.eggs) e.SetParent(_templateHolder, true);
        }
        if (logPlan)
        {
            int v = 0, s2 = 0, h = 0; foreach (var t in _templates) { if (t.type == ChunkType.Vertical) v++; else if (t.type == ChunkType.Scaffold) s2++; else h++; }
            Debug.Log($"[Endless] Templates: {v} vertical, {s2} scaffold, {h} horizontal");
        }
    }

    private static Vector2 FloorCentreTop(Transform plat)
    {
        float lo = float.MaxValue, hi = float.MinValue, top = float.MinValue;
        foreach (var c in plat.GetComponentsInChildren<Collider2D>(false))
        {
            if (c.isTrigger || !c.name.StartsWith("Floor_Tile")) continue;
            lo = Mathf.Min(lo, c.bounds.min.x); hi = Mathf.Max(hi, c.bounds.max.x); top = Mathf.Max(top, c.bounds.max.y);
        }
        return new Vector2((lo + hi) * 0.5f, top);
    }

    private static float FloorTop(Transform plat)
    {
        float top = float.MinValue;
        if (plat == null) return top;
        foreach (var c in plat.GetComponentsInChildren<Collider2D>(false))
            if (!c.isTrigger && c.name.StartsWith("Floor_Tile")) top = Mathf.Max(top, c.bounds.max.y);
        return top;
    }

    /// <summary>Easy = straight full-width; Medium = some short floors; Hard = staggered.</summary>
    private static Tier RateVertical(Transform s)
    {
        int shortFloors = 0, minTiles = 99; float maxShift = 0f, prevC = float.NaN;
        var plats = new List<Transform>();
        foreach (Transform p in s) if (p.name.StartsWith("Platform")) plats.Add(p);
        plats.Sort((a, b) => a.position.y.CompareTo(b.position.y));
        foreach (var p in plats)
        {
            int tiles = 0; float lo = float.MaxValue, hi = float.MinValue;
            foreach (Transform c in p)
            {
                if (!c.name.StartsWith("Floor_Tile") || !c.gameObject.activeSelf) continue;
                tiles++;
                var col = c.GetComponent<Collider2D>();
                if (col != null) { lo = Mathf.Min(lo, col.bounds.min.x); hi = Mathf.Max(hi, col.bounds.max.x); }
            }
            if (tiles == 0) continue;
            if (tiles < 9) shortFloors++;
            minTiles = Mathf.Min(minTiles, tiles);
            float cx = (lo + hi) * 0.5f;
            if (!float.IsNaN(prevC)) maxShift = Mathf.Max(maxShift, Mathf.Abs(cx - prevC));
            prevC = cx;
        }
        if (shortFloors == 0) return Tier.Easy;
        if (maxShift > 5f || minTiles <= 2) return Tier.Hard;
        return Tier.Medium;
    }

    /// <summary>Lowest and highest walkable floor of a stack: (centre x, top y).</summary>
    private static bool MeasureFloors(Transform stack, out Vector2 entry, out Vector2 exit)
    {
        entry = exit = Vector2.zero;
        float lowY = float.MaxValue, highY = float.MinValue; bool any = false;
        foreach (Transform plat in stack)
        {
            if (!plat.gameObject.activeInHierarchy) continue;
            float top = float.MinValue, lo = float.MaxValue, hi = float.MinValue; bool has = false;
            foreach (var c in plat.GetComponentsInChildren<Collider2D>(false))
            {
                if (c.isTrigger || !(c.name.StartsWith("Floor_Tile") || c.name.StartsWith("Up Floor"))) continue;
                top = Mathf.Max(top, c.bounds.max.y); lo = Mathf.Min(lo, c.bounds.min.x); hi = Mathf.Max(hi, c.bounds.max.x); has = true;
            }
            if (!has) continue;
            any = true;
            if (top < lowY) { lowY = top; entry = new Vector2((lo + hi) * 0.5f, top); }
            if (top > highY) { highY = top; exit = new Vector2((lo + hi) * 0.5f, top); }
        }
        return any;
    }

    // ------------------------------------------------------------------
    // Sequencing
    private Template Choose(int index, Template prev)
    {
        int band = index / 5, slot = index % 5;
        var pick = PickFor(band, slot, prev);
        // join not possible: fall back to a vertical stack - medium for peaks so the peak keeps its bite
        if (!JoinAllowed(prev.type, pick.type) || pick == prev) pick = Pick(ChunkType.Vertical, slot == 3 ? Tier.Medium : Tier.Easy, prev);
        _recent.Add(pick); while (_recent.Count > avoidRepeatWithin) _recent.RemoveAt(0);
        if (logPlan) Debug.Log($"[Endless] chunk {index} (floors {index * 10}-{index * 10 + 10}, band {band}, {SlotName(slot)}): {(pick.root != null ? pick.root.name : pick.type.ToString())} [{pick.type}/{pick.tier}]");
        return pick;
    }

    private static string SlotName(int s) => s == 0 ? "intro" : s == 3 ? "peak" : s == 4 ? "breather" : "practice";

    private Template PickFor(int band, int slot, Template prev)
    {
        if (slot == 4) return Pick(ChunkType.Vertical, Tier.Easy, prev);                        // breather
        switch (band)
        {
            case 0:                                                                              // vertical only
                if (slot == 3 && !_firstRun) return Pick(ChunkType.Vertical, Tier.Medium, prev);   // first run stays gentle
                return Pick(ChunkType.Vertical, Tier.Easy, prev);
            case 1:                                                                              // + scaffolds
                if (slot == 0) return Pick(ChunkType.Scaffold, null, prev);                      // scaffold intro
                if (slot == 2) return Pick(ChunkType.Diagonal, null, prev);                      // diagonal intro (guaranteed)
                if (slot == 3) { double r3 = _rng.NextDouble(); return r3 < 0.34 ? Pick(ChunkType.Vertical, Tier.Medium, prev) : r3 < 0.67 ? Pick(ChunkType.Diagonal, null, prev) : Pick(ChunkType.Scaffold, null, prev); }
                { double r1 = _rng.NextDouble(); return r1 < 0.3 ? Pick(ChunkType.Scaffold, null, prev) : r1 < 0.55 ? Pick(ChunkType.Diagonal, null, prev) : Pick(ChunkType.Vertical, Tier.Easy, prev); }
            case 2:                                                                              // + horizontal
                if (slot == 0) return Pick(ChunkType.Horizontal, null, prev);
                if (slot == 3) return Chance(0.5) ? Pick(ChunkType.Horizontal, null, prev) : Pick(ChunkType.Vertical, Tier.Hard, prev);
                return Mixed(prev, hard: false);
            case 3:                                                                              // + staggered
                if (slot == 0) return Pick(ChunkType.Vertical, Tier.Medium, prev);
                if (slot == 3) return Pick(ChunkType.Vertical, Tier.Hard, prev);
                return Mixed(prev, hard: true);
            default:                                                                             // remix
                if (slot == 0) return band == twinFromBand ? Pick(ChunkType.Twin, null, prev) : Mixed(prev, hard: false);   // twin towers arrive at floor 200
                if (band > twinFromBand && slot < 3 && Chance(0.15)) return Pick(ChunkType.Twin, null, prev);
                if (slot == 3) return Chance(0.5) ? Pick(ChunkType.Vertical, Tier.Hard, prev) : Pick(ChunkType.Horizontal, null, prev);
                return Mixed(prev, hard: true);
        }
    }

    private Template Mixed(Template prev, bool hard)
    {
        double r = _rng.NextDouble();
        if (r < 0.25) return Pick(ChunkType.Scaffold, null, prev);
        if (r < 0.38) return Pick(ChunkType.Horizontal, null, prev);
        if (r < 0.5) return Pick(ChunkType.Diagonal, null, prev);
        if (r < 0.7) return Pick(ChunkType.Vertical, Tier.Medium, prev);
        if (hard && r < 0.85) return Pick(ChunkType.Vertical, Tier.Hard, prev);
        return Pick(ChunkType.Vertical, Tier.Easy, prev);
    }

    private bool Chance(double p) => _rng.NextDouble() < p;

    /// <summary>Random template of a type (and tier), never the same as prev, falling back gracefully.</summary>
    private Template Pick(ChunkType type, Tier? tier, Template prev)
    {
        if (type == ChunkType.Diagonal) return _diagonal ?? Pick(ChunkType.Vertical, Tier.Easy, prev);
        if (type == ChunkType.Twin) return _twin ?? Pick(ChunkType.Vertical, Tier.Easy, prev);
        var pool = _templates.FindAll(t => t.type == type && (tier == null || t.tier == tier) && t != prev && !_recent.Contains(t));
        if (pool.Count == 0) pool = _templates.FindAll(t => t.type == type && (tier == null || t.tier == tier) && t != prev);
        if (pool.Count == 0 && tier != null) pool = _templates.FindAll(t => t.type == type && t != prev);
        if (pool.Count == 0) pool = _templates.FindAll(t => t.type == ChunkType.Vertical && t != prev);
        return pool[_rng.Next(pool.Count)];
    }

    /// <summary>Only the joins that exist (and are proven) in the hand-built level.</summary>
    private static bool JoinAllowed(ChunkType from, ChunkType to)
    {
        switch (from)
        {
            case ChunkType.Vertical: return true;                                   // V->V, V->S, V->H, V->D
            case ChunkType.Scaffold: return to != ChunkType.Scaffold;               // S->V, S->H, S->D
            default: return to == ChunkType.Vertical;                               // H->V, D->V
        }
    }

    // ------------------------------------------------------------------
    // Spawning
    private void SpawnChunk(Template tpl, Vector3 position)
    {
        var go = Instantiate(tpl.root.gameObject, container);
        go.name = tpl.root.name + " #" + (_chunks.Count == 0 ? 0 : _chunks[_chunks.Count - 1].index + 1);
        go.transform.position = position;
        go.SetActive(true);

        int index = _chunks.Count == 0 ? 0 : _chunks[_chunks.Count - 1].index + 1;
        int floor = index * 10;
        int slot = index % 5;
        int tiles = floor >= narrowTo5AtFloor ? 5 : floor >= narrowTo6AtFloor ? 6 : floor >= narrowTo7AtFloor ? 7 : floor >= narrowTo8AtFloor ? 8 : 9;
        if (slot == 3) tiles = Mathf.Max(4, tiles - peakExtraNarrow);          // peak: tighter
        else if (slot == 4) tiles = Mathf.Min(9, tiles + breatherRelief);      // breather: relief
        int pieces = floor >= scaffold2PiecesAtFloor ? 2 : floor >= scaffold3PiecesAtFloor ? 3 : floor >= scaffold4PiecesAtFloor ? 4 : 99;
        if (slot == 3 && pieces < 99) pieces = Mathf.Max(3, pieces - 1);
        Physics2D.SyncTransforms();                                   // collider bounds are valid only after a sync
        if (tpl.type == ChunkType.Scaffold && pieces < 99) { NarrowScaffold(go.transform, pieces); Physics2D.SyncTransforms(); }
        if (tpl.type == ChunkType.Scaffold) { TrimStub(go.transform); Physics2D.SyncTransforms(); }
        if (tpl.type == ChunkType.Scaffold && floor >= scaffoldRandomFromFloor) { RandomizeScaffold(go.transform); Physics2D.SyncTransforms(); }
        if (tpl.type == ChunkType.Vertical && tiles < 9)
        {
            Narrow(go.transform, tiles);
            Physics2D.SyncTransforms();
            // these only re-check themselves automatically in the editor, so ask them now
            foreach (var pv in go.GetComponentsInChildren<PlatformVine>(true)) pv.Refresh();
            foreach (var sn in go.GetComponentsInChildren<SnakeIdle>(true)) sn.RefreshVisibility();
            foreach (var wt in go.GetComponentsInChildren<WallTorch>(true)) wt.Refresh();
        }

        // eggs travel with their chunk
        for (int i = 0; i < tpl.eggs.Count; i++)
        {
            var egg = Instantiate(tpl.eggs[i].gameObject, go.transform);
            egg.transform.position = position + tpl.eggOffsets[i];
            egg.SetActive(true);
        }

        Physics2D.SyncTransforms();
        foreach (Transform e in go.transform) if (e.name.StartsWith("Egg_")) SeatEgg(e, go.transform);

        AddOrTagMilestone(go.transform, index);
        NormalizeRowPanels(go.transform, index);
        MeasureFloors(go.transform, out var entry, out var exit);
        _chunks.Add(new Chunk { index = index, template = tpl, root = go.transform, entryTop = entry.y, exitTop = exit.y, exitCentreX = exit.x });
    }

    // ------------------------------------------------------------------
    // Diagonal climb: platforms from an easy vertical stack, trimmed to the middle tiles,
    // each one floor up and diagonalShift sideways in one direction. Back wall on, front open.
    private void SpawnDiagonal(Chunk prev, int index)
    {
        int band = index / 5;
        int keep = band >= diagonalHardFromBand ? diagonalTilesHard : diagonalTiles;
        int dir = _lastDiagDir == 0 ? (_rng.NextDouble() < 0.5 ? -1 : 1) : -_lastDiagDir;   // alternate directions
        _lastDiagDir = dir;

        var rootGo = new GameObject((dir > 0 ? "Diagonal R" : "Diagonal L") + " #" + index);
        var rootT = rootGo.transform;
        rootT.SetParent(container, false);
        rootT.position = Vector3.zero;

        for (int i = 0; i < diagonalFloors; i++)
        {
            var src = i == 0 ? _diagSrcFirst : _diagSrcMid;
            var off = i == 0 ? _diagFirstOff : _diagMidOff;
            var go = Instantiate(src.gameObject, rootT);
            go.name = "Platform " + (i + 1);
            go.transform.position = new Vector3(i * diagonalShift * dir - off.x, i * _diagSpacing - off.y, src.position.z);
            go.SetActive(true);
            Physics2D.SyncTransforms();
            KeepMiddleTiles(go.transform, keep, dir);
            if (i == 0) foreach (var trig in go.GetComponentsInChildren<StackExitTrigger>(true)) trig.SetTriggerMode(CameraFollow.Mode.Diagonal);
            else foreach (var trig in go.GetComponentsInChildren<StackExitTrigger>(true)) Destroy(trig.gameObject);   // one trigger per chunk
        }
        Physics2D.SyncTransforms();

        // place: first floor centred over the previous chunk's top floor, one jump above it
        MeasureFloors(rootT, out var entry, out _);
        rootT.position += new Vector3(prev.exitCentreX - entry.x, prev.exitTop + joinGap - entry.y, 0f);
        Physics2D.SyncTransforms();

        // one egg, on the middle platform
        if (_eggSource != null)
        {
            var mid = rootT.Find("Platform " + (diagonalFloors / 2 + 1));
            if (mid != null)
            {
                var egg = Instantiate(_eggSource, rootT);
                var ct = FloorCentreTop(mid);
                egg.transform.position = new Vector3(ct.x, ct.y + 1f, _eggSource.transform.position.z);
                egg.SetActive(true);
                Physics2D.SyncTransforms();
                SeatEgg(egg.transform, rootT);
            }
        }
        foreach (var pv in rootGo.GetComponentsInChildren<PlatformVine>(true)) pv.Refresh();
        foreach (var wt in rootGo.GetComponentsInChildren<WallTorch>(true)) wt.Refresh();

        AddOrTagMilestone(rootT, index);
        NormalizeRowPanels(rootT, index);
        MeasureFloors(rootT, out var en, out var ex);
        _chunks.Add(new Chunk { index = index, template = _diagonal, root = rootT, entryTop = en.y, exitTop = ex.y, exitCentreX = ex.x });
    }

    private void KeepMiddleTiles(Transform plat, int keep, int dir)
    {
        var tiles = new List<Transform>();
        foreach (Transform c in plat) if (c.name.StartsWith("Floor_Tile") && c.gameObject.activeSelf) tiles.Add(c);
        if (tiles.Count <= keep) return;
        tiles.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        GetExtent(tiles, out float oldL, out float oldR);
        int start = (tiles.Count - keep) / 2;
        var kept = new List<Transform>();
        for (int i = 0; i < tiles.Count; i++)
        {
            bool k = i >= start && i < start + keep;
            tiles[i].gameObject.SetActive(k);
            if (k) kept.Add(tiles[i]);
        }
        GetExtent(kept, out float newL, out float newR);
        foreach (Transform c in plat)
        {
            string nm = c.name.Trim();
            // back wall on (travelling right -> left wall), front open
            if (nm == "Left Wall") { c.position += new Vector3(newL - oldL, 0f, 0f); c.gameObject.SetActive(dir > 0); }
            else if (nm == "Right Wall") { c.position += new Vector3(newR - oldR, 0f, 0f); c.gameObject.SetActive(dir < 0); }
            else if (nm == "Milestones") FitWidth(c, oldL, oldR, newL, newR);
        }
        var vine = plat.Find("Vine");
        if (vine != null)
        {
            var sr = vine.GetComponent<SpriteRenderer>(); var pv = vine.GetComponent<PlatformVine>();
            if (sr != null && pv != null) pv.minTiles = (sr.bounds.min.x >= newL - 0.2f && sr.bounds.max.x <= newR + 0.2f) ? keep : 99;
        }
    }

    // ------------------------------------------------------------------
    // Twin towers: 10 floors alternating between a left and a right tower (one floor = 3 up),
    // separated by a small gap. Outer walls only, so Brutus bounces off the outside, runs to the gap
    // and jumps across and up. Floors are one-way, so the far tower never bumps his head.
    private void SpawnTwin(Chunk prev, int index)
    {
        var rootGo = new GameObject("Twin Towers #" + index);
        var rootT = rootGo.transform;
        rootT.SetParent(container, false);
        rootT.position = Vector3.zero;
        int keep = Mathf.Clamp(twinTiles, 2, 4);

        for (int i = 0; i < diagonalFloors; i++)
        {
            bool left = i % 2 == 0;
            var src = i == 0 ? _diagSrcFirst : _diagSrcMid;
            var off = i == 0 ? _diagFirstOff : _diagMidOff;
            var go = Instantiate(src.gameObject, rootT);
            go.name = "Platform " + (i + 1);
            go.transform.position = new Vector3(-off.x, i * _diagSpacing - off.y, src.position.z);
            go.SetActive(true);
            Physics2D.SyncTransforms();
            // left tower keeps the left-most tiles, right tower the right-most; outer wall on, inner open
            KeepTileRange(go.transform, left ? 0 : 9 - keep, keep, leftWallOn: left, rightWallOn: !left);
            if (i == 0) { foreach (var trig in go.GetComponentsInChildren<StackExitTrigger>(true)) trig.SetTriggerMode(CameraFollow.Mode.Vertical); }
            else foreach (var trig in go.GetComponentsInChildren<StackExitTrigger>(true)) Destroy(trig.gameObject);
        }
        Physics2D.SyncTransforms();

        // pull the right tower in so the gap between the towers is exactly twinGap
        float leftInner = float.MinValue, rightInner = float.MaxValue;
        foreach (Transform plat in rootT)
        {
            int k = int.Parse(plat.name.Substring(9)) - 1;
            GetPlatformFloor(plat, out float lo, out float hi);
            if (k % 2 == 0) leftInner = Mathf.Max(leftInner, hi); else rightInner = Mathf.Min(rightInner, lo);
        }
        float shift = (leftInner + twinGap) - rightInner;
        foreach (Transform plat in rootT) { int k = int.Parse(plat.name.Substring(9)) - 1; if (k % 2 == 1) plat.position += new Vector3(shift, 0f, 0f); }
        Physics2D.SyncTransforms();

        // place: first floor centred over the previous chunk's top floor, one jump above it
        MeasureFloors(rootT, out var entry, out _);
        rootT.position += new Vector3(prev.exitCentreX - entry.x, prev.exitTop + joinGap - entry.y, 0f);
        Physics2D.SyncTransforms();

        if (_eggSource != null)
        {
            var mid = rootT.Find("Platform " + (diagonalFloors / 2 + 1));
            if (mid != null)
            {
                var egg = Instantiate(_eggSource, rootT);
                var ct = FloorCentreTop(mid);
                egg.transform.position = new Vector3(ct.x, ct.y + 1f, _eggSource.transform.position.z);
                egg.SetActive(true);
                Physics2D.SyncTransforms();
                SeatEgg(egg.transform, rootT);
            }
        }
        foreach (var pv in rootGo.GetComponentsInChildren<PlatformVine>(true)) pv.Refresh();
        foreach (var wt in rootGo.GetComponentsInChildren<WallTorch>(true)) wt.Refresh();

        AddOrTagMilestone(rootT, index);
        NormalizeRowPanels(rootT, index);
        MeasureFloors(rootT, out var en, out var ex);
        _chunks.Add(new Chunk { index = index, template = _twin, root = rootT, entryTop = en.y, exitTop = ex.y, exitCentreX = ex.x });
    }

    private static void GetPlatformFloor(Transform plat, out float lo, out float hi)
    {
        lo = float.MaxValue; hi = float.MinValue;
        foreach (var c in plat.GetComponentsInChildren<Collider2D>(false))
            if (!c.isTrigger && c.name.StartsWith("Floor_Tile")) { lo = Mathf.Min(lo, c.bounds.min.x); hi = Mathf.Max(hi, c.bounds.max.x); }
    }

    /// <summary>Keep tiles [from, from+count) of a 9-tile floor; move/enable walls at the new ends.</summary>
    private void KeepTileRange(Transform plat, int from, int count, bool leftWallOn, bool rightWallOn)
    {
        var tiles = new List<Transform>();
        foreach (Transform c in plat) if (c.name.StartsWith("Floor_Tile") && c.gameObject.activeSelf) tiles.Add(c);
        tiles.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        if (tiles.Count < from + count) return;
        GetExtent(tiles, out float oldL, out float oldR);
        var kept = new List<Transform>();
        for (int i = 0; i < tiles.Count; i++) { bool k = i >= from && i < from + count; tiles[i].gameObject.SetActive(k); if (k) kept.Add(tiles[i]); }
        GetExtent(kept, out float newL, out float newR);
        foreach (Transform c in plat)
        {
            string nm = c.name.Trim();
            if (nm == "Left Wall") { c.position += new Vector3(newL - oldL, 0f, 0f); c.gameObject.SetActive(leftWallOn); }
            else if (nm == "Right Wall") { c.position += new Vector3(newR - oldR, 0f, 0f); c.gameObject.SetActive(rightWallOn); }
            else if (nm == "Milestones") FitWidth(c, oldL, oldR, newL, newR);
        }
        var vine = plat.Find("Vine");
        if (vine != null)
        {
            var sr = vine.GetComponent<SpriteRenderer>(); var pv = vine.GetComponent<PlatformVine>();
            if (sr != null && pv != null) pv.minTiles = (sr.bounds.min.x >= newL - 0.2f && sr.bounds.max.x <= newR + 0.2f) ? count : 99;
        }
    }

    // ------------------------------------------------------------------
    // Milestones: the chunk's top floor carries a white band tagged with the chunk index (shown as 00, 01...).
    private static bool IsWhiteBand(SpriteRenderer sr)
    {
        if (sr == null || !sr.enabled || sr.sprite == null || sr.sprite.name != "Square") return false;
        var c = sr.color;
        return c.r > 0.9f && c.g > 0.9f && c.b > 0.9f && c.a < 0.6f;
    }

    private void AddOrTagMilestone(Transform chunk, int index)
    {
        Physics2D.SyncTransforms();
        // top floor of the chunk
        Transform topPlat = null; float top = float.MinValue, lo = 0f, hi = 0f;
        foreach (Transform plat in chunk)
        {
            if (!plat.name.StartsWith("Platform") || !plat.gameObject.activeInHierarchy) continue;
            float t2 = float.MinValue, l2 = float.MaxValue, h2 = float.MinValue; bool scaffold = false;
            foreach (var c in plat.GetComponentsInChildren<Collider2D>(false))
            {
                if (c.isTrigger || !(c.name.StartsWith("Floor_Tile") || c.name.StartsWith("Up Floor"))) continue;
                t2 = Mathf.Max(t2, c.bounds.max.y); l2 = Mathf.Min(l2, c.bounds.min.x); h2 = Mathf.Max(h2, c.bounds.max.x);
                if (c.name.StartsWith("Up Floor")) scaffold = true;
            }
            if (t2 == float.MinValue || t2 <= top) continue;
            if (scaffold)   // the visible planks, not the wider collider
            {
                float pl = float.MaxValue, ph = float.MinValue;
                foreach (var sr in plat.GetComponentsInChildren<SpriteRenderer>(false)) if (sr.sprite != null && sr.sprite.name.StartsWith("Scaffold_WoodPlatform")) { pl = Mathf.Min(pl, sr.bounds.min.x); ph = Mathf.Max(ph, sr.bounds.max.x); }
                if (pl < ph) { l2 = pl; h2 = ph; }
            }
            topPlat = plat; top = t2; lo = l2; hi = h2;
        }
        if (topPlat == null) return;

        // already has a white band just above its floor? tag it
        foreach (var sr in topPlat.GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (!IsWhiteBand(sr) || Mathf.Abs(sr.bounds.min.y - top) > 0.6f) continue;
            var host = sr.transform.parent != null ? sr.transform.parent : sr.transform;
            var mi0 = host.GetComponent<MilestoneIndex>() ?? host.gameObject.AddComponent<MilestoneIndex>();
            mi0.value = index;
            return;
        }
        if (!milestoneOnEveryChunk || _bandSprite == null) return;

        var group = new GameObject("Milestones (added)");
        group.transform.SetParent(topPlat, false);
        group.AddComponent<MilestoneIndex>().value = index;
        var band = new GameObject("Band", typeof(SpriteRenderer));
        band.transform.SetParent(group.transform, false);
        var bsr = band.GetComponent<SpriteRenderer>();
        bsr.sprite = _bandSprite; bsr.color = new Color(1f, 1f, 1f, 0.18f); bsr.sortingOrder = 8;
        Vector2 native = _bandSprite.bounds.size;
        const float bandH = 2.5f;
        band.transform.localScale = new Vector3((hi - lo) / native.x / topPlat.lossyScale.x, bandH / native.y / topPlat.lossyScale.y, 1f);
        band.transform.position = new Vector3((lo + hi) * 0.5f, top + bandH * 0.5f, topPlat.position.z);
    }

    // ------------------------------------------------------------------
    // Narrowing: switch off end tiles (colliders go with them), move the existing walls in with the
    // new floor ends, and resize the full-width panels. Walls that were off stay off.
    private void Narrow(Transform stack, int tiles)
    {
        foreach (Transform plat in stack)
        {
            if (!plat.name.StartsWith("Platform")) continue;
            var floorTiles = new List<Transform>();
            foreach (Transform c in plat) if (c.name.StartsWith("Floor_Tile") && c.gameObject.activeSelf) floorTiles.Add(c);
            if (floorTiles.Count != 9) continue;                    // only full-width floors; designed short ones stay as they are
            floorTiles.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            GetExtent(floorTiles, out float oldL, out float oldR);
            // remove from the ends: 8 -> last tile, 7 -> first + last, 6 -> first, second + last
            var remove = new List<Transform>();
            if (tiles <= 8) remove.Add(floorTiles[8]);
            if (tiles <= 7) remove.Add(floorTiles[0]);
            if (tiles <= 6) remove.Add(floorTiles[1]);
            if (tiles <= 5) remove.Add(floorTiles[7]);
            if (tiles <= 4) remove.Add(floorTiles[6]);
            foreach (var r in remove) r.gameObject.SetActive(false);
            floorTiles.RemoveAll(remove.Contains);
            GetExtent(floorTiles, out float newL, out float newR);

            foreach (Transform c in plat)
            {
                string n = c.name.Trim();
                if (n == "Left Wall") c.position += new Vector3(newL - oldL, 0f, 0f);
                else if (n == "Right Wall") c.position += new Vector3(newR - oldR, 0f, 0f);
                else if (n == "Milestones") FitWidth(c, oldL, oldR, newL, newR);
            }

            // keep the vine if it still sits over floor
            var vine = plat.Find("Vine");
            if (vine != null)
            {
                var sr = vine.GetComponent<SpriteRenderer>();
                var pv = vine.GetComponent<PlatformVine>();
                if (sr != null && pv != null)
                    pv.minTiles = (sr.bounds.min.x >= newL - 0.2f && sr.bounds.max.x <= newR + 0.2f) ? tiles : 99;
            }
        }
    }

    // Scaffold narrowing: keep the middle plank pieces (re-centred), move the corner caps to the new ends,
    // shrink each floor collider by the same amount (keeping its overhang under the dragons), resize the
    // backing panel, and move both dragon groups (with their vines) inward. Dragons only move vertically.
    private void NarrowScaffold(Transform stack, int keep)
    {
        float leftShift = 0f, rightShift = 0f; int measured = 0;
        // The lowest floor is the entry stub: designed off-centre, with a dragon standing at its end.
        // It is not narrowed; it moves with that dragon so the dragon still covers its end.
        Transform entry = null; float entryTop = float.MaxValue;
        foreach (Transform plat in stack)
        {
            if (!plat.name.StartsWith("Platform")) continue;
            var e = plat.Find("Up Floor"); var ec = e != null ? e.GetComponent<Collider2D>() : null;
            if (ec != null && ec.bounds.max.y < entryTop) { entryTop = ec.bounds.max.y; entry = plat; }
        }
        int stubSide = 0;   // +1 right dragon, -1 left dragon
        if (entry != null)
        {
            var ec0 = entry.Find("Up Floor").GetComponent<Collider2D>();
            float dL = float.MaxValue, dR = float.MaxValue;
            foreach (Transform c in stack)
            {
                bool isL = c.name.StartsWith("Dragon Left"), isR = c.name.StartsWith("Dragon Right");
                if (!isL && !isR) continue;
                foreach (var dc in c.GetComponentsInChildren<Collider2D>(false))
                {
                    if (dc.isTrigger) continue;
                    float d = Mathf.Max(0f, Mathf.Max(ec0.bounds.min.x - dc.bounds.max.x, dc.bounds.min.x - ec0.bounds.max.x)) + Mathf.Abs(dc.bounds.min.y - ec0.bounds.max.y) * 0.1f;
                    if (isL) dL = Mathf.Min(dL, d); else dR = Mathf.Min(dR, d);
                }
            }
            stubSide = dR <= dL ? 1 : -1;
        }
        foreach (Transform plat in stack)
        {
            if (!plat.name.StartsWith("Platform") || plat == entry) continue;
            var uf = plat.Find("Up Floor"); if (uf == null) continue;
            var box = uf.GetComponent<BoxCollider2D>(); if (box == null) continue;
            var planks = new List<SpriteRenderer>(); SpriteRenderer capL = null, capR = null;
            foreach (Transform c in uf)
            {
                var sr = c.GetComponent<SpriteRenderer>(); if (sr == null || sr.sprite == null) continue;
                string sn = sr.sprite.name;
                if (sn.Contains("Corner_Left")) capL = sr;
                else if (sn.Contains("Corner_Right")) capR = sr;
                else if (c.gameObject.activeSelf && sn.StartsWith("Scaffold_WoodPlatform")) planks.Add(sr);
            }
            if (planks.Count <= keep) continue;
            planks.Sort((a, b) => a.bounds.center.x.CompareTo(b.bounds.center.x));
            float oldL = planks[0].bounds.min.x, oldR = planks[planks.Count - 1].bounds.max.x, centre = (oldL + oldR) * 0.5f;
            int drop = planks.Count - keep, dropL = (drop + 1) / 2;
            var kept = planks.GetRange(dropL, keep);
            foreach (var pl in planks) if (!kept.Contains(pl)) pl.gameObject.SetActive(false);
            float keptC = (kept[0].bounds.min.x + kept[kept.Count - 1].bounds.max.x) * 0.5f;
            foreach (var k in kept) k.transform.position += new Vector3(centre - keptC, 0f, 0f);
            float newL = oldL + (centre - keptC) + (kept[0].bounds.min.x - oldL), newR = newL + (kept[kept.Count - 1].bounds.max.x - kept[0].bounds.min.x);
            newL = centre - (kept[kept.Count - 1].bounds.max.x - kept[0].bounds.min.x) * 0.5f;
            newR = centre + (kept[kept.Count - 1].bounds.max.x - kept[0].bounds.min.x) * 0.5f;
            if (capL != null) capL.transform.position += new Vector3(newL - oldL, 0f, 0f);
            if (capR != null) capR.transform.position += new Vector3(newR - oldR, 0f, 0f);
            float shrink = (oldR - oldL) - (newR - newL);
            float sx = Mathf.Abs(uf.lossyScale.x) > 1e-4f ? uf.lossyScale.x : 1f;
            float colL = box.bounds.min.x + (newL - oldL), colR = box.bounds.max.x + (newR - oldR);
            box.size = new Vector2(box.size.x - shrink / sx, box.size.y);
            box.offset = new Vector2(uf.InverseTransformPoint(new Vector3((colL + colR) * 0.5f, uf.position.y, uf.position.z)).x, box.offset.y);
            foreach (Transform c in plat)
                if (c != uf && c.GetComponent<SpriteRenderer>() != null && c.name.StartsWith("Background_gameplay"))
                    FitWidth(c, oldL, oldR, newL, newR);
            leftShift += newL - oldL; rightShift += newR - oldR; measured++;
        }
        if (measured == 0) return;
        leftShift /= measured; rightShift /= measured;
        foreach (Transform c in stack)
        {
            if (c.name.StartsWith("Dragon Left")) c.position += new Vector3(leftShift, 0f, 0f);
            else if (c.name.StartsWith("Dragon Right")) c.position += new Vector3(rightShift, 0f, 0f);
        }
        if (entry != null)
        {
            var ec = entry.Find("Up Floor").GetComponent<Collider2D>();
            float stackMid = 0f; int m = 0;
            foreach (Transform plat in stack) { if (plat == entry || !plat.name.StartsWith("Platform")) continue; var u = plat.Find("Up Floor"); var uc = u != null ? u.GetComponent<Collider2D>() : null; if (uc != null) { stackMid += uc.bounds.center.x; m++; } }
            if (m > 0) stackMid /= m;
            // stub hugging the right side follows the right dragon, otherwise the left one
            float shift = stubSide >= 0 ? rightShift : leftShift;   // follow the dragon that stands on the stub
            entry.position += new Vector3(shift, 0f, 0f);
        }
    }

    // ------------------------------------------------------------------
    // Scaffold helpers
    private struct ScaffoldFloor { public Transform plat, uf; public BoxCollider2D box; public List<SpriteRenderer> planks; public SpriteRenderer capL, capR; public float lo, hi, top; }

    private static ScaffoldFloor ReadScaffoldFloor(Transform plat)
    {
        var f = new ScaffoldFloor { plat = plat, planks = new List<SpriteRenderer>(), lo = float.MaxValue, hi = float.MinValue };
        f.uf = plat.Find("Up Floor"); if (f.uf == null) return f;
        f.box = f.uf.GetComponent<BoxCollider2D>();
        foreach (Transform c in f.uf)
        {
            var sr = c.GetComponent<SpriteRenderer>(); if (sr == null || sr.sprite == null) continue;
            string sn = sr.sprite.name;
            if (sn.Contains("Corner_Left")) f.capL = sr;
            else if (sn.Contains("Corner_Right")) f.capR = sr;
            else if (c.gameObject.activeSelf && sn.StartsWith("Scaffold_WoodPlatform")) f.planks.Add(sr);
        }
        f.planks.Sort((a, b) => a.bounds.center.x.CompareTo(b.bounds.center.x));
        foreach (var pl in f.planks) { f.lo = Mathf.Min(f.lo, pl.bounds.min.x); f.hi = Mathf.Max(f.hi, pl.bounds.max.x); }
        if (f.box != null) f.top = f.box.bounds.max.y;
        return f;
    }

    /// <summary>Set a floor collider to world x-range [l, r], keeping its height.</summary>
    private static void SetColliderX(Transform uf, BoxCollider2D box, float l, float r)
    {
        float sx = Mathf.Abs(uf.lossyScale.x) > 1e-4f ? uf.lossyScale.x : 1f;
        box.size = new Vector2((r - l) / sx, box.size.y);
        box.offset = new Vector2(uf.InverseTransformPoint(new Vector3((l + r) * 0.5f, uf.position.y, uf.position.z)).x, box.offset.y);
    }

    private static Collider2D DragonCollider(Transform stack, string name)
    {
        var d = stack.Find(name); if (d == null) return null;
        foreach (var c in d.GetComponentsInChildren<Collider2D>(false)) if (!c.isTrigger) return c;
        return null;
    }

    /// <summary>
    /// The scaffold only works if every plank reaches BOTH dragons (Brutus bounces between them).
    /// The entry stub was short with a long invisible collider; make it a real full plank, identical
    /// to the floor above it: same pieces, same positions, same collider, same backing panel.
    /// </summary>
    private void TrimStub(Transform stack)
    {
        var floors = new List<ScaffoldFloor>();
        foreach (Transform plat in stack)
        {
            if (!plat.name.StartsWith("Platform")) continue;
            var f = ReadScaffoldFloor(plat);
            if (f.box != null && f.planks.Count > 0) floors.Add(f);
        }
        if (floors.Count < 2) return;
        floors.Sort((a, b) => a.top.CompareTo(b.top));
        var entry = floors[0]; var model = floors[1];

        // same horizontal placement as the floor above
        entry.plat.position = new Vector3(model.plat.position.x, entry.plat.position.y, entry.plat.position.z);
        entry.uf.localPosition = new Vector3(model.uf.localPosition.x, entry.uf.localPosition.y, entry.uf.localPosition.z);
        // same pieces on/off and at the same spots
        foreach (Transform piece in model.uf)
        {
            var twin = entry.uf.Find(piece.name);
            if (twin == null) continue;
            twin.gameObject.SetActive(piece.gameObject.activeSelf);
            twin.localPosition = new Vector3(piece.localPosition.x, twin.localPosition.y, twin.localPosition.z);
        }
        // same collider (reaching under both dragons)
        entry.box.size = new Vector2(model.box.size.x, entry.box.size.y);
        entry.box.offset = new Vector2(model.box.offset.x, entry.box.offset.y);
        // same backing panel width
        foreach (Transform c in entry.plat)
        {
            if (c == entry.uf || !c.name.StartsWith("Background_gameplay")) continue;
            var m = model.plat.Find(c.name);
            if (m != null) { c.localScale = m.localScale; c.localPosition = new Vector3(m.localPosition.x, c.localPosition.y, c.localPosition.z); }
        }
    }

    /// <summary>
    /// Variation that keeps the scaffold working: every plank still reaches both dragons, but each
    /// floor gets a random mix of the plank art pieces and random mirroring, so no two rungs look alike.
    /// </summary>
    private void RandomizeScaffold(Transform stack)
    {
        var sprites = new List<Sprite>();
        foreach (var sr in stack.GetComponentsInChildren<SpriteRenderer>(true))
            if (sr.sprite != null && sr.sprite.name.StartsWith("Scaffold_WoodPlatform") && !sr.sprite.name.Contains("Corner") && !sprites.Contains(sr.sprite)) sprites.Add(sr.sprite);
        if (sprites.Count == 0) return;
        foreach (Transform plat in stack)
        {
            if (!plat.name.StartsWith("Platform")) continue;
            var f = ReadScaffoldFloor(plat);
            if (f.box == null || _rng.NextDouble() > scaffoldRandomChance) continue;
            foreach (var pl in f.planks)
            {
                pl.sprite = sprites[_rng.Next(sprites.Count)];
                pl.flipX = _rng.NextDouble() < 0.5;
            }
        }
    }

    // ------------------------------------------------------------------
    // Row panels: only real milestones are white; the tan start row belongs to the very first chunk only.
    private static readonly Color RowColor = new Color(0.73f, 0.25f, 0.25f, 0.18f);

    private static void NormalizeRowPanels(Transform chunk, int index)
    {
        // placeholder texts from the hand-built level (e.g. "Level Dn" on scaffold tops) - the milestone number replaces them
        foreach (var txt in chunk.GetComponentsInChildren<TMPro.TMP_Text>(true))
            if (txt.text != null && txt.text.StartsWith("Level")) txt.gameObject.SetActive(false);
        foreach (var sr in chunk.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null || sr.sprite.name != "Square") continue;
            var c = sr.color;
            bool tan = c.a > 0.9f && c.r > 0.6f && c.r < 0.76f && c.g > 0.4f && c.g < 0.56f && c.b > 0.28f && c.b < 0.42f;
            bool strayWhite = c.r > 0.9f && c.g > 0.9f && c.b > 0.9f && c.a < 0.6f && sr.GetComponentInParent<MilestoneIndex>() == null;
            if ((tan && index > 0) || strayWhite) sr.color = RowColor;
        }
    }

    private static void GetExtent(List<Transform> tiles, out float lo, out float hi)
    {
        lo = float.MaxValue; hi = float.MinValue;
        foreach (var t in tiles)
        {
            var c = t.GetComponent<Collider2D>(); if (c == null) continue;
            lo = Mathf.Min(lo, c.bounds.min.x); hi = Mathf.Max(hi, c.bounds.max.x);
        }
    }

    /// <summary>Stretch full-width panel sprites so their edges follow the floor ends.</summary>
    private static void FitWidth(Transform group, float oldL, float oldR, float newL, float newR)
    {
        foreach (var sr in group.GetComponentsInChildren<SpriteRenderer>(true))
        {
            var b = sr.bounds;
            if (b.size.x < (oldR - oldL) * 0.6f) continue;          // only the full-width pieces
            float targetMin = newL + (b.min.x - oldL), targetMax = newR + (b.max.x - oldR);
            float k = (targetMax - targetMin) / b.size.x;
            var s = sr.transform.localScale; s.x *= k; sr.transform.localScale = s;
            b = sr.bounds;
            sr.transform.position += new Vector3((targetMin + targetMax) * 0.5f - b.center.x, 0f, 0f);
        }
    }

    // ------------------------------------------------------------------
    // Eggs: rest on a floor Brutus runs on, inside its edges, clear of walls and dragons.
    private static void SeatEgg(Transform egg, Transform stack)
    {
        const float halfH = 0.625f, halfW = 0.55f, inset = 0.35f;
        Collider2D floor = null;
        foreach (var h in Physics2D.RaycastAll((Vector2)egg.position + Vector2.up * 0.3f, Vector2.down, 10f))
        {
            if (h.collider.isTrigger || !h.collider.transform.IsChildOf(stack)) continue;
            string n = h.collider.name;
            if (n.StartsWith("Floor_Tile") || n.StartsWith("Up Floor")) { floor = h.collider; break; }
        }
        if (floor == null)   // nothing under it any more (tile removed): use the nearest floor in this stack
        {
            float best = float.MaxValue;
            foreach (var c in stack.GetComponentsInChildren<Collider2D>(false))
            {
                if (c.isTrigger || !(c.name.StartsWith("Floor_Tile") || c.name.StartsWith("Up Floor"))) continue;
                float d = Vector2.Distance(egg.position, c.bounds.ClosestPoint(egg.position));
                if (d < best) { best = d; floor = c; }
            }
            if (floor == null) { egg.gameObject.SetActive(false); return; }
        }
        float top = floor.bounds.max.y;
        Transform plat = floor.transform.parent;
        while (plat != null && !plat.name.StartsWith("Platform")) plat = plat.parent;

        float lo = floor.bounds.min.x, hi = floor.bounds.max.x;
        if (plat != null)
        {
            if (floor.name.StartsWith("Up Floor"))
            {
                lo = float.MaxValue; hi = float.MinValue;
                foreach (var sr in plat.GetComponentsInChildren<SpriteRenderer>(false))
                    if (sr.sprite != null && sr.sprite.name.StartsWith("Scaffold_WoodPlatform")) { lo = Mathf.Min(lo, sr.bounds.min.x); hi = Mathf.Max(hi, sr.bounds.max.x); }
            }
            else
            {
                lo = float.MaxValue; hi = float.MinValue;
                foreach (var c in plat.GetComponentsInChildren<Collider2D>(false))
                    if (!c.isTrigger && c.name.StartsWith("Floor_Tile") && Mathf.Abs(c.bounds.max.y - top) < 0.2f) { lo = Mathf.Min(lo, c.bounds.min.x); hi = Mathf.Max(hi, c.bounds.max.x); }
            }
        }
        float minX = 0f, maxX = -1f;
        // First try to keep clear of the dragons' sprites; in the tightest scaffolds there is no room for that,
        // so fall back to their colliders (egg tucked between them, still reachable).
        for (int pass = 0; pass < 2 && minX > maxX; pass++)
        {
            minX = lo + halfW + inset; maxX = hi - halfW - inset;
            foreach (var c in stack.GetComponentsInChildren<Collider2D>(false))
            {
                if (c.isTrigger) continue;
                bool wall = c.name.Trim() == "Left Wall" || c.name.Trim() == "Right Wall";
                bool dragon = c.transform.name.StartsWith("Dragon") || (c.transform.parent != null && c.transform.parent.name.StartsWith("Dragon"));
                if (!wall && !dragon) continue;
                Bounds bb = c.bounds;
                if (dragon && pass == 0) foreach (var sr in c.GetComponentsInChildren<SpriteRenderer>(false)) bb.Encapsulate(sr.bounds);
                if (!dragon && (bb.max.y < top || bb.min.y > top + 2f * halfH)) continue;
                if (bb.center.x < (lo + hi) * 0.5f) minX = Mathf.Max(minX, bb.max.x + halfW + inset);
                else maxX = Mathf.Min(maxX, bb.min.x - halfW - inset);
            }
        }
        if (minX > maxX) { egg.gameObject.SetActive(false); return; }
        egg.position = new Vector3(Mathf.Clamp(egg.position.x, minX, maxX), top + halfH, egg.position.z);
    }

    // ------------------------------------------------------------------
    /// <summary>Spawns chunks up to the given index without a player (for validation in Play mode).</summary>
    public void GenerateUpTo(int index)
    {
        while (_chunks[_chunks.Count - 1].index < index)
        {
            var prev = _chunks[_chunks.Count - 1];
            var tpl = Choose(prev.index + 1, prev.template);
            if (tpl.type == ChunkType.Diagonal) { SpawnDiagonal(prev, prev.index + 1); continue; }
            if (tpl.type == ChunkType.Twin) { SpawnTwin(prev, prev.index + 1); continue; }
            SpawnChunk(tpl, new Vector3(prev.exitCentreX - tpl.entryLocal.x, prev.exitTop + joinGap - tpl.entryLocal.y, tpl.root.position.z));
        }
    }
}
