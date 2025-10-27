using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vampire Survivors-style wave spawner.
/// Spawns enemies outside camera view based on time-based difficulty scaling.
/// </summary>
public class WaveSpawnManager : MonoBehaviour
{
    [System.Serializable]
    public class EnemySpawnData
    {
        [Header("Enemy Info")]
        public string enemyName = "Basic Enemy";
        public GameObject enemyPrefab;
        
        [Header("Spawn Timing")]
        [Tooltip("When this enemy starts spawning (in seconds)")]
        public float startTime = 0f;
        
        [Tooltip("When this enemy stops spawning (0 = never stops)")]
        public float endTime = 0f;
        
        [Header("Spawn Weight/Probability")]
        [Tooltip("Base spawn weight at startTime")]
        public float baseWeight = 100f;
        
        [Tooltip("Weight at peak time")]
        public float peakWeight = 100f;
        
        [Tooltip("When weight reaches peak (in seconds)")]
        public float peakTime = 300f;
        
        [Header("Spawn Rate")]
        [Tooltip("How many of this enemy spawn per wave at start")]
        public int minSpawnCount = 1;
        public int maxSpawnCount = 3;
        
        [Tooltip("Multiplier for spawn count over time")]
        public AnimationCurve spawnCountMultiplier = AnimationCurve.Linear(0, 1, 600, 2);
        
        // Runtime
        [HideInInspector] public float currentWeight;
        
        [HideInInspector] public bool isActive;
    }

    [Header("References")]
    [SerializeField] private Transform heroTransform;
    [SerializeField] private Camera mainCamera;

    [Header("Enemy Types")]
    [SerializeField] private EnemySpawnData[] enemyTypes;

    [Header("Spawn Settings")]
    [SerializeField] private float baseSpawnInterval = 2f; // Seconds between spawn waves
    [Tooltip("Minimum spawn interval (fastest spawning)")]
    [SerializeField] private float minSpawnInterval = 0.3f;
    
    [Tooltip("How spawn interval changes over time")]
    [SerializeField] private AnimationCurve spawnIntervalCurve = AnimationCurve.EaseInOut(0, 1, 600, 0.3f);

    [Header("Spawn Area")]
    [SerializeField] private float spawnDistanceFromCamera = 2f; // Units outside camera view
    [SerializeField] private float minDistanceFromHero = 8f; // Don't spawn too close to hero

    [Header("Performance")]
    [SerializeField] private int maxEnemiesAlive = 200; // Prevent lag
    [SerializeField] private Transform enemyContainer; // Parent for spawned enemies

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showSpawnZoneGizmos = true;

    // Private runtime data
    private float gameTime = 0f;
    private float nextSpawnTime = 0f;
    private int totalEnemiesSpawned = 0;
    private int currentEnemiesAlive = 0;
    private List<GameObject> aliveEnemies = new List<GameObject>();

    public static WaveSpawnManager Instance { get; private set; }

    // Properties for external access
    public float GameTime => gameTime;
    public int TotalSpawned => totalEnemiesSpawned;
    public int CurrentAlive => currentEnemiesAlive;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Auto-find references if not assigned
        if (heroTransform == null)
        {
            GameObject hero = GameObject.FindGameObjectWithTag("Hero");
            if (hero != null) heroTransform = hero.transform;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (enemyContainer == null)
        {
            GameObject container = new GameObject("EnemyContainer");
            enemyContainer = container.transform;
        }
    }

    void Start()
    {
        if (heroTransform == null)
        {
            Debug.LogError("WaveSpawnManager: Hero not found! Assign Hero transform or tag Hero with 'Hero' tag.");
            enabled = false;
            return;
        }

        if (mainCamera == null)
        {
            Debug.LogError("WaveSpawnManager: Camera not found!");
            enabled = false;
            return;
        }

        if (enemyTypes.Length == 0)
        {
            Debug.LogError("WaveSpawnManager: No enemy types assigned!");
            enabled = false;
            return;
        }

        nextSpawnTime = baseSpawnInterval;
    }

    void Update()
    {
        gameTime += Time.deltaTime;

        // Update enemy weights based on time
        UpdateEnemyWeights();

        // Check if it's time to spawn
        if (gameTime >= nextSpawnTime)
        {
            SpawnWave();
            
            // Calculate next spawn time based on curve
            float intervalMultiplier = spawnIntervalCurve.Evaluate(gameTime);
            float currentInterval = Mathf.Lerp(baseSpawnInterval, minSpawnInterval, intervalMultiplier);
            nextSpawnTime = gameTime + currentInterval;
        }

        // Clean up destroyed enemies
        CleanupDestroyedEnemies();

        // Debug info
        if (showDebugInfo)
        {
            DrawDebugInfo();
        }
    }

