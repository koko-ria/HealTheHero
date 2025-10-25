using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UtilOfAi;

public class HeroAI : MonoBehaviour
{
    // --- Public Properties (Visible in Inspector) ---
    [Header("Targeting")]
    [SerializeField] private Transform enemy; // Will be set when enemy enters trigger

    [Header("Idle Roaming Settings")]
    [SerializeField] private float maxDurationOfRoaming = 3f; // Max duration for a single roaming phase
    [SerializeField] private float maxDistanceToRoam = 9f;
    [SerializeField] private float minDistanceToRoam = 3f;

    [Header("Attack Settings - Rush")]
    [SerializeField] private float rushOverShootingDist = 5f;
    [SerializeField] private float rushDuration = 0.2f; // Duration of the rush movement
    [SerializeField] private float rushCooldown = 1.0f; // Cooldown after a rush attack

    [Header("Attack Settings - Whirl")]
    [SerializeField] private float whirlRadius = 3f; // Max radius of the whirl
    [SerializeField] private float whirlSpeed = 360f; // Speed of rotation in degrees per second
    [SerializeField] private float whirlDuration = 3f; // How long to whirl at full radius
    [SerializeField] private float whirlEntryDuration = 0.5f; // Duration for gradually reaching full whirl radius
    [SerializeField] private float whirlCooldown = 2.0f; // Cooldown after a whirl attack

    [Header("Initial State")]
    [SerializeField] private State startingState = State.Idle;
    [SerializeField] private Attacks initialAttackType = Attacks.Rush; // Set initial attack for testing

    // --- Private Members ---
    private NavMeshAgent navMeshAgent;
    private float currentRoamingDuration;
    private bool isCurrentlyRoaming = false;
    private bool isRushing = false;
    private bool isWhirling = false;
    private float lastRushTime = -Mathf.Infinity; // To track cooldowns
    private float lastWhirlTime = -Mathf.Infinity; // To track cooldowns
    private Attacks currentAttackType; // To cycle between attacks

    public static HeroAI Instance { get; private set; } // Singleton pattern

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
        if (navMeshAgent == null)
        {
            Debug.LogError("NavMeshAgent component missing!", this);
            enabled = false;
            return;
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
    }

