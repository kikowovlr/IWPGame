using Fusion;
using UnityEngine;

/// <summary>
/// tracks culmulative goo exposure from any source
/// drive screen consumption effect + death
/// </summary>
public class GooExposure : NetworkBehaviour
{
    [HideInInspector] [Networked] public float ExposureAmount {  get; private set; }// 0 = clean, 1 = fully consumed
    [HideInInspector][Networked] public NetworkBool IsInGooSource { get; private set; }

    [SerializeField] private float _recoveryRate = 0.15f;
    private bool _fedThisTick = false;

    private PlayerHealthHandler _healthHandler;
    private PlayerDrowning _drowning;
    private PlayerBuoyancy _buoyancy;

    private void Awake()
    {
        PlayerComponentRegistry registry = transform.root.GetComponent<PlayerComponentRegistry>();
        if (registry != null)
        {
            _healthHandler = registry.Health;
            _drowning = registry.Drowning;
            _buoyancy = registry.Buoyancy;
        }
    }

    /// <summary>
    /// any goo source calls this 
    /// </summary>
    public void ApplyExposure(float ratePerSecond)
    {
        _fedThisTick = true;
        IsInGooSource = true;
        ExposureAmount = Mathf.Clamp01(ExposureAmount + ratePerSecond * Runner.DeltaTime);
    }

    /// <summary>
    /// controller calls this once per tick after all sources fed
    /// handles recovery when not fed this tick
    /// </summary>
    public void EndTick()
    {
        if (!_fedThisTick)
        {
            IsInGooSource = false;
            ExposureAmount = Mathf.Clamp01(ExposureAmount - _recoveryRate * Runner.DeltaTime); // recovery from goo consumption
        }

        _fedThisTick = false;

        if (Object.HasStateAuthority && ExposureAmount >= 1f)
            OnFullyConsumed();
    }

    private void OnFullyConsumed()
    {
        // sink to oceanfloor when dead and in water
        if (_buoyancy.IsSubmerged)
            _drowning.BeginSink();

        _healthHandler.Rpc_EnvironmentalEliminate();
        ExposureAmount = 0f;
    }

    public override void Render()
    {
        // only drive local player's own screen overlay
        if (!Object.HasInputAuthority) return;
        if (ScreenGooOverlay.Instance == null) return;

        if (ExposureAmount > 0.01f)
            ScreenGooOverlay.Instance.SetGooExposure(1f);
        else
            ScreenGooOverlay.Instance.StopGoo();
    }
}
