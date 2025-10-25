using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private float speedForce = 3f;
    private Vector3 directionToGo;
    private Rigidbody2D rb;

    public static Player Instance;
    void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        MovementOfObject();
    }

    private void MovementOfObject()
    {
        directionToGo = GameInput.Instance.GetTheVector();
        PlayerVisual.Instance.AnimationHandler(directionToGo);
        rb.MovePosition(transform.position + directionToGo * speedForce * Time.deltaTime);
    }
    
    public Vector3 PlayerPosition()
    {
        return transform.position;
    }
}
