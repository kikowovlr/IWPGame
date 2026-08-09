using Fusion;
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

public enum TutorialStepPhase
{
    Intro,          // TUTORIAL STAGE! popup, input blocked
    Narrating,      // typewriter typing, no step tracking yet
    Tracking,       // side panel slides in, players perform action
    StepComplete    // Step Done! Moving on... panel about to slide out
}

public enum SuddenDeathPhase
{
    None,
    Banner,     // "SUDDEN DEATH!" overlay, input blocked, teleport+seed happen here
    Countdown,  // READY / SET (driven via ICountdownSource)
    Fighting,   // GO fired, input unblocked, walls down, real KOs, poll for winner
    Done        // winner decided
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
            manager.TeleportPlayersToCharacterSelectStage();
            manager.ResetAllPlayersCharacterSelectState();
            manager.SetFinalCharacterSelectCountdown(false);
            manager.ResetStateTimer(manager.CharacterSelectDuration);
        }

        TransitionUIManager.OnTransitionComplete -= HandleTransitionComplete; // remove before adding just in case
        TransitionUIManager.OnTransitionComplete += HandleTransitionComplete;
    }

    public void OnStateUpdate(TutorialManager manager)
    {
        if (!manager.Object.HasStateAuthority) return;

        if (!manager.IsInFinalCharacterSelectCountdown)
        {
            bool allReady = manager.AreAllPlayersReady();

            if (allReady)
            {
                manager.BeginFinalCharacterSelectCountdown();
            }
            else if (manager.IsStateTimerExpired)
            {
                // auto-lock everyone still selecting using whatever character is currently hovered
                manager.AutoLockUnreadyPlayers();
                manager.BeginFinalCharacterSelectCountdown();
            }
        }
        else
        {
            if (manager.IsStateTimerExpired)
            {
                manager.MarkCharacterSelectComplete();
                manager.BeginTransitionToTutorialActive();
            }
        }
    }

    public void OnStateExit(TutorialManager manager)
    {
        Debug.Log("[TUTORIAL] -> Exit CharacterSelect");
        TransitionUIManager.OnTransitionComplete -= HandleTransitionComplete; // clean up just in case
    }

    private void HandleTransitionComplete()
    {
        SoundManager.Instance?.PlayMusic(MusicID.CharacterSelect);
        // one-shot: stop listening once we've played
        TransitionUIManager.OnTransitionComplete -= HandleTransitionComplete;
    }
}

public class TutorialActiveState : ITutorialState
{
    public TutorialState StateType => TutorialState.TutorialActive;

    public void OnStateEnter(TutorialManager manager)
    {
        Debug.Log("[TUTORIAL] -> TutorialActive");

        // every client plays circle wipe + shows intro popup locally
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SetDefaultBlendStyle(CameraManager.Instance.CutCameraBlend);
            CameraManager.Instance.SetCameraState(CameraManager.CameraMode.Gameplay);
        }

        if (manager.TutorialUI != null)
            manager.TutorialUI.ShowIntroPopup(); // "TUTORIAL STAGE!" —> local visual

        if (manager.Object.HasStateAuthority)
        {
            manager.SetGlobalInputRestrictions(InputRestrictions.BlockEverything);
            manager.SetStepPhase(TutorialStepPhase.Intro, manager.Settings.TutorialIntroDuration);
            manager.ResetStepIndexToStart();
        }

        SoundManager.Instance?.PlayMusic(MusicID.Tutorial);
    }

    public void OnStateUpdate(TutorialManager manager)
    {
        if (!manager.Object.HasStateAuthority) return;

        switch (manager.CurrentStepPhase)
        {
            case TutorialStepPhase.Intro:
                // intro popup done -> unblock input, start first narration
                if (!manager.IsPhaseTimerExpired) return;
                manager.SetGlobalInputRestrictions(InputRestrictions.None);
                manager.SetStepPhase(TutorialStepPhase.Narrating, manager.GetNarrationDuration());
                break;
            case TutorialStepPhase.Narrating:
                if (!manager.IsPhaseTimerExpired) return;
                // narration done -> start tracking (side panel slides in)
                manager.SetStepPhase(TutorialStepPhase.Tracking);
                break;
            case TutorialStepPhase.Tracking:
                // gated on completion
                if (manager.HaveAllPlayersCompletedCurrentStep())
                    manager.SetStepPhase(TutorialStepPhase.StepComplete, manager.Settings.StepCompleteDisplayDuration);
                break;
            case TutorialStepPhase.StepComplete:
                if (!manager.IsPhaseTimerExpired) return;
                // "moving on" hold done -> next step or finish
                if (manager.IsOnLastStep)
                {
                    if (manager.IsSoloTutorial())
                    {
                        // skip sudden death
                        manager.MarkSoloTutorial();
                        manager.TransitionToState(TutorialState.TutorialStageOver);
                    }
                    else
                    {
                        manager.TransitionToState(TutorialState.SuddenDeath);
                    }
                }
                else
                {
                    manager.AdvanceStep();
                    manager.SetStepPhase(TutorialStepPhase.Narrating, manager.GetNarrationDuration());
                }
                break;
        }
    }

    public void OnStateExit(TutorialManager manager)
    {
        Debug.Log("[TUTORIAL] -> Exit TutorialActive");
        if (manager.TutorialUI != null)
            manager.TutorialUI.HideAll();

        // clean up
        manager.DespawnBots(); 
    }
}

