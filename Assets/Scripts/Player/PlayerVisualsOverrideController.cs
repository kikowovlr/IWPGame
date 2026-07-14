using Fusion;
using UnityEngine;

public class PlayerVisualsOverrideController : NetworkBehaviour
{
    [Header("Visuals Controllers")]
    [SerializeField] private StatusEffectManager _statusEffectManager;
    [SerializeField] private PlayerAbilityVisuals _abilityVisuals;

    private GameObject _activeCharacterVisualRoot;
    private NetworkPlayerController _playerController;

    [Networked, OnChangedRender(nameof(OnVisualStateChanged))]
    private NetworkBool IsVisualsEnabled { get; set; } = true;

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

    public override void Spawned()
    {
        ToggleSuppressionGating(!IsVisualsEnabled);
        if (_activeCharacterVisualRoot != null)
        {
            _activeCharacterVisualRoot.SetActive(IsVisualsEnabled);
        }
    }

    public void UpdateActiveCharacterVisualReference(GameObject visualRoot)
    {
        _activeCharacterVisualRoot = visualRoot;
    }

    private void HandlePlayerEliminated(PlayerEliminationHandler handler)
    {
        if (handler.Object != this.Object) return; // ensure only this player handles their own death by checking network object

        ToggleSuppressionGating(true);
    }

    private void ToggleSuppressionGating(bool suppress)
    {
        // shut down vfx and indicators
        if (_statusEffectManager != null)
            _statusEffectManager.SuppressVisuals = suppress;
        if (_abilityVisuals != null)
            _abilityVisuals.SuppressVisuals = suppress;
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
        if (!Object.HasStateAuthority) return;

        // setting this fires OnVisualStateChanged for all clients
        if (IsVisualsEnabled)
        {
            EvaluateVisualState(true);
        }
        else
        {
            IsVisualsEnabled = true;
        }
    }

    private void OnVisualStateChanged()
    {
        EvaluateVisualState(IsVisualsEnabled);
    }

    public void EvaluateVisualState(bool areVisualsActive)
    {
        ToggleSuppressionGating(!areVisualsActive);

        if (_activeCharacterVisualRoot != null)
        {
            _activeCharacterVisualRoot.SetActive(areVisualsActive);
        }

        if (_playerController != null)
        {
            if (areVisualsActive)
            {
                _playerController.EnableAllRagdollColliders();
                _playerController.Recover(playAnim: false);
            }
            else
            {
                _playerController.DisableAllRigidbodyColliders();
            }
        }
    }
}
