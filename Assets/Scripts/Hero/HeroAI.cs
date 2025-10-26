using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System; // For MathF

[RequireComponent(typeof(NavMeshAgent), typeof(Rigidbody2D))]
public class HeroAI : MonoBehaviour
{
    // ... (Keep all existing Header/SerializeField/public variables as they are) ...

    // Public (Inspector)
    [Header("Targeting")]
    // Changed nearestEnemy to a property for controlled access
    private Transform _nearestEnemy;
    public Transform GetCurrentTarget() => _nearestEnemy; // Public getter for EnemyBehavior/Detection

    [Header("NavMesh")]
    [SerializeField] private float navSampleDistance = 1f;

    [Header("Roaming")]
    [SerializeField] private float maxRoamDuration = 3f;
    [SerializeField] private float minRoamDistance = 3f;
    [SerializeField] private float maxRoamDistance = 9f;

    [Header("Rush Attack")]
    [SerializeField] private float rushDistanceBeyondTarget = 1.5f;
    [SerializeField] private float rushDuration = 0.2f;
    [SerializeField] private float rushCooldown = 1f;
    [SerializeField] private int rushDamage = 1;
    [SerializeField] private float rushDamageRadius = 0.6f; // New: Explicit radius for rush damage

    [Header("Whirl Attack")]
    [SerializeField] private float whirlRadius = 2.5f;
    [SerializeField] private float whirlSpeedDegPerSec = 360f;
    [SerializeField] private float whirlEntryTime = 0.4f;
    [SerializeField] private float whirlDuration = 2.5f;
    [SerializeField] private float whirlCooldown = 2f;
    [SerializeField] private int whirlDamagePerTick = 1;
    [SerializeField] private float whirlDamageTickInterval = 0.4f;
    [SerializeField] private float whirlDamageRadius = 0.8f; // New: Explicit radius for whirl damage

    [Header("Stats")]
    [SerializeField] private int baseHealth = 10;
    [SerializeField] private float baseMoveSpeed = 3.5f;
    [SerializeField] private float attackEngagementDistance = 1.5f;

    [Header("Audio & Animation Placeholders")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sfxRush;
    [SerializeField] private AudioClip sfxWhirl;
    [SerializeField] private AudioClip sfxBuffReceived;
    [SerializeField] private Animator animator; // optional

    // Private
    private NavMeshAgent agent;
    private Rigidbody2D rb;
    private Collider2D mainCollider;
    private HeroDetection heroDetection;

    private float roamTimer;
    private bool isRoaming;
    private Coroutine currentAttackRoutine;
    private float lastRushTime = -Mathf.Infinity;
    private float lastWhirlTime = -Mathf.Infinity;

    private int currentHealth;
    public static HeroAI Instance { get; private set; }

    // Buff state
    private float damageMultiplier = 1f;
    private float damageMultiplierExpireTime = 0f;

    private float defenseMultiplier = 1f;
    private float defenseExpireTime = 0f;

    private float resistanceMultiplier = 1f;
    private float resistanceExpireTime = 0f;

    private enum State { Idle, Attacking, Stunned, Dying }
    private State currentState = State.Idle;

    private enum AttackType { Rush, Whirl }
    private AttackType nextAttack = AttackType.Rush;

    private float nearestEnemyDistance = Mathf.Infinity;

    // Layer mask for damaging enemies (can be configured in Inspector)
    [SerializeField] private LayerMask enemyLayer; // New field for clarity

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody2D>();
        mainCollider = GetComponent<Collider2D>();
        heroDetection = GetComponentInChildren<HeroDetection>();

        if (agent == null) { Debug.LogError("HeroAI requires NavMeshAgent"); enabled = false; return; }
        if (rb == null) { Debug.LogError("HeroAI requires Rigidbody2D"); enabled = false; return; }
        if (mainCollider == null) { Debug.LogError("HeroAI requires a Collider2D"); enabled = false; return; }

        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = baseMoveSpeed;

        // Make sure enemyLayer is set up, fallback if not
        if (enemyLayer.value == 0)
        {
            enemyLayer = LayerMask.GetMask("Enemy");
            Debug.LogWarning("HeroAI: 'enemyLayer' not set in Inspector. Defaulting to layer 'Enemy'.");
        }
        
        // IMPORTANT: Rigidbody2D Body Type in Inspector should be Kinematic
        // This makes sure agent can control it and MovePosition works without physics fighting
        rb.isKinematic = true; // Ensure this is true initially for agent to control

        currentHealth = baseHealth;
        roamTimer = maxRoamDuration;
        Debug.Log("HeroAI Awake: Initialized.");
    }

