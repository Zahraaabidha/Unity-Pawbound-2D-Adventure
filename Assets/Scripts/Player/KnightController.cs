using UnityEngine;

/// <summary>
/// Endless-runner player controller for the Knight.
///
/// The Knight runs forward automatically at <see cref="runSpeed"/>; the player
/// only controls the jump. Ground is detected with a small overlap circle at the
/// feet so a jump can only start from the ground (plus a short coyote-time and
/// input-buffer window for responsiveness). There is intentionally no double jump.
///
/// All feel values are exposed in the Inspector. Difficulty systems call
/// <see cref="SetRunSpeed"/>; the game-over system calls <see cref="Die"/>.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class KnightController : MonoBehaviour
{
    [Header("Run")]
    [SerializeField] float runSpeed = 6f;
    [SerializeField] float maxRunSpeed = 16f;

    [Header("Jump")]
    [SerializeField] float jumpForce = 12f;
    [SerializeField, Range(0f, 1f)] float shortHopFactor = 0.5f;
    [SerializeField] float fallGravityMultiplier = 1.7f;
    [SerializeField] float coyoteTime = 0.10f;
    [SerializeField] float jumpBufferTime = 0.10f;

    [Header("Ground Check")]
    [SerializeField] Transform groundCheck;
    [SerializeField] float groundCheckRadius = 0.18f;
    [SerializeField] LayerMask groundLayer;

    [Header("Duck")]
    [Tooltip("Any of these held = duck (also honours the Vertical axis pressed down).")]
    [SerializeField] KeyCode[] duckKeys = { KeyCode.DownArrow, KeyCode.S };
    [Tooltip("Standing collider (local space; the transform is scaled).")]
    [SerializeField] Vector2 standingColliderSize = new Vector2(0.20f, 0.30f);
    [SerializeField] Vector2 standingColliderOffset = new Vector2(0f, 0.16f);
    [Tooltip("Ducking collider - same feet, lower head.")]
    [SerializeField] Vector2 duckColliderSize = new Vector2(0.20f, 0.15f);
    [SerializeField] Vector2 duckColliderOffset = new Vector2(0f, 0.085f);
    [Tooltip("Extra downward acceleration while duck is held in the air (never a second jump).")]
    [SerializeField] float airDuckFastFall = 14f;
    [SerializeField] BoxCollider2D bodyCollider;

    [Header("References")]
    [SerializeField] Animator animator;

    public bool IsGrounded { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsDucking { get; private set; }
    public float RunSpeed => runSpeed;

    /// <summary>Raised once when the Knight dies (collision / game over).</summary>
    public event System.Action Died;

    Rigidbody2D rb;
    float baseGravityScale;
    float coyoteCounter;
    float jumpBufferCounter;
    bool jumpHeld;
    bool duckHeld;
    bool controlEnabled = true;

    static readonly int HashSpeed = Animator.StringToHash("Speed");
    static readonly int HashGrounded = Animator.StringToHash("Grounded");
    static readonly int HashVerticalSpeed = Animator.StringToHash("VerticalSpeed");
    static readonly int HashJump = Animator.StringToHash("Jump");
    static readonly int HashDead = Animator.StringToHash("Dead");
    static readonly int HashDucking = Animator.StringToHash("Ducking");

    void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
        bodyCollider = GetComponent<BoxCollider2D>();
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (bodyCollider == null) bodyCollider = GetComponent<BoxCollider2D>();
        baseGravityScale = rb.gravityScale;
        ApplyStandingCollider();
    }

    void Update()
    {
        if (IsDead || !controlEnabled) return;

        bool jumpPressed = Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.UpArrow);
        jumpHeld = Input.GetButton("Jump") || Input.GetKey(KeyCode.UpArrow);

        if (jumpPressed) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        coyoteCounter = IsGrounded ? coyoteTime : coyoteCounter - Time.deltaTime;

        // Duck: held-only, ground-only (air-hold just fast-falls, see FixedUpdate).
        duckHeld = DuckHeld();
        SetDucking(duckHeld && IsGrounded);

        UpdateAnimator();
    }

    bool DuckHeld()
    {
        if (duckKeys != null)
            for (int i = 0; i < duckKeys.Length; i++)
                if (Input.GetKey(duckKeys[i])) return true;
        return Input.GetAxisRaw("Vertical") < -0.5f;
    }

    void SetDucking(bool value)
    {
        if (IsDucking == value) return;
        IsDucking = value;
        if (value) ApplyDuckCollider(); else ApplyStandingCollider();
        if (animator != null) animator.SetBool(HashDucking, value);
    }

    void ApplyStandingCollider()
    {
        if (bodyCollider == null) return;
        bodyCollider.size = standingColliderSize;
        bodyCollider.offset = standingColliderOffset;
    }

    void ApplyDuckCollider()
    {
        if (bodyCollider == null) return;
        bodyCollider.size = duckColliderSize;
        bodyCollider.offset = duckColliderOffset;
    }

    void FixedUpdate()
    {
        if (IsDead)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        IsGrounded = groundCheck != null &&
                     Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (controlEnabled)
            rb.linearVelocity = new Vector2(runSpeed, rb.linearVelocity.y);
        else
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        // Buffered + coyote jump. Ducking blocks the jump (release to jump) -
        // it does NOT touch the buffer/coyote counters, so nothing else changes.
        if (controlEnabled && !IsDucking && jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            if (animator != null) animator.SetTrigger(HashJump);
        }

        // Variable jump height: cut the rise short if the button was released.
        if (rb.linearVelocity.y > 0f && !jumpHeld)
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * baseGravityScale *
                                 (1f - shortHopFactor) * Time.fixedDeltaTime;

        // Air-duck = gentle fast-fall once at/after apex. Never adds upward force,
        // never re-arms a jump.
        if (controlEnabled && duckHeld && !IsGrounded && rb.linearVelocity.y < 2f)
            rb.linearVelocity += Vector2.down * airDuckFastFall * Time.fixedDeltaTime;

        // Heavier gravity while falling for a snappier arc.
        rb.gravityScale = rb.linearVelocity.y < -0.01f
            ? baseGravityScale * fallGravityMultiplier
            : baseGravityScale;
    }

    void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat(HashSpeed, Mathf.Abs(rb.linearVelocity.x));
        animator.SetBool(HashGrounded, IsGrounded);
        animator.SetFloat(HashVerticalSpeed, rb.linearVelocity.y);
    }

    /// <summary>Clamp-and-apply a new forward speed (used by the difficulty ramp).</summary>
    public void SetRunSpeed(float speed) => runSpeed = Mathf.Clamp(speed, 0f, maxRunSpeed);

    /// <summary>Freeze/unfreeze player control without killing the Knight (Start / GameOver screens).</summary>
    public void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;
        if (!enabled) SetDucking(false);
    }

    public void Die()
    {
        if (IsDead) return;
        IsDead = true;
        SetDucking(false);
        rb.linearVelocity = Vector2.zero;
        if (animator != null) animator.SetTrigger(HashDead);
        Died?.Invoke();
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