public class TutorialSuddenDeathState : ITutorialState
{
    public TutorialState StateType => TutorialState.SuddenDeath;

    public void OnStateEnter(TutorialManager manager)
    {
        Debug.Log("[TUTORIAL] -> SuddenDeath");

        if (manager.Object.HasStateAuthority)
        {
            manager.SetGlobalInputRestrictions(InputRestrictions.BlockEverything);
            manager.SetWallsActive(true);
            manager.SetSuddenDeathPhase(SuddenDeathPhase.Banner, manager.Settings.SuddenDeathBannerDuration);
        }

        SoundManager.Instance?.PlayMusic(MusicID.SuddenDeath, 1.5f);
    }

    public void OnStateUpdate(TutorialManager manager)
    {
        if (!manager.Object.HasStateAuthority) return;

        switch (manager.CurrentSuddenDeathPhase)
        {
            case SuddenDeathPhase.Banner:
                if (!manager.IsStateTimerExpired) return;
                // teleport + seed living players WHILE still covered/blocked, then start countdown
                manager.TeleportPlayersToSuddenDeathPositions();
                manager.SeedLivingPlayersForSuddenDeath();
                manager.RestoreFullHealthAllPlayers();
                manager.SetSuddenDeathPhase(SuddenDeathPhase.Countdown, manager.Settings.CountdownDuration);
                break;

            case SuddenDeathPhase.Countdown:
                if (!manager.IsStateTimerExpired) return;
                // GO: unblock input, drop walls (handled in OnSuddenDeathPhaseChanged), enable real KOs
                manager.SetGlobalInputRestrictions(InputRestrictions.None);
                manager.SetSuddenDeathPhase(SuddenDeathPhase.Fighting);
                break;

            case SuddenDeathPhase.Fighting:
                // last player standing wins
                int living = manager.GetLivingPlayerCount();
                if (living <= 1)
                {
                    PlayerRef winner = living == 1
                        ? manager.GetLastLivingPlayer()
                        : manager.PickRandomWinnerFallback();  // 0 alive = same-tick, random
                    manager.SetMatchWinner(winner);
                    manager.SetSuddenDeathPhase(SuddenDeathPhase.Done);
                    manager.TransitionToState(TutorialState.TutorialStageOver);
                }
                break;
        }
    }

    public void OnStateExit(TutorialManager manager)
    {
        Debug.Log("[TUTORIAL] -> Exit SuddenDeath");
    }
}

public class TutorialStageOverState : ITutorialState
{
    public TutorialState StateType => TutorialState.TutorialStageOver;

    public void OnStateEnter(TutorialManager manager)
    {
        Debug.Log("[TUTORIAL] -> TutorialStageOver");

        if (manager.Object.HasStateAuthority)
            manager.SetGlobalInputRestrictions(InputRestrictions.BlockEverything);

        if (manager.WasSoloTutorial)
        {
            // solo run - no winner, just TUTORIAL DONE
            if (manager.RoundEndDisplay != null)
                manager.RoundEndDisplay.ShowTutorialSoloComplete();
        }
        else
        {
            // local win/lose result (every client evaluates for itself)
            PlayerRef winner = manager.MatchWinner;
            bool localWon = manager.LocalPlayerIsWinner(winner);
            string winnerName = manager.GetPlayerName(winner);

            if (manager.RoundEndDisplay != null)
                manager.RoundEndDisplay.ShowTutorialResult(localWon, winnerName);
        }

        if (manager.Object.HasStateAuthority)
            manager.ResetStateTimer(manager.Settings.MatchOverBufferDuration);
    }

    public void OnStateUpdate(TutorialManager manager)
    {
        if (!manager.Object.HasStateAuthority) return;
        if (!manager.IsStateTimerExpired) return;

        manager.ReturnToLobby();
    }

    public void OnStateExit(TutorialManager manager)
    {
    }
}