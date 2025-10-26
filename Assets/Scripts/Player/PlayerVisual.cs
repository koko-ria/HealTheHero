using System;
using UnityEngine;

public class PlayerVisual : MonoBehaviour
{
    private const string WALK_CONDITION = "IsWalking"; // Ensure this matches your Animator parameter name
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    public static PlayerVisual Instance;
    private Vector3 mousePos;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("PlayerVisual: Animator component not found on PlayerVisual object.", this);
        }
    }

    // HandleMousePosition is now called by Player.cs in Update
    // void FixedUpdate() { HandleMousePosition(); } // Removed

    // Accept Vector3 so callers that use Vector3 world/movement vectors (like `Player`) can pass
    // the value directly. Only x/y are used for 2D animation decisions.
    public void AnimationHandler(Vector3 movement)
    {
        if (animator == null)
        {
            // Debug.LogWarning is already done in Awake if animator is missing.
            return;
        }

        // Use a threshold to determine if player is "walking"
        bool isWalking = movement.magnitude > 0.1f; // Using magnitude for cleaner check
        animator.SetBool(WALK_CONDITION, isWalking);
    }

    public void HandleMousePosition()
    {
        mousePos = GameInput.Instance.GetMousePosition();

        // Flip sprite based on mouse position relative to player
        if (Player.Instance.PlayerPosition().x > mousePos.x)
        {
            spriteRenderer.flipX = true;
        }
        else
        {
            spriteRenderer.flipX = false;
        }
    }
}