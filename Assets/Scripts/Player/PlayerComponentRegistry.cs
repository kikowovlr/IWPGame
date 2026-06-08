using UnityEngine;

public class PlayerComponentRegistry : MonoBehaviour
{
    [Tooltip("Access Player Components through this script")]
    [SerializeField] private NetworkPlayerController _playerController;
    [SerializeField] private PlayerHealthHandler _healthHandler;
    [SerializeField] private PlayerEliminationHandler _eliminationHandler;
    [SerializeField] private PunchHandler _punchHandler;
    [SerializeField] private KickHandler _kickHandler;

    public NetworkPlayerController Controller => _playerController;
    public PlayerHealthHandler Health => _healthHandler;
    public PlayerEliminationHandler Elimination => _eliminationHandler;
    public PunchHandler Punch => _punchHandler;
    public KickHandler Kick => _kickHandler;
}
