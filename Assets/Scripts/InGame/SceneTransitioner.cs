using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitioner : MonoBehaviour
{
    public static SceneTransitioner Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void PerformTransition(string sceneName)
    {
        MonoBehaviour[] cleanupObjects = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
        foreach (var obj in cleanupObjects)
        {
            if (obj is ICleanup cleaner)
            {
                cleaner.Cleanup();
            }
        }

        GameManager.ResetInstance();
        LevelLoader.ResetInstance();
        LeaderboardManager.ResetInstance();

        var runner = FindAnyObjectByType<NetworkRunner>();
        if (runner != null)
        {
            runner.Shutdown();
        }

        SceneManager.LoadScene(sceneName);
    }
}
