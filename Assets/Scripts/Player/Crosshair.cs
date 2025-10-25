using UnityEngine;

public class Crosshair : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color canShootColor = Color.green;
    [SerializeField] private Color cannotShootColor = Color.red;
    
    void Start()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
        
        // Ensure crosshair renders on top
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = 100;
        }
    }
    
    public void SetColor(bool canShoot)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = canShoot ? canShootColor : cannotShootColor;
        }
    }
    
    public void SetNormalColor()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = normalColor;
        }
    }
}