using UnityEngine;

/// <summary>
/// Airborne hazard. Travels in a fixed direction at a constant speed, ends the
/// run on trigger contact with the Knight, and cleans itself up when it times
/// out, passes well behind the player, or leaves the vertical play area. It is
/// script-driven; the Kinematic <see cref="Rigidbody2D"/> exists only so trigger
/// callbacks fire against the player's dynamic body.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Fireball : MonoBehaviour
{
    [Header("Motion")]
    [SerializeField] Vector2 direction = Vector2.left;
    [SerializeField] float speed = 6f;
    [Tooltip("Rotate the sprite to point along its travel direction.")]
    [SerializeField] bool orientToDirection = true;

    [Header("Lifetime")]
    [SerializeField] float maxLifetime = 8f;
    [SerializeField] float despawnBehindDistance = 15f;
    [SerializeField] float verticalKillRange = 12f;

    Transform player;
    int playerLayer = -1;
    float life;
    bool consumed;

    void Awake()
    {
        playerLayer = LayerMask.NameToLayer("Player");
    }

    void OnEnable()
    {
        life = maxLifetime;
        consumed = false;

        var knight = Object.FindFirstObjectByType<KnightController>();
        player = knight != null ? knight.transform : null;

        Orient();
    }

    /// <summary>Spawner override for travel direction + speed.</summary>
    public void Configure(Vector2 dir, float spd)
    {
        direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.left;
        speed = spd;
        Orient();
    }

    void Orient()
    {
        if (!orientToDirection) return;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void Update()
    {
        transform.position += (Vector3)(direction.normalized * speed * Time.deltaTime);

        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }

        if (Mathf.Abs(transform.position.y) > verticalKillRange) { Destroy(gameObject); return; }

        if (player != null && player.position.x - transform.position.x > despawnBehindDistance)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;

        bool isPlayer = other.CompareTag("Player") ||
                        (playerLayer >= 0 && other.gameObject.layer == playerLayer);
        if (!isPlayer) return;

        var kc = other.GetComponentInParent<KnightController>();
        if (kc == null || kc.IsDead) return;

        consumed = true;
        kc.Die();
        Destroy(gameObject);
    }
}
