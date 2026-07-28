using System.Collections;
using Fusion;
using UnityEngine;

public class GooPuddle : NetworkBehaviour
{
    // use heartbeat to continue inflicting the status as long as in goo
    // 0.1s serves as a small buffer so the status doesnt flicker due to network ticks
    private const float HeartbeatDuration = 0.1f;
    [SerializeField] private float _puddleGooRate = 0.55f;

    [Header("Telegraph")]
    [SerializeField] private GameObject _visualRoot; // actual puddle mesh
    [SerializeField] private ParticleSystem _telegraphVFX; // vfx particle

    [Header("Growth Anim")]
    [SerializeField] private AnimationCurve _growCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float _growDuration = 1.0f;

    [Networked, OnChangedRender(nameof(OnActiveChanged))] private NetworkBool IsActive { get; set; }
    [Networked] private TickTimer _telegraphTimer { get; set; }

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
        if (_telegraphVFX != null)
            _telegraphVFX.Play();
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
        }
    }

    /// <summary>
    /// enable goo puddle after the vfx finishes
    /// </summary>
    private void OnActiveChanged()
    {
        if (!IsActive) return;

        if (_visualRoot != null)
        {
            _visualRoot.SetActive(true);
            StartCoroutine(GrowPuddleRoutine());
        }
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

    private void OnTriggerStay(Collider other)
    {
        if (!IsActive) return; // dont trigger anything during telegraph window

        if (other.TryGetComponent(out PlayerComponentRegistry registry))
        {
            if (registry.Controller.Object != null && registry.Controller.Object.HasStateAuthority)
            {
                registry.Goo.ApplyExposure(_puddleGooRate);
                // constantly refresh slowness duration 
                registry.Status.InflictStatus(StatusEffectType.Slowness, HeartbeatDuration);
            }
        }
    }
}
