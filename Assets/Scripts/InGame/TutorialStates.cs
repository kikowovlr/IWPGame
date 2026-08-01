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
    SuddenDeath,        // teleported to fixed spots, READY SET GO, last player standing
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
            // NOTE: your existing CharacterSelectUIController should activate itself
            // the same way it does in the real game. If it keys off GameManager state,
            // give it a tutorial entry point or have it also listen to TutorialManager.
        }
    }

    public void OnStateUpdate(TutorialManager manager)
    {
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