using UnityEngine;
public class HUDCoordinator : MonoBehaviour
{
    [SerializeField] private GameObject _playerHUDRoot;          
    [SerializeField] private GameObject _spectatorRoot;
    [SerializeField] private GameObject _miniLeaderboardRoot;

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
        if (MatchContext.Current == null || !MatchContext.Current.IsSpawned) return;

        bool current = MatchContext.Current.IsInGameplayPhase;
        if (current != _lastGameplay)
        {
            _lastGameplay = current;
            Reevaluate();
        }
    }

    private void HandleSpectatorReady(PlayerEliminationHandler handler)
    {
        // only react to the LOCAL player's transition
        if (!handler.Object.HasInputAuthority) return;
        _isSpectating = true;
        ShowSpectatorUI();
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
        //RoundState state = GameManager.Instance != null
        //        ? GameManager.Instance.CurrentRoundState
        //        : RoundState.None;

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

    private void ShowSpectatorUI()
    {
        if (_playerHUDRoot != null) _playerHUDRoot.SetActive(false);
        if (_spectatorRoot != null) _spectatorRoot.SetActive(true);
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
}
