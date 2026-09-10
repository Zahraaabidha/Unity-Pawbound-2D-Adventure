using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Distance-based spawner: drops a Skeleton on the ground a fixed distance ahead
/// of the Knight every time the Knight has run a randomised gap further. Because
/// spawns always happen <see cref="spawnAheadDistance"/> in front of the player,
/// a Skeleton is never placed on top of the player and there is always time to
/// see it and jump. Stale Skeletons are pruned so nothing accumulates.
/// </summary>
public class SkeletonSpawner : MonoBehaviour
{
    [Header("Prefab & Target")]
    [SerializeField] GameObject skeletonPrefab;
    [SerializeField] Transform player;

    [Header("Placement")]
    [Tooltip("How far ahead of the player each Skeleton appears.")]
    [SerializeField] float spawnAheadDistance = 20f;
    [Tooltip("World Y of the ground surface (Skeleton pivot is bottom-centre).")]
    [SerializeField] float groundY = -4f;
    [Tooltip("Player must run a random distance in this range between spawns.")]
    [SerializeField] Vector2 spawnGapRange = new Vector2(9f, 16f);

    [Header("Limits")]
    [SerializeField] int maxAlive = 6;
    [SerializeField] bool spawnOnStart = true;

    [Tooltip("Driven by GameManager. Nothing spawns until the run starts.")]
    [SerializeField] bool spawningEnabled = false;

    readonly List<GameObject> alive = new List<GameObject>();
    float lastSpawnPlayerX;
    float nextGap;

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
        lastSpawnPlayerX = player != null ? player.position.x : 0f;
        nextGap = Random.Range(spawnGapRange.x, spawnGapRange.y);

        if (spawnOnStart && player != null && skeletonPrefab != null)
        {
            SpawnOne();
            lastSpawnPlayerX = player.position.x;
            nextGap = Random.Range(spawnGapRange.x, spawnGapRange.y);
        }
    }

    /// <summary>Destroy every live Skeleton (called on game over / reset).</summary>
    public void ClearAll()
    {
        foreach (var g in alive)
            if (g != null) Destroy(g);
        alive.Clear();
    }

    // ---- driven by DifficultyManager -------------------------------------
    public void SetSpawnGapRange(Vector2 range) => spawnGapRange = range;
    public void SetMaxAlive(int n) => maxAlive = Mathf.Max(1, n);

    void Update()
    {
        if (!spawningEnabled || player == null || skeletonPrefab == null) return;

        alive.RemoveAll(g => g == null);

        if (alive.Count >= maxAlive) return;

        if (player.position.x - lastSpawnPlayerX >= nextGap)
        {
            // Fairness gate: keep clear of other hazards. If blocked, retry next frame.
            if (!HazardTraffic.TryReserve(player.position.x)) return;

            SpawnOne();
            lastSpawnPlayerX = player.position.x;
            nextGap = Random.Range(spawnGapRange.x, spawnGapRange.y);
        }
    }

    void SpawnOne()
    {
        var pos = new Vector3(player.position.x + spawnAheadDistance, groundY, 0f);
        var go = Instantiate(skeletonPrefab, pos, Quaternion.identity, transform);
        alive.Add(go);
    }

    void OnDrawGizmosSelected()
    {
        if (player == null) return;
        Gizmos.color = Color.red;
        var p = new Vector3(player.position.x + spawnAheadDistance, groundY + 1f, 0f);
        Gizmos.DrawWireCube(p, new Vector3(0.6f, 2f, 0.1f));
    }
}
