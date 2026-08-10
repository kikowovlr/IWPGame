using UnityEngine;
public class HUDCoordinator : MonoBehaviour
{
    [SerializeField] private GameObject _playerHUDRoot;          
    [SerializeField] private GameObject _spectatorRoot;
    [SerializeField] private GameObject _miniLeaderboardRoot;
    [SerializeField] private GameObject _tutorialHUDRoot;

    private bool _isSpectating = false;
    private bool _lastGameplay = false;

    private void OnEnable()
    {
        PlayerEliminationHandler.OnPlayerSpectatorReady += HandleSpectatorReady;
        PlayerEliminationHandler.OnPlayerEliminated += HandleEliminationChanged;
    }

    private void OnDisable()
    {
        PlayerEliminationHandler.OnPlayerSpectatorReady -= HandleSpectatorReady;
        PlayerEliminationHandler.OnPlayerEliminated -= HandleEliminationChanged;
    }

    private void Start()
    {
        HideAllHUDs();
    }

    private void Update()
    {
        var ctx = MatchContext.Current;
        if (ctx == null || !ctx.IsSpawned) return;

        // reevaluate whenever tutorial state changes -> every frame when tutorial manager is active
        if (TutorialManager.Instance != null)
        {
            Reevaluate();
        }
        else
        {
            bool current = MatchContext.Current.IsInGameplayPhase;
            if (current != _lastGameplay)
            {
                _lastGameplay = current;
                Reevaluate();
            }
        }
    }

    private void HandleSpectatorReady(PlayerEliminationHandler handler)
    {
        // only react to the LOCAL player's transition
        if (!handler.Object.HasInputAuthority) return;
        _isSpectating = true;
        Reevaluate();
    }

    private void HandleEliminationChanged(PlayerEliminationHandler handler)
    {
        if (!handler.Object.HasInputAuthority) return;

        // local player is back in play -> restore the gameplay HUD.
        if (!handler.IsEliminated)
        {
            _isSpectating = false;
            Reevaluate();
        }
    }

    private void Reevaluate()
    {
        // change based on specific tutorial state if tutorial manager is active
        if (TutorialManager.Instance != null)
        {
            ReevaluateTutorial(TutorialManager.Instance.CurrentState);
            return;
        }

        bool isGameplay = MatchContext.Current != null && MatchContext.Current.IsInGameplayPhase;

        if (!isGameplay)
        {
            HideAllHUDs();
            return;
        }

        if (_isSpectating)
        {
            SetGameplayHUDActive(false);
            if (_spectatorRoot != null) _spectatorRoot.SetActive(true);
        }
        else
        {
            SetGameplayHUDActive(true);
            if (_spectatorRoot != null) _spectatorRoot.SetActive(false);
        }
    }

    /// <summary>
    /// tutorial specific HUD
    /// </summary>
    private void ReevaluateTutorial(TutorialState state)
    {
        switch (state)
        {
            case TutorialState.TutorialActive:
                // only the tutorial HUD; no player HUD, spectator, or leaderboard
                SetPlayerHUDActive(false);
                if (_spectatorRoot != null) _spectatorRoot.SetActive(false);
                if (_miniLeaderboardRoot != null) _miniLeaderboardRoot.SetActive(false);
                break;

            case TutorialState.Countdown:
            case TutorialState.SuddenDeath:
                // player HUD (+ spectator when spectating); no tutorial HUD, no leaderboard
                if (_miniLeaderboardRoot != null) _miniLeaderboardRoot.SetActive(false);

                if (_isSpectating)
                {
                    SetPlayerHUDActive(false);
                    if (_spectatorRoot != null) _spectatorRoot.SetActive(true);
                }
                else
                {
                    SetPlayerHUDActive(true);
                    if (_spectatorRoot != null) _spectatorRoot.SetActive(false);
                }
                break;

            case TutorialState.CharacterSelect:
            case TutorialState.TutorialStageOver:
            case TutorialState.None:
            default:
                // hide everything here
                HideAllHUDs();
                break;
        }
    }

    private void HideAllHUDs()
    {
        _isSpectating = false;
        if (_spectatorRoot != null) _spectatorRoot.SetActive(false);
        SetGameplayHUDActive(false);
    }

    private void SetGameplayHUDActive(bool active)
    {
        if (_playerHUDRoot != null) _playerHUDRoot.SetActive(active);
        if (_miniLeaderboardRoot != null) _miniLeaderboardRoot.SetActive(active);
    }

    // player HUD alone (tutorial sudden death uses this WITHOUT the leaderboard)
    private void SetPlayerHUDActive(bool active)
    {
        if (_playerHUDRoot != null) _playerHUDRoot.SetActive(active);
    }
}
