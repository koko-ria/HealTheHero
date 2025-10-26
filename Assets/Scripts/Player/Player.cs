using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speedForce = 3f;
    private Vector3 directionToGo;
    private Rigidbody2D rb;

    [Header("Stats")]
    [SerializeField] private int maxHealth = 10;
    private int currentHealth;

    public static Player Instance;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;
    }

    void Update() // Changed to Update for input polling
    {
        MovementOfObject();
    }

    private void MovementOfObject()
    {
        directionToGo = GameInput.Instance.GetTheVector();
        // Only update visual if there's actual movement
        if (directionToGo.magnitude > 0.1f)
        {
            PlayerVisual.Instance.AnimationHandler(directionToGo);
        }
        else
        {
            PlayerVisual.Instance.AnimationHandler(Vector3.zero); // Stop walking animation
        }
        rb.MovePosition(transform.position + directionToGo * speedForce * Time.deltaTime);
        PlayerVisual.Instance.HandleMousePosition(); // Update player facing direction
    }

    public Vector3 PlayerPosition()
    {
        return transform.position;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log($"Player took {damage} damage. Health: {currentHealth}");
        // TODO: Hook up hurt animation, UI, audio
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("Player Died!");
        // TODO: Add death animation/audio and handle respawn/game over
        Destroy(gameObject);
    }

    public int GetHealth() => currentHealth;
}