    void Update()
    {
        if (currentState == State.Dying) return;

        UpdateBuffTimers();

        if (_nearestEnemy != null && currentAttackRoutine == null)
        {
            Vector3 lookDir = (_nearestEnemy.position - transform.position).normalized;
            if (lookDir.x > 0) transform.localScale = new Vector3(1, 1, 1);
            else if (lookDir.x < 0) transform.localScale = new Vector3(-1, 1, 1);
        }

        switch (currentState)
        {
            case State.Idle:
                HandleIdle();
                break;
            case State.Attacking:
                HandleAttacking();
                break;
            case State.Stunned:
                // ...
                break;
        }
    }

    #region State Logic

    private void HandleIdle()
    {
        if (_nearestEnemy != null)
        {
            Debug.Log($"Hero: Detected {_nearestEnemy.name}, transitioning to Attacking state.");
            currentState = State.Attacking;
            EnableNavMeshAgent(true);
            return;
        }

        bool reachedDestination = !agent.pathPending && agent.remainingDistance < 0.1f && (!agent.hasPath || agent.velocity.sqrMagnitude == 0f);

        if (isRoaming && reachedDestination)
        {
            isRoaming = false;
        }

        if (isRoaming)
        {
            roamTimer -= Time.deltaTime;
            if (roamTimer <= 0f)
            {
                isRoaming = false;
            }
        }
        else
        {
            StartRoamToRandomPoint();
        }
    }

    private void HandleAttacking()
    {
        // If target is lost, go back to idle.
        if (_nearestEnemy == null)
        {
            Debug.Log("Hero: Lost target, transitioning to Idle state.");
            currentState = State.Idle;
            StopCurrentAttack();
            EnableNavMeshAgent(false); // Stop agent movement
            return;
        }

        // If an attack is currently running, let it finish.
        if (currentAttackRoutine != null)
        {
            // Debug.Log("Hero: Attack routine in progress, waiting...");
            return;
        }

        float distToEnemy = Vector2.Distance(transform.position, _nearestEnemy.position);

        // Movement Phase: Move towards enemy if too far
        if (distToEnemy > attackEngagementDistance)
        {
            EnableNavMeshAgent(true); // Ensure agent is enabled for movement
            agent.SetDestination(_nearestEnemy.position);
            // Debug.Log($"Hero: Moving towards {_nearestEnemy.name}. Distance: {distToEnemy:F2}");
            return; // Don't try to attack while moving to engage
        }

        // Attack Phase: Stop movement and attempt an attack
        EnableNavMeshAgent(false); // Stop agent movement to perform attack

        // Try to perform the next attack in sequence
        if (nextAttack == AttackType.Rush)
        {
            if (Time.time >= lastRushTime + rushCooldown)
            {
                Debug.Log($"Hero: Initiating Rush attack on {_nearestEnemy.name}.");
                currentAttackRoutine = StartCoroutine(RushRoutine(_nearestEnemy));
                lastRushTime = Time.time;
                nextAttack = AttackType.Whirl; // Cycle to the next attack type
            }
            else
            {
                // Debug.Log($"Hero: Rush on cooldown. Next Rush in {lastRushTime + rushCooldown - Time.time:F2}s");
            }
        }
        else if (nextAttack == AttackType.Whirl)
        {
            if (Time.time >= lastWhirlTime + whirlCooldown)
            {
                Debug.Log($"Hero: Initiating Whirl attack on {_nearestEnemy.name}.");
                currentAttackRoutine = StartCoroutine(WhirlRoutine(_nearestEnemy));
                lastWhirlTime = Time.time;
                nextAttack = AttackType.Rush; // Cycle to the next attack type
            }
            else
            {
                // Debug.Log($"Hero: Whirl on cooldown. Next Whirl in {lastWhirlTime + whirlCooldown - Time.time:F2}s");
            }
        }
    }

