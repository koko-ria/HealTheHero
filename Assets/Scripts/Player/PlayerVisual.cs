using System;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerVisual : MonoBehaviour
{
    private const string WALK_CONDITION = "IS_WALKING";
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    public static PlayerVisual Instance;
    private Vector3 mousePos;

    void Awake()
    {
        Instance = this;
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    void FixedUpdate()
    {
        HandleMousePosition();
    }
    // Accept Vector3 so callers that use Vector3 world/movement vectors (like `Player`) can pass
    // the value directly. Only x/y are used for 2D animation decisions.
    public void AnimationHandler(Vector3 movement)
    {
        if (animator == null)
        {
            Debug.LogWarning("PlayerVisual: Animator is missing.", this);
            return;
        }

        if (Math.Abs(movement.x) < 0.2f && Math.Abs(movement.y) < 0.2f)
        {
            animator.SetBool(WALK_CONDITION, false);
        }
        else
        {
            animator.SetBool(WALK_CONDITION, true);
        }
    }

    private void HandleMousePosition()
    {
        mousePos = GameInput.Instance.GetMousePosition();

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
