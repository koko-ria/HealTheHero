using UnityEngine;

[CreateAssetMenu(fileName = "New Ammo Type", menuName = "Weapon/Ammo Type")]
public class AmmoType : ScriptableObject
{
    [Header("Basic Info")]
    public string ammoName;
    public Sprite icon;
    public AmmoCategory category;
    
    [Header("Stats")]
    public float cooldown = 1f;
    public int manaCost = 10;
    public float range = 10f;
    
    [Header("Heal Settings")]
    [Tooltip("Amount to heal (if heal type)")]
    public int healAmount = 20;
    
    [Header("Buff Settings")]
    [Tooltip("Buff type (if buff category)")]
    public BuffType buffType;
    [Tooltip("Buff duration in seconds")]
    public float buffDuration = 5f;
    [Tooltip("Buff strength multiplier")]
    public float buffStrength = 1.5f;
    
    [Header("Repulsive Settings")]
    [Tooltip("Projectile prefab (if repulsive type)")]
    public GameObject projectilePrefab;
    [Tooltip("Explosion radius")]
    public float explosionRadius = 3f;
    [Tooltip("Knockback force")]
    public float knockbackForce = 10f;
    
    [Header("Visual")]
    public Color laserColor = Color.green;
    public float laserWidth = 0.1f;
    public GameObject impactEffectPrefab;
}

public enum AmmoCategory
{
    Heal,
    BuffShield,
    BuffDamage,
    BuffResistance,
    Repulsive
}

public enum BuffType
{
    Shield,
    Damage,
    Resistance
}