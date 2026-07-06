using Fusion;
using UnityEngine;

public class PlayerVisualsOverrideController : NetworkBehaviour
{
    [Header("Visuals Controllers")]
    [SerializeField] private StatusEffectManager _statusEffectManager;
    [SerializeField] private PlayerAbilityVisuals _abilityVisuals;

    private GameObject _activeCharacterVisualRoot;
    private NetworkPlayerController _playerController;

    private void Awake()
    {
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
        {
            _playerController = registry.Controller;
        }
    }

    private void OnEnable()
    {
        PlayerEliminationHandler.OnPlayerEliminated += HandlePlayerEliminated;
        PlayerEliminationHandler.OnPlayerSpectatorReady += HandleSpectatorReady;
    }

    private void OnDisable()
    {
        PlayerEliminationHandler.OnPlayerEliminated -= HandlePlayerEliminated;
        PlayerEliminationHandler.OnPlayerSpectatorReady -= HandleSpectatorReady;
    }

    public void UpdateActiveCharacterVisualReference(GameObject visualRoot)
    {
        _activeCharacterVisualRoot = visualRoot;
    }

    private void HandlePlayerEliminated(PlayerEliminationHandler handler)
    {
        if (handler.Object != this.Object) return; // ensure only this player handles their own death by checking network object

        // shut down vfx and indicators
        if (_statusEffectManager != null)
            _statusEffectManager.SuppressVisuals = true;

        if (_abilityVisuals != null)
            _abilityVisuals.SuppressVisuals = true;
    }

    private void HandleSpectatorReady(PlayerEliminationHandler handler)
    {
        if (handler.Object != this.Object) return;

        // hide currently active model
        if (_activeCharacterVisualRoot != null)
        {
            _activeCharacterVisualRoot.SetActive(false);
        }

        if (_playerController != null)
        {
            _playerController.DisableAllRigidbodyColliders();
        }
    }

    /// <summary>
    /// call this from game manager when a new round begins
    /// activates all visuals again
    /// </summary>
    public void EnableVisuals()
    {
        if (_statusEffectManager != null) _statusEffectManager.SuppressVisuals = false;
        if (_abilityVisuals != null) _abilityVisuals.SuppressVisuals = false;

        if (_activeCharacterVisualRoot != null)
        {
            _activeCharacterVisualRoot.SetActive(true);
        }

        if (_playerController != null)
        {
            _playerController.EnableAllRagdollColliders();
            _playerController.Recover(playAnim: false);
        }
    }
}
