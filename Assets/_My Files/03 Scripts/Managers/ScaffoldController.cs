using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to Scaffold_Ptero (child with collider) under Dragon Left AND Dragon Right.
/// Vines fade bottom to top while Ptero slides up simultaneously.
/// </summary>
public class ScaffoldController : MonoBehaviour
{
    [Header("Tuning")]
    [SerializeField] private float pteroMoveDistance = 6f;
    [SerializeField] private int floorsPerHit = 2;

    [Header("Animation")]
    [SerializeField] private float pteroMoveDuration = 0.4f;

    private List<List<GameObject>> _vinePairs = new List<List<GameObject>>();
    private int _currentPairIndex = 0;
    private bool _isAnimating = false;
    private bool _allVinesCleared = false;

    private void Awake()
    {
        Transform parent = transform.parent;
        if (parent == null) return;

        List<(float y, GameObject obj)> vineList = new List<(float, GameObject)>();

        foreach (Transform child in parent)
        {
            if (child == transform) continue;
            if (!child.name.Contains("Vine")) continue;
            if (!child.gameObject.activeSelf) continue;
            vineList.Add((child.position.y, child.gameObject));
        }

        vineList.Sort((a, b) => a.y.CompareTo(b.y));

        float tolerance = 0.5f;
        List<GameObject> currentPair = new List<GameObject>();
        float pairY = float.MinValue;

        foreach (var (y, obj) in vineList)
        {
            if (Mathf.Abs(y - pairY) > tolerance)
            {
                if (currentPair.Count > 0)
                    _vinePairs.Add(new List<GameObject>(currentPair));
                currentPair.Clear();
                pairY = y;
            }
            currentPair.Add(obj);
        }
        if (currentPair.Count > 0)
            _vinePairs.Add(new List<GameObject>(currentPair));

        Debug.Log($"ScaffoldController [{parent.name}]: Found {_vinePairs.Count} active vine pairs.");
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!_isAnimating && !_allVinesCleared && collision.gameObject.CompareTag("Player"))
            StartCoroutine(RemoveVinesAndMove());
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!_isAnimating && !_allVinesCleared && collision.CompareTag("Player"))
            StartCoroutine(RemoveVinesAndMove());
    }

    private IEnumerator RemoveVinesAndMove()
    {
        _isAnimating = true;

        int remaining = _vinePairs.Count - _currentPairIndex;
        int pairsToRemove = Mathf.Min(floorsPerHit, remaining);

        List<List<GameObject>> pairsThisHit = new List<List<GameObject>>();
        for (int i = 0; i < pairsToRemove; i++)
            pairsThisHit.Add(_vinePairs[_currentPairIndex + i]);

        float moveAmount = pteroMoveDistance * ((float)pairsToRemove / floorsPerHit);

        // Ptero slides up AND vines fade bottom to top simultaneously
        yield return StartCoroutine(SlideAndFadeVines(moveAmount, pairsThisHit));

        // Disable after animation completes
        foreach (var pair in pairsThisHit)
        {
            foreach (var vine in pair)
                if (vine != null) vine.SetActive(false);
            _currentPairIndex++;
        }

        if (_currentPairIndex >= _vinePairs.Count)
        {
            _allVinesCleared = true;
            Debug.Log($"ScaffoldController [{transform.parent.name}]: All vines cleared — Ptero stopped.");
        }

        _isAnimating = false;
    }

    private IEnumerator SlideAndFadeVines(float distance, List<List<GameObject>> pairs)
    {
        // Gather all renderers per pair (ordered bottom to top)
        List<List<SpriteRenderer>> renderersByPair = new List<List<SpriteRenderer>>();
        foreach (var pair in pairs)
        {
            List<SpriteRenderer> srs = new List<SpriteRenderer>();
            foreach (var vine in pair)
            {
                foreach (var sr in vine.GetComponentsInChildren<SpriteRenderer>())
                    srs.Add(sr);
                SpriteRenderer root = vine.GetComponent<SpriteRenderer>();
                if (root != null && !srs.Contains(root)) srs.Add(root);
            }
            renderersByPair.Add(srs);
        }

        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + new Vector3(0, distance, 0);
        float elapsed = 0f;

        while (elapsed < pteroMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / pteroMoveDuration; // 0 to 1 linear progress

            // Ptero slides up with ease out cubic
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.Lerp(startPos, endPos, easedT);

            // Fade pairs bottom to top staggered across the duration
            // Pair 0 fades first half, pair 1 fades second half (for 2 pairs)
            for (int i = 0; i < renderersByPair.Count; i++)
            {
                float pairStart = (float)i / renderersByPair.Count;
                float pairEnd = (float)(i + 1) / renderersByPair.Count;
                float pairT = Mathf.InverseLerp(pairStart, pairEnd, t);
                float alpha = Mathf.Lerp(1f, 0f, pairT);

                foreach (var sr in renderersByPair[i])
                {
                    if (sr == null) continue;
                    Color c = sr.color;
                    c.a = alpha;
                    sr.color = c;
                }
            }

            yield return null;
        }

        transform.position = endPos;

        // Ensure fully transparent at end
        foreach (var srs in renderersByPair)
            foreach (var sr in srs)
            {
                if (sr == null) continue;
                Color c = sr.color;
                c.a = 0f;
                sr.color = c;
            }
    }

    public void ResetScaffold()
    {
        _currentPairIndex = 0;
        _isAnimating = false;
        _allVinesCleared = false;

        foreach (var pair in _vinePairs)
        {
            foreach (var vine in pair)
            {
                if (vine == null) continue;
                vine.SetActive(true);
                foreach (var sr in vine.GetComponentsInChildren<SpriteRenderer>())
                {
                    Color c = sr.color; c.a = 1f; sr.color = c;
                }
            }
        }
    }
}