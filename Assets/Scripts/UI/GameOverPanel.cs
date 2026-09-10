using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Game Over screen. <see cref="GameManager"/> calls <see cref="Show"/> with
/// the final score (unchanged signature); the high score and "new record" flag
/// are read straight from <see cref="ScoreManager"/>. The RESTART / MAIN MENU
/// buttons are wired (persistent listeners) to <see cref="GameManager"/>.
/// </summary>
public class GameOverPanel : MonoBehaviour
{
    [SerializeField] ScoreManager scoreManager;
    [SerializeField] Text scoreText;
    [SerializeField] Text hiScoreText;
    [SerializeField] GameObject newHighScoreBadge;

    void Awake()
    {
        if (scoreManager == null) scoreManager = Object.FindFirstObjectByType<ScoreManager>();
    }

    public void Show(int finalScore)
    {
        if (scoreManager == null) scoreManager = Object.FindFirstObjectByType<ScoreManager>();

        int hi = scoreManager != null ? scoreManager.HighScore : finalScore;
        bool isNewRecord = scoreManager != null && scoreManager.NewHighScoreThisRun;

        if (scoreText != null) scoreText.text = "SCORE " + finalScore.ToString("D5");
        if (hiScoreText != null) hiScoreText.text = "HI-SCORE " + hi.ToString("D5");
        if (newHighScoreBadge != null) newHighScoreBadge.SetActive(isNewRecord);

        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);
}
