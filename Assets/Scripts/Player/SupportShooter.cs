using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // new input system

[RequireComponent(typeof(LineRenderer))]
public class SupportShooter : MonoBehaviour
{
    public enum AmmoType { Heal, BuffDefence, BuffResistance, BuffDamage, Repulse }

    [Header("Ammo Settings")]
    public AmmoType currentAmmo = AmmoType.Heal;
    [Tooltip("How strong each buff/heal is.")]
    public int potency = 1;
    [Tooltip("How long buffs last (seconds).")]
    public float effectDuration = 6f;
    public float repulseRadius = 2.5f;
    public float repulseForce = 5f;

    [Header("Laser/VFX")]
    public LineRenderer laserLine;
    public float laserDuration = 0.12f;

    [Header("Raycast")]
    public LayerMask hitLayers; // Define what layers the laser can hit
    public float maxRange = 15f;

    [Header("Audio & Animation Placeholders")]
    public AudioSource audioSource;
    public AudioClip sfxFire;
    public AudioClip sfxCycleAmmo;
    public Animator animator;

    // Cached input reference
    private PlayersInput input;

    void Awake()
    {
        if (laserLine == null) laserLine = GetComponent<LineRenderer>();
        if (laserLine != null)
        {
            laserLine.positionCount = 2;
            laserLine.enabled = false;
        }

        input = new PlayersInput();
        input.Enable();

        // Set default hitLayers if not set in inspector (e.g., "Hero" and "Wall")
        if (hitLayers.value == 0)
        {
            hitLayers = LayerMask.GetMask("Hero", "Wall", "Enemy"); // Add "Enemy" if you want to hit enemies too
            Debug.LogWarning($"SupportShooter: hitLayers not set. Defaulting to: {LayerMask.LayerToName(hitLayers)}");
        }
    }

    void OnDestroy()
    {
        input?.Disable();
    }

    void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        // Fire laser
        if (input.Player.Fire.WasPressedThisFrame())
        {
            TryFireAtTarget();
        }

        // Cycle ammo
        if (input.Player.AltFire.WasPressedThisFrame())
        {
            CycleAmmo();
        }

        // Optional: quick numeric keys
        if (Keyboard.current.digit1Key.wasPressedThisFrame) SetAmmo(AmmoType.Heal);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SetAmmo(AmmoType.BuffDefence);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) SetAmmo(AmmoType.BuffResistance);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) SetAmmo(AmmoType.BuffDamage);
        if (Keyboard.current.digit5Key.wasPressedThisFrame) SetAmmo(AmmoType.Repulse);
    }

    private void TryFireAtTarget()
    {
        Vector3 origin = transform.position;
        Vector3 mouseWorld = GameInput.Instance.GetMousePosition();
        Vector3 dir = (mouseWorld - origin).normalized;

        // Use `hitLayers` to filter what the raycast can hit.
        // The `~0` (bitwise NOT of 0) used previously means "everything".
        RaycastHit2D hit = Physics2D.Raycast(origin, dir, maxRange, hitLayers);
        Vector3 endPoint = origin + dir * maxRange;

        if (hit.collider != null)
        {
            endPoint = hit.point;

            // Check if it's the Hero
            if (hit.collider.CompareTag("Hero"))
            {
                var hero = hit.collider.GetComponent<HeroAI>();
                if (hero != null)
                {
                    //hero.ApplySupportEffect(currentAmmo, effectDuration, potency, repulseRadius, repulseForce);
                    Debug.Log($"Laser hit Hero with {currentAmmo} at {Time.time:F2}s");
                }
            }
            // Add other targets if needed, e.g., if you want to hit enemies with 'Repulse' directly
            else if (hit.collider.CompareTag("Enemy") && currentAmmo == AmmoType.Repulse)
            {
                // This allows you to apply repulse directly on the hit enemy if desired
                // The current Repulse only affects nearby enemies when applied TO the hero.
                // If you want to repulse the *hit* enemy, you'd add logic here.
                var enemyRb = hit.collider.GetComponent<Rigidbody2D>();
                if (enemyRb != null)
                {
                     Vector2 repulseDir = (hit.collider.transform.position - transform.position).normalized;
                     enemyRb.AddForce(repulseDir * repulseForce, ForceMode2D.Impulse);
                }
            }
        }

        StartCoroutine(ShowLaser(origin, endPoint));

        if (audioSource && sfxFire) audioSource.PlayOneShot(sfxFire);
        animator?.SetTrigger("Fire");
    }

    private IEnumerator ShowLaser(Vector3 start, Vector3 end)
    {
        if (!laserLine) yield break;

        laserLine.SetPosition(0, start);
        laserLine.SetPosition(1, end);
        laserLine.enabled = true;
        yield return new WaitForSeconds(laserDuration);
        laserLine.enabled = false;
    }

    private void CycleAmmo()
    {
        int next = ((int)currentAmmo + 1) % System.Enum.GetValues(typeof(AmmoType)).Length;
        currentAmmo = (AmmoType)next;

        if (audioSource && sfxCycleAmmo) audioSource.PlayOneShot(sfxCycleAmmo);

        // TODO: update UI to reflect new ammo type
        Debug.Log($"Switched ammo to {currentAmmo}");
    }

    private void SetAmmo(AmmoType type)
    {
        currentAmmo = type;
        if (audioSource && sfxCycleAmmo) audioSource.PlayOneShot(sfxCycleAmmo);
        Debug.Log($"Selected ammo: {currentAmmo}");
    }
}