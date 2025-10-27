// PlayerSupport.cs (FINAL MODIFICATIONS FOR INPUT SYSTEM)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // NEW for Input System

public class PlayerSupport : MonoBehaviour
{
    public static PlayerSupport Instance { get;  set; }

    [Header("Stats")]
    [SerializeField] private int maxHealth = 10;
    private int actualHealth;
    [SerializeField] private float speedForce = 3f;

    private Vector2 moveInput;
    private Rigidbody2D rb;

    [Header("References")]
    [SerializeField] private HeroAI heroAI; // Assign HeroAI in inspector

    // NEW: Reference to our generated input actions
    private PlayerInputActions playerInputActions;

    // UI elements (assign in inspector or find dynamically)
    [Header("UI References (Optional)")]
    [SerializeField] private Image healFillImage; // For the healing mini-game bar
    [SerializeField] private RectTransform greenZoneParent; // Parent of the green zones (for visual placement, not logic)
    [SerializeField] private GameObject shieldBuffIcon;
    [SerializeField] private GameObject resistanceBuffIcon;
    [SerializeField] private GameObject damageBuffIcon;
    [SerializeField] private List<Image> abilityCooldownImages; // For special abilities
    [SerializeField] private GameObject endGameButton; // For the end game ability

    [Header("Healing Mechanic")]
    [SerializeField] private float healBarSpeed = 1.0f; // Speed of the moving bar
    [SerializeField] private float healBarMaxTime = 2.0f; // Time for bar to go across once
    [SerializeField] private float goodGreenZoneWidth = 0.1f; // Percentage of bar width for good zone
    [SerializeField] private float perfectGreenZoneWidth = 0.03f; // Percentage of bar width for perfect zone
    [SerializeField] private float healCooldown = 1.0f;
    private float lastHealTime;
    private bool healingMiniGameActive = false;
    private float healBarCurrentPosition = 0f; // 0 to 1 for bar position
    private Coroutine healBarCoroutine;

    [Header("Buffs")]
    [SerializeField] private float buffDuration = 10f;
    [SerializeField] private float buffCooldown = 5f; // Cooldown between applying *any* buff
    private float lastBuffTime = -Mathf.Infinity;
    private BuffType activeBuff = BuffType.None;
    private Coroutine buffCoroutine;

    // Buff values (adjust as needed)
    [SerializeField] private float shieldBuffAmount = 0.5f; // Flat damage reduction (e.g., 0.5f means -0.5 damage)
    [SerializeField] private float resistanceBuffAmount = 0.3f; // Damage resistance percentage (0 to 1)
    [SerializeField] private float damageBuffAmount = 0.5f; // Damage increase percentage (e.g., 0.5 for +50% damage)

    public enum BuffType { None, Shield, Resistance, Damage }

    [Header("Special Abilities - Timed Unlocks")]
    [SerializeField] private float abilityUnlockInterval = 120f; // 2 minutes
    private float timeSinceLastAbilityUnlock;
    private List<AbilityType> unlockedAbilities = new List<AbilityType>();
    private int abilitiesAwardedCount = 0; // Tracks how many abilities have been given

    [Header("Ability 1: Annihilation")]
    [SerializeField] private float annihilationRadius = 20f;
    [SerializeField] private float annihilationCooldown = 600f; // 10 minutes
    private float lastAnnihilationTime = -Mathf.Infinity;
    [SerializeField] private LayerMask enemyLayer; // Assign the Enemy layer

    [Header("Ability 2: Invulnerability")]
    [SerializeField] private float invulnerabilityDuration = 20f;
    [SerializeField] private float invulnerabilityCooldown = 120f; // 2 minutes
    private float lastInvulnerabilityTime = -Mathf.Infinity;

    [Header("Ability 3: Time Stop")]
    [SerializeField] private float timeStopDuration = 10f;
    [SerializeField] private float timeStopCooldown = 60f; // 1 minute
    private float lastTimeStopTime = -Mathf.Infinity;

    [Header("Ability 4: Invisibility")]
    [SerializeField] private float invisibilityDuration = 30f;
    [SerializeField] private float invisibilityCooldown = 30f; // 30 seconds
    private float lastInvisibilityTime = -Mathf.Infinity;

    [Header("Ability 5: End Game")]
    [SerializeField] private float endGameUnlockTime = 600f; // Unlock after 10 minutes of gameplay
    private bool isEndGameAvailable = false;

