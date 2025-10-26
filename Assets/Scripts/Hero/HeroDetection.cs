using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class HeroDetection : MonoBehaviour
{
    private CircleCollider2D detectionCollider; // Reference to the collider

    void Awake()
    {
        // Assumes HeroDetection is on a child object of the Hero GameObject
        if (HeroAI.Instance == null)
        {
            Debug.LogError("HeroDetection: Could not find HeroAI in parent.", this);
            enabled = false;
            return;
        }

        detectionCollider = GetComponent<CircleCollider2D>();
        if (detectionCollider == null)
        {
            Debug.LogError("HeroDetection: CircleCollider2D component missing!", this);
            enabled = false;
            return;
        }

        detectionCollider.isTrigger = true;
        // Set the collider's radius based on HeroAI's detectionRange
        detectionCollider.radius = HeroAI.Instance.detectionRange;
    }

    // You might want to update the collider radius if hero.detectionRange changes at runtime
    void Update()
    {
        if (HeroAI.Instance != null && detectionCollider.radius != HeroAI.Instance.detectionRange)
        {
            detectionCollider.radius = HeroAI.Instance.detectionRange;
        }
    }


    void OnTriggerStay2D(Collider2D other)
    {
        if (HeroAI.Instance == null) return;

        if (other.CompareTag("Enemy"))
        {
            float dist = Vector2.Distance(other.transform.position, HeroAI.Instance.transform.position);
            HeroAI.Instance.UpdateNearestEnemyCandidate(other.transform, dist);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (HeroAI.Instance == null) return;

        if (other.CompareTag("Enemy"))
        {
            HeroAI.Instance.ClearNearestEnemy(other.transform);
        }
    }
}