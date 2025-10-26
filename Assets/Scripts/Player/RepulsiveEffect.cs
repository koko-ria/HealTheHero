using UnityEngine;
using System.Collections.Generic; // For Physics2D.OverlapCircleAll

public class RepulsiveEffect : MonoBehaviour
{
    private float repulsiveForceRadius;
    private float repulsiveForceAmount;
    private float effectDuration;
    private string ownerTag; // To ignore the player who spawned it

    // Using Start for initialization to ensure collider is ready
    // You could also call Initialize after instantiation
    public void Initialize(float radius, float forceAmount, string tagOfOwner)
    {
        repulsiveForceRadius = radius;
        repulsiveForceAmount = forceAmount;
        effectDuration = 0.5f; // Duration of visual effect, not the push itself
        ownerTag = tagOfOwner;

        // Set collider radius (assuming a CircleCollider2D)
        CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
        if (circleCollider != null)
        {
            circleCollider.radius = repulsiveForceRadius;
            circleCollider.isTrigger = true; // Ensure it's a trigger
        }
        else
        {
            Debug.LogWarning("RepulsiveEffect requires a CircleCollider2D to set its radius.", this);
        }
        
        // Trigger the effect immediately after setup
        ApplyRepulsion();

        // Destroy the effect after a short duration (e.g., after particle animation)
        Destroy(gameObject, effectDuration);
    }

    private void ApplyRepulsion()
    {
        // Get all colliders within the repulsive radius
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, repulsiveForceRadius);

        foreach (Collider2D hitCollider in hitColliders)
        {
            // Do not repulse the owner (the Player)
            if (hitCollider.CompareTag(ownerTag))
            {
                continue;
            }

            // Repulse enemies or potentially other physics objects
            // No longer checking for "Player" tag for this, as abilities are for the Hero.
            if (hitCollider.CompareTag("Enemy"))
            {
                Rigidbody2D targetRb = hitCollider.GetComponent<Rigidbody2D>();
                if (targetRb != null)
                {
                    // Calculate direction from the center of the explosion to the target
                    Vector2 directionFromCenter = (hitCollider.transform.position - transform.position).normalized;
                    targetRb.AddForce(directionFromCenter * repulsiveForceAmount, ForceMode2D.Impulse);
                    Debug.Log($"Repulsed {hitCollider.name} from {gameObject.name}");
                }
            }
        }
    }
}