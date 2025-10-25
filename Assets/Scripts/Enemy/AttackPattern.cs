using UnityEngine;

[CreateAssetMenu(fileName = "New Attack Pattern", menuName = "Enemy/Attack Pattern")]
public class AttackPattern : ScriptableObject
{
    [Header("Pattern Settings")]
    public PatternType patternType;
    public float cooldown = 2f;
    
    [Header("Projectile")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 5f;
    public int damage = 1;
    
    [Header("Pattern Specifics")]
    [Tooltip("Number of projectiles in burst/circle")]
    public int projectileCount = 8;
    
    [Tooltip("Angle spread for burst shots")]
    public float spreadAngle = 45f;
    
    [Tooltip("Delay between projectiles in burst")]
    public float burstDelay = 0.1f;
}

public enum PatternType
{
    Single,         // One projectile at player
    Burst,          // Multiple projectiles in a spread
    Circle,         // 360 degree pattern
    Spiral,         // Rotating pattern
    Predictive      // Aims where player will be
}