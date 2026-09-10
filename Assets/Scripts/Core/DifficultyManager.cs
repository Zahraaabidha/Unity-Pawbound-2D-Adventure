using UnityEngine;

/// <summary>
/// Survival-time difficulty curve. Coordinates the <b>existing</b>
/// <see cref="SkeletonSpawner"/> and <see cref="FireballSpawner"/> (it never
/// spawns anything itself) by pushing tuned values into them every
/// <see cref="applyInterval"/> seconds while the run is Playing.
///
/// It listens to <see cref="GameManager.StateChanged"/>: the timer runs only in
/// <see cref="GameState.Playing"/>, resets on each new Playing, and stops on
/// Game Over. A scene reload (Restart / Main Menu) gives a fresh instance, so
/// difficulty always starts from stage 0.
///
/// Every stage value is an editable array indexed against <see cref="stageTimes"/>
/// (seconds); values are linearly interpolated between stage points.
/// </summary>
public class DifficultyManager : MonoBehaviour
{
    [Header("Coordinated systems (auto-found if empty)")]
    [SerializeField] SkeletonSpawner skeletonSpawner;
    [SerializeField] FireballSpawner fireballSpawner;
    [SerializeField] KnightController knight;

    [Header("Stage boundaries (seconds)")]
    [SerializeField] float[] stageTimes = { 0f, 15f, 30f, 60f, 90f, 120f };

    [Header("Skeleton spawn gap (world units, min / max)")]
    [SerializeField] float[] skeletonGapMin = { 16f, 14f, 11f, 9f, 7f, 6f };
    [SerializeField] float[] skeletonGapMax = { 24f, 20f, 17f, 14f, 12f, 10f };
    [SerializeField] int[] skeletonMaxAlive = { 3, 4, 4, 5, 6, 6 };

    [Header("Fireball spawn interval (seconds)")]
    [SerializeField] float[] fireballInterval = { 5.5f, 4.5f, 3.6f, 2.8f, 2.2f, 1.8f };

    [Header("Fireball HIGH chance (0..1)")]
    [SerializeField] float[] highFireballChance = { 0.35f, 0.40f, 0.45f, 0.50f, 0.50f, 0.55f };

    [Header("Fireball speed (units/sec) - conservative")]
    [SerializeField] float[] fireballSpeed = { 4.5f, 4.5f, 4.6f, 4.85f, 5.2f, 5.5f };

    [Header("Minimum hazard separation (units of player progress)")]
    [SerializeField] float[] minHazardSeparation = { 10f, 9f, 8f, 7f, 6f, 6f };

    [Header("Run speed (small late increase)")]
    [SerializeField] float[] runSpeed = { 6f, 6f, 6f, 6.2f, 6.4f, 6.5f };

    [Header("Apply rate")]
    [SerializeField] float applyInterval = 0.5f;

    public float Elapsed { get; private set; }

    bool running;
    float applyTimer;
    GameManager gm;

    void Start()
    {
        AutoResolve();

        gm = GameManager.Instance;
        if (gm != null)
        {
            gm.StateChanged += OnStateChanged;
            OnStateChanged(gm.State);   // catch the current state if we started late
        }

        ApplyStage(0f);                 // ensure the easy stage-0 values are in place immediately
    }

    void OnDestroy()
    {
        if (gm != null) gm.StateChanged -= OnStateChanged;
        HazardTraffic.Reset();
    }

    void AutoResolve()
    {
        if (skeletonSpawner == null) skeletonSpawner = Object.FindFirstObjectByType<SkeletonSpawner>();
        if (fireballSpawner == null) fireballSpawner = Object.FindFirstObjectByType<FireballSpawner>();
        if (knight == null) knight = Object.FindFirstObjectByType<KnightController>();
    }

    void OnStateChanged(GameState state)
    {
        if (state == GameState.Playing)
        {
            Elapsed = 0f;
            applyTimer = 0f;
            running = true;
            HazardTraffic.Reset();
            ApplyStage(0f);
        }
        else
        {
            running = false;   // Game Over / anything else: freeze progression
        }
    }

    void Update()
    {
        if (!running) return;

        Elapsed += Time.deltaTime;
        applyTimer -= Time.deltaTime;
        if (applyTimer <= 0f)
        {
            applyTimer = applyInterval;
            ApplyStage(Elapsed);
        }
    }

    void ApplyStage(float t)
    {
        if (skeletonSpawner != null)
        {
            skeletonSpawner.SetSpawnGapRange(new Vector2(Sample(skeletonGapMin, t), Sample(skeletonGapMax, t)));
            skeletonSpawner.SetMaxAlive(SampleInt(skeletonMaxAlive, t));
        }

        if (fireballSpawner != null)
        {
            fireballSpawner.SetTuning(
                Sample(fireballInterval, t),
                Sample(highFireballChance, t),
                Sample(fireballSpeed, t));
        }

        HazardTraffic.SetMinSeparation(Sample(minHazardSeparation, t));

        if (knight != null) knight.SetRunSpeed(Sample(runSpeed, t));
    }

    // ---- interpolation across stageTimes ----------------------------------
    float Sample(float[] values, float t)
    {
        if (values == null || values.Length == 0) return 0f;
        if (stageTimes == null || stageTimes.Length == 0) return values[0];

        int n = Mathf.Min(values.Length, stageTimes.Length);
        if (t <= stageTimes[0]) return values[0];
        if (t >= stageTimes[n - 1]) return values[n - 1];

        for (int i = 1; i < n; i++)
        {
            if (t <= stageTimes[i])
            {
                float span = Mathf.Max(0.0001f, stageTimes[i] - stageTimes[i - 1]);
                float u = (t - stageTimes[i - 1]) / span;
                return Mathf.Lerp(values[i - 1], values[i], u);
            }
        }
        return values[n - 1];
    }

    int SampleInt(int[] values, float t)
    {
        if (values == null || values.Length == 0) return 1;
        var f = new float[values.Length];
        for (int i = 0; i < values.Length; i++) f[i] = values[i];
        return Mathf.Max(1, Mathf.RoundToInt(Sample(f, t)));
    }
}
