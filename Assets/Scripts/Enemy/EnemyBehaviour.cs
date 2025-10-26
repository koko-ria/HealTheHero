using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBehavior : MonoBehaviour
{
    [Header("References")]
    public Transform player; // This will now typically be the Player (support)
    public AttackPattern[] attackPatterns;

    [Header("Movement")]
    public MovementType movementType;
    public float moveSpeed = 2f;
    public float detectionRange = 10f;
    public float attackRange = 6f;
    public float wanderRadius = 5f;
    public float orbitRadius = 4f;
    public float pathUpdateInterval = 0.5f; // How often to recalculate path

    [Header("Stats")]
    public int health = 3;

    private Vector2 startPos;
    private Vector2 wanderTarget;
    private Vector3 orbitTargetPos; // This variable is not used
    private float attackTimer;
    private float pathUpdateTimer;
    private int currentPatternIndex;
    private NavMeshAgent agent;
    private bool hasReachedWanderTarget; // This variable is not used

    private Transform currentTarget; // The actual target (Hero or Player)

    void Start()
    {
        // Setup NavMeshAgent
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = moveSpeed;

        startPos = transform.position;

        // Initial target setting: Prioritize Hero, then Player
        Transform hero = GameObject.FindGameObjectWithTag("Hero")?.transform;
        if (hero != null)
        {
            SetTarget(hero);
        }
        else if (player == null) // If player field is not set in inspector, try to find it
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player != null)
            {
                SetTarget(player);
            }
        }
        else // If player field is set in inspector and no hero, use that player
        {
            SetTarget(player);
        }


        if (movementType == MovementType.Wander)
            SetNewWanderTarget();

        // Initialize attack timer for first attack
        if (attackPatterns.Length > 0)
        {
            attackTimer = attackPatterns[0].cooldown;
        }
    }

    void Update()
    {
        if (currentTarget == null)
        {
            // If target disappeared, try to find Hero first, then Player
            Transform hero = GameObject.FindGameObjectWithTag("Hero")?.transform;
            if (hero != null)
            {
                SetTarget(hero);
            }
            else if (player != null)
            {
                SetTarget(player);
            }
            else
            {
                agent.isStopped = true;
                return; // No target to pursue or attack
            }
        }

        float distToTarget = Vector2.Distance(transform.position, currentTarget.position);

        // Update path periodically instead of every frame (performance optimization)
        pathUpdateTimer -= Time.deltaTime;
        if (pathUpdateTimer <= 0)
        {
            HandleMovement(distToTarget);
            pathUpdateTimer = pathUpdateInterval;
        }

        // Handle attacking
        if (distToTarget <= attackRange)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0 && attackPatterns.Length > 0)
            {
                Attack();
            }
        }
    }

    void HandleMovement(float distToTarget)
    {
        switch (movementType)
        {
            case MovementType.Chase:
                HandleChase(distToTarget);
                break;

            case MovementType.KeepDistance:
                HandleKeepDistance(distToTarget);
                break;

            case MovementType.Wander:
                HandleWander();
                break;

            case MovementType.Stationary:
                agent.isStopped = true;
                break;

            case MovementType.Orbit:
                HandleOrbit(distToTarget);
                break;
        }
    }

    void HandleChase(float distToTarget)
    {
        if (distToTarget <= detectionRange)
        {
            agent.isStopped = false;
            agent.SetDestination(currentTarget.position);
        }
        else
        {
            agent.isStopped = true;
        }
    }

    void HandleKeepDistance(float distToTarget)
    {
        if (distToTarget <= detectionRange)
        {
            agent.isStopped = false;

            // Too close - back away
            if (distToTarget < attackRange * 0.7f)
            {
                Vector3 directionAway = (transform.position - currentTarget.position).normalized;
                Vector3 retreatPos = transform.position + directionAway * 2f;

                // Make sure the retreat position is valid on NavMesh
                NavMeshHit hit;
                if (NavMesh.SamplePosition(retreatPos, out hit, 2f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
                else
                {
                    // If no valid retreat path, just stop
                    agent.isStopped = true;
                }
            }
            // Too far - move closer
            else if (distToTarget > attackRange)
            {
                agent.SetDestination(currentTarget.position);
            }
            // At good distance - stop
            else
            {
                agent.isStopped = true;
            }
        }
        else
        {
            agent.isStopped = true;
        }
    }

    void HandleWander()
    {
        agent.isStopped = false;

        // If no destination or reached current destination
        if (!agent.hasPath || agent.remainingDistance < 0.5f || agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            SetNewWanderTarget();
        }
    }

    void HandleOrbit(float distToTarget)
    {
        if (distToTarget <= detectionRange)
        {
            agent.isStopped = false;

            // Calculate orbit position (perpendicular to target direction)
            Vector2 toTarget = (Vector2)(currentTarget.position - transform.position);
            float currentAngle = Mathf.Atan2(toTarget.y, toTarget.x);

            // Move in circular path around target
            // Add or subtract 90 degrees (pi/2 radians) to get a perpendicular direction
            float orbitAngle = currentAngle + (90f * Mathf.Deg2Rad);

            Vector3 orbitOffset = new Vector3(
                Mathf.Cos(orbitAngle) * orbitRadius,
                Mathf.Sin(orbitAngle) * orbitRadius,
                0
            );

            Vector3 targetPos = currentTarget.position + orbitOffset;

            // Make sure position is valid on NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPos, out hit, orbitRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
            else
            {
                // If orbit position not valid, just move toward target
                agent.SetDestination(currentTarget.position);
            }
        }
        else
        {
            agent.isStopped = true;
        }
    }

    void SetNewWanderTarget()
    {
        // Generate random point within wander radius from the start position
        Vector2 randomDirection = Random.insideUnitCircle * wanderRadius;
        Vector3 targetPosition = new Vector3(startPos.x + randomDirection.x, startPos.y + randomDirection.y, 0);

        // Find nearest valid point on NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPosition, out hit, wanderRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            wanderTarget = hit.position;
        }
        else
        {
            // If cannot find a valid wander point, stop
            agent.isStopped = true;
        }
    }

    void Attack()
    {
        if (attackPatterns.Length == 0 || currentTarget == null) return;

        AttackPattern pattern = attackPatterns[currentPatternIndex];

        // Ensure we handle cases where patterns might have a projectile prefab that is null
        if (pattern.projectilePrefab == null)
        {
            Debug.LogWarning($"EnemyBehavior: Attack pattern {pattern.name} has no projectilePrefab assigned!", this);
            attackTimer = pattern.cooldown; // Still apply cooldown to prevent spamming
            currentPatternIndex = (currentPatternIndex + 1) % attackPatterns.Length;
            return;
        }

        switch (pattern.patternType)
        {
            case PatternType.Single:
                ShootSingle(pattern);
                break;
            case PatternType.Burst:
                StartCoroutine(ShootBurst(pattern));
                break;
            case PatternType.Circle:
                ShootCircle(pattern);
                break;
            case PatternType.Spiral:
                StartCoroutine(ShootSpiral(pattern));
                break;
            case PatternType.Predictive:
                ShootPredictive(pattern);
                break;
        }

        attackTimer = pattern.cooldown;
        currentPatternIndex = (currentPatternIndex + 1) % attackPatterns.Length;
    }

    void ShootSingle(AttackPattern pattern)
    {
        Vector2 dir = (currentTarget.position - transform.position).normalized;
        CreateProjectile(pattern, dir);
    }

    IEnumerator ShootBurst(AttackPattern pattern)
    {
        Vector2 baseDir = (currentTarget.position - transform.position).normalized;
        float startAngle = -pattern.spreadAngle / 2;

        for (int i = 0; i < pattern.projectileCount; i++)
        {
            // Ensure target is still valid during burst
            if (currentTarget == null) yield break;

            float angle = startAngle + (pattern.spreadAngle / Mathf.Max(1, pattern.projectileCount - 1)) * i; // Mathf.Max(1, ...) to prevent division by zero for single projectile burst
            Vector2 dir = Rotate(baseDir, angle);
            CreateProjectile(pattern, dir);

            if (i < pattern.projectileCount - 1)
                yield return new WaitForSeconds(pattern.burstDelay);
        }
    }

    void ShootCircle(AttackPattern pattern)
    {
        float angleStep = 360f / Mathf.Max(1, pattern.projectileCount); // Mathf.Max(1, ...) to prevent division by zero

        for (int i = 0; i < pattern.projectileCount; i++)
        {
            float angle = angleStep * i;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            CreateProjectile(pattern, dir);
        }
    }

    IEnumerator ShootSpiral(AttackPattern pattern)
    {
        float angleOffset = 0;

        for (int i = 0; i < pattern.projectileCount; i++)
        {
            if (currentTarget == null) yield break;

            float angleStep = 360f / 8; // Assuming 8 projectiles per spiral wave
            for (int j = 0; j < 8; j++)
            {
                float angle = angleStep * j + angleOffset;
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                CreateProjectile(pattern, dir);
            }

            angleOffset += 15f; // Adjust spiral tightness
            yield return new WaitForSeconds(pattern.burstDelay);
        }
    }

    void ShootPredictive(AttackPattern pattern)
    {
        // Only predict if target has a Rigidbody2D for velocity
        Rigidbody2D targetRb = currentTarget.GetComponent<Rigidbody2D>();
        if (targetRb != null && targetRb.linearVelocity.magnitude > 0.1f)
        {
            Vector2 toTarget = currentTarget.position - transform.position;
            float dist = toTarget.magnitude;
            float timeToHit = dist / pattern.projectileSpeed;
            Vector2 predictedPos = (Vector2)currentTarget.position + targetRb.linearVelocity * timeToHit; // Use Rigidbody2D velocity
            Vector2 dir = (predictedPos - (Vector2)transform.position).normalized;
            CreateProjectile(pattern, dir);
        }
        else
        {
            // Fallback to single shot if target has no Rigidbody2D or is stationary
            ShootSingle(pattern);
        }
    }

    void CreateProjectile(AttackPattern pattern, Vector2 direction)
    {
        GameObject proj = Instantiate(pattern.projectilePrefab, transform.position, Quaternion.identity);
        Projectile projectileComponent = proj.GetComponent<Projectile>();
        if (projectileComponent != null)
        {
            projectileComponent.damage = pattern.damage; // Set damage from pattern
        }

        Rigidbody2D projRb = proj.GetComponent<Rigidbody2D>();
        if (projRb != null)
        {
            projRb.linearVelocity = direction * pattern.projectileSpeed;
        }

        // Rotate projectile visual to match direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        proj.transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(rad);
        float cos = Mathf.Cos(rad);
        return new Vector2(cos * v.x - sin * v.y, sin * v.x + cos * v.y);
    }

    public void TakeDamage(int damage)
    {
        health -= damage;
        Debug.Log($"Enemy {name} took {damage} damage. Health: {health}");
        // TODO: Play hurt animation/sound

        if (health <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log($"Enemy {name} Died!");
        // TODO: Play death animation/sound, drop loot etc.
        Destroy(gameObject);
    }

    // New methods for target management
    public void SetTarget(Transform newTarget)
    {
        if (newTarget != null && currentTarget != newTarget)
        {
            currentTarget = newTarget;
            // Optionally, force a path recalculation when target changes
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(currentTarget.position);
            }
            Debug.Log($"{name} set target to {currentTarget.name}");
        }
    }

    public Transform GetCurrentTarget()
    {
        return currentTarget;
    }

    // Visualize detection/attack ranges in editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (movementType == MovementType.Wander)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(startPos, wanderRadius);
        }

        if (movementType == MovementType.Orbit)
        {
            Gizmos.color = Color.cyan;
            if (currentTarget != null) // Use currentTarget for orbit visualization
                Gizmos.DrawWireSphere(currentTarget.position, orbitRadius);
        }

        if (currentTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, currentTarget.position);
            Gizmos.DrawWireSphere(currentTarget.position, 0.3f);
        }
    }
}

public enum MovementType
{
    Stationary,
    Chase,
    KeepDistance,
    Wander,
    Orbit
}