    void UpdateEnemyWeights()
    {
        foreach (var enemyData in enemyTypes)
        {
            // Check if enemy should be active at current time
            bool startsNow = gameTime >= enemyData.startTime;
            bool endsNow = enemyData.endTime > 0 && gameTime >= enemyData.endTime;
            enemyData.isActive = startsNow && !endsNow;

            if (!enemyData.isActive)
            {
                enemyData.currentWeight = 0f;
                continue;
            }

            // Calculate weight based on time progression
            float timeSinceStart = gameTime - enemyData.startTime;
            float timeUntilPeak = enemyData.peakTime - enemyData.startTime;

            if (timeUntilPeak <= 0) timeUntilPeak = 1f; // Prevent division by zero

            float progression = Mathf.Clamp01(timeSinceStart / timeUntilPeak);
            enemyData.currentWeight = Mathf.Lerp(enemyData.baseWeight, enemyData.peakWeight, progression);
        }
    }

    void SpawnWave()
    {
        // Check if we hit enemy limit
        if (currentEnemiesAlive >= maxEnemiesAlive)
        {
            if (showDebugInfo)
            {
                Debug.Log($"Enemy limit reached ({maxEnemiesAlive}), skipping spawn.");
            }
            return;
        }

        // Select which enemies to spawn this wave
        List<EnemySpawnData> enemiesToSpawn = SelectEnemiesForWave();

        if (enemiesToSpawn.Count == 0)
        {
            Debug.LogWarning($"No active enemies at time {gameTime:F1}s");
            return;
        }

        // Spawn selected enemies
        foreach (var enemyData in enemiesToSpawn)
        {
            int spawnCount = CalculateSpawnCount(enemyData);
            
            for (int i = 0; i < spawnCount; i++)
            {
                if (currentEnemiesAlive >= maxEnemiesAlive) break;
                
                Vector3 spawnPos = GetRandomSpawnPosition();
                SpawnEnemy(enemyData.enemyPrefab, spawnPos);
            }
        }
    }

    List<EnemySpawnData> SelectEnemiesForWave()
    {
        List<EnemySpawnData> selected = new List<EnemySpawnData>();

        // Calculate total weight of active enemies
        float totalWeight = 0f;
        foreach (var enemyData in enemyTypes)
        {
            if (enemyData.isActive)
            {
                totalWeight += enemyData.currentWeight;
            }
        }

        if (totalWeight <= 0) return selected;

        // Weighted random selection - pick 1-3 enemy types per wave
        int typesToSpawn = Random.Range(1, Mathf.Min(4, enemyTypes.Length + 1));

        for (int i = 0; i < typesToSpawn; i++)
        {
            float randomValue = Random.Range(0f, totalWeight);
            float cumulativeWeight = 0f;

            foreach (var enemyData in enemyTypes)
            {
                if (!enemyData.isActive) continue;

                cumulativeWeight += enemyData.currentWeight;
                if (randomValue <= cumulativeWeight)
                {
                    if (!selected.Contains(enemyData))
                    {
                        selected.Add(enemyData);
                    }
                    break;
                }
            }
        }

        return selected;
    }

    int CalculateSpawnCount(EnemySpawnData enemyData)
    {
        // Base count
        int baseCount = Random.Range(enemyData.minSpawnCount, enemyData.maxSpawnCount + 1);
        
        // Apply time multiplier
        float multiplier = enemyData.spawnCountMultiplier.Evaluate(gameTime);
        
        return Mathf.RoundToInt(baseCount * multiplier);
    }

