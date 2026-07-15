using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// state enums to track round state
public enum RoundState
{
    Setup,          // brief buffer to spawn players, "ROUND _" UI, use a 2d background
    Countdown,      // transition to game scene, 3, 2, 1.. countdown (UI ticks, movement and camera locked)
    RoundActive,    // actual gameplay
    RoundOver,      // last player left - round winner, crown awarded, UI displays
    MatchOver       // someone hits 3 crowns, "GAME OVER" UI, transition to win screen podium
}

public interface IRoundState
{
    RoundState StateType { get; }
    void OnStateEnter(GameManager manager);
    void OnStateUpdate(GameManager manager);
    void OnStateExit(GameManager manager);
}

// states
#region STATES IMPLEMENTATION

// brief buffer to spawn players, "ROUND _" UI, use a 2d background
public class SetupState : IRoundState
{
    public RoundState StateType => RoundState.Setup;
    private bool _hasStartedTimer = false;

    public void OnStateEnter(GameManager manager)
    {
        _hasStartedTimer = false;
        Debug.Log("[MATCH ENGINE] -> Entered Setup State.");

        // all players on overview camera mode on scene boot
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SetDefaultBlendStyle(CameraManager.Instance.CutCameraBlend);
            CameraManager.Instance.SetCameraState(CameraManager.CameraMode.StaticOverview);
        }

        // increment round - managed by server
        if (manager.Object.HasStateAuthority)
        {
            manager.IncrementRoundCounter();
            manager.SetGlobalInputRestrictions(InputRestrictions.BlockEverything);
            manager.ResetRoundEntities();
        }
    }

    public void OnStateUpdate(GameManager manager)
    {
        // TODO: test
        if (manager.GetTotalRegisteredCount() < 2)
        {
            if (TransitionUIManager.Instance != null)
            {
                TransitionUIManager.Instance.UpdateLoadingStatus($"Waiting for players... ({manager.GetTotalRegisteredCount()}/2)");
            }
            return;
        }

        if (!_hasStartedTimer)
        {
            // show round ui 
            if (manager.Object.HasStateAuthority)
                manager.SetSetupUIActive(true);

            manager.ResetStateTimer(manager.Settings.SetUpDuration);
            _hasStartedTimer = true;
        }

        // once setup timer finishes, transition to next state
        if (manager.IsStateTimerExpired)
        {
            manager.TransitionToState(RoundState.Countdown, manager.Settings.CountdownDuration);
        }
    }

    public void OnStateExit(GameManager manager)
    {
        Debug.Log("[MATCH ENGINE] -> Exited Setup State.");

        // init leaderboard when setup ends (everyone is connected)
        if (LeaderboardManager.Instance != null)
            LeaderboardManager.Instance.InitialiseLeaderboard();
    }
}

// transition to game scene, 3, 2, 1.. countdown (UI ticks, movement and camera locked)
public class CountdownState : IRoundState
{
    public RoundState StateType => RoundState.Countdown;

    public void OnStateEnter(GameManager manager)
    {
        Debug.Log("[MATCH ENGINE] -> Enter Countdown State.");

        // TODO: Add transition
        if (manager.Object.HasStateAuthority)
        {
            manager.SetSetupUIActive(false);
            manager.ResetStateTimer(manager.Settings.CountdownDuration);
        }

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SetDefaultBlendStyle(CameraManager.Instance.GameplayIntroBlend);
            CameraManager.Instance.SetCameraState(CameraManager.CameraMode.Gameplay);
        }
    }

    public void OnStateUpdate(GameManager manager)
    {
        if (manager.IsStateTimerExpired)
        {
            manager.TransitionToState(RoundState.RoundActive);
        }
    }

    public void OnStateExit(GameManager manager)
    {
        Debug.Log("[MATCH ENGINE] -> Exit Countdown State.");

        // allow controls
        if (manager.Object.HasStateAuthority)
            manager.SetGlobalInputRestrictions(InputRestrictions.None);

        if (CameraManager.Instance != null)
            CameraManager.Instance.SetDefaultBlendStyle(CameraManager.Instance.CutCameraBlend);
    }
}

