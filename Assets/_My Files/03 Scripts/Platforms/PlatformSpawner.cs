using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class PlatformSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject platformPrefab;
    public GameObject coinPrefab;
    public GameObject trapPrefab;

    [Header("Player")]
    public Transform player;

    [Header("Settings")]
    public int minPlatforms = 13;
    public float destroyOffset = 15f;

    [Header("JSON")]
    public string url =
        "https://raw.githubusercontent.com/sac1441/Brutus_gamelevels/main/levels.json?v=1";

    private PlatformData data;

    private int currentIndex = 0;

    private List<GameObject> spawnedPlatforms =
        new List<GameObject>();

    void Start()
    {
        Debug.Log("PLATFORM SPAWNER STARTED");

        StartCoroutine(LoadFromWeb());
    }

    IEnumerator LoadFromWeb()
    {
        Debug.Log("DOWNLOADING JSON...");

        UnityWebRequest req =
            UnityWebRequest.Get(url);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(
                "JSON DOWNLOAD FAILED : " +
                req.error
            );

            yield break;
        }

        string json =
            req.downloadHandler.text;

        Debug.Log("RAW JSON :\n" + json);

        data =
            JsonUtility.FromJson<PlatformData>(json);

        if (data == null)
        {
            Debug.LogError("DATA IS NULL");
            yield break;
        }

        if (data.platforms == null)
        {
            Debug.LogError("PLATFORMS ARRAY NULL");
            yield break;
        }

        Debug.Log(
            "TOTAL PLATFORMS : " +
            data.platforms.Count
        );

        // DEBUG ALL LEVEL DATA

        for (int i = 0; i < data.platforms.Count; i++)
        {
            PlatformConfig c =
                data.platforms[i];

            Debug.Log(
                "LEVEL " + i +
                " | Y = " + c.y +
                " | LEFT = " + c.leftWall +
                " | RIGHT = " + c.rightWall
            );
        }

        ClearAllPlatforms();

        while (spawnedPlatforms.Count < minPlatforms)
        {
            SpawnNext();
        }
    }

    void Update()
    {
        if (data == null) return;

        while (spawnedPlatforms.Count < minPlatforms)
        {
            SpawnNext();
        }

        Cleanup();
    }

    void SpawnNext()
    {
        if (data.platforms == null ||
            data.platforms.Count == 0)
        {
            Debug.LogWarning("NO PLATFORM DATA");
            return;
        }

        PlatformConfig config =
            data.platforms[currentIndex];

        Debug.Log("--------------------------------");
        Debug.Log(
            "SPAWNING PLATFORM INDEX : " +
            currentIndex
        );

        GameObject platform =
            SpawnPlatform(config);

        spawnedPlatforms.Add(platform);

        currentIndex++;

        if (currentIndex >= data.platforms.Count)
        {
            currentIndex = 0;
        }
    }

    GameObject SpawnPlatform(PlatformConfig config)
    {
        Debug.Log(
            "SPAWNING PLATFORM AT Y : " +
            config.y
        );

        GameObject platform = Instantiate(
            platformPrefab,
            new Vector3(0, config.y, 0),
            Quaternion.identity
        );

        Transform root =
            platform.transform;

        // PRINT FULL HIERARCHY

        PrintHierarchy(root);

        // ---------------- FLOORS ----------------

        for (int i = 0; i < config.floors.Length; i++)
        {
            string tileName =
                "Floor_Tile_0" + (i + 1);

            Transform tile =
                root.Find(tileName);

            if (tile == null)
            {
                Debug.LogWarning(
                    tileName + " NOT FOUND"
                );

                continue;
            }

            bool active =
                config.floors[i] == 1;

            Debug.Log(
                tileName +
                " -> ACTIVE : " +
                active
            );

            tile.gameObject.SetActive(active);

            if (!active)
                continue;

            Vector3 spawnPos =
                tile.position + Vector3.up * 0.5f;

            // COINS

            if (config.coins != null &&
                config.coins.Length > i &&
                config.coins[i] == 1)
            {
                Debug.Log(
                    "SPAWNING COIN ON : " +
                    tileName
                );

                Instantiate(
                    coinPrefab,
                    spawnPos,
                    Quaternion.identity
                );
            }

            // TRAPS

            if (config.traps != null &&
                config.traps.Length > i &&
                config.traps[i] == 1)
            {
                Debug.Log(
                    "SPAWNING TRAP ON : " +
                    tileName
                );

                Instantiate(
                    trapPrefab,
                    spawnPos,
                    Quaternion.identity
                );
            }
        }

        // ---------------- WALLS ----------------

        Debug.Log(
            "JSON LEFT WALL : " +
            config.leftWall
        );

        Debug.Log(
            "JSON RIGHT WALL : " +
            config.rightWall
        );

        bool leftFound = false;
        bool rightFound = false;

        foreach (Transform t in
            root.GetComponentsInChildren<Transform>(true))
        {
            Debug.Log(
                "CHECKING OBJECT : " +
                t.name
            );

            // LEFT WALL

            if (t.name.Trim() == "Left Wall")
            {
                leftFound = true;

                Debug.Log(
                    "LEFT WALL FOUND"
                );

                Debug.Log(
                    "SETTING LEFT WALL ACTIVE : " +
                    config.leftWall
                );

                t.gameObject.SetActive(
                    config.leftWall
                );
            }

            // RIGHT WALL

            if (t.name.Trim() == "Right Wall")
            {
                rightFound = true;

                Debug.Log(
                    "RIGHT WALL FOUND"
                );

                Debug.Log(
                    "SETTING RIGHT WALL ACTIVE : " +
                    config.rightWall
                );

                t.gameObject.SetActive(
                    config.rightWall
                );
            }
        }

        if (!leftFound)
        {
            Debug.LogWarning(
                "LEFT WALL NOT FOUND IN PREFAB"
            );
        }

        if (!rightFound)
        {
            Debug.LogWarning(
                "RIGHT WALL NOT FOUND IN PREFAB"
            );
        }

        // ---------------- BG + TEXT ----------------

        foreach (Transform t in
            root.GetComponentsInChildren<Transform>(true))
        {
            // BACKGROUND

            if (t.name.Trim() == "Background_Level")
            {
                t.gameObject.SetActive(
                    config.showBackground
                );
            }

            // TEXT

            if (t.name.Trim() == "Text (TMP)")
            {
                t.gameObject.SetActive(
                    config.showLabel
                );

                TMPro.TextMeshPro tmp =
                    t.GetComponent<TMPro.TextMeshPro>();

                if (tmp != null)
                {
                    tmp.text =
                        config.labelText;
                }
            }
        }
        return platform;
    }

    void Cleanup()
    {
        /*
        for (int i = spawnedPlatforms.Count - 1; i >= 0; i--)
        {
            if (player.position.y -
                spawnedPlatforms[i].transform.position.y >
                destroyOffset)
            {
                Destroy(spawnedPlatforms[i]);

                spawnedPlatforms.RemoveAt(i);
            }
        }
        */
    }

    void ClearAllPlatforms()
    {
        Debug.Log("CLEARING OLD PLATFORMS");

        foreach (GameObject p in spawnedPlatforms)
        {
            if (p != null)
            {
                Destroy(p);
            }
        }

        spawnedPlatforms.Clear();

        currentIndex = 0;
    }

    void PrintHierarchy(
        Transform parent,
        string space = ""
    )
    {
        Debug.Log(space + parent.name);

        foreach (Transform child in parent)
        {
            PrintHierarchy(
                child,
                space + "----"
            );
        }
    }
}