using UnityEngine;

/// <summary>
/// Tiny shared gate that keeps Skeleton and Fireball spawns from bunching up.
/// Both spawners call <see cref="TryReserve"/> with the player's current world X
/// before they instantiate; a spawn only goes through once the player has moved
/// at least <see cref="minSeparation"/> further than the previous hazard, so any
/// two hazards are always separated by that much player progress (≈ that many
/// seconds ÷ run speed of reaction time).
///
/// <see cref="DifficultyManager"/> shrinks <see cref="minSeparation"/> as the run
/// gets harder. It is not procedural generation — just a fairness spacer.
/// </summary>
public static class HazardTraffic
{
    static float lastHazardPlayerX = float.NegativeInfinity;
    static float minSeparation = 10f;

    // Static state persists between Play sessions in the Editor - clear it every launch.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        lastHazardPlayerX = float.NegativeInfinity;
        minSeparation = 10f;
    }

    public static void SetMinSeparation(float units) => minSeparation = Mathf.Max(0f, units);

    public static void Reset() => lastHazardPlayerX = float.NegativeInfinity;

    /// <summary>Returns true (and records the position) if a hazard may spawn now.</summary>
    public static bool TryReserve(float playerX)
    {
        if (playerX - lastHazardPlayerX < minSeparation) return false;
        lastHazardPlayerX = playerX;
        return true;
    }
}
