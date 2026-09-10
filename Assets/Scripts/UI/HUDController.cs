using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mirrors the live score and persistent high score into the top-right HUD.
/// The high-score line is styled as secondary; it tints gold once the current
/// run is setting a new record.
/// </summary>
public class HUDController : MonoBehaviour
{
    [SerializeField] ScoreManager scoreManager;
    [SerializeField] Text scoreText;
    [SerializeField] Text hiScoreText;

    static readonly Color HiScoreNormal = new Color(0.75f, 0.78f, 0.85f);
    static readonly Color HiScoreRecord = new Color(1f, 0.85f, 0.30f);

    void Start()
    {
        if (scoreManager == null) scoreManager = Object.FindFirstObjectByType<ScoreManager>();
    }

    void Update()
    {
        if (scoreManager == null) return;

        if (scoreText != null)
            scoreText.text = "SCORE " + scoreManager.Score.ToString("D5");

        if (hiScoreText != null)
        {
            hiScoreText.text = "HI-SCORE " + scoreManager.HighScore.ToString("D5");
            hiScoreText.color = scoreManager.NewHighScoreThisRun ? HiScoreRecord : HiScoreNormal;
        }
    }
}
