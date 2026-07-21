using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// attach to water plane
/// registers itself so players can query
/// </summary>
public class WaterBody : MonoBehaviour
{
    [SerializeField] private MeshRenderer _waterMeshRenderer; // pulls XZ bounds
    [SerializeField] private float _manualWidth = 50f;        // fallback if no renderer assigned
    [SerializeField] private float _manualLength = 50f;
    [SerializeField] private float _surfaceHeightOffset = 0f;

    public float SurfaceHeight => transform.position.y + _surfaceHeightOffset;

    private Bounds _worldBounds;
    private static readonly List<WaterBody> _all = new List<WaterBody>();

    private void OnEnable()
    {
        RecalculateBounds();
        _all.Add(this);
    }

    private void OnDisable()
    {
        _all.Remove(this);
    }

    private void RecalculateBounds()
    {
        _worldBounds = _waterMeshRenderer != null
            ? _waterMeshRenderer.bounds
            : new Bounds(transform.position, new Vector3(_manualWidth, 0.1f, _manualLength));
    }

    private bool ContainsXZ(Vector3 worldPos)
    {
        return worldPos.x >= _worldBounds.min.x && worldPos.x <= _worldBounds.max.x
            && worldPos.z >= _worldBounds.min.z && worldPos.z <= _worldBounds.max.z;
    }

    /// <summary>
    /// checks all registered water bodies, returns the highest surface a point is under
    /// </summary>
    public static bool TryGetSurfaceHeight(Vector3 worldPos, out float surfaceY)
    {
        surfaceY = float.NegativeInfinity;
        bool found = false;

        for (int i = 0; i < _all.Count; i++)
        {
            WaterBody body = _all[i];
            if (body == null || !body.ContainsXZ(worldPos)) continue;

            if (body.SurfaceHeight > surfaceY)
            {
                surfaceY = body.SurfaceHeight;
                found = true;
            }
        }
        return found;
    }

//#if UNITY_EDITOR
//    private void OnDrawGizmosSelected()
//    {
//        RecalculateBounds();
//        Gizmos.color = Color.aquamarine;
//        Gizmos.DrawCube(_worldBounds.center, _worldBounds.size);
//    }
//#endif
}
