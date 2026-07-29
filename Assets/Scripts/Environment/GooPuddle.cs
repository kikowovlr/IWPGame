using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GooPuddle : NetworkBehaviour
{
    // use heartbeat to continue inflicting the status as long as in goo
    // 0.1s serves as a small buffer so the status doesnt flicker due to network ticks
    private const float HeartbeatDuration = 0.1f;
    [SerializeField] private float _puddleGooRate = 0.55f;

    [Header("Telegraph")]
    [SerializeField] private GameObject _visualRoot; // actual puddle mesh
    [SerializeField] private GooRainVFXController _rainVFX; // vfx particle
    [SerializeField] private Vector2 _rainAreaSize = new Vector2(2f, 2f);

    [Header("Growth Anim")]
    [SerializeField] private AnimationCurve _growCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float _growDuration = 1.0f;

    [Header("Lifetime")]
    [SerializeField] private float _puddleMinLifeTime = 10f;
    [SerializeField] private float _puddleMaxLifeTime = 15f;
    [SerializeField] private float _shrinkDuration = 0.6f;
    [SerializeField] private AnimationCurve _shrinkCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Networked] private TickTimer _lifetimeTimer { get; set; }
    private bool _isShrinking = false;

    [Networked, OnChangedRender(nameof(OnActiveChanged))] private NetworkBool IsActive { get; set; }
    [Networked] private TickTimer _telegraphTimer { get; set; }
    private Dictionary<PlayerComponentRegistry, int> _lastAppliedTick = new Dictionary<PlayerComponentRegistry, int>();
    private FallingIslandPiece _parentTile;

    private void Awake()
    {
        if (_visualRoot != null) _visualRoot.SetActive(false); // disables mesh + collider
    }

    /// <summary>
    /// call right after spawning - warning window before puddle is dangerous
    /// </summary>
    public void BeginTelegraph(float duration)
    {
        if (!Object.HasStateAuthority) return;

        _telegraphTimer = TickTimer.CreateFromSeconds(Runner, duration);

        // fires on every client onChangedRender, but VFX shld play insta, not on server
        RPC_PlayTelegrahVFX();
    }

    /// <summary>
    /// call if puddle is spawning on a breakable tile - despawns with it when it sinks
    /// </summary>
    public void AttachToTile(FallingIslandPiece tile)
    {
        _parentTile = tile;
        transform.SetParent(tile.transform, worldPositionStays: true);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayTelegrahVFX()
    {
        if (_rainVFX != null)
        {
            _rainVFX.SetAreaSize(_rainAreaSize);
            _rainVFX.Play();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (!IsActive)
        {
            if (_telegraphTimer.Expired(Runner))
                IsActive = true; // triggers OnActiveChanged on every client
        }

        // if tile has gone fully underwater, despawn along with it
        if (_parentTile != null && _parentTile.HasEngagedSinking)
        {
            Runner.Despawn(Object);
            return;
        }

        // natural lifetime expiry - start shrinking
        if (IsActive && !_isShrinking && _lifetimeTimer.Expired(Runner))
        {
            _isShrinking = true;
            RPC_BeginShrink();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_BeginShrink()
    {
        StartCoroutine(ShrinkThenDespawnRoutine());
    }

    private IEnumerator ShrinkThenDespawnRoutine()
    {
        Vector3 startScale = _visualRoot.transform.localScale;
        float elapsed = 0f;

        while (elapsed < _shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = _shrinkCurve.Evaluate(elapsed / _shrinkDuration);

            _visualRoot.transform.localScale = new Vector3(
                startScale.x * t,
                startScale.y,
                startScale.z * t
            );
            yield return null;
        }

        // only the state authority actually despawns the networked object -> clients just finish playing the local shrink animation and stop there
        if (Object.HasStateAuthority)
            Runner.Despawn(Object);
    }

    /// <summary>
    /// enable goo puddle after the vfx finishes
    /// </summary>
    private void OnActiveChanged()
    {
        if (!IsActive) return;

        if (_rainVFX != null)
            _rainVFX.Stop();

        if (_visualRoot != null)
        {
            _visualRoot.SetActive(true);
            StartCoroutine(GrowPuddleRoutine());
        }

        if (Object.HasStateAuthority)
            _lifetimeTimer = TickTimer.CreateFromSeconds(Runner, Random.Range(_puddleMinLifeTime, _puddleMaxLifeTime));
    }

    private IEnumerator GrowPuddleRoutine()
    {
        Vector3 targetScale = _visualRoot.transform.localScale;
        float elapsed = 0f;

        while (elapsed < _growDuration)
        {
            elapsed += Time.deltaTime;
            float t = _growCurve.Evaluate(elapsed / _growDuration);

            _visualRoot.transform.localScale = new Vector3(
                targetScale.x * t,
                targetScale.y,      // height untouched
                targetScale.z * t
            );

            yield return null;
        }

        _visualRoot.transform.localScale = targetScale;
    }

    public void HandleTriggerStay(Collider other)
    {
        if (!IsActive) return; // dont trigger anything during telegraph window
        if (!Object.HasStateAuthority) return;

        if (other.TryGetComponent(out PlayerComponentRegistry registry))
        {
            int currentTick = Runner.Tick.Raw;

            // ensure each puddle hits players only ONCE per tick even with multiple colliders
            if (_lastAppliedTick.TryGetValue(registry, out int lastTick) && lastTick == currentTick)
                return;

            _lastAppliedTick[registry] = currentTick;

            registry.Goo.ApplyExposure(_puddleGooRate);
            // constantly refresh slowness duration 
            registry.Status.InflictStatus(StatusEffectType.Slowness, HeartbeatDuration);
        }
    }
}
