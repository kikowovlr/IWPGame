using UnityEngine;

/// <summary>
/// TUTORIAL FLOW
/// == CharacterSelect -> TutorialActive -> SuddenDeath -> Victory
/// players have health and can be knocked out but cannot be ELIMINATED until sudden death
/// </summary>

public enum TutorialState
{
    None,
    CharacterSelect,
    TutorialActive,     // step-by-step objectives, infinite lives
    Countdown,          // teleported to fixed spots, READY SET GO
    SuddenDeath,        // last player standing
    TutorialStageOver   // victory overlay -> back to lobby/main menu
}

public interface ITutorialState
{
    TutorialState StateType { get; }
    void OnStateEnter(TutorialManager manager);
    void OnStateUpdate(TutorialManager manager);
    void OnStateExit(TutorialManager manager);
}

public class TutorialCharacterSelectState : ITutorialState
{
    public TutorialState StateType => TutorialState.CharacterSelect;

    public void OnStateEnter(TutorialManager manager)
    {
        Debug.Log("[TUTORIAL] -> CharacterSelect");

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SetDefaultBlendStyle(CameraManager.Instance.CutCameraBlend);
            CameraManager.Instance.SetCameraState(CameraManager.CameraMode.CharacterSelect);
        }

        if (manager.Object.HasStateAuthority)
        {
            manager.SetGlobalInputRestrictions(InputRestrictions.BlockEverything);
            manager.ResetStateTimer(manager.CharacterSelectDuration);
        }
    }

    public void OnStateUpdate(TutorialManager manager)
    {
        if (!manager.Object.HasStateAuthority) return;

        // advance when everyone's ready, or the timer runs out (auto-lock stragglers)
        if (manager.AreAllPlayersReady() || manager.IsStateTimerExpired)
        {
            if (manager.IsStateTimerExpired)
                manager.AutoLockUnreadyPlayers();

            manager.TransitionToState(TutorialState.TutorialActive);
        }

        if (!manager.IsInFinalCharacterSelectCountdown
            && manager.GetRemainingStateTime() <= manager.Settings.FinalCharacterSelectCountdown)
        {
            manager.SetFinalCountdown(true);
        }
    }

    public void OnStateExit(TutorialManager manager)
    {
    }
}

public class TutorialActiveState : ITutorialState
{
    public TutorialState StateType => TutorialState.TutorialActive;

    public void OnStateEnter(TutorialManager manager)
    {
    }

    public void OnStateUpdate(TutorialManager manager)
    {
        throw new System.NotImplementedException();
    }

    public void OnStateExit(TutorialManager manager)
    {
    }
}

public class TutorialSuddenDeathState : ITutorialState
{
    public TutorialState StateType => TutorialState.SuddenDeath;

    public void OnStateEnter(TutorialManager manager)
    {
    }

    public void OnStateUpdate(TutorialManager manager)
    {
    }

    public void OnStateExit(TutorialManager manager)
    {
    }
}

public class TutorialStageOverState : ITutorialState
{
    public TutorialState StateType => TutorialState.TutorialStageOver;

    public void OnStateEnter(TutorialManager manager)
    {
    }

    public void OnStateUpdate(TutorialManager manager)
    {
    }

    public void OnStateExit(TutorialManager manager)
    {
    }
}