using Fusion;
using Fusion.Statistics;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// handles photon fusion 2 startup sequence and scene loading
/// persists across scene changed automatically
/// </summary>
public class NetworkLauncher : MonoBehaviour
{
    public static NetworkLauncher Instance { get; private set; }
    [SerializeField] private NetworkRunner _runnerPrefab;
    public NetworkRunner Runner { get; private set; }
    public bool HasSkippedInitialSceneLoad { get; set; } = false;
    public bool IsReturningFromTutorial { get; set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// kicks off connection sequence and loads target gameplay scene
    /// </summary>
    public async Task<StartGameResult> LaunchSession(GameMode mode, int gameplaySceneIndex, string sessionName)
    {
        // DIAGNOSTIC
        if (Runner == null)
            Debug.Log("[NetworkLauncher] LaunchSession start — Runner is NULL (clean)");
        else
            Debug.Log($"[NetworkLauncher] LaunchSession start — Runner not null, IsRunning={Runner.IsRunning}, GO={(Runner.gameObject != null ? "alive" : "destroyed")}");

        HasSkippedInitialSceneLoad = false;

        if (Runner != null)
        {
            try
            {
                if (Runner.IsRunning)
                    await Runner.Shutdown();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NetworkLauncher] Old Runner shutdown threw (likely already disconnected) — ignoring: {e.Message}");
            }
            finally
            {
                if (Runner != null && Runner.gameObject != null)
                    Destroy(Runner.gameObject);
                Runner = null;
            }
        }

        Runner = Instantiate(_runnerPrefab);
        Runner.ProvideInput = true;
        DontDestroyOnLoad(Runner.gameObject);

        // configure default scene manager container if missing
        var sceneManager = Runner.GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null)
        {
            sceneManager = Runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        }

        // start fusion game session and initiate scene change
        var result = await Runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = sessionName,
            Scene = SceneRef.FromIndex(gameplaySceneIndex),
            SceneManager = sceneManager
        });

        return result;
    }

    /// <summary>
    /// tear down the current runner. Call this whenever a session ends
    /// (forced disconnect, leave, return to menu) so the next LaunchSession starts clean.
    /// </summary>
    public async Task CleanupRunner()
    {
        if (Runner != null)
        {
            try
            {
                if (Runner.IsRunning)
                    await Runner.Shutdown();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[NetworkLauncher] Cleanup shutdown threw (ignoring): {e.Message}");
            }
            finally
            {
                if (Runner != null && Runner.gameObject != null)
                    Destroy(Runner.gameObject);
                Runner = null;
            }
        }
        HasSkippedInitialSceneLoad = false;
    }
}
