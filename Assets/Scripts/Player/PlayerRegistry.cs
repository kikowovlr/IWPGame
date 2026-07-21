using System;
using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;
using Fusion;

public static class PlayerRegistry
{
    public static event Action<Transform> OnLocalPlayerSpawned;
    public static CinemachineBrain SceneBrain { get; private set; }
    public static CinemachineCamera SceneVirtualCamera { get; private set; }
    public static Transform LocalPlayerTransform { get; private set; }

    // local client lookup map matching network ids to the player root obj 
    private static readonly Dictionary<PlayerRef, Transform> _playerAvatarTransforms = new Dictionary<PlayerRef, Transform>();
    private static readonly Dictionary<Transform, PlayerDrowning> _drowningByPivot = new Dictionary<Transform, PlayerDrowning>();

    public static void RegisterLocalPlayerTransform(Transform transform)
    {
        LocalPlayerTransform = transform;
        OnLocalPlayerSpawned?.Invoke(transform);
    }

    public static void RegisterCameraSystem(CinemachineBrain brain, CinemachineCamera vcam)
    {
        SceneBrain = brain;
        SceneVirtualCamera = vcam;
    }

    /// <summary>
    /// links player's network ID to player root
    /// </summary>
    public static void SetAvatarTransform(PlayerRef player, Transform avatarTransform)
    {
        if (avatarTransform == null)
        {
            _playerAvatarTransforms.Remove(player);
            return;
        }

        _playerAvatarTransforms[player] = avatarTransform;
    }

    /// <summary>
    /// fetches player root transform for a given network player
    /// </summary>
    public static Transform GetAvatarTransform(PlayerRef player)
    {
        if (_playerAvatarTransforms.TryGetValue(player, out var transform))
        {
            return transform;
        }

        return null;
    }

    public static void RegisterDrowning(Transform cameraPivot, PlayerDrowning drowning)
    {
        if (cameraPivot != null)
            _drowningByPivot[cameraPivot] = drowning;
    }

    public static void UnregisterDrowning(Transform cameraPivot)
    {
        if (cameraPivot != null)
            _drowningByPivot.Remove(cameraPivot);
    }

    public static PlayerDrowning GetDrowning(Transform cameraPivot)
    {
        return cameraPivot != null && _drowningByPivot.TryGetValue(cameraPivot, out var d) ? d : null;
    }
}
