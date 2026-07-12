using Fusion;
using NUnit.Framework;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

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
        // TODO: Ensure network synchronization before kicking off countdown
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

    public void OnStateEnter(GameManager manager)
    {
        Debug.Log("[MATCH ENGINE] -> Enter RoundActive State.");

        // allow controls
        if (manager.Object.HasStateAuthority)
            manager.SetGlobalInputRestrictions(InputRestrictions.None);
    }

    public void OnStateUpdate(GameManager manager)
    {
        if (!manager.Object.HasStateAuthority) return;

        // track how many players alive - see if need to end round or not
        int playersRemaining = manager.GetLivingPlayerCount();

        // if one player left
        if (playersRemaining == 1)
        {
            List<PlayerRef> winnerList = manager.GetLivingPlayerIDs();

            if (winnerList.Count > 0)
            {
                PlayerRef roundWinner = winnerList[0];

                manager.AwardCrownToPlayer(roundWinner);
            }
        }
        // if draw (players died at the same network tick) -> draw: no one wins crown
        else if (playersRemaining == 0)
        {
            Utils.DebugLog("[SERVER] -> Condition Met: Zero survivors left standing. Processing draw round sequence.");
        }
    }

    public void OnStateExit(GameManager manager)
    {
        Debug.Log("[MATCH ENGINE] -> Exit RoundActive State.");

        // TODO show round over UI
        // TODO only trigger round over when the last player that was eliminated has became a spectator -> go by spectator count??
        if (manager.Object.HasStateAuthority)
            manager.TransitionToState(RoundState.RoundOver, manager.Settings.RoundOverBufferDuration);
    }
}

// last player left - round winner, crown awarded, UI displays
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
    }

    public void OnStateExit(GameManager manager)
    {
    }

    public void OnStateUpdate(GameManager manager)
    {
    }
}

// someone hits 3 crowns, "GAME OVER" UI, transition to win screen podium
public class MatchOverState : IRoundState
{
    public RoundState StateType => RoundState.MatchOver;

    public void OnStateEnter(GameManager manager)
    {
    }

    public void OnStateExit(GameManager manager)
    {
    }

    public void OnStateUpdate(GameManager manager)
    {
    }
}

#endregion
