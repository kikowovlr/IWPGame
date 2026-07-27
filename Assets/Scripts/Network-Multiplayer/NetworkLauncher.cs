using Fusion;
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
}
