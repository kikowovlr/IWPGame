using Fusion;
using UnityEngine;

public class GooPuddleManager : NetworkBehaviour
{
    [Header("Puddle Prefabs")]
    [SerializeField] private NetworkObject[] _puddlePrefabs;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] _candidateSpawnPoints;


}