// actual gameplay
public class RoundActiveState : IRoundState
{
    public RoundState StateType => RoundState.RoundActive;
    private bool _roundEndingSequenceTriggered = false;

    public void OnStateEnter(GameManager manager)
    {
        Debug.Log("[MATCH ENGINE] -> Enter RoundActive State.");
        _roundEndingSequenceTriggered = false;

        // allow controls
        if (manager.Object.HasStateAuthority)
            manager.SetGlobalInputRestrictions(InputRestrictions.None);
    }

    public void OnStateUpdate(GameManager manager)
    {
        if (!manager.Object.HasStateAuthority) return;

        // track how many players alive - see if need to end round or not
        int totalPlayersInMatch = manager.Runner.ActivePlayers.Count();
        int livingPlayers = manager.GetLivingPlayerCount();
        int readySpectators = manager.GetActiveSpectatorCount();

        int expectedSpectators = totalPlayersInMatch - 1;

        // if one player left
        if (livingPlayers == 1)
        {
            // check if all other players turned into spectators alr
            if (readySpectators >= expectedSpectators)
            {
                if (!_roundEndingSequenceTriggered)
                {
                    _roundEndingSequenceTriggered = true;
                    List<PlayerRef> winnerList = manager.GetLivingPlayerIDs();
                    if (winnerList.Count > 0)
                    {
                        manager.SetRoundWinner(winnerList[0]);
                        manager.TransitionToState(RoundState.RoundOver, manager.Settings.RoundOverBufferDuration);
                    }
                }
            }
        }
        // if draw (players died at the same network tick) -> draw: no one wins crown
        else if (livingPlayers == 0)
        {
            // wait until all players turn into spectators then transition
            if (readySpectators >= totalPlayersInMatch)
            {
                if (!_roundEndingSequenceTriggered)
                {
                    _roundEndingSequenceTriggered = true;
                    manager.SetRoundWinner(PlayerRef.None);
                    manager.TransitionToState(RoundState.RoundOver, manager.Settings.RoundOverBufferDuration);
                }
            }
        }
    }

    public void OnStateExit(GameManager manager)
    {
        Debug.Log("[MATCH ENGINE] -> Exit RoundActive State.");
    }
}

// last player left - round winner, crown awarded, UI displays
// round over UI - "Round _ over!" ovelay
// show full leaderboard on the left side
// animate the changing of rankings??
public class RoundOverState : IRoundState
{
    public RoundState StateType => RoundState.RoundOver;

    public void OnStateEnter(GameManager manager)
    {
        Debug.Log("[MATCH ENGINE] -> Enter RoundOver State.");

        if (manager.Object.HasStateAuthority)
        {
            manager.SetGlobalInputRestrictions(InputRestrictions.BlockEverything);
        }

        PlayerRef winner = manager.LastRoundWinner;
        List<LeaderboardItemData> snapshot = new List<LeaderboardItemData>();
        var runner = manager.Object.Runner;

        // reconstruct a raw snapshot directly from networked properties
        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (runner.TryGetPlayerObject(player, out NetworkObject playerObj))
            {
                PlayerComponentRegistry registry = playerObj.GetComponent<PlayerComponentRegistry>();
                if (registry != null && registry.Stats != null)
                {
                    string pName = !string.IsNullOrEmpty(registry.Stats.PlayerName)
                        ? registry.Stats.PlayerName
                        : $"Player {player.PlayerId}";

                    Sprite pIcon = (registry.ActiveCharacterLinker != null && registry.ActiveCharacterLinker._characterData != null)
                        ? registry.ActiveCharacterLinker._characterData.CharacterIcon
                        : null;

                    int currentCrowns = registry.Stats.CrownCount;

                    // If the client's network buffer already received the updated crown count 
                    // BEFORE OnStateEnter fires, we safely step it down by 1 to match the host's timeline.
                    if (player == winner && !manager.Object.HasStateAuthority && currentCrowns > 0)
                    {
                        currentCrowns -= 1;
                    }

                    snapshot.Add(new LeaderboardItemData(player, pName, pIcon, currentCrowns));
                }
            }
        }