    #endregion

    #region NavMeshAgent / Rigidbody Control

    private void EnableNavMeshAgent(bool enable)
    {
        if (agent.enabled != enable)
        {
            agent.enabled = enable;
            // When agent is enabled, rb MUST be kinematic for it to work correctly.
            // When agent is disabled, rb should still be kinematic if we plan to use MovePosition (like in attacks).
            // We'll manage rb.isKinematic explicitly within attack coroutines.
            rb.isKinematic = enable;

            if (!enable)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            else
            {
                agent.isStopped = false;
            }
            // Debug.Log($"Hero: NavMeshAgent enabled: {enable}, Rigidbody Kinematic: {rb.isKinematic}");
        }
    }

    #endregion

    #region Roaming

    private void StartRoamToRandomPoint()
    {
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning("Hero: Not on NavMesh, cannot roam.");
            EnableNavMeshAgent(false);
            return;
        }

        Vector2 randDir = UnityEngine.Random.insideUnitCircle.normalized;
        float dist = UnityEngine.Random.Range(minRoamDistance, maxRoamDistance);
        Vector3 candidate = transform.position + (Vector3)(randDir * dist);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(candidate, out hit, navSampleDistance * 5f, NavMesh.AllAreas))
        {
            isRoaming = true;
            roamTimer = UnityEngine.Random.Range(1f, maxRoamDuration);
            EnableNavMeshAgent(true);
            agent.SetDestination(hit.position);
            Debug.Log($"Hero: Roaming to {hit.position}.");
        }
        else
        {
            isRoaming = false;
            EnableNavMeshAgent(false);
            Debug.LogWarning("Hero: Could not find a valid roam point on NavMesh.");
        }
    }

    #endregion

    #region Attacks (Coroutines)

    private IEnumerator RushRoutine(Transform target)
    {
        Debug.Log("Hero: RushRoutine started.");
        if (target == null || target.gameObject == null || !target.gameObject.activeInHierarchy) // Check for valid target
        {
            Debug.Log("Hero: Rush target invalid or destroyed, aborting.");
            currentAttackRoutine = null;
            EnableNavMeshAgent(true);
            yield break;
        }

        animator?.SetTrigger("Rush");
        if (audioSource != null && sfxRush != null) audioSource.PlayOneShot(sfxRush);

        // Ensure agent is off. rb.isKinematic will be handled here.
        EnableNavMeshAgent(false);
        rb.isKinematic = true; // Explicitly set kinematic for MovePosition

        Vector3 startPos = transform.position;
        Vector3 dirToTarget = (target.position - transform.position).normalized;
        Vector3 endPoint = target.position + dirToTarget * rushDistanceBeyondTarget;

        NavMeshHit navHit;
        // Sample near the target's position, not far beyond, for a safer dash endpoint
        if (NavMesh.SamplePosition(endPoint, out navHit, navSampleDistance * 2f, NavMesh.AllAreas))
        {
            endPoint = navHit.position;
        } else {
             // If the calculated endpoint is off-mesh, try to dash only to the target's position
             if (NavMesh.SamplePosition(target.position, out navHit, navSampleDistance * 2f, NavMesh.AllAreas))
             {
                 endPoint = navHit.position;
             } else {
                // If even the target's position isn't on NavMesh, abort or simplify
                Debug.LogWarning("Hero: Rush target and endPoint not on NavMesh, dashing directly towards target.");
                // As a last resort, just go straight towards target position
                // This might put Hero off NavMesh but keeps the attack going
                endPoint = target.position;
             }
        }


        float elapsed = 0f;
        while (elapsed < rushDuration)
        {
            if (target == null || target.gameObject == null || !target.gameObject.activeInHierarchy) // Mid-attack target check
            {
                 Debug.Log("Hero: Rush target lost mid-attack, aborting.");
                 break; // Exit the loop to proceed to cleanup
            }

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / rushDuration);
            rb.MovePosition(Vector3.Lerp(startPos, endPoint, t));
            yield return null;
        }

        // Ensure final position is set if loop completed
        rb.MovePosition(endPoint);

        // Damage enemies in a circle at the Hero's final position
        DealDamageInArea(transform.position, rushDamageRadius, Mathf.CeilToInt(rushDamage * damageMultiplier), "Rush");

        // --- Cleanup ---
        currentAttackRoutine = null;
        // Restore agent control, which also sets rb.isKinematic = true
        EnableNavMeshAgent(true);
        Debug.Log("Hero: RushRoutine finished.");
    }

    private IEnumerator WhirlRoutine(Transform target)
    {
        Debug.Log("Hero: WhirlRoutine started.");
        if (target == null || target.gameObject == null || !target.gameObject.activeInHierarchy) // Check for valid target
        {
            Debug.Log("Hero: Whirl target invalid or destroyed, aborting.");
            currentAttackRoutine = null;
            EnableNavMeshAgent(true);
            yield break;
        }

        animator?.SetTrigger("Whirl");
        if (audioSource != null && sfxWhirl != null) audioSource.PlayOneShot(sfxWhirl);

        // Ensure agent is off. rb.isKinematic will be handled here.
        EnableNavMeshAgent(false);
        rb.isKinematic = true; // Explicitly set kinematic for MovePosition

        float elapsed = 0f;
        float angle = 0f;
        // Store the target's *current* position at the start of the whirl
        // This is important if the target moves significantly, the hero will still orbit that initial point
        Vector3 whirlCenter = target.position;

        // Entry ramp up (radius 0 -> full)
        while (elapsed < whirlEntryTime)
        {
            if (target == null || target.gameObject == null || !target.gameObject.activeInHierarchy) // Mid-attack target check
            {
                 Debug.Log("Hero: Whirl target lost mid-attack, aborting entry.");
                 break;
            }
            elapsed += Time.deltaTime;
            float t = elapsed / whirlEntryTime;
            float currentRadius = Mathf.SmoothStep(0f, whirlRadius, t);
            angle += whirlSpeedDegPerSec * Time.deltaTime;

            Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f) * currentRadius;
            rb.MovePosition(whirlCenter + offset); // Orbit around the initial target position
            yield return null;
        }

        // Full whirl phase
        elapsed = 0f;
        float damageTickTimer = 0f;
        while (elapsed < whirlDuration)
        {
            if (target == null || target.gameObject == null || !target.gameObject.activeInHierarchy) // Mid-attack target check
            {
                 Debug.Log("Hero: Whirl target lost mid-attack, aborting whirl phase.");
                 break;
            }

            elapsed += Time.deltaTime;
            angle += whirlSpeedDegPerSec * Time.deltaTime;

            Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f) * whirlRadius;
            rb.MovePosition(whirlCenter + offset); // Continue to orbit the initial target position

            damageTickTimer -= Time.deltaTime;
            if (damageTickTimer <= 0f)
            {
                DealDamageInArea(transform.position, whirlDamageRadius, Mathf.CeilToInt(whirlDamagePerTick * damageMultiplier), "Whirl Tick");
                damageTickTimer = whirlDamageTickInterval;
            }

            yield return null;
        }

        // --- Cleanup ---
        currentAttackRoutine = null;
        EnableNavMeshAgent(true); // Restore agent control
        Debug.Log("Hero: WhirlRoutine finished.");
    }

    private void DealDamageInArea(Vector3 center, float radius, int damage, string attackName)
    {
        // Debug.DrawRay(center, Vector3.up * radius, Color.red, 1f); // Visual for debugging
        Collider2D[] cols = Physics2D.OverlapCircleAll(center, radius, enemyLayer); // Use the new enemyLayer
        foreach (var c in cols)
        {
            // Ensure we don't hit ourselves or friendly units if we expand enemyLayer later
            if (c.CompareTag("Enemy"))
            {
                var enemy = c.GetComponent<EnemyBehavior>();
                if (enemy != null)
                {
                    enemy.TakeDamage(damage);
                    Debug.Log($"Hero: {attackName} hit {c.name} for {damage} damage.");
                }
            }
        }
    }

    private void StopCurrentAttack()
    {
        if (currentAttackRoutine != null)
        {
            StopCoroutine(currentAttackRoutine);
            currentAttackRoutine = null;
            Debug.Log("Hero: Stopped current attack routine.");
        }
        EnableNavMeshAgent(true); // Ensure agent is re-enabled and rb.isKinematic is true
    }

    #endregion

    #region Damage & Buffs

    public void TakeDamage(int damage)
    {
        if (currentState == State.Dying) return; // Prevent damage while dying

        int finalDamage = Mathf.CeilToInt(damage * defenseMultiplier);
        currentHealth -= finalDamage;
        Debug.Log($"Hero took {finalDamage} damage. Current Health: {currentHealth}");

        animator?.SetTrigger("Hit");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (currentState == State.Dying) return; // Only die once
        currentState = State.Dying;
        Debug.Log("Hero Died!");

        // Stop all movement and detection
        StopCurrentAttack();
        EnableNavMeshAgent(false); // Ensure agent is off and rb is non-kinematic for death animation
        rb.isKinematic = false; // Allow physics for death reaction if desired

        if (heroDetection != null)
        {
            heroDetection.enabled = false; // Disable detection immediately
            // Optionally, make its collider a trigger or disable it
            if (heroDetection.GetComponent<CircleCollider2D>() != null)
            {
                heroDetection.GetComponent<CircleCollider2D>().enabled = false;
            }
        }

        // Ensure main collider becomes a trigger so it doesn't block other things after death
        if (mainCollider != null)
        {
            mainCollider.isTrigger = true;
            mainCollider.enabled = false; // Disable collision
        }


        animator?.SetTrigger("Die");
        // Destroy the GameObject after a short delay to allow death animation to play
        Destroy(gameObject, 1.5f); // Adjust delay as needed for your animation

        // Optional: Notify a Game Manager that the Hero has died
        // GameManager.Instance.HeroDied();
    }

    public void ApplySupportEffect(SupportShooter.AmmoType ammo, float effectDuration, int potency = 1, float repulseRadius = 2f, float repulseForce = 5f)
    {
        if (audioSource != null && sfxBuffReceived != null) audioSource.PlayOneShot(sfxBuffReceived);

        switch (ammo)
        {
            case SupportShooter.AmmoType.Heal:
                currentHealth = Mathf.Min(currentHealth + potency, baseHealth);
                Debug.Log($"Hero healed for {potency}. Current Health: {currentHealth}");
                break;
            case SupportShooter.AmmoType.BuffDamage:
                damageMultiplier = 1f + potency;
                damageMultiplierExpireTime = Time.time + effectDuration;
                Debug.Log($"Hero Damage Buff: {damageMultiplier}x for {effectDuration}s");
                break;
            case SupportShooter.AmmoType.BuffDefence:
                defenseMultiplier = 1f - (0.2f * potency);
                defenseMultiplier = Mathf.Max(0f, defenseMultiplier);
                defenseExpireTime = Time.time + effectDuration;
                Debug.Log($"Hero Defense Buff: {defenseMultiplier}x incoming damage for {effectDuration}s");
                break;
            case SupportShooter.AmmoType.BuffResistance:
                resistanceMultiplier = 1f - (0.2f * potency);
                resistanceMultiplier = Mathf.Max(0f, resistanceMultiplier);
                resistanceExpireTime = Time.time + effectDuration;
                Debug.Log($"Hero Resistance Buff: {resistanceMultiplier}x incoming elemental damage for {effectDuration}s");
                break;
            case SupportShooter.AmmoType.Repulse:
                Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, repulseRadius);
                foreach (var c in cols)
                {
                    if (c.CompareTag("Enemy"))
                    {
                        Rigidbody2D enemyRb = c.attachedRigidbody; // Get Rigidbody2D from enemy
                        if (enemyRb != null)
                        {
                            Vector2 dir = (c.transform.position - transform.position).normalized;
                            enemyRb.AddForce(dir * repulseForce, ForceMode2D.Impulse);
                            Debug.Log($"Hero repulsed {c.name}");
                        }
                    }
                }
                Debug.Log($"Hero used Repulse: Radius {repulseRadius}, Force {repulseForce}");
                break;
        }
    }

    private void UpdateBuffTimers()
    {
        if (damageMultiplierExpireTime > 0f && Time.time > damageMultiplierExpireTime)
        {
            damageMultiplier = 1f;
            damageMultiplierExpireTime = 0f;
            Debug.Log("Hero Damage Buff expired.");
        }

        if (defenseExpireTime > 0f && Time.time > defenseExpireTime)
        {
            defenseMultiplier = 1f;
            defenseExpireTime = 0f;
            Debug.Log("Hero Defense Buff expired.");
        }

        if (resistanceExpireTime > 0f && Time.time > resistanceExpireTime)
        {
            resistanceMultiplier = 1f;
            resistanceExpireTime = 0f;
            Debug.Log("Hero Resistance Buff expired.");
        }
    }

    #endregion

    #region Detection Helpers
    // This method now actually sets the target if it's new or closer
    public void UpdateNearestEnemyCandidate(Transform enemy, float distance)
    {
        if (currentState == State.Dying) return; // No new targets if dying

        if (enemy == null)
        {
            ClearNearestEnemy(_nearestEnemy); // Clear if null passed
            return;
        }

        // Only update if it's a new target, or if the current target is null, or if the new enemy is closer
        if (_nearestEnemy == null || enemy != _nearestEnemy || distance < nearestEnemyDistance)
        {
            _nearestEnemy = enemy;
            nearestEnemyDistance = distance;
            // Transition to attacking if we find a target
            if (currentState != State.Attacking)
            {
                currentState = State.Attacking;
                EnableNavMeshAgent(true); // Ensure agent is enabled and ready to move
                Debug.Log($"Hero: New nearest enemy candidate - {enemy.name} at {distance:F2}. Transitioning to Attacking.");
            }
            else
            {
                // If already attacking, just update path to new (potentially closer) target
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.SetDestination(_nearestEnemy.position);
                }
                Debug.Log($"Hero: Updated target to {enemy.name} at {distance:F2}.");
            }
        }
    }

    public void ClearNearestEnemy(Transform t)
    {
        // Only clear if the target being removed IS the current nearest enemy
        if (_nearestEnemy == t && currentState != State.Dying)
        {
            _nearestEnemy = null;
            nearestEnemyDistance = Mathf.Infinity; // Use MathF for float
            // Transition to Idle when target is lost
            currentState = State.Idle;
            StopCurrentAttack(); // Stop any attack that might be aimed at the lost target
            EnableNavMeshAgent(false); // Stop agent movement
            Debug.Log($"Hero: Current nearest enemy {t?.name ?? "null"} left detection range. Transitioning to Idle.");
        }
        else if (t == null && _nearestEnemy != null)
        {
            // If we're told to clear a null target, but we still have one, don't clear.
            // This case handles when the RecheckNearestEnemy passes null but Hero still has a target.
            // Debug.Log("Hero: ClearNearestEnemy received null, but still has a target. Ignoring.");
        }
    }

    #endregion
