using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyBehavior : MonoBehaviour
{
    [Header("References")]
    public Transform player;
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
    private Vector3 orbitTargetPos;
    private float attackTimer;
    private float pathUpdateTimer;
    private int currentPatternIndex;
    private NavMeshAgent agent;
    private bool hasReachedWanderTarget;
    
    void Start()
    {
        // Setup NavMeshAgent
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = moveSpeed;
        
        startPos = transform.position;
        
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        if (movementType == MovementType.Wander)
            SetNewWanderTarget();
    }
    
    void Update()
    {
        if (player == null) return;
        
        float distToPlayer = Vector2.Distance(transform.position, player.position);
        
        // Update path periodically instead of every frame (performance optimization)
        pathUpdateTimer -= Time.deltaTime;
        if (pathUpdateTimer <= 0)
        {
            HandleMovement(distToPlayer);
            pathUpdateTimer = pathUpdateInterval;
        }
        
        // Handle attacking
        if (distToPlayer <= attackRange)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0 && attackPatterns.Length > 0)
            {
                Attack();
            }
        }
        
        // Check if reached wander target
        if (movementType == MovementType.Wander && !agent.pathPending)
        {
            if (agent.remainingDistance <= agent.stoppingDistance)
            {
                if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
                {
                    SetNewWanderTarget();
                }
            }
        }
    }
    
    void HandleMovement(float distToPlayer)
    {
        switch (movementType)
        {
            case MovementType.Chase:
                HandleChase(distToPlayer);
                break;
                
            case MovementType.KeepDistance:
                HandleKeepDistance(distToPlayer);
                break;
                
            case MovementType.Wander:
                HandleWander();
                break;
                
            case MovementType.Stationary:
                agent.isStopped = true;
                break;
                
            case MovementType.Orbit:
                HandleOrbit(distToPlayer);
                break;
        }
    }
    
    void HandleChase(float distToPlayer)
    {
        if (distToPlayer <= detectionRange)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
        else
        {
            agent.isStopped = true;
        }
    }
    
    void HandleKeepDistance(float distToPlayer)
    {
        if (distToPlayer <= detectionRange)
        {
            agent.isStopped = false;
            
            // Too close - back away
            if (distToPlayer < attackRange * 0.7f)
            {
                Vector3 directionAway = (transform.position - player.position).normalized;
                Vector3 retreatPos = transform.position + directionAway * 2f;
                
                // Make sure the retreat position is valid on NavMesh
                NavMeshHit hit;
                if (NavMesh.SamplePosition(retreatPos, out hit, 2f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
            }
            // Too far - move closer
            else if (distToPlayer > attackRange)
            {
                agent.SetDestination(player.position);
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
        if (!agent.hasPath || agent.remainingDistance < 0.5f)
        {
            SetNewWanderTarget();
        }
    }
    
    void HandleOrbit(float distToPlayer)
    {
        if (distToPlayer <= detectionRange)
        {
            agent.isStopped = false;
            
            // Calculate orbit position (perpendicular to player direction)
            Vector2 toPlayer = (Vector2)(player.position - transform.position);
            float currentAngle = Mathf.Atan2(toPlayer.y, toPlayer.x);
            
            // Move in circular path around player
            float orbitAngle = currentAngle + (90f * Mathf.Deg2Rad); // 90 degrees perpendicular
            
            Vector3 orbitOffset = new Vector3(
                Mathf.Cos(orbitAngle) * orbitRadius,
                Mathf.Sin(orbitAngle) * orbitRadius,
                0
            );
            
            Vector3 targetPos = player.position + orbitOffset;
            
            // Make sure position is valid on NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPos, out hit, orbitRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
            else
            {
                // If orbit position not valid, just move toward player
                agent.SetDestination(player.position);
            }
        }
        else
        {
            agent.isStopped = true;
        }
    }
    
    void SetNewWanderTarget()
    {
        // Generate random point within wander radius
        Vector2 randomDirection = Random.insideUnitCircle * wanderRadius;
        Vector3 targetPosition = new Vector3(startPos.x + randomDirection.x, startPos.y + randomDirection.y, 0);
        
        // Find nearest valid point on NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPosition, out hit, wanderRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            wanderTarget = hit.position;
        }
    }
    
    void Attack()
    {
        AttackPattern pattern = attackPatterns[currentPatternIndex];
        
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
        Vector2 dir = (player.position - transform.position).normalized;
        CreateProjectile(pattern, dir);
    }
    
    IEnumerator ShootBurst(AttackPattern pattern)
    {
        Vector2 baseDir = (player.position - transform.position).normalized;
        float startAngle = -pattern.spreadAngle / 2;
        
        for (int i = 0; i < pattern.projectileCount; i++)
        {
            float angle = startAngle + (pattern.spreadAngle / (pattern.projectileCount - 1)) * i;
            Vector2 dir = Rotate(baseDir, angle);
            CreateProjectile(pattern, dir);
            
            if (i < pattern.projectileCount - 1)
                yield return new WaitForSeconds(pattern.burstDelay);
        }
    }
    
    void ShootCircle(AttackPattern pattern)
    {
        float angleStep = 360f / pattern.projectileCount;
        
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
            float angleStep = 360f / 8;
            for (int j = 0; j < 8; j++)
            {
                float angle = angleStep * j + angleOffset;
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                CreateProjectile(pattern, dir);
            }
            
            angleOffset += 15f;
            yield return new WaitForSeconds(pattern.burstDelay);
        }
    }
    
    void ShootPredictive(AttackPattern pattern)
    {
        NavMeshAgent playerAgent = player.GetComponent<NavMeshAgent>();
        if (playerAgent != null && playerAgent.velocity.magnitude > 0.1f)
        {
            Vector2 toPlayer = player.position - transform.position;
            float dist = toPlayer.magnitude;
            float timeToHit = dist / pattern.projectileSpeed;
            Vector2 predictedPos = (Vector2)player.position + (Vector2)playerAgent.velocity * timeToHit;
            Vector2 dir = (predictedPos - (Vector2)transform.position).normalized;
            CreateProjectile(pattern, dir);
        }
        else
        {
            ShootSingle(pattern);
        }
    }
    
    void CreateProjectile(AttackPattern pattern, Vector2 direction)
    {
        GameObject proj = Instantiate(pattern.projectilePrefab, transform.position, Quaternion.identity);
        Rigidbody2D projRb = proj.GetComponent<Rigidbody2D>();
        if (projRb != null)
        {
            projRb.linearVelocity = direction * pattern.projectileSpeed;
        }
        
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
        if (health <= 0)
        {
            Die();
        }
    }
    
    void Die()
    {
        Destroy(gameObject);
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
            if (player != null)
                Gizmos.DrawWireSphere(player.position, orbitRadius);
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