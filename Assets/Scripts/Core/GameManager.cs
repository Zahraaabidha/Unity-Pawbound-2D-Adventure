using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Single owner of the Pawbound game flow. Lives in the gameplay scene.
///
/// Flow: the MainMenu scene shows the menu; pressing START loads the gameplay
/// scene, where this manager drives <see cref="GameState.Playing"/> →
/// <see cref="GameState.GameOver"/>. RESTART reloads the gameplay scene (a clean
/// slate, so nothing can be duplicated); MAIN MENU loads the menu scene.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Scenes")]
    [SerializeField] string mainMenuSceneName = "MainMenu";
    [SerializeField] string gameplaySceneName = "SampleScene";

    [Header("Gameplay references")]
    [SerializeField] KnightController knight;
    [SerializeField] SkeletonSpawner skeletonSpawner;
    [SerializeField] FireballSpawner fireballSpawner;
    [SerializeField] ScoreManager scoreManager;

    [Header("UI")]
    [SerializeField] GameObject hudRoot;
    [SerializeField] GameOverPanel gameOverPanel;

    public GameState State { get; private set; } = GameState.Playing;
    public event Action<GameState> StateChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (knight != null) knight.Died -= HandleKnightDied;
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (knight == null) knight = UnityEngine.Object.FindFirstObjectByType<KnightController>();
        if (scoreManager == null) scoreManager = UnityEngine.Object.FindFirstObjectByType<ScoreManager>();
        if (skeletonSpawner == null) skeletonSpawner = UnityEngine.Object.FindFirstObjectByType<SkeletonSpawner>();
        if (fireballSpawner == null) fireballSpawner = UnityEngine.Object.FindFirstObjectByType<FireballSpawner>();

        if (knight != null) knight.Died += HandleKnightDied;

        BeginRun();
    }

    void BeginRun()
    {
        State = GameState.Playing;
        Time.timeScale = 1f;

        if (scoreManager != null) scoreManager.Begin();
        if (skeletonSpawner != null) skeletonSpawner.SetSpawning(true);
        if (fireballSpawner != null) fireballSpawner.SetSpawning(true);

        if (hudRoot != null) hudRoot.SetActive(true);
        if (gameOverPanel != null) gameOverPanel.Hide();

        StateChanged?.Invoke(State);
    }

    void HandleKnightDied() => EnterGameOver();

    void EnterGameOver()
    {
        if (State == GameState.GameOver) return;
        State = GameState.GameOver;

        if (scoreManager != null) scoreManager.StopScoring();

        if (skeletonSpawner != null) { skeletonSpawner.SetSpawning(false); skeletonSpawner.ClearAll(); }
        if (fireballSpawner != null) { fireballSpawner.SetSpawning(false); fireballSpawner.ClearAll(); }

        if (gameOverPanel != null)
            gameOverPanel.Show(scoreManager != null ? scoreManager.Score : 0);

        StateChanged?.Invoke(State);
    }

    // ---- button hooks (persistent listeners) -------------------------------
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