// Adjust OnDrawGizmosSelected to use _nearestEnemy instead of nearestEnemy
    void OnDrawGizmosSelected()
    {
        // ... (previous gizmo code) ...

        if (_nearestEnemy != null) // Changed from nearestEnemy
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, _nearestEnemy.position);
            Gizmos.DrawWireSphere(_nearestEnemy.position, 0.4f); // Mark target with a sphere
            
            // Draw NavMeshAgent path if available
            if (agent != null && agent.enabled && agent.hasPath)
            {
                Gizmos.color = Color.blue;
                Vector3[] pathCorners = agent.path.corners;
                for (int i = 0; i < pathCorners.Length - 1; i++)
                {
                    Gizmos.DrawLine(pathCorners[i], pathCorners[i+1]);
                    Gizmos.DrawWireSphere(pathCorners[i], 0.1f);
                }
                if (pathCorners.Length > 0)
                {
                    Gizmos.DrawWireSphere(pathCorners[pathCorners.Length - 1], 0.1f);
                }
            }
        }

        // Draw Rush damage radius
        if (currentState == State.Attacking && currentAttackRoutine != null && nextAttack == AttackType.Whirl) // If Rush was just performed
        {
             Gizmos.color = Color.yellow;
             Gizmos.DrawWireSphere(transform.position, rushDamageRadius);
        }
        // Draw Whirl damage radius
        if (currentState == State.Attacking && currentAttackRoutine != null && nextAttack == AttackType.Rush) // If Whirl is active/just performed
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, whirlDamageRadius);
        }
    }
}   