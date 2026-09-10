using UnityEngine;

/// <summary>
/// Survival score plus a persistent arcade high score.
///
/// Current score counts up while the run is active and resets on <see cref="Begin"/>.
/// The high score is loaded from <see cref="HighScoreKey"/> on Awake, updated in
/// memory while Playing, and written back to PlayerPrefs when the run ends or this
/// object is torn down (scene reload / quit) — never every frame.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    /// <summary>PlayerPrefs key for the persistent high score.</summary>
    public const string HighScoreKey = "Pawbound_HighScore";

    [SerializeField] float pointsPerSecond = 10f;

    public int Score { get; private set; }
    public int HighScore { get; private set; }

    /// <summary>True once the current run has strictly beaten the high score it started with.</summary>
    public bool NewHighScoreThisRun { get; private set; }

    float raw;
    bool running;
    int highScoreAtRunStart;

    void Awake()
    {
        HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
    }

    public void Begin()
    {
        raw = 0f;
        Score = 0;
        running = true;
        NewHighScoreThisRun = false;
        highScoreAtRunStart = HighScore;
    }

    public void StopScoring()
    {
        running = false;
        SaveHighScore();
    }

    void Update()
    {
        if (!running) return;

        raw += pointsPerSecond * Time.deltaTime;
        Score = Mathf.FloorToInt(raw);

        if (Score > HighScore)
        {
            HighScore = Score;                       // in-memory only; persisted on run end / teardown
            if (Score > highScoreAtRunStart)
                NewHighScoreThisRun = true;
        }
    }

    void SaveHighScore()
    {
        if (PlayerPrefs.GetInt(HighScoreKey, 0) == HighScore) return;
        PlayerPrefs.SetInt(HighScoreKey, HighScore);
        PlayerPrefs.Save();
    }

    void OnDestroy() => SaveHighScore();
    void OnApplicationQuit() => SaveHighScore();
}
