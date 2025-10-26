using System.Collections;
using System.Collections.Generic; // Added for List
using UnityEngine;
using UnityEngine.AI;
using UtilOfAi;

public class HeroAI : MonoBehaviour
{

    [Header("Components")]
    [SerializeField] private Animator animator; // Assign this in the Inspector!

    [Header("Targeting")]
    [SerializeField] private Transform enemy; // This will now be the *current* target enemy
    [SerializeField] public float detectionRange = 10f; // Range for the detection collider

    // Private list to keep track of enemies currently in detection range
    private List<Transform> enemiesInRange = new List<Transform>();
    private Transform nearestEnemyCandidate = null; // Used by detection script
    private float nearestEnemyDistance = Mathf.Infinity; // Used by detection script


    [Header("Idle Roaming Settings")]
    [SerializeField] private float maxDurationOfRoaming = 3f;
    [SerializeField] private float maxDistanceToRoam = 9f;
    [SerializeField] private float minDistanceToRoam = 3f;

    [Header("Attack Settings - Rush")]
    [SerializeField] private float rushOverShootingDist = 5f;
    [SerializeField] private float rushDuration = 0.2f;
    [SerializeField] private float rushCooldown = 1.0f;
    [SerializeField] private int rushDamage = 1;
    [SerializeField] private float rushHitRadius = 1f;

    [Header("Attack Settings - Whirl")]
    [SerializeField] private float whirlRadius = 3f;
    [SerializeField] private float whirlSpeed = 360f;
    [SerializeField] private float whirlDuration = 3f;
    [SerializeField] private float whirlEntryDuration = 0.5f;
    [SerializeField] private float whirlCooldown = 2.0f;
    [SerializeField] private int whirlDamage = 1;
    [SerializeField] private float whirlDamageTickRate = 0.5f;

    [Header("Attack Settings - General")]
    [SerializeField] private float heroAttackRange = 2f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Initial State")]
    [SerializeField] private State startingState = State.Idle;
    [SerializeField] private Attacks initialAttackType = Attacks.Rush;

    // --- Private Members ---
    private NavMeshAgent navMeshAgent;
    private float currentRoamingDuration;
    private bool isCurrentlyRoaming = false;
    private bool isRushing = false;
    private bool isWhirling = false;
    private float lastRushTime = -Mathf.Infinity;
    private float lastWhirlTime = -Mathf.Infinity;
    private Attacks currentAttackType;
    public float health = 10;

    public static HeroAI Instance { get; private set; }

    // --- Enums ---
    public enum State
    {
        Idle,
        Attacking,
        NextArea
    }

    public enum Attacks
    {
        Rush,
        Whirl,
        Bomb
    }

    // --- Unity Lifecycle Methods ---
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (navMeshAgent == null)
        {
            Debug.LogError("NavMeshAgent component missing!", this);
            enabled = false;
            return;
        }
        if (animator == null)
        {
            Debug.LogWarning("Animator component missing on HeroAI! Animations won't play. Ensure it's on this GameObject or assigned in Inspector.", this);
        }

