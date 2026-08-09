using Fusion;
using Fusion.Addons.Physics;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Unity.Collections.Unicode;

public class PodiumSceneController : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform _firstPlaceSpawn;
    [SerializeField] private Transform _secondPlaceSpawn;
    [SerializeField] private Transform _thirdPlaceSpawn;
    [SerializeField] private Collider _crowdZone; // for rest of the players not on podium

    [SerializeField] private GameObject _crownPrefab;

    private void Start()
    {
        // Run the initialization safely in a coroutine to prevent race conditions
        StartCoroutine(SafeNetworkInitRoutine());
        CameraManager.Instance.RequestCursorVisible("PodiumScene", true);

        SoundManager.Instance?.PlayMusic(MusicID.Podium);
    }

    private IEnumerator SafeNetworkInitRoutine()
    {
        NetworkRunner runner = null;

        while (runner == null)
        {
            runner = FindAnyObjectByType<NetworkRunner>();
            if (runner == null) yield return null;
        }
        while (!runner.IsRunning) yield return null;
        while (runner.ActivePlayers.Count() == 0) yield return null;

        InitializePodium(runner);
    }

    public void InitializePodium(NetworkRunner runner)
    {
        StartCoroutine(PodiumSequenceRoutine(runner));
    }

    private IEnumerator PodiumSequenceRoutine(NetworkRunner runner)
    {
        yield return StartCoroutine(WaitForNetworkToSettle(runner)); // wait for syncing

        if (runner.TryGetPlayerObject(runner.LocalPlayer, out NetworkObject localPlayerObj))
            PlayerRegistry.RegisterLocalPlayerTransform(localPlayerObj.transform);

        if (GameManager.Instance != null)
            GameManager.Instance.SetGlobalInputRestrictions(InputRestrictions.None);

        if (CameraManager.Instance != null)
            CameraManager.Instance.SetCameraState(CameraManager.CameraMode.Gameplay);

        SpawnCrownLocally(runner);

        if (LevelLoader.Instance != null)
            LevelLoader.Instance.TransitionIn();
    }

    private IEnumerator WaitForNetworkToSettle(NetworkRunner runner)
    {
        yield return null; // give unity a frame to register physical scene changes

        while (!runner.TryGetPlayerObject(runner.LocalPlayer, out var localPlayerObjplayer))
        {
            yield return null; // check again next frame
        }

        yield return new WaitForEndOfFrame(); // wait for extra frame to ensure interpolation finishes
    }

    private void SpawnCrownLocally(NetworkRunner runner)
    {
        var leaderboard = LeaderboardManager.Instance.GetSortedLeaderboard;
        if (leaderboard == null || leaderboard.Count == 0) return;

        PlayerRef winner = leaderboard[0].PlayerReference;
        if (!runner.TryGetPlayerObject(winner, out NetworkObject winnerObj)) return;

        PlayerComponentRegistry registry = winnerObj.GetComponent<PlayerComponentRegistry>();
        if (registry == null || registry.vfxAnchors == null) return;

        Transform head = registry.vfxAnchors.Head;
        if (head == null) return;

        GameObject crown = Instantiate(_crownPrefab);
        HoveringCrown hover = crown.GetComponent<HoveringCrown>();
        if (hover == null) hover = crown.AddComponent<HoveringCrown>();
        hover.SetTarget(head);
    }

    private void OnDestroy()
    {
        CameraManager.Instance.RequestCursorVisible("EndGamePodiumScene", false); // release req    
    }
}
