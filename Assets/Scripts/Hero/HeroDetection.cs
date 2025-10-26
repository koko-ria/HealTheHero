using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class HeroDetection : MonoBehaviour
{
    private HeroAI hero;

    void Awake()
    {
        hero = GetComponentInParent<HeroAI>(); // Gets HeroAI from parent GameObject
        if (hero == null)
        {
            Debug.LogError("HeroDetection: Could not find HeroAI in parent.", this);
            enabled = false;
            return;
        }

        var col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (hero == null) return;

        if (other.CompareTag("Enemy"))
        {
            float dist = Vector2.Distance(other.transform.position, hero.transform.position);
            hero.UpdateNearestEnemyCandidate(other.transform, dist);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (hero == null) return;

        if (other.CompareTag("Enemy"))
        {
            hero.ClearNearestEnemy(other.transform);
        }
    }
}