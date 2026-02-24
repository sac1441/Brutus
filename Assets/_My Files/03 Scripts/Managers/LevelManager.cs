using System.Collections.Generic;
using TarodevController;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [System.Serializable]
    public class PlatformCategory
    {
        public string name;
        public GameObject[] prefabs;
    }

    public PlayerController PlayerController;
    public PlatformCategory[] platformCategories;
    public GameObject[] collectables;
    public Transform spawnContainer;
    public Transform spawnPos;
    public int maxPlatforms = 20; // Platforms to spawn at Start
    public int totalMaxPlatforms = 30; // Maximum allowed in the scene
    public float spawnDistance = 3f;
    public float collectableMaxXpos = 8f;

    private float nextSpawnY;
    private List<GameObject> activePlatforms = new List<GameObject>();
    private int totalPlatformCount = 0;
    private int lastRandomIndex = -1; // add this at top with other variables
    private int categoryIndex = 0;

    void Start()
    {
        //CharacterController2D.instance.jumpEvent += SpawnPlatform;
        PlayerController.Jumped += SpawnPlatform;
        nextSpawnY = spawnPos.position.y;

        SpawnFirstPlatform();

        for (int i = 1; i < maxPlatforms; i++) // Skip 0, first is already spawned
        {
            SpawnPlatform();
        }
    }

    private void SpawnFirstPlatform()
    {
        // Choose a random category
        categoryIndex = Random.Range(0, platformCategories.Length);
        GameObject firstPlatform = Instantiate(platformCategories[categoryIndex].prefabs[0], spawnPos.position, Quaternion.identity, spawnContainer);
        activePlatforms.Add(firstPlatform);
        nextSpawnY += spawnDistance;
        totalPlatformCount++;
    }

    void SpawnPlatform()
    {
        GameObject platformToSpawn;
        if (totalPlatformCount % 10 == 0)
        {
            // Choose a random category
            categoryIndex = Random.Range(0, platformCategories.Length);
            platformToSpawn = platformCategories[categoryIndex].prefabs[0];
            Debug.Log("Spawned save point platform");
        }
        else
        {
            PlatformCategory selectedCategory = platformCategories[categoryIndex];
            // Choose a random prefab within the selected category
            int prefabIndex;
            do
            {
                prefabIndex = Random.Range(1, selectedCategory.prefabs.Length);
            } while (selectedCategory.prefabs.Length > 1 && prefabIndex == lastRandomIndex);

            lastRandomIndex = prefabIndex;
            platformToSpawn = selectedCategory.prefabs[prefabIndex];
        }

        Vector3 spawnPosition = new Vector3(0, nextSpawnY, 0);
        GameObject newPlatform = Instantiate(platformToSpawn, spawnPosition, Quaternion.identity, spawnContainer);
        activePlatforms.Add(newPlatform);

        // Collectable spawn logic
        if (collectables.Length > 0 && Random.value < 0.5f)
        {
            int collectableIndex = Random.Range(0, collectables.Length);
            float xpos = Random.Range(-collectableMaxXpos, collectableMaxXpos);
            Vector3 collectablePos = spawnPosition + new Vector3(xpos, 1f, 0);
            Instantiate(collectables[collectableIndex], collectablePos, Quaternion.identity, spawnContainer);
        }

        nextSpawnY += spawnDistance;
        totalPlatformCount++;

        if (activePlatforms.Count > totalMaxPlatforms)
        {
            Destroy(activePlatforms[0]);
            activePlatforms.RemoveAt(0);
        }
    }

    private void OnDisable()
    {
        if (PlayerController != null)
            PlayerController.Jumped -= SpawnPlatform;
    }
}
