using Fusion;
using System;
using UnityEngine;

public enum LobbyMode
{
    Random = 0,   // random real map, normal game flow
    Tutorial = 1  // fixed tutorial map, tutorial flow
}

/// <summary>
/// holds networked selected mode for lobby
/// put on same prefab as lobby manager
/// </summary>
public class LobbyMapSelection : NetworkBehaviour
{
    public static LobbyMapSelection Instance { get; private set; }

    [SerializeField] private MapCatalog _catalog;
    
    [HideInInspector] [Networked, OnChangedRender(nameof(OnModeChanged))] public LobbyMode SelectedMode {  get; private set; }
    [HideInInspector] [Networked, OnChangedRender(nameof(OnResolvedChanged))] public int ResolvedSceneIndex { get; private set; }

    public static event Action<LobbyMode> OnSelectionChanged;

    public MapCatalog Catalog => _catalog;

    public override void Spawned()
    {
        Instance = this;
        if (Object.HasStateAuthority)
        {
            SelectedMode = LobbyMode.Random; // default
            ResolvedSceneIndex = -1;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasStateAuthority)
    {
        if (Instance == this) Instance = null;
    }

    public void SetModeAsHost(LobbyMode mode)
    {
        if (!Object.HasStateAuthority) return;
        SelectedMode = mode;
    }

    public void CommitResolvedScene()
    {
        if (!Object.HasStateAuthority) return;
        if (_catalog == null) { ResolvedSceneIndex = -1; return; }

        ResolvedSceneIndex = SelectedMode == LobbyMode.Tutorial
            ? _catalog.GetTutorialSceneIndex()
            : _catalog.GetRandomMapSceneIndex();  
    }

    private void OnResolvedChanged()
    {
        // fires on all clients — copy into the persistent cache before this object dies
        if (PendingMapCache.Instance != null)
            PendingMapCache.Instance.Set(ResolvedSceneIndex);
    }

    public string GetModeLabel()
    {
        if (_catalog == null) return SelectedMode.ToString().ToUpperInvariant();
        return SelectedMode == LobbyMode.Tutorial
            ? _catalog.TutorialModeLabel
            : _catalog.RandomModeLabel;
    }

    private void OnModeChanged()
    {
        OnSelectionChanged?.Invoke(SelectedMode);
    }
}
