using UnityEngine;

/// <summary>
/// Helper class with preset animation curves for common spawn patterns.
/// Attach to WaveSpawnManager GameObject for easy access in Inspector.
/// </summary>
public class SpawnCurvePresets : MonoBehaviour
{
    [Header("Spawn Interval Presets")]
    [Tooltip("Gentle difficulty increase")]
    public AnimationCurve easyInterval = AnimationCurve.EaseInOut(0, 1, 900, 0.5f); // 15 min to reach 50% speed
    
    [Tooltip("Normal difficulty increase")]
    public AnimationCurve normalInterval = AnimationCurve.EaseInOut(0, 1, 600, 0.3f); // 10 min to reach 30% speed
    
    [Tooltip("Fast difficulty increase")]
    public AnimationCurve hardInterval = AnimationCurve.EaseInOut(0, 1, 300, 0.2f); // 5 min to reach 20% speed
    
    [Tooltip("Brutal difficulty - spawns get fast quickly")]
    public AnimationCurve brutalInterval = AnimationCurve.EaseInOut(0, 1, 120, 0.1f); // 2 min to reach 10% speed

    [Header("Enemy Count Multiplier Presets")]
    [Tooltip("Slow growth - enemies gradually increase")]
    public AnimationCurve slowGrowth = AnimationCurve.Linear(0, 1, 900, 1.5f); // 1.5x at 15 min
    
    [Tooltip("Normal growth")]
    public AnimationCurve normalGrowth = AnimationCurve.Linear(0, 1, 600, 2.5f); // 2.5x at 10 min
    
    [Tooltip("Fast growth - quickly overwhelm player")]
    public AnimationCurve fastGrowth = AnimationCurve.EaseInOut(0, 1, 300, 4f); // 4x at 5 min
    
    [Tooltip("Exponential - late game explosion")]
    public AnimationCurve exponentialGrowth;
    
    [Tooltip("S-Curve - slow start, rapid middle, slow end")]
    public AnimationCurve sCurveGrowth;

    void OnValidate()
    {
        // Setup exponential curve
        exponentialGrowth = new AnimationCurve();
        exponentialGrowth.AddKey(0, 1);
        exponentialGrowth.AddKey(300, 2);
        exponentialGrowth.AddKey(600, 5);
        exponentialGrowth.AddKey(900, 10);

        // Setup S-curve
        sCurveGrowth = AnimationCurve.EaseInOut(0, 1, 600, 3);
        sCurveGrowth.keys[0].outTangent = 0.1f;
        sCurveGrowth.keys[sCurveGrowth.length - 1].inTangent = 0.1f;
    }

    [Header("Enemy Weight Progression Examples")]
    [Tooltip("Basic enemy - strong at start, weaker later")]
    public AnimationCurve basicEnemyWeight = AnimationCurve.Linear(0, 100, 600, 30);
    
    [Tooltip("Elite enemy - weak at start, strong later")]
    public AnimationCurve eliteEnemyWeight = AnimationCurve.Linear(0, 20, 600, 100);
    
    [Tooltip("Boss enemy - very rare, slight increase")]
    public AnimationCurve bossEnemyWeight = AnimationCurve.Linear(0, 5, 600, 25);
}

/// <summary>
/// Example spawn configurations for different difficulty modes.
/// Copy these values to your WaveSpawnManager.
/// </summary>
[System.Serializable]
public class DifficultyPreset
{
    public string difficultyName;
    public float baseSpawnInterval;
    public float minSpawnInterval;
    public int maxEnemiesAlive;
    public float spawnDistanceFromCamera;
    public float minDistanceFromHero;
}

public static class SpawnPresets
{
    // Easy mode - for beginners
    public static DifficultyPreset Easy = new DifficultyPreset
    {
        difficultyName = "Easy",
        baseSpawnInterval = 3.0f,
        minSpawnInterval = 0.8f,
        maxEnemiesAlive = 100,
        spawnDistanceFromCamera = 3f,
        minDistanceFromHero = 10f
    };

    // Normal mode - balanced
    public static DifficultyPreset Normal = new DifficultyPreset
    {
        difficultyName = "Normal",
        baseSpawnInterval = 2.0f,
        minSpawnInterval = 0.4f,
        maxEnemiesAlive = 150,
        spawnDistanceFromCamera = 2f,
        minDistanceFromHero = 8f
    };

