using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class EnemyDetection : MonoBehaviour
{
    private EnemyBehavior enemy;

    void Awake()
    {
        enemy = GetComponentInParent<EnemyBehavior>();
        if (enemy == null)
        {
            Debug.LogError("EnemyDetection: No EnemyBehavior found on parent or self!", this);
            enabled = false;
            return;
        }

        var col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 8f; // Example: Set a default detection radius or make it serialized

        // Ensure this GameObject's layer is set to "Detection" in the Inspector
        // so that the SupportShooter can ignore it.
        // gameObject.layer = LayerMask.NameToLayer("Detection"); // Programmatic way if not set manually
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (enemy == null) return;

        // Priority 1: Hero
        if (other.CompareTag("Hero"))
        {
            // If the current target is NOT the Hero, or if there's no current target, set Hero as target
            if (enemy.GetCurrentTarget() == null || !enemy.GetCurrentTarget().CompareTag("Hero"))
            {
                enemy.SetTarget(other.transform);
            }
        }
        // Priority 2: Player (only if no Hero is currently targeted)
        else if (other.CompareTag("Player"))
        {
            if (enemy.GetCurrentTarget() == null) // If no target at all
            {
                enemy.SetTarget(other.transform);
            }
            else if (enemy.GetCurrentTarget().CompareTag("Player")) // If already targeting Player, confirm
            {
                // This branch helps ensure the Player remains the target if a Hero isn't present
                // and the Player is still in range.
                enemy.SetTarget(other.transform);
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (enemy == null) return;

        // If the exiting collider was our current target, clear it.
        // The EnemyBehavior will then try to find a new target (Hero first, then Player).
        if (enemy.GetCurrentTarget() == other.transform)
        {
            // Do not immediately set to null here, instead, let EnemyBehavior re-evaluate.
            // This allows the enemy to potentially find another target (e.g., if Hero leaves, find Player).
            enemy.SetTarget(null);
            // In EnemyBehavior's Update, if currentTarget is null, it will re-evaluate and find a new one.
        }
        // Specific handling if Hero exits while Player was also in range
        else if (other.CompareTag("Hero") && enemy.GetCurrentTarget() != null && enemy.GetCurrentTarget().CompareTag("Player"))
        {
            // If Hero leaves, but we were targeting Player, no change needed.
            // If Hero leaves and we were targeting Hero, the above 'enemy.SetTarget(null)' will handle it
            // and EnemyBehavior will try to find Player in its Update.
        }
    }
}