    void FixedUpdate()
    {
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

    // --- State Handlers ---
    private void HandleIdleState()
    {
        if (isCurrentlyRoaming)
        {
            currentRoamingDuration -= Time.fixedDeltaTime;
            if (currentRoamingDuration <= 0)
            {
                currentRoamingDuration = maxDurationOfRoaming;
                isCurrentlyRoaming = false;
            }
        }
        else
        {
            StartIdleRoaming();
        }
    }

    private void HandleAttackingState()
    {
        // Only attempt attacks if an enemy is present and we are not currently executing an attack
        if (enemy != null && !isRushing && !isWhirling)
        {
            AttemptAttack(currentAttackType);
        }
        else if (enemy == null)
        {
            // If enemy leaves while in Attacking state, transition back to Idle
            startingState = State.Idle;
            Debug.Log("Enemy left, transitioning to Idle.");
            StopAllAttackCoroutines(); // Ensure any ongoing attack coroutines are stopped
        }
    }

    // --- Attack Logic ---
    private void AttemptAttack(Attacks attackToAttempt)
    {
        switch (attackToAttempt)
        {
            case Attacks.Rush:
                if (Time.time >= lastRushTime + rushCooldown)
                {
                    StartRush();
                    currentAttackType = Attacks.Whirl; // Cycle to next attack
                }
                break;
            case Attacks.Whirl:
                if (Time.time >= lastWhirlTime + whirlCooldown)
                {
                    StartWhirl();
                    currentAttackType = Attacks.Rush; // Cycle to next attack
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
            Debug.Log("Launching Rush Attack!");
            StartCoroutine(RushingAttackCoroutine(hit.position));
            lastRushTime = Time.time;
        }
        else
        {
            Debug.LogWarning("Rush target position not on NavMesh!");
        }
    }

    private void StartWhirl()
    {
        if (isWhirling || enemy == null) return;

        Debug.Log("Launching Whirl Attack!");
        StartCoroutine(WhirlingAttackCoroutine());
        lastWhirlTime = Time.time;
    }

    // --- Coroutines for Attacks ---
    private IEnumerator RushingAttackCoroutine(Vector3 target)
    {
        isRushing = true;
        navMeshAgent.enabled = false;
        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < rushDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / rushDuration;
            t = t * t * (3f - 2f * t);
            transform.position = Vector3.Lerp(startPos, target, t);
            yield return null;
        }

        transform.position = target; // Ensure exact final position

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
        navMeshAgent.enabled = false;

        Vector3 whirlCenterAtStart = enemy.position;
        float elapsed = 0f;
        float currentAngle = 0f; // Keep track of the current angle for smooth transition

        // --- Gradual Entry Phase ---
        while (elapsed < whirlEntryDuration)
        {
            // If enemy disappears during entry, just complete the entry
            Vector3 currentWhirlCenter = enemy != null ? enemy.position : whirlCenterAtStart;
            
            elapsed += Time.deltaTime;
            float t = elapsed / whirlEntryDuration;
            float easedT = t * t * (3f - 2f * t); // Smoothstep for radius increase

            float currentRadius = Mathf.Lerp(0f, whirlRadius, easedT);
            currentAngle += whirlSpeed * Time.deltaTime; // Continue rotating during entry

            Vector3 offset = new Vector3(
                Mathf.Cos(currentAngle * Mathf.Deg2Rad) * currentRadius,
                Mathf.Sin(currentAngle * Mathf.Deg2Rad) * currentRadius,
                0
            );
            transform.position = currentWhirlCenter + offset;
            yield return null;
        }

        // Ensure we are at full radius and continue from current angle
        currentAngle = (currentAngle % 360); // Normalize angle to prevent huge numbers
        elapsed = 0f; // Reset elapsed time for the full whirl duration

        // --- Full Whirl Phase ---
        while (elapsed < whirlDuration)
        {
            if (enemy == null)
            {
                Debug.Log("Enemy left during whirl - completing current whirl around last position");
            }

            Vector3 currentWhirlCenter = enemy != null ? enemy.position : whirlCenterAtStart;

            elapsed += Time.deltaTime;
            currentAngle += whirlSpeed * Time.deltaTime; // Update angle for continuous rotation

            Vector3 offset = new Vector3(
                Mathf.Cos(currentAngle * Mathf.Deg2Rad) * whirlRadius,
                Mathf.Sin(currentAngle * Mathf.Deg2Rad) * whirlRadius,
                0
            );
            transform.position = currentWhirlCenter + offset;
            yield return null;
        }

        // Cleanup
        navMeshAgent.enabled = true;
        isWhirling = false;
    }

    // --- Idle Roaming Methods ---
    private void StartIdleRoaming()
    {
        if (!navMeshAgent.isOnNavMesh)
        {
            Debug.LogWarning("NavMeshAgent is not on NavMesh, cannot roam!", this);
            isCurrentlyRoaming = false;
            return;
        }

        isCurrentlyRoaming = true;
        Vector3 randDir = Utility.Instance.RandomDirection();

        Vector3 targetPos = transform.position + randDir * UnityEngine.Random.Range(minDistanceToRoam, maxDistanceToRoam);

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            navMeshAgent.SetDestination(hit.position);
        }
        else
        {
            Debug.LogWarning("Could not find valid NavMesh position for roaming!", this);
            isCurrentlyRoaming = false;
        }
    }

    // --- Trigger Detection ---
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            enemy = other.transform;
            Debug.Log("Enemy detected! Transitioning to Attacking state.");
            startingState = State.Attacking;
            
            // Immediately allow an attack when enemy is detected
            lastRushTime = -rushCooldown; 
            lastWhirlTime = -whirlCooldown;
            // Optionally: set initialAttackType here if you want it to be predictable upon detection
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Enemy") && other.transform == enemy)
        {
            enemy = null;
            Debug.Log("Enemy left detection range! Transitioning to Idle state.");
            startingState = State.Idle;
            StopAllAttackCoroutines();
        }
    }

    // --- Helper Methods ---
    private void StopAllAttackCoroutines()
    {
        // Stop specific coroutines if they are running
        if (isRushing)
        {
            StopCoroutine(RushingAttackCoroutine(Vector3.zero));
            isRushing = false;
        }
        if (isWhirling)
        {
            StopCoroutine(WhirlingAttackCoroutine());
            isWhirling = false;
        }

        // Always re-enable NavMeshAgent if any attack was stopped,
        // but only if it was disabled.
        if (!navMeshAgent.enabled)
        {
            navMeshAgent.enabled = true;
        }
    }
}