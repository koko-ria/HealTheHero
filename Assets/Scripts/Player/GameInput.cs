using UnityEngine;
using UnityEngine.InputSystem;

public class GameInput : MonoBehaviour
{
    private PlayersInput playerInput;
    private Vector2 moveInput;
    public static GameInput Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        playerInput = new PlayersInput();
        playerInput.Enable();
    }

    // No FixedUpdate needed here, GetTheVector is called by Player.cs
    // public void FixedUpdate() { GetTheVector(); } // Removed

    public Vector2 GetTheVector()
    {
        moveInput = playerInput.Player.movement.ReadValue<Vector2>();
        return moveInput;
    }

    public Vector3 GetMousePosition()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mousePos.z = 0; // Ensure Z is 0 for 2D
        return mousePos;
    }
}