        navMeshAgent.updateRotation = false;
        navMeshAgent.updateUpAxis = false;
    }

    void Start()
    {
        currentRoamingDuration = maxDurationOfRoaming;
        currentAttackType = initialAttackType;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
        {
            Debug.LogError("Hero is not placed on a NavMesh! Please bake NavMesh in Window > AI > Navigation.", this);
            enabled = false;
            return;
        }

        // Initialize detection range for the detection collider if it's on a child object.
        // The HeroDetection script will handle setting its collider's radius.
    }

    void FixedUpdate()
    {
        // Update the 'enemy' target based on `enemiesInRange`
        UpdateCurrentTargetEnemy();

        switch (startingState)
        {
            case State.Idle:
                HandleIdleState();
                break;
            case State.Attacking:
                HandleAttackingState();
                break;
            case State.NextArea:
                // Future implementation
                break;
            default:
                Debug.LogWarning("Unknown state encountered: " + startingState);
                break;
        }
    }

    // --- New Methods for HeroDetection Integration ---

    /// <summary>
    /// Called by HeroDetection to provide candidates for the nearest enemy.
    /// </summary>
    public void UpdateNearestEnemyCandidate(Transform potentialEnemy, float distance)
    {
        // Add to list if not already present
        if (!enemiesInRange.Contains(potentialEnemy))
        {
            enemiesInRange.Add(potentialEnemy);
        }

        // Update nearest candidate if this one is closer
        if (distance < nearestEnemyDistance)
        {
            nearestEnemyCandidate = potentialEnemy;
            nearestEnemyDistance = distance;
        }
    }

    /// <summary>
    /// Called by HeroDetection when an enemy leaves the detection range.
    /// </summary>
    public void ClearNearestEnemy(Transform departingEnemy)
    {
        enemiesInRange.Remove(departingEnemy);
        // If the departing enemy was our current target or nearest candidate,
        // we need to re-evaluate.
        if (enemy == departingEnemy || nearestEnemyCandidate == departingEnemy)
        {
            nearestEnemyCandidate = null;
            nearestEnemyDistance = Mathf.Infinity;
            // Force an update to find a new target if available
            UpdateCurrentTargetEnemy();
        }
    }

    /// <summary>
    /// Decides the primary 'enemy' target from the list of detected enemies.
    /// Called periodically (e.g., in FixedUpdate).
    /// </summary>
    private void UpdateCurrentTargetEnemy()
    {
        // Clean up any null references (destroyed enemies) from the list
        enemiesInRange.RemoveAll(t => t == null);

        if (enemiesInRange.Count > 0)
        {
            // Find the actual nearest enemy from the list
            Transform currentNearest = null;
            float currentMinDistance = Mathf.Infinity;

            foreach (Transform potentialEnemy in enemiesInRange)
            {
                if (potentialEnemy == null) continue; // Should already be handled by RemoveAll, but good for safety
                float dist = Vector3.Distance(transform.position, potentialEnemy.position);
                if (dist < currentMinDistance)
                {
                    currentMinDistance = dist;
                    currentNearest = potentialEnemy;
                }
            }

            enemy = currentNearest; // Set our main 'enemy' target
            if (startingState != State.Attacking)
            {
                startingState = State.Attacking; // Transition to attacking if an enemy is found
                Debug.Log("Enemy detected via detection script! Transitioning to Attacking state.");
                lastRushTime = Time.time - rushCooldown;
                lastWhirlTime = Time.time - whirlCooldown;
            }
        }
        else // No enemies in range
        {
            enemy = null;
            if (startingState == State.Attacking)
            {
                startingState = State.Idle; // Transition to idle if no enemies are left
                Debug.Log("No enemies in range, transitioning to Idle state.");
                StopAllAttackCoroutines();
            }
        }
    }

    // --- State Handlers ---
    private void HandleIdleState()
    {
        // If an enemy is detected, UpdateCurrentTargetEnemy will transition state
        if (enemy != null) return; // Don't roam if an enemy has been assigned

        if (isCurrentlyRoaming)
        {
            currentRoamingDuration -= Time.fixedDeltaTime;
            if (currentRoamingDuration <= 0)
            {
                currentRoamingDuration = maxDurationOfRoaming;
                isCurrentlyRoaming = false;
                if (navMeshAgent.enabled) navMeshAgent.isStopped = true; // Stop agent after roaming duration
            }
        }
        else
        {
            StartIdleRoaming();
        }
    }

    private void HandleAttackingState()
    {
        // If enemy becomes null during FixedUpdate (e.g., destroyed, or left range), UpdateCurrentTargetEnemy will transition to Idle.
        if (enemy == null) return;

        // Only attempt attacks if we are not currently executing an attack (Rush or Whirl)
        if (!isRushing && !isWhirling)
        {
            float distanceToEnemy = Vector3.Distance(transform.position, enemy.position);

            if (distanceToEnemy > heroAttackRange)
            {
                // Move towards the enemy if outside attack range
                if (navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
                {
                    navMeshAgent.isStopped = false;
                    navMeshAgent.SetDestination(enemy.position);
                }
            }
            else // Within attack range, stop and attempt attack
            {
                if (navMeshAgent.enabled) navMeshAgent.isStopped = true;
                AttemptAttack(currentAttackType);
            }
        }
        else
        {
            // If an attack is in progress, the coroutine is handling movement,
            // and NavMeshAgent is disabled. Do nothing here.
        }
    }

    // --- Attack Logic (same as before) ---
    private void AttemptAttack(Attacks attackToAttempt)
    {
        switch (attackToAttempt)
        {
            case Attacks.Rush:
                if (Time.time >= lastRushTime + rushCooldown)
                {
                    StartRush();
                    currentAttackType = Attacks.Whirl;
                }
                break;
            case Attacks.Whirl:
                if (Time.time >= lastWhirlTime + whirlCooldown)
                {
                    StartWhirl();
                    currentAttackType = Attacks.Rush;
                }
                break;
            case Attacks.Bomb:
                // Future implementation
                break;
            default:
                break;
        }
    }

    private void StartRush()
    {
        if (isRushing || enemy == null) return;

        Vector2 dir = (enemy.position - transform.position).normalized;
        Vector3 targetPos = enemy.position + (Vector3)(dir * rushOverShootingDist);

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 1f, NavMesh.AllAreas))
        {
            Debug.Log("Hero: Launching Rush Attack!");
            StartCoroutine(RushingAttackCoroutine(hit.position));
            lastRushTime = Time.time;
        }
        else
        {
            Debug.LogWarning("Rush target position not on NavMesh! Trying original enemy position instead.");
            if (NavMesh.SamplePosition(enemy.position, out hit, 1f, NavMesh.AllAreas))
            {
                StartCoroutine(RushingAttackCoroutine(hit.position));
                lastRushTime = Time.time;
            }
            else
            {
                Debug.LogWarning("Enemy position also not on NavMesh, cannot rush.");
            }
        }
    }

    private void StartWhirl()
    {
        if (isWhirling || enemy == null) return;

        Debug.Log("Hero: Launching Whirl Attack!");
        StartCoroutine(WhirlingAttackCoroutine());
        lastWhirlTime = Time.time;
    }

    // --- Coroutines for Attacks (same as before) ---
    private IEnumerator RushingAttackCoroutine(Vector3 target)
    {
        isRushing = true;
        if (navMeshAgent.enabled) navMeshAgent.isStopped = true;
        navMeshAgent.enabled = false;

        Vector3 startPos = transform.position;
        float elapsed = 0f;

        if (animator != null) animator.SetTrigger("Rush");

        while (elapsed < rushDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / rushDuration;
            t = t * t * (3f - 2f * t);
            transform.position = Vector3.Lerp(startPos, target, t);
            yield return null;
        }

        transform.position = target;

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, rushHitRadius, enemyLayer);
        foreach (Collider2D hit in hitEnemies)
        {
            EnemyBehavior enemyComponent = hit.GetComponent<EnemyBehavior>();
            if (enemyComponent != null && hit.transform != transform) // Ensure hero doesn't damage itself
            {
                enemyComponent.TakeDamage(rushDamage);
                Debug.Log($"Hero dealt {rushDamage} Rush damage to {hit.name}");
            }
        }

        navMeshAgent.enabled = true;
        isRushing = false;
    }

    private IEnumerator WhirlingAttackCoroutine()
    {
        if (enemy == null)
        {
            Debug.LogWarning("Cannot start whirl - no enemy in range!");
            yield break;
        }

        isWhirling = true;
        if (navMeshAgent.enabled) navMeshAgent.isStopped = true;
        navMeshAgent.enabled = false;

        if (animator != null) animator.SetTrigger("Whirl");

        Vector3 whirlCenterAtStart = enemy.position;
        float elapsed = 0f;
        float currentAngle = 0f;

        while (elapsed < whirlEntryDuration)
        {
            Vector3 currentWhirlCenter = (enemy != null) ? enemy.position : whirlCenterAtStart;
            elapsed += Time.deltaTime;
            float t = elapsed / whirlEntryDuration;
            float easedT = t * t * (3f - 2f * t);

            float currentRadius = Mathf.Lerp(0f, whirlRadius, easedT);
            currentAngle += whirlSpeed * Time.deltaTime;

            Vector3 offset = new Vector3(
                Mathf.Cos(currentAngle * Mathf.Deg2Rad) * currentRadius,
                Mathf.Sin(currentAngle * Mathf.Deg2Rad) * currentRadius,
                0
            );
            transform.position = currentWhirlCenter + offset;
            yield return null;
        }

        currentAngle = (currentAngle % 360);
        elapsed = 0f;

        float damageTickTimer = 0f;

        while (elapsed < whirlDuration)
        {
            Vector3 currentWhirlCenter = (enemy != null) ? enemy.position : whirlCenterAtStart;

            elapsed += Time.deltaTime;
            currentAngle += whirlSpeed * Time.deltaTime;

            Vector3 offset = new Vector3(
                Mathf.Cos(currentAngle * Mathf.Deg2Rad) * whirlRadius,
                Mathf.Sin(currentAngle * Mathf.Deg2Rad) * whirlRadius,
                0
            );
            transform.position = currentWhirlCenter + offset;

            damageTickTimer += Time.deltaTime;
            if (damageTickTimer >= whirlDamageTickRate)
            {
                Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, whirlRadius, enemyLayer);
                foreach (Collider2D hit in hitEnemies)
                {
                    EnemyBehavior enemyComponent = hit.GetComponent<EnemyBehavior>();
                    if (enemyComponent != null && hit.transform != transform) // Ensure hero doesn't damage itself
                    {
                        enemyComponent.TakeDamage(whirlDamage);
                        Debug.Log($"Hero dealt {whirlDamage} Whirl damage to {hit.name}");
                    }
                }
                damageTickTimer = 0f;
            }

            yield return null;
        }

        navMeshAgent.enabled = true;
        isWhirling = false;
    }

    // --- Idle Roaming Methods (same as before) ---
    private void StartIdleRoaming()
    {
        if (!navMeshAgent.isOnNavMesh)
        {
            Debug.LogWarning("NavMeshAgent is not on NavMesh, cannot roam!", this);
            isCurrentlyRoaming = false;
            return;
        }

        isCurrentlyRoaming = true;
        Vector3 randDir = UtilOfAi.Utility.Instance.RandomDirection();

        Vector3 targetPos = transform.position + randDir * UnityEngine.Random.Range(minDistanceToRoam, maxDistanceToRoam);

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(hit.position);
        }
        else
        {
            Debug.LogWarning("Could not find valid NavMesh position for roaming!", this);
            isCurrentlyRoaming = false;
        }
    }

    // --- Damage and Death (same as before) ---
    public void TakeDamage(int damage)
    {
        health -= damage;
        Debug.Log($"Hero took {damage} damage. Health: {health}");

        if (health <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log("Hero Died!");
        Destroy(gameObject);
    }

    // --- Helper Methods (same as before, with minor adjustment to StopCoroutine) ---
    private void StopAllAttackCoroutines()
    {
        StopCoroutine("RushingAttackCoroutine");
        StopCoroutine("WhirlingAttackCoroutine");
        isRushing = false;
        isWhirling = false;

        if (!navMeshAgent.enabled)
        {
            navMeshAgent.enabled = true;
        }
        navMeshAgent.isStopped = true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, heroAttackRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, rushHitRadius);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, whirlRadius);

        // Visualize detection range as well (controlled by HeroAI, used by HeroDetection)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}