    // Hard mode - challenging
    public static DifficultyPreset Hard = new DifficultyPreset
    {
        difficultyName = "Hard",
        baseSpawnInterval = 1.5f,
        minSpawnInterval = 0.2f,
        maxEnemiesAlive = 200,
        spawnDistanceFromCamera = 2f,
        minDistanceFromHero = 6f
    };

    // Nightmare mode - overwhelming
    public static DifficultyPreset Nightmare = new DifficultyPreset
    {
        difficultyName = "Nightmare",
        baseSpawnInterval = 1.0f,
        minSpawnInterval = 0.1f,
        maxEnemiesAlive = 300,
        spawnDistanceFromCamera = 1.5f,
        minDistanceFromHero = 5f
    };
}

/// <summary>
/// Example enemy configurations for common archetypes.
/// </summary>
public static class EnemyArchetypes
{
    public class ArchetypeConfig
    {
        public string name;
        public float startTime;
        public float baseWeight;
        public float peakWeight;
        public float peakTime;
        public int minSpawn;
        public int maxSpawn;
    }

    // Weak early game enemy
    public static ArchetypeConfig Fodder = new ArchetypeConfig
    {
        name = "Fodder",
        startTime = 0,
        baseWeight = 100,
        peakWeight = 40,
        peakTime = 300,
        minSpawn = 3,
        maxSpawn = 8
    };

    // Standard enemy throughout game
    public static ArchetypeConfig Standard = new ArchetypeConfig
    {
        name = "Standard",
        startTime = 30,
        baseWeight = 50,
        peakWeight = 80,
        peakTime = 300,
        minSpawn = 2,
        maxSpawn = 5
    };

    // Tough mid-game enemy
    public static ArchetypeConfig Elite = new ArchetypeConfig
    {
        name = "Elite",
        startTime = 120,
        baseWeight = 20,
        peakWeight = 60,
        peakTime = 450,
        minSpawn = 1,
        maxSpawn = 3
    };

    // Rare powerful enemy
    public static ArchetypeConfig MiniBoss = new ArchetypeConfig
    {
        name = "MiniBoss",
        startTime = 180,
        baseWeight = 10,
        peakWeight = 30,
        peakTime = 600,
        minSpawn = 1,
        maxSpawn = 2
    };

    // Super rare boss
    public static ArchetypeConfig Boss = new ArchetypeConfig
    {
        name = "Boss",
        startTime = 300,
        baseWeight = 5,
        peakWeight = 20,
        peakTime = 900,
        minSpawn = 1,
        maxSpawn = 1
    };

    // Fast swarm enemy
    public static ArchetypeConfig Swarm = new ArchetypeConfig
    {
        name = "Swarm",
        startTime = 60,
        baseWeight = 30,
        peakWeight = 70,
        peakTime = 400,
        minSpawn = 5,
        maxSpawn = 15
    };
}

/// <summary>
/// Quick setup wizard - attach this to any GameObject to quickly configure
/// a WaveSpawnManager with preset values.
/// </summary>
public class SpawnManagerQuickSetup : MonoBehaviour
{
    [Header("Quick Setup")]
    [SerializeField] private WaveSpawnManager targetManager;
    
    [Header("Select Difficulty")]
    public DifficultyMode difficulty = DifficultyMode.Normal;
    
    [Header("Enemy Setup Templates")]
    [Tooltip("Automatically configure enemy types based on templates")]
    public bool autoConfigureEnemies = false;
    
    [SerializeField] private GameObject basicEnemyPrefab;
    [SerializeField] private GameObject eliteEnemyPrefab;
    [SerializeField] private GameObject bossEnemyPrefab;

    public enum DifficultyMode
    {
        Easy,
        Normal,
        Hard,
        Nightmare
    }

    [ContextMenu("Apply Difficulty Settings")]
    void ApplyDifficultySettings()
    {
        if (targetManager == null)
        {
            targetManager = GetComponent<WaveSpawnManager>();
            if (targetManager == null)
            {
                Debug.LogError("No WaveSpawnManager found!");
                return;
            }
        }

        Debug.Log($"Applying {difficulty} difficulty settings to WaveSpawnManager");
        
        // Note: This is pseudocode - you'd need to expose these fields publicly
        // in WaveSpawnManager or use SerializedObject/SerializedProperty in an Editor script
        
        switch (difficulty)
        {
            case DifficultyMode.Easy:
                Debug.Log("Easy: Spawn Interval 3s → 0.8s, Max 100 enemies");
                break;
            case DifficultyMode.Normal:
                Debug.Log("Normal: Spawn Interval 2s → 0.4s, Max 150 enemies");
                break;
            case DifficultyMode.Hard:
                Debug.Log("Hard: Spawn Interval 1.5s → 0.2s, Max 200 enemies");
                break;
            case DifficultyMode.Nightmare:
                Debug.Log("Nightmare: Spawn Interval 1s → 0.1s, Max 300 enemies");
                break;
        }
    }