    public enum AbilityType { Annihilation, Invulnerability, TimeStop, Invisibility, EndGame }


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        if (heroAI == null)
        {
            heroAI = FindObjectOfType<HeroAI>();
            if (heroAI == null)
            {
                Debug.LogError("PlayerSupport: HeroAI not found in scene!", this);
                enabled = false;
            }
        }

        playerInputActions = new PlayerInputActions(); // Initialize new input actions
        // Initialize UI elements states
        UpdateBuffIcons();
        rb = GetComponent<Rigidbody2D>();
        if (endGameButton != null) endGameButton.SetActive(false);
        if (healFillImage != null && healFillImage.transform.parent != null) healFillImage.transform.parent.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        playerInputActions.Enable();
        // Subscribe to input events
        playerInputActions.Player.Heal.performed += OnHealPerformed;
        playerInputActions.Player.BuffShield.performed += ctx => TryApplyBuff(BuffType.Shield);
        playerInputActions.Player.BuffResistance.performed += ctx => TryApplyBuff(BuffType.Resistance);
        playerInputActions.Player.BuffDamage.performed += ctx => TryApplyBuff(BuffType.Damage);
        playerInputActions.Player.Ability1.performed += ctx => TryUseAbility(AbilityType.Annihilation);
        playerInputActions.Player.Ability2.performed += ctx => TryUseAbility(AbilityType.Invulnerability);
        playerInputActions.Player.Ability3.performed += ctx => TryUseAbility(AbilityType.TimeStop);
        playerInputActions.Player.Ability4.performed += ctx => TryUseAbility(AbilityType.Invisibility);
        playerInputActions.Player.Ability5.performed += ctx => TryUseAbility(AbilityType.EndGame);
    }

    private void OnDisable()
    {
        // Unsubscribe from input events
        playerInputActions.Player.Heal.performed -= OnHealPerformed;
        playerInputActions.Player.BuffShield.performed -= ctx => TryApplyBuff(BuffType.Shield);
        playerInputActions.Player.BuffResistance.performed -= ctx => TryApplyBuff(BuffType.Resistance);
        playerInputActions.Player.BuffDamage.performed -= ctx => TryApplyBuff(BuffType.Damage);
        playerInputActions.Player.Ability1.performed -= ctx => TryUseAbility(AbilityType.Annihilation);
        playerInputActions.Player.Ability2.performed -= ctx => TryUseAbility(AbilityType.Invulnerability);
        playerInputActions.Player.Ability3.performed -= ctx => TryUseAbility(AbilityType.TimeStop);
        playerInputActions.Player.Ability4.performed -= ctx => TryUseAbility(AbilityType.Invisibility);
        playerInputActions.Player.Ability5.performed -= ctx => TryUseAbility(AbilityType.EndGame);
        playerInputActions.Disable();
    }

    private void Start()
    {
        actualHealth = maxHealth;

        lastHealTime = -healCooldown; // Allow immediate healing
        timeSinceLastAbilityUnlock = 0f;
    }

    private void Update()
    {
        HandleAbilityUnlocks();
        UpdateAbilityCooldownsUI();
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = moveInput * speedForce;
    }
    private void OnHealPerformed(InputAction.CallbackContext context)
    {
        if (Time.time >= lastHealTime + healCooldown)
        {
            if (!healingMiniGameActive)
            {
                StartHealMiniGame();
            }
            else
            {
                ProcessHealInput();
            }
        }
    }


    private void HandleAbilityUnlocks()
    {
        // Unlock abilities based on time elapsed
        // In Plant vs Zombies style, new abilities are awarded one by one
        if (abilitiesAwardedCount < 4) // Max 4 timed abilities (Annihilation, Invulnerability, TimeStop, Invisibility)
        {
            timeSinceLastAbilityUnlock += Time.deltaTime;
            if (timeSinceLastAbilityUnlock >= abilityUnlockInterval)
            {
                timeSinceLastAbilityUnlock = 0f; // Reset timer for next unlock

                // Award the next ability in sequence
                switch (unlockedAbilities.Count) // Use count to determine next unlock
                {
                    case 0:
                        unlockedAbilities.Add(AbilityType.Annihilation);
                        Debug.Log("Ability Unlocked: Annihilation (1)");
                        break;
                    case 1:
                        unlockedAbilities.Add(AbilityType.Invulnerability);
                        Debug.Log("Ability Unlocked: Invulnerability (2)");
                        break;
                    case 2:
                        unlockedAbilities.Add(AbilityType.TimeStop);
                        Debug.Log("Ability Unlocked: Time Stop (3)");
                        break;
                    case 3:
                        unlockedAbilities.Add(AbilityType.Invisibility);
                        Debug.Log("Ability Unlocked: Invisibility (4)");
                        break;
                }
                abilitiesAwardedCount++;
            }
        }

        // Check for End Game ability unlock specifically
        if (!isEndGameAvailable && Time.timeSinceLevelLoad >= endGameUnlockTime)
        {
            isEndGameAvailable = true;
            if (endGameButton != null) endGameButton.SetActive(true);
            Debug.Log("Ability Unlocked: End Game (5)");
        }
    }

    private bool HasAbility(AbilityType type)
    {
        return unlockedAbilities.Contains(type);
    }

    // --- Healing Mini-Game ---
    private void StartHealMiniGame()
    {
        healingMiniGameActive = true;
        Debug.Log("Healing Mini-Game Started! Press Space again to heal.");
        // Visually activate the healing bar UI
        if (healFillImage != null && healFillImage.transform.parent != null)
        {
            healFillImage.transform.parent.gameObject.SetActive(true); // Assuming parent is the container
            healFillImage.fillAmount = 0;
        }
        healBarCurrentPosition = 0f;
        if (healBarCoroutine != null) StopCoroutine(healBarCoroutine);
        healBarCoroutine = StartCoroutine(HealBarMovement());
    }
    private IEnumerator HealBarMovement()
    {
        float timer = 0f;
        float direction = 1f; // 1 for right, -1 for left

        while (healingMiniGameActive)
        {
            timer += Time.deltaTime * healBarSpeed * direction;
            healBarCurrentPosition = Mathf.Clamp01(timer / healBarMaxTime);

            if (healFillImage != null)
            {
                healFillImage.fillAmount = healBarCurrentPosition;
            }

            if (healBarCurrentPosition >= 1f && direction == 1f)
            {
                direction = -1f; // Change direction to go left
            }
            else if (healBarCurrentPosition <= 0f && direction == -1f)
            {
                direction = 1f; // Change direction to go right
            }
            yield return null;
        }
    }

    private void ProcessHealInput()
    {
        if (heroAI == null)
        {
            Debug.LogWarning("HeroAI reference missing for healing.");
            EndHealMiniGame();
            return;
        }

        float healPercentage = 0f;
        string feedback = "";

        // Determine quality of click based on healBarCurrentPosition
        float center = 0.5f;
        float perfectZoneStart = center - (perfectGreenZoneWidth / 2f);
        float perfectZoneEnd = center + (perfectGreenZoneWidth / 2f);
        float goodZoneStart = center - (goodGreenZoneWidth / 2f);
        float goodZoneEnd = center + (goodGreenZoneWidth / 2f);

        if (healBarCurrentPosition >= perfectZoneStart && healBarCurrentPosition <= perfectZoneEnd)
        {
            healPercentage = 0.5f; // Super Green / Perfect
            feedback = "PERFECT HEAL!";
        }
        else if (healBarCurrentPosition >= goodZoneStart && healBarCurrentPosition <= goodZoneEnd)
        {
            healPercentage = 0.3f; // Nice Green / Good
            feedback = "NICE HEAL!";
        }
        else if (healBarCurrentPosition >= 0.4f && healBarCurrentPosition <= 0.6f) // Slightly wider "norm" green zone
        {
            healPercentage = 0.15f; // Norm Green
            feedback = "OKAY HEAL.";
        }
        else if (healBarCurrentPosition >= 0.2f && healBarCurrentPosition <= 0.8f) // Wider "so-so" green zone
        {
            healPercentage = 0.05f; // So-so Green
            feedback = "MEH HEAL.";
        }
        else
        {
            healPercentage = 0.0f; // Хуйня зеленая / Miss
            feedback = "MISSED HEAL!";
        }

        if (healPercentage > 0)
        {
            // Calculate actual heal amount based on hero's MAX health (assuming a max health property)
            // If you don't have a max health, you might want to heal a flat amount or percentage of current health.
            // For now, let's assume `heroAI.maxHealth` exists, or heal a flat amount of a percentage of the existing health.
            // Let's use current health as the prompt implies "отхиливаем Героя на определенный процент хп"
            heroAI.Heal(heroAI.health * healPercentage);
        }
        Debug.Log($"Heal Result: {feedback} (Healed for {healPercentage * 100}%)");

        lastHealTime = Time.time;
        EndHealMiniGame();
    }

    private void EndHealMiniGame()
    {
        healingMiniGameActive = false;
        if (healBarCoroutine != null)
        {
            StopCoroutine(healBarCoroutine);
        }
        if (healFillImage != null && healFillImage.transform.parent != null)
        {
            healFillImage.transform.parent.gameObject.SetActive(false);
        }
    }

    // --- Buff System ---
    private void TryApplyBuff(BuffType type)
    {
        if (Time.time < lastBuffTime + buffCooldown)
        {
            Debug.Log($"Buffs are on cooldown. {Mathf.Ceil(lastBuffTime + buffCooldown - Time.time)}s remaining.");
            return;
        }

        // Only one buff can be active at a time
        if (activeBuff != BuffType.None)
        {
            Debug.Log("Another buff is already active. Cannot apply new buff.");
            return;
        }

        lastBuffTime = Time.time;
        activeBuff = type;
        Debug.Log($"Applying {type} buff to Hero for {buffDuration} seconds.");

        if (buffCoroutine != null) StopCoroutine(buffCoroutine);
        buffCoroutine = StartCoroutine(ApplyBuffCoroutine(type));
    }

    private IEnumerator ApplyBuffCoroutine(BuffType type)
    {
        // Apply buff effect
        switch (type)
        {
            case BuffType.Shield:
                heroAI.ApplyShield(shieldBuffAmount);
                break;
            case BuffType.Resistance:
                heroAI.ApplyResistance(resistanceBuffAmount);
                break;
            case BuffType.Damage:
                heroAI.ApplyDamageBuff(damageBuffAmount);
                break;
        }
        UpdateBuffIcons();

        yield return new WaitForSeconds(buffDuration);

        // Remove buff effect
        switch (type)
        {
            case BuffType.Shield:
                heroAI.RemoveShield();
                break;
            case BuffType.Resistance:
                heroAI.RemoveResistance();
                break;
            case BuffType.Damage:
                heroAI.RemoveDamageBuff();
                break;
        }
        activeBuff = BuffType.None;
        UpdateBuffIcons();
        Debug.Log($"{type} buff expired.");
    }

    private void UpdateBuffIcons()
    {
        if (shieldBuffIcon != null) shieldBuffIcon.SetActive(activeBuff == BuffType.Shield);
        if (resistanceBuffIcon != null) resistanceBuffIcon.SetActive(activeBuff == BuffType.Resistance);
        if (damageBuffIcon != null) damageBuffIcon.SetActive(activeBuff == BuffType.Damage);
    }

    // --- Special Abilities ---
    private void TryUseAbility(AbilityType type)
    {
        float currentTime = Time.time;
        float cooldown = 0;
        float lastUsedTime = 0;

        switch (type)
        {
            case AbilityType.Annihilation:
                cooldown = annihilationCooldown;
                lastUsedTime = lastAnnihilationTime;
                break;
            case AbilityType.Invulnerability:
                cooldown = invulnerabilityCooldown;
                lastUsedTime = lastInvulnerabilityTime;
                break;
            case AbilityType.TimeStop:
                cooldown = timeStopCooldown;
                lastUsedTime = lastTimeStopTime;
                break;
            case AbilityType.Invisibility:
                cooldown = invisibilityCooldown;
                lastUsedTime = lastInvisibilityTime;
                break;
            case AbilityType.EndGame:
                // End game has no cooldown, only a single use after unlock
                if (!isEndGameAvailable)
                {
                    Debug.Log("End Game ability not yet available.");
                    return;
                }
                Debug.Log("GAME OVER MAN, GAME OVER!");
                // Implement actual game ending logic here
                #if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
                #else
                    Application.Quit();
                #endif
                return; // Don't proceed to cooldown check for EndGame
        }

        if (!HasAbility(type) && type != AbilityType.EndGame) // Only check HasAbility for timed unlocks, EndGame is separate
        {
             Debug.Log($"{type} ability not yet unlocked.");
             return;
        }

        if (currentTime < lastUsedTime + cooldown)
        {
            Debug.Log($"{type} is on cooldown. {Mathf.Ceil(lastUsedTime + cooldown - currentTime)}s remaining.");
            return;
        }

        // Use ability
        switch (type)
        {
            case AbilityType.Annihilation:
                StartCoroutine(AnnihilationAbility());
                lastAnnihilationTime = currentTime;
                break;
            case AbilityType.Invulnerability:
                StartCoroutine(InvulnerabilityAbility());
                lastInvulnerabilityTime = currentTime;
                break;
            case AbilityType.TimeStop:
                StartCoroutine(TimeStopAbility());
                lastTimeStopTime = currentTime;
                break;
            case AbilityType.Invisibility:
                StartCoroutine(InvisibilityAbility());
                lastInvisibilityTime = currentTime;
                break;
        }
        Debug.Log($"Activated {type} ability.");
        UpdateAbilityCooldownsUI();
    }

    private IEnumerator AnnihilationAbility()
    {
        Debug.Log("Annihilation! All enemies in radius will be destroyed.");
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(heroAI.transform.position, annihilationRadius, enemyLayer);
        foreach (Collider2D hit in hitEnemies)
        {
            EnemyBehavior enemyComponent = hit.GetComponent<EnemyBehavior>();
            if (enemyComponent != null)
            {
                enemyComponent.TakeDamage(9999); // Instant kill
            }
        }
        // TODO: Add visual effect for annihilation here (e.g., an explosion particle effect)
        yield return null;
    }

    private IEnumerator InvulnerabilityAbility()
    {
        heroAI.SetInvulnerable(true);
        Debug.Log($"Hero is now invulnerable for {invulnerabilityDuration} seconds!");
        // TODO: Add visual indicator for invulnerability (e.g., glow, shield effect)
        yield return new WaitForSeconds(invulnerabilityDuration);
        heroAI.SetInvulnerable(false);
        Debug.Log("Hero is no longer invulnerable.");
    }

    private IEnumerator TimeStopAbility()
    {
        Debug.Log($"Time Stop activated for {timeStopDuration} seconds!");
        // We allow Player to move/heal/buff during Time Stop, so we don't affect PlayerSupport itself
        GameTimeManager.Instance.StopTime();
        // TODO: Add visual indicator for time stop (e.g., desaturate screen, slow particles)
        yield return new WaitForSeconds(timeStopDuration);
        GameTimeManager.Instance.ResumeTime();
        Debug.Log("Time Stop ended.");
    }

    private IEnumerator InvisibilityAbility()
    {
        heroAI.SetInvisibility(true);
        Debug.Log($"Hero is now invisible for {invisibilityDuration} seconds! (No damage, pass through enemies)");
        // TODO: Add visual indicator for invisibility (e.g., hero becomes translucent or disappears)
        yield return new WaitForSeconds(invisibilityDuration);
        heroAI.SetInvisibility(false);
        Debug.Log("Hero is no longer invisible.");
    }

    private void UpdateAbilityCooldownsUI()
    {
        if (abilityCooldownImages == null || abilityCooldownImages.Count == 0) return;

        // Annihilation (Index 0 for example)
        if (abilityCooldownImages.Count > 0)
        {
            if (unlockedAbilities.Contains(AbilityType.Annihilation))
            {
                float remaining = Mathf.Max(0, lastAnnihilationTime + annihilationCooldown - Time.time);
                abilityCooldownImages[0].fillAmount = remaining / annihilationCooldown;
            }
            else { abilityCooldownImages[0].fillAmount = 1f; } // Grayed out if not unlocked
        }

        // Invulnerability (Index 1)
        if (abilityCooldownImages.Count > 1)
        {
            if (unlockedAbilities.Contains(AbilityType.Invulnerability))
            {
                float remaining = Mathf.Max(0, lastInvulnerabilityTime + invulnerabilityCooldown - Time.time);
                abilityCooldownImages[1].fillAmount = remaining / invulnerabilityCooldown;
            }
            else { abilityCooldownImages[1].fillAmount = 1f; }
        }

        // Time Stop (Index 2)
        if (abilityCooldownImages.Count > 2)
        {
            if (unlockedAbilities.Contains(AbilityType.TimeStop))
            {
                float remaining = Mathf.Max(0, lastTimeStopTime + timeStopCooldown - Time.time);
                abilityCooldownImages[2].fillAmount = remaining / timeStopCooldown;
            }
            else { abilityCooldownImages[2].fillAmount = 1f; }
        }

        // Invisibility (Index 3)
        if (abilityCooldownImages.Count > 3)
        {
            if (unlockedAbilities.Contains(AbilityType.Invisibility))
            {
                float remaining = Mathf.Max(0, lastInvisibilityTime + invisibilityCooldown - Time.time);
                abilityCooldownImages[3].fillAmount = remaining / invisibilityCooldown;
            }
            else { abilityCooldownImages[3].fillAmount = 1f; }
        }

        // End Game (Index 4) - fillAmount for this might just be 0 when available, 1 when not
        if (abilityCooldownImages.Count > 4)
        {
            abilityCooldownImages[4].fillAmount = isEndGameAvailable ? 0f : 1f;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (heroAI != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(heroAI.transform.position, annihilationRadius);
        }
    }
}