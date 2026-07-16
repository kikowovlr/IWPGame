using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [SerializeField] private Transform[] _spawnPoints;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public List<Transform> GetRandomisedSpawnPoints(int playerCount)
    {
        if (_spawnPoints == null || _spawnPoints.Length == 0) return new List<Transform>();

        // shuffle indices using a pseudo-random linq approach to ensure no duplicates
        // shuffles length of array and saves them to a new list 
        System.Random rng = new System.Random();
        List<int> shuffledIndices = Enumerable.Range(0, _spawnPoints.Length)
                                                .OrderBy(x => rng.Next())
                                                .ToList();

        List<Transform> selectedPoints = new List<Transform>();
        for (int i = 0; i < playerCount; i++)
        {
            // fallback in case playercount exceeds spawn points
            int targetIndex = shuffledIndices[i % _spawnPoints.Length];
            selectedPoints.Add(_spawnPoints[targetIndex]);
        }

        return selectedPoints;
    }

    public List<Transform> GetAllSpawnPoints()
    {
        return new List<Transform>(_spawnPoints);
    }
}
