using UnityEngine;
public class HUDCoordinator : MonoBehaviour
{
    [SerializeField] private GameObject _playerHUDRoot;          
    [SerializeField] private GameObject _spectatorRoot;
    [SerializeField] private GameObject _miniLeaderboardRoot;

    private bool _isSpectating = false;
    private RoundState _lastKnownState = RoundState.None;

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
        // poll round state (GameManager is networked; no C# event for state change
        // that this local UI can rely on cross-scene, so a light poll is simplest)
        if (GameManager.Instance == null || !GameManager.Instance.IsSpawned) return;

        RoundState current = GameManager.Instance.CurrentRoundState;
        if (current != _lastKnownState)
        {
            _lastKnownState = current;
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
        RoundState state = GameManager.Instance != null
                ? GameManager.Instance.CurrentRoundState
                : RoundState.None;

        bool isGameplay = state == RoundState.Countdown || state == RoundState.RoundActive;

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
