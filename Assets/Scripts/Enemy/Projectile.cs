using UnityEngine;

public class Projectile : MonoBehaviour
{
    public int damage = 1;
    public float lifetime = 5f;
    public bool destroyOnHit = true;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // damage Hero
        if (other.CompareTag("Hero"))
        {
            HeroAI.Instance?.TakeDamage(damage); // Use null conditional operator for safety
            Debug.Log($"Projectile hit Hero. Damage: {damage}");
            if (destroyOnHit) Destroy(gameObject);
            return;
        }

        // damage Player (support)
        if (other.CompareTag("Player"))
        {
            //Player.Instance?.TakeDamage(damage); // Use null conditional operator for safety
            Debug.Log($"Projectile hit Player. Damage: {damage}");
            if (destroyOnHit) Destroy(gameObject);
            return;
        }

        // Destroy on walls
        if (other.CompareTag("Wall"))
        {
            Destroy(gameObject);
            return;
        }

        // You might want projectiles to ignore detection colliders,
        // so ensure your "Detection" colliders are on a separate layer
        // or have a specific tag that projectiles should ignore.
    }
}