    [ContextMenu("Log Enemy Archetype Configs")]
    void LogArchetypeConfigs()
    {
        Debug.Log("=== Enemy Archetype Templates ===");
        Debug.Log($"FODDER: Start {EnemyArchetypes.Fodder.startTime}s | Weight {EnemyArchetypes.Fodder.baseWeight}→{EnemyArchetypes.Fodder.peakWeight} | Spawn {EnemyArchetypes.Fodder.minSpawn}-{EnemyArchetypes.Fodder.maxSpawn}");
        Debug.Log($"STANDARD: Start {EnemyArchetypes.Standard.startTime}s | Weight {EnemyArchetypes.Standard.baseWeight}→{EnemyArchetypes.Standard.peakWeight} | Spawn {EnemyArchetypes.Standard.minSpawn}-{EnemyArchetypes.Standard.maxSpawn}");
        Debug.Log($"ELITE: Start {EnemyArchetypes.Elite.startTime}s | Weight {EnemyArchetypes.Elite.baseWeight}→{EnemyArchetypes.Elite.peakWeight} | Spawn {EnemyArchetypes.Elite.minSpawn}-{EnemyArchetypes.Elite.maxSpawn}");
        Debug.Log($"MINI-BOSS: Start {EnemyArchetypes.MiniBoss.startTime}s | Weight {EnemyArchetypes.MiniBoss.baseWeight}→{EnemyArchetypes.MiniBoss.peakWeight} | Spawn {EnemyArchetypes.MiniBoss.minSpawn}-{EnemyArchetypes.MiniBoss.maxSpawn}");
        Debug.Log($"BOSS: Start {EnemyArchetypes.Boss.startTime}s | Weight {EnemyArchetypes.Boss.baseWeight}→{EnemyArchetypes.Boss.peakWeight} | Spawn {EnemyArchetypes.Boss.minSpawn}-{EnemyArchetypes.Boss.maxSpawn}");
        Debug.Log($"SWARM: Start {EnemyArchetypes.Swarm.startTime}s | Weight {EnemyArchetypes.Swarm.baseWeight}→{EnemyArchetypes.Swarm.peakWeight} | Spawn {EnemyArchetypes.Swarm.minSpawn}-{EnemyArchetypes.Swarm.maxSpawn}");
    }
}

#if UNITY_EDITOR
/// <summary>
/// Custom editor helper to quickly apply preset values.
/// Place this in an Editor folder.
/// </summary>
[UnityEditor.CustomEditor(typeof(SpawnManagerQuickSetup))]
public class SpawnManagerQuickSetupEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SpawnManagerQuickSetup setup = (SpawnManagerQuickSetup)target;

        UnityEditor.EditorGUILayout.Space(10);
        UnityEditor.EditorGUILayout.LabelField("Quick Actions", UnityEditor.EditorStyles.boldLabel);

        if (GUILayout.Button("Apply Difficulty Settings"))
        {
            setup.SendMessage("ApplyDifficultySettings");
        }

        if (GUILayout.Button("Log Enemy Templates to Console"))
        {
            setup.SendMessage("LogArchetypeConfigs");
        }

        UnityEditor.EditorGUILayout.Space(10);
        UnityEditor.EditorGUILayout.HelpBox(
            "Copy these values to manually configure your WaveSpawnManager:\n\n" +
            "EASY: Interval 3→0.8s | Max 100 enemies | Safe distance 10\n" +
            "NORMAL: Interval 2→0.4s | Max 150 enemies | Safe distance 8\n" +
            "HARD: Interval 1.5→0.2s | Max 200 enemies | Safe distance 6\n" +
            "NIGHTMARE: Interval 1→0.1s | Max 300 enemies | Safe distance 5",
            UnityEditor.MessageType.Info
        );
    }
}
#endif