    Vector3 GetRandomSpawnPosition()
    {
        // Get camera bounds in world space
        float cameraHeight = mainCamera.orthographicSize * 2f;
        float cameraWidth = cameraHeight * mainCamera.aspect;
        Vector3 cameraPos = mainCamera.transform.position;

        // Choose random edge (0=top, 1=right, 2=bottom, 3=left)
        int edge = Random.Range(0, 4);
        Vector3 spawnPos = Vector3.zero;

        float extraDistance = spawnDistanceFromCamera;

        switch (edge)
        {
            case 0: // Top
                spawnPos = new Vector3(
                    Random.Range(cameraPos.x - cameraWidth / 2, cameraPos.x + cameraWidth / 2),
                    cameraPos.y + cameraHeight / 2 + extraDistance,
                    0
                );
                break;
            case 1: // Right
                spawnPos = new Vector3(
                    cameraPos.x + cameraWidth / 2 + extraDistance,
                    Random.Range(cameraPos.y - cameraHeight / 2, cameraPos.y + cameraHeight / 2),
                    0
                );
                break;
            case 2: // Bottom
                spawnPos = new Vector3(
                    Random.Range(cameraPos.x - cameraWidth / 2, cameraPos.x + cameraWidth / 2),
                    cameraPos.y - cameraHeight / 2 - extraDistance,
                    0
                );
                break;
            case 3: // Left
                spawnPos = new Vector3(
                    cameraPos.x - cameraWidth / 2 - extraDistance,
                    Random.Range(cameraPos.y - cameraHeight / 2, cameraPos.y + cameraHeight / 2),
                    0
                );
                break;
        }

        // Check distance from hero
        if (heroTransform != null)
        {
            float distToHero = Vector3.Distance(spawnPos, heroTransform.position);
            if (distToHero < minDistanceFromHero)
            {
                // Push spawn position further away
                Vector3 dirFromHero = (spawnPos - heroTransform.position).normalized;
                spawnPos = heroTransform.position + dirFromHero * minDistanceFromHero;
            }
        }

        return spawnPos;
    }

    void SpawnEnemy(GameObject enemyPrefab, Vector3 position)
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("Attempted to spawn null enemy prefab!");
            return;
        }

        GameObject enemy = Instantiate(enemyPrefab, position, Quaternion.identity, enemyContainer);
        aliveEnemies.Add(enemy); // Добавляем в список
        currentEnemiesAlive++;
        totalEnemiesSpawned++;

        if (showDebugInfo && totalEnemiesSpawned % 50 == 0)
        {
            Debug.Log($"[{gameTime:F1}s] Total spawned: {totalEnemiesSpawned} | Alive: {currentEnemiesAlive}");
        }
    }

    void CleanupDestroyedEnemies()
    {
        aliveEnemies.RemoveAll(enemy => enemy == null);
        currentEnemiesAlive = aliveEnemies.Count;
    }

    public List<GameObject> GetActiveEnemies()
    {
        // Возвращаем копию списка, чтобы внешние скрипты не могли напрямую изменять наш внутренний список
        return new List<GameObject>(aliveEnemies);
    }

    // Public API
    public void ClearAllEnemies()
    {
        foreach (var enemy in aliveEnemies)
        {
            if (enemy != null) Destroy(enemy);
        }
        aliveEnemies.Clear();
        currentEnemiesAlive = 0;
    }

    public void ResetGame()
    {
        ClearAllEnemies();
        gameTime = 0f;
        nextSpawnTime = baseSpawnInterval;
        totalEnemiesSpawned = 0;
    }

    // Debug visualization
    void DrawDebugInfo()
    {
        // This will show in Scene view
    }

    void OnDrawGizmos()
    {
        if (!showSpawnZoneGizmos || mainCamera == null) return;

        // Draw spawn zone around camera
        float cameraHeight = mainCamera.orthographicSize * 2f;
        float cameraWidth = cameraHeight * mainCamera.aspect;
        Vector3 cameraPos = mainCamera.transform.position;

        // Camera view (green)
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(cameraPos, new Vector3(cameraWidth, cameraHeight, 0));

        // Spawn zone (red)
        Gizmos.color = Color.red;
        float spawnWidth = cameraWidth + spawnDistanceFromCamera * 2;
        float spawnHeight = cameraHeight + spawnDistanceFromCamera * 2;
        Gizmos.DrawWireCube(cameraPos, new Vector3(spawnWidth, spawnHeight, 0));

        // Hero safety radius (yellow)
        if (heroTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(heroTransform.position, minDistanceFromHero);
        }
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        // Display debug info
        GUIStyle style = new GUIStyle();
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.UpperLeft;

        string info = $"=== Wave Spawn Manager ===\n";
        info += $"Time: {gameTime:F1}s\n";
        info += $"Total Spawned: {totalEnemiesSpawned}\n";
        info += $"Currently Alive: {currentEnemiesAlive}/{maxEnemiesAlive}\n";
        info += $"Next Spawn: {(nextSpawnTime - gameTime):F1}s\n\n";

        info += "=== Active Enemy Types ===\n";
        foreach (var enemyData in enemyTypes)
        {
            if (enemyData.isActive)
            {
                info += $"{enemyData.enemyName}: Weight {enemyData.currentWeight:F0}\n";
            }
        }

        GUI.Label(new Rect(10, 10, 300, 400), info, style);
    }
}