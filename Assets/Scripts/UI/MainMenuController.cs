using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Controls the MainMenu scene. START loads the gameplay scene.</summary>
public class MainMenuController : MonoBehaviour
{
    [SerializeField] string gameplaySceneName = "SampleScene";
    [SerializeField] Text hiScoreText;

    void Start()
    {
        if (hiScoreText != null)
        {
            int hi = PlayerPrefs.GetInt(ScoreManager.HighScoreKey, 0);
            hiScoreText.text = "HI-SCORE " + hi.ToString("D5");
        }
    }

    public void StartGame() => SceneManager.LoadScene(gameplaySceneName);

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
