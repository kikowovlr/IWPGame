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
    [SerializeField] private RespawnHandler _respawnHandler;
    [SerializeField] private PlayerBuoyancy _buoyancy;
    [SerializeField] private GooExposure _gooExposure;
    [SerializeField] private PlayerDrowning _drowning;
    [SerializeField] private PlayerCharacterSelect _characterSelect;
    [SerializeField] private PlayerBoost _boost;
    [SerializeField] private PlayerTutorialProgress _progress;
    [SerializeField] private PlayerStamina _stamina;
    [SerializeField] private StatusEffectManager _statusEffectManager;
    [SerializeField] private PlayerFallHandler _fall;
    [SerializeField] private PlayerCombatAudio _combatAudio;
    [SerializeField] private PlayerAbilityAudio _abilityAudio;
    [SerializeField] private PlayerAbilityAudioNetworked _abilityAudioNet;
    [SerializeField] private PlayerImpactAudio _impactAudio;

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
    public CharacterComponentLinker ActiveCharacterLinker { get; private set; }
    public RespawnHandler RespawnHandler => _respawnHandler;
    public PlayerBuoyancy Buoyancy => _buoyancy;
    public GooExposure Goo => _gooExposure;
    public PlayerDrowning Drowning => _drowning;
    public PlayerCharacterSelect CharacterSelect => _characterSelect;
    public PlayerBoost Boost => _boost;
    public PlayerTutorialProgress Progress => _progress;
    public PlayerStamina Stamina => _stamina;
    public StatusEffectManager StatusManager => _statusEffectManager;
    public PlayerFallHandler Fall => _fall;
    public PlayerCombatAudio CombatAudio => _combatAudio;
    public PlayerAbilityAudio AbilityAudio => _abilityAudio;
    public PlayerAbilityAudioNetworked AbilityAudioNet => _abilityAudioNet;
    public PlayerImpactAudio ImpactAudio => _impactAudio;

    private void Awake()
    {
        Status = GetComponentInChildren<IAffectedByStatusEffects>();
    }

    public void UpdateActiveLinker(CharacterComponentLinker newLinker)
    {
        ActiveCharacterLinker = newLinker;
    }
}
 