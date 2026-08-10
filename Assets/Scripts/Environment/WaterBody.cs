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

#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField] private bool _drawDebugPlane = true;
    [SerializeField] private bool _drawAlways = true;   // draw even when not selected
    [SerializeField] private Color _fillColor = new Color(0.2f, 0.6f, 1f, 0.25f);
    [SerializeField] private Color _lineColor = new Color(0.2f, 0.6f, 1f, 0.9f);

    private void OnDrawGizmos()
    {
        if (_drawDebugPlane && _drawAlways) DrawWaterDebug();
    }

    private void OnDrawGizmosSelected()
    {
        if (_drawDebugPlane && !_drawAlways) DrawWaterDebug();
    }

    private void DrawWaterDebug()
    {
        // recompute so the gizmo matches runtime bounds exactly (same as RecalculateBounds)
        Bounds b = _waterMeshRenderer != null
            ? _waterMeshRenderer.bounds
            : new Bounds(transform.position, new Vector3(_manualWidth, 0.1f, _manualLength));

        float y = SurfaceHeight;   // the EXACT height buoyancy uses (transform.y + offset)

        // four corners of the surface plane at the true water height
        Vector3 min = b.min;
        Vector3 max = b.max;
        Vector3 p0 = new Vector3(min.x, y, min.z);
        Vector3 p1 = new Vector3(max.x, y, min.z);
        Vector3 p2 = new Vector3(max.x, y, max.z);
        Vector3 p3 = new Vector3(min.x, y, max.z);

        // translucent fill (flat cube at the surface height, spanning the XZ bounds)
        Gizmos.color = _fillColor;
        Vector3 center = new Vector3(b.center.x, y, b.center.z);
        Gizmos.DrawCube(center, new Vector3(b.size.x, 0.02f, b.size.z));

        // border — this is exactly the XZ region ContainsXZ() tests
        Gizmos.color = _lineColor;
        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p0);

        // grid so the height reads clearly against walls/geometry
        int divisions = 10;
        Gizmos.color = new Color(_lineColor.r, _lineColor.g, _lineColor.b, 0.3f);
        for (int i = 1; i < divisions; i++)
        {
            float t = i / (float)divisions;
            Gizmos.DrawLine(Vector3.Lerp(p0, p1, t), Vector3.Lerp(p3, p2, t));
            Gizmos.DrawLine(Vector3.Lerp(p0, p3, t), Vector3.Lerp(p1, p2, t));
        }

        // show the min-submerge threshold as a second, fainter plane —
        // a player must be this far BELOW the surface to count as submerged
        // (matches PlayerBuoyancy._minSubmergeDepth; set this to the same value)
        if (_debugMinSubmergeDepth > 0f)
        {
            float thresholdY = y - _debugMinSubmergeDepth;
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.7f);
            Vector3 tCenter = new Vector3(b.center.x, thresholdY, b.center.z);
            Gizmos.DrawWireCube(tCenter, new Vector3(b.size.x, 0.01f, b.size.z));
        }
    }

    [SerializeField] private float _debugMinSubmergeDepth = 0.5f;  // mirror PlayerBuoyancy._minSubmergeDepth
#endif
}
