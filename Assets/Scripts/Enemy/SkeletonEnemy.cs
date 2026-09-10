using UnityEngine;

/// <summary>
/// Simple endless-runner ground hazard. The Skeleton sits (or slowly shambles)
/// on the ground ahead of the Knight, plays a telegraph attack when the Knight
/// gets close, and ends the run on dangerous body contact. It is deliberately
/// NOT a combat AI &mdash; <see cref="Hurt"/> / <see cref="Kill"/> exist only so
/// later phases (fireball kills, stomps, scoring) can extend it.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SkeletonEnemy : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("World units/sec the Skeleton walks toward the player (-X). 0 = stationary sentry.")]
    [SerializeField] float walkSpeed = 1.2f;
    [Tooltip("Face left toward the incoming Knight. Flip if the art faces the other way.")]
    [SerializeField] bool faceLeft = true;

    [Header("Player Detection")]
    [SerializeField] float attackRange = 2.5f;
    [SerializeField] float attackCooldown = 1.5f;

    [Header("Lifetime")]
    [Tooltip("Destroyed once the player is this far past the Skeleton.")]
    [SerializeField] float despawnBehindDistance = 22f;

    [Header("References")]
    [SerializeField] Animator animator;
    [SerializeField] Collider2D bodyCollider;

    public bool IsDead { get; private set; }

    Transform player;
    KnightController knight;
    SpriteRenderer spriteRenderer;
    int playerLayer = -1;
    float attackTimer;
    bool contactResolved;

    static readonly int HashMoving = Animator.StringToHash("Moving");
    static readonly int HashAttack = Animator.StringToHash("Attack");
    static readonly int HashHurt = Animator.StringToHash("Hurt");
    static readonly int HashDead = Animator.StringToHash("Dead");

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        bodyCollider = GetComponent<Collider2D>();
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
        playerLayer = LayerMask.NameToLayer("Player");
    }

    void OnEnable()
    {
        knight = Object.FindFirstObjectByType<KnightController>();
        player = knight != null ? knight.transform : null;

        IsDead = false;
        contactResolved = false;
        attackTimer = 0f;

        if (spriteRenderer != null) spriteRenderer.flipX = faceLeft;
        if (animator != null)
        {
            animator.ResetTrigger(HashAttack);
            animator.ResetTrigger(HashHurt);
            animator.ResetTrigger(HashDead);
            animator.SetBool(HashMoving, walkSpeed > 0f);
        }
    }

    void Update()
    {
        if (IsDead) return;

        attackTimer -= Time.deltaTime;

        if (walkSpeed > 0f)
            transform.position += Vector3.left * walkSpeed * Time.deltaTime;

        if (player == null) return;

        float dx = player.position.x - transform.position.x;

        // Telegraph attack when the Knight is near (visual only; damage is body contact).
        bool knightAlive = knight == null || !knight.IsDead;
        if (knightAlive && Mathf.Abs(dx) <= attackRange && attackTimer <= 0f)
        {
            attackTimer = attackCooldown;
            if (animator != null) animator.SetTrigger(HashAttack);
        }

        // Recycle once well behind the player.
        if (dx > despawnBehindDistance)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsDead || contactResolved) return;

        bool isPlayer = other.CompareTag("Player") ||
                        (playerLayer >= 0 && other.gameObject.layer == playerLayer);
        if (!isPlayer) return;

        var kc = other.GetComponentInParent<KnightController>();
        if (kc == null || kc.IsDead) return;

        contactResolved = true;
        if (animator != null) animator.SetTrigger(HashAttack);
        kc.Die();
    }

    // ---- extension hooks for later phases -------------------------------
    public void Hurt()
    {
        if (IsDead) return;
        if (animator != null) animator.SetTrigger(HashHurt);
    }

    public void Kill()
    {
        if (IsDead) return;
        IsDead = true;
        if (bodyCollider != null) bodyCollider.enabled = false;
        if (animator != null) animator.SetTrigger(HashDead);
        Destroy(gameObject, 2f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, attackRange);
    }
}
