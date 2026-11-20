using System;
using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum GameOverReason { FellOutOfMap, Collision }

    [SerializeField, Min(0f)] private float gameOverDelay = 1.5f;
    [SerializeField] private string gameOverSceneName = "ScoreScene";

    public bool IsGameOverPending { get; private set; }
    public bool IsGameOver { get; private set; }
    public GameOverReason? PendingReason { get; private set; }
    public event Action<GameOverReason> GameOverDeclared;

    // Called when the whole body leaves the map, or AFTER Collision has finished.
    public void RequestGameOver(GameOverReason reason)
    {
        if (IsGameOverPending || IsGameOver) return;
        IsGameOverPending = true;
        PendingReason = reason;
        StartCoroutine(GameOverAfterDelay(reason));
    }

    private IEnumerator GameOverAfterDelay(GameOverReason reason)
    {
        yield return new WaitForSeconds(gameOverDelay);
        IsGameOver = true;
        IsGameOverPending = false;
        GameOverDeclared?.Invoke(reason);

        if (!Application.CanStreamedLevelBeLoaded(gameOverSceneName))
        {
            Debug.LogError("Game-over scene is missing from Build Settings: " + gameOverSceneName, this);
            yield break;
        }

        if (gameOverSceneName == "ScoreScene" && SceneManager.Instance != null)
            SceneManager.Instance.LoadScore();
        else
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(gameOverSceneName);
    }
}
