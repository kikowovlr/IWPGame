using Fusion;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// handles photon fusion 2 startup sequence and scene loading
/// persists across scene changed automatically
/// </summary>
[RequireComponent(typeof(NetworkRunner))]
public class NetworkLauncher : MonoBehaviour
{
    private NetworkRunner _runner;

    private void Awake()
    {
        _runner = GetComponent<NetworkRunner>();
    }

    /// <summary>
    /// kicks off connection sequence and loads target gameplay scene
    /// </summary>
    public async Task<StartGameResult> LaunchSession(GameMode mode, int gameplaySceneIndex)
    {
        _runner.ProvideInput = true;

        // configure default scene manager container if missing
        var sceneManager = GetComponent<NetworkSceneManagerDefault>();
        if (sceneManager == null)
        {
            sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
        }

        // start fusion game session and initiate scene change
        var result = await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = "DebugRoom", // TODO: hardcoded room name
            Scene = SceneRef.FromIndex(gameplaySceneIndex),
            SceneManager = sceneManager
        });

        return result;
    }
}
