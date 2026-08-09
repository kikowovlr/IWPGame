using UnityEngine;

public static class QuatCompression
{
    const float RANGE = 0.70710678f; // 1/sqrt 2 — max magnitude of the non-largest components

    // Quaternion -> uint: 2 bits largest index + 3x10 bits components
    public static uint Compress(Quaternion q)
    {
        float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        if (mag < 1e-6f) return 0; // degenerate -> decodes to identity-ish
        float inv = 1f / mag;
        float x = q.x * inv, y = q.y * inv, z = q.z * inv, w = q.w * inv;

        float ax = Mathf.Abs(x), ay = Mathf.Abs(y), az = Mathf.Abs(z), aw = Mathf.Abs(w);
        int largest = 0; float maxv = ax;
        if (ay > maxv) { largest = 1; maxv = ay; }
        if (az > maxv) { largest = 2; maxv = az; }
        if (aw > maxv) { largest = 3; maxv = aw; }

        // force dropped component positive (q and -q are the same rotation)
        float sign = 1f;
        switch (largest)
        {
            case 0: sign = x < 0 ? -1f : 1f; break;
            case 1: sign = y < 0 ? -1f : 1f; break;
            case 2: sign = z < 0 ? -1f : 1f; break;
            case 3: sign = w < 0 ? -1f : 1f; break;
        }
        x *= sign; y *= sign; z *= sign; w *= sign;

        uint a, b, c;
        switch (largest)
        {
            case 0: a = Q(y); b = Q(z); c = Q(w); break;
            case 1: a = Q(x); b = Q(z); c = Q(w); break;
            case 2: a = Q(x); b = Q(y); c = Q(w); break;
            default: a = Q(x); b = Q(y); c = Q(z); break;
        }
        return ((uint)largest << 30) | (a << 20) | (b << 10) | c;
    }

    public static Quaternion Decompress(uint data)
    {
        int largest = (int)(data >> 30) & 0x3;
        float a = DQ((data >> 20) & 0x3FF);
        float b = DQ((data >> 10) & 0x3FF);
        float c = DQ(data & 0x3FF);
        float d = Mathf.Sqrt(Mathf.Max(0f, 1f - (a * a + b * b + c * c))); // Max guard: rounding can push sum past 1

        switch (largest)
        {
            case 0: return new Quaternion(d, a, b, c);
            case 1: return new Quaternion(a, d, b, c);
            case 2: return new Quaternion(a, b, d, c);
            default: return new Quaternion(a, b, c, d);
        }
    }

    static uint Q(float v) => (uint)Mathf.RoundToInt((Mathf.Clamp(v, -RANGE, RANGE) / RANGE * 0.5f + 0.5f) * 1023f) & 0x3FF;
    static float DQ(uint q) => ((q / 1023f) * 2f - 1f) * RANGE;
}
