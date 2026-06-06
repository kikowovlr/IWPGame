using UnityEngine;

public class PlayerComponentRegistry : MonoBehaviour
{
    [Tooltip("Access Player Components through this script")]
    [SerializeField] private NetworkPlayerController _playerController;
    [SerializeField] private PlayerHealthHandler _healthHandler;
    [SerializeField] private PlayerEliminationHandler _eliminationHandler;

    public NetworkPlayerController Controller => _playerController;
    public PlayerHealthHandler Health => _healthHandler;
    public PlayerEliminationHandler Elimination => _eliminationHandler;
}
