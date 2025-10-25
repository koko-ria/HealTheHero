using UnityEngine;
using UnityEngine.InputSystem;
public class GameInput : MonoBehaviour
{
    private PlayersInput playerInput;
    private Vector2 moveInput;
    public static GameInput Instance;
    private void Awake()
    {
        Instance = this;

        playerInput = new PlayersInput();

        playerInput.Enable();
    }

    void FixedUpdate()
    {
        GetTheVector();
    }
    public Vector2 GetTheVector()
    {
        moveInput = playerInput.Player.movement.ReadValue<Vector2>();
        return moveInput;
    }

    public Vector3 GetMousePosition()
    {
        return Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
    }
}

