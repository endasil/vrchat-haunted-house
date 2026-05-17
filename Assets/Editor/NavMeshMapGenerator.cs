using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class NavMeshMapGenerator
{
    const int TexSize = 1024;

    [MenuItem("Tools/Export NavMesh Map")]
    static void ExportNavMeshMap()
    {
        var tri = NavMesh.CalculateTriangulation();
        if (tri.vertices.Length == 0)
        {
            Debug.LogError("No NavMesh data found in scene.");
            return;
        }

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var v in tri.vertices)
        {
            if (v.x < minX) minX = v.x;
            if (v.x > maxX) maxX = v.x;
            if (v.z < minZ) minZ = v.z;
            if (v.z > maxZ) maxZ = v.z;
        }

        float worldW = maxX - minX;
        float worldH = maxZ - minZ;

        int texW, texH;
        if (worldW >= worldH)
        {
            texW = TexSize;
            texH = Mathf.Max(1, Mathf.RoundToInt(TexSize * worldH / worldW));
        }
        else
        {
            texH = TexSize;
            texW = Mathf.Max(1, Mathf.RoundToInt(TexSize * worldW / worldH));
        }

        var tex = new Texture2D(texW, texH, TextureFormat.RGB24, false);
        var pixels = new Color[texW * texH];
        tex.SetPixels(pixels); // defaults to black

        for (int i = 0; i < tri.indices.Length; i += 3)
        {
            Vector2Int p0 = ToPixel(tri.vertices[tri.indices[i]],     minX, minZ, worldW, worldH, texW, texH);
            Vector2Int p1 = ToPixel(tri.vertices[tri.indices[i + 1]], minX, minZ, worldW, worldH, texW, texH);
            Vector2Int p2 = ToPixel(tri.vertices[tri.indices[i + 2]], minX, minZ, worldW, worldH, texW, texH);
            FillTriangle(tex, p0, p1, p2, Color.white);
        }

        tex.Apply();

        string fullPath = Path.Combine(Application.dataPath, "NavMeshMap.png");
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.Refresh();
        Debug.Log($"NavMesh map saved to Assets/NavMeshMap.png ({texW}x{texH}, world bounds X:{minX:F1}–{maxX:F1} Z:{minZ:F1}–{maxZ:F1})");
    }

    static Vector2Int ToPixel(Vector3 world, float minX, float minZ, float worldW, float worldH, int texW, int texH)
    {
        int px = Mathf.Clamp((int)((world.x - minX) / worldW * texW), 0, texW - 1);
        int py = Mathf.Clamp((int)((world.z - minZ) / worldH * texH), 0, texH - 1);
        return new Vector2Int(px, py);
    }

    static void FillTriangle(Texture2D tex, Vector2Int a, Vector2Int b, Vector2Int c, Color color)
    {
        // Sort by y ascending
        if (a.y > b.y) { var t = a; a = b; b = t; }
        if (b.y > c.y) { var t = b; b = c; c = t; }
        if (a.y > b.y) { var t = a; a = b; b = t; }

        int totalH = c.y - a.y;
        if (totalH == 0) return;

        for (int y = a.y; y <= c.y; y++)
        {
            bool lower = y > b.y || b.y == a.y;
            int segH = lower ? c.y - b.y : b.y - a.y;
            if (segH == 0) continue;

            float alpha = (float)(y - a.y) / totalH;
            float beta  = lower ? (float)(y - b.y) / segH : (float)(y - a.y) / segH;

            int x0 = a.x + (int)((c.x - a.x) * alpha);
            int x1 = lower ? b.x + (int)((c.x - b.x) * beta) : a.x + (int)((b.x - a.x) * beta);

            if (x0 > x1) { var t = x0; x0 = x1; x1 = t; }

            for (int x = x0; x <= x1; x++)
            {
                if ((uint)x < (uint)tex.width && (uint)y < (uint)tex.height)
                    tex.SetPixel(x, y, color);
            }
        }
    }
}
