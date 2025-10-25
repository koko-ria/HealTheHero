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
        // Check if hit player
        if (other.CompareTag("Player"))
        {
            // Deal damage to player
            if (HeroAI.Instance != null)
            {
                HeroAI.Instance.TakeDamage(damage);
            }

            if (destroyOnHit)
            {
                Destroy(gameObject);
            }
        }
        
        // Destroy on walls
        if (other.CompareTag("Wall"))
        {
            Destroy(gameObject);
        }
    }
}