using Fusion;
using UnityEngine;

public class PlayerEliminationHandler : NetworkBehaviour
{
    [Header("Life Settings")]
    [SerializeField] private int _maxLives;
    [Networked] public int CurrentLives { get; private set; }

    private PlayerHealthHandler _healthHandler;
    private NetworkPlayerController _playerController;

    private void Awake()
    {
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
        {
            _healthHandler = registry.Health;
            _playerController = registry.Controller;
        }
    }
}