        snapshot = snapshot
            .OrderByDescending(item => item.CrownCount)
            .ThenBy(item => item.PlayerReference.PlayerId)
            .ToList();

        if (manager.RoundEndDisplay != null)
        {
            manager.RoundEndDisplay.ShowRoundOverSequence(
                snapshot,
                winner,
                manager.CurrentRoundNumber
            );
        }

        // award crown
        if (manager.Object.HasStateAuthority && manager.LastRoundWinner != PlayerRef.None)
        {
            manager.AwardCrownToPlayer(manager.LastRoundWinner);
        }
    }

    public void OnStateUpdate(GameManager manager)
    {
        if (!manager.Object.HasStateAuthority) return;

        // go to next round setup once display buffer time expires
        if (manager.IsStateTimerExpired)
        {
            // check if shld transition to match over
            if (manager.IsMatchOver())
                manager.TransitionToState(RoundState.MatchOver, manager.Settings.MatchOverBufferDuration);
            else
                manager.TransitionToState(RoundState.Setup, manager.Settings.SetUpDuration);
        }
    }

    public void OnStateExit(GameManager manager)
    {
        Debug.Log("[MATCH ENGINE] -> Exit RoundOver State.");

        if (manager.RoundEndDisplay != null)
            manager.RoundEndDisplay.gameObject.SetActive(false);
    }
}

// someone hits 3 crowns, "GAME OVER" UI, transition to win screen podium
public class MatchOverState : IRoundState
{
    public RoundState StateType => RoundState.MatchOver;

    public void OnStateEnter(GameManager manager)
    {
        Debug.Log("[MATCH ENGINE] -> Enter MatchOver State.");

        if (manager.Object.HasStateAuthority)
        {
            manager.SetGlobalInputRestrictions(InputRestrictions.BlockEverything);
        }

        // show victory or defeat UI
        if (manager.RoundEndDisplay != null)
        {
            // find out which player won
            PlayerRef matchWinner = manager.GetOverallMatchWinner();
            PlayerRef localPlayer = manager.Object.Runner.LocalPlayer;

            if (localPlayer != matchWinner)
            {
                // display defeat
                manager.RoundEndDisplay.ShowMatchDefeatOverlay();
            }
            else
            {
                manager.RoundEndDisplay.ShowMatchVictoryOverlay();
            }
        }

    }

    public void OnStateUpdate(GameManager manager)
    {
        if (!manager.Object.HasStateAuthority) return;

        if (manager.IsStateTimerExpired)
        {
            // grab path of scene
            string targetPath = manager.Settings.PodiumScene.ScenePath;
            int sceneBuildIndex = SceneUtility.GetBuildIndexByScenePath(targetPath);

            if (sceneBuildIndex != -1)
                manager.StartCoroutine(TransitionAndLoad(manager, sceneBuildIndex));
        }
    }

    private IEnumerator TransitionAndLoad(GameManager manager, int sceneBuildIndex)
    {
        // tell all clients to play transition
        if (LevelLoader.Instance != null)
            yield return manager.StartCoroutine(LevelLoader.Instance.FadeToBlack()); // transition call

        manager.Object.Runner.LoadScene(SceneRef.FromIndex(sceneBuildIndex), LoadSceneMode.Single);
    }
        
    public void OnStateExit(GameManager manager)
    {
        Debug.Log("[MATCH ENGINE] -> Exit MatchOver State.");

        if (manager.RoundEndDisplay != null)
            manager.RoundEndDisplay.gameObject.SetActive(false);
    }
}

#endregion
