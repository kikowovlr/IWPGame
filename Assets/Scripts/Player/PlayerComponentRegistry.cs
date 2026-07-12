using UnityEngine;

public class PlayerComponentRegistry : MonoBehaviour
{
    [Tooltip("Access Player Components through this script")]
    [SerializeField] private NetworkPlayerController _playerController;
    [SerializeField] private PlayerHealthHandler _healthHandler;
    [SerializeField] private PlayerEliminationHandler _eliminationHandler;
    [SerializeField] private PunchHandler _punchHandler;
    [SerializeField] private KickHandler _kickHandler;
    [SerializeField] private HeadbuttHandler _headbuttHandler;
    [SerializeField] private PlayerVFXAnchors _vfxAnchors;
    [SerializeField] private PlayerVisualsOverrideController _visualOverrideController;
    [SerializeField] private NetworkPlayerStats _playerStats;

    public NetworkPlayerController Controller => _playerController;
    public PlayerHealthHandler Health => _healthHandler;
    public PlayerEliminationHandler Elimination => _eliminationHandler;
    public PunchHandler Punch => _punchHandler;
    public KickHandler Kick => _kickHandler;
    public HeadbuttHandler Headbutt => _headbuttHandler;
    public IAffectedByStatusEffects Status { get; private set; }
    public PlayerVFXAnchors vfxAnchors => _vfxAnchors;
    public PlayerVisualsOverrideController VisualsOverrider => _visualOverrideController;
    public NetworkPlayerStats Stats => _playerStats;

    private void Awake()
    {
        Status = GetComponentInChildren<IAffectedByStatusEffects>();
    }
}
 