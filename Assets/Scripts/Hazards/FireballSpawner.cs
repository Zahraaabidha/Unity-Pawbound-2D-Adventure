using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns airborne fireballs ahead of the Knight. Each fireball is either LOW
/// (the Knight must jump over it) or HIGH (the Knight must stay on the ground and
/// NOT jump). Spawn frequency rises over the run: the interval eases from
/// <see cref="initialInterval"/> down to <see cref="minInterval"/> across
/// <see cref="rampSeconds"/> of play.
///
/// This is a self-contained Phase-C frequency ramp. The global DifficultyManager
/// added in Phase D can later drive these values instead.
/// </summary>
public class FireballSpawner : MonoBehaviour
{
    [Header("Prefab & Target")]
    [SerializeField] GameObject fireballPrefab;
    [SerializeField] Transform player;

    [Header("Placement")]
    [SerializeField] float groundY = -4f;
    [SerializeField] float spawnAheadDistance = 26f;
    [Tooltip("Height above the ground for a LOW fireball the Knight JUMPS over.")]
    [SerializeField] Vector2 lowHeightRange = new Vector2(0.4f, 0.9f);
    [Tooltip("Height above the ground for a HIGH fireball the Knight must DUCK under " +
             "(hits the standing collider, clears the ducking collider; jumping into it still collides).")]
    [SerializeField] Vector2 highHeightRange = new Vector2(1.35f, 1.55f);
    [Range(0f, 1f)]
    [SerializeField] float highFireballChance = 0.5f;

    [Header("Motion")]
    [SerializeField] float fireballSpeed = 4.5f;

    [Header("Frequency ramp")]
    [SerializeField] float firstSpawnDelay = 6f;
    [SerializeField] float initialInterval = 5f;
    [SerializeField] float minInterval = 1.6f;
    [SerializeField] float rampSeconds = 90f;
    [Range(0f, 0.9f)]
    [SerializeField] float intervalJitter = 0.25f;

    [Header("Limits")]
    [SerializeField] int maxAlive = 5;

    [Tooltip("Driven by GameManager. Nothing spawns until the run starts.")]
    [SerializeField] bool spawningEnabled = false;

    [Tooltip("When true, DifficultyManager supplies the interval instead of the internal ramp.")]
    [SerializeField] bool useExternalTuning = false;

    readonly List<GameObject> alive = new List<GameObject>();
    float elapsed;
    float timer;
    float externalInterval = 5f;

    void Start()
    {
        ResolvePlayer();
    }

    void ResolvePlayer()
    {
        if (player == null)
        {
            var knight = Object.FindFirstObjectByType<KnightController>();
            if (knight != null) player = knight.transform;
        }
    }

    /// <summary>GameManager turns spawning on at run start and off on game over.</summary>
    public void SetSpawning(bool on)
    {
        spawningEnabled = on;
        if (!on) return;

        ResolvePlayer();
        elapsed = 0f;
        timer = firstSpawnDelay;
    }

    /// <summary>Destroy every live fireball (called on game over / reset).</summary>
    public void ClearAll()
    {
        foreach (var g in alive)
            if (g != null) Destroy(g);
        alive.Clear();
    }

    // ---- driven by DifficultyManager -----------------------------------
    public void SetTuning(float interval, float highChance, float speed)
    {
        useExternalTuning = true;
        externalInterval = Mathf.Max(0.4f, interval);
        highFireballChance = Mathf.Clamp01(highChance);
        fireballSpeed = Mathf.Max(1f, speed);
    }

    void Update()
    {
        if (!spawningEnabled || player == null || fireballPrefab == null) return;

        elapsed += Time.deltaTime;
        alive.RemoveAll(g => g == null);

        timer -= Time.deltaTime;
        if (timer > 0f || alive.Count >= maxAlive) return;

        // Fairness gate: keep clear of other hazards. If blocked, retry shortly.
        if (!HazardTraffic.TryReserve(player.position.x)) { timer = 0.4f; return; }

        SpawnOne();

        float interval;
        if (useExternalTuning)
        {
            interval = externalInterval;
        }
        else
        {
            float t = rampSeconds > 0f ? Mathf.Clamp01(elapsed / rampSeconds) : 1f;
            interval = Mathf.Lerp(initialInterval, minInterval, t);
        }
        interval *= 1f + Random.Range(-intervalJitter, intervalJitter);
        timer = Mathf.Max(0.25f, interval);
    }

    void SpawnOne()
    {
        bool high = Random.value < highFireballChance;
        Vector2 band = high ? highHeightRange : lowHeightRange;
        float h = Random.Range(band.x, band.y);

        var pos = new Vector3(player.position.x + spawnAheadDistance, groundY + h, 0f);
        var go = Instantiate(fireballPrefab, pos, Quaternion.identity, transform);

        var fb = go.GetComponent<Fireball>();
        if (fb != null) fb.Configure(Vector2.left, fireballSpeed);

        alive.Add(go);
    }

    void OnDrawGizmosSelected()
    {
        if (player == null) return;
        float x = player.position.x + spawnAheadDistance;

        Gizmos.color = new Color(1f, 0.4f, 0f, 0.9f);
        Gizmos.DrawWireCube(
            new Vector3(x, groundY + (lowHeightRange.x + lowHeightRange.y) * 0.5f, 0f),
            new Vector3(0.4f, Mathf.Max(0.05f, lowHeightRange.y - lowHeightRange.x), 0.1f));

        Gizmos.color = new Color(1f, 0.75f, 0f, 0.9f);
        Gizmos.DrawWireCube(
            new Vector3(x, groundY + (highHeightRange.x + highHeightRange.y) * 0.5f, 0f),
            new Vector3(0.4f, Mathf.Max(0.05f, highHeightRange.y - highHeightRange.x), 0.1f));
    }
}
