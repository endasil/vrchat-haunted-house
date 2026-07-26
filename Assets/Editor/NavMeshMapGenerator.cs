using System.IO;
using Assets._3DStealthGame.Scripts;
using StealthGame;
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

        var doors = Object.FindObjectsByType<DoorU>(FindObjectsSortMode.None);
        foreach (var door in doors)
        {
            Vector2Int px = ToPixel(door.transform.position, minX, minZ, worldW, worldH, texW, texH);
            DrawCircle(tex, px, 6, PillColorToColor(door.PillColor));
        }

        var pills = Object.FindObjectsByType<Pill>(FindObjectsSortMode.None);
        foreach (var pill in pills)
        {
            Vector2Int px = ToPixel(pill.transform.position, minX, minZ, worldW, worldH, texW, texH);
            DrawCircle(tex, px, 4, PillColorToColor(pill.PillColor));
        }

        var ghosts = Object.FindObjectsByType<SeekerGhost>(FindObjectsSortMode.None);
        foreach (var ghost in ghosts)
        {
            Vector2Int px = ToPixel(ghost.transform.position, minX, minZ, worldW, worldH, texW, texH);
            DrawX(tex, px, 6, Color.cyan);
        }

        var gargoyles = Object.FindObjectsByType<GargoyleU>(FindObjectsSortMode.None);
        foreach (var gargoyle in gargoyles)
        {
            Vector2Int px = ToPixel(gargoyle.transform.position, minX, minZ, worldW, worldH, texW, texH);
            FillTriangle(tex,
                new Vector2Int(px.x, px.y + 6),
                new Vector2Int(px.x - 6, px.y - 5),
                new Vector2Int(px.x + 6, px.y - 5),
                new Color(1f, 0.5f, 0f));
        }

        var butlers = Object.FindObjectsByType<ButlerGhost>(FindObjectsSortMode.None);
        foreach (var butler in butlers)
        {
            Vector2Int px = ToPixel(butler.transform.position, minX, minZ, worldW, worldH, texW, texH);
            DrawSquare(tex, px, 5, Color.yellow);
        }

        var networks = Object.FindObjectsByType<WaypointNetwork>(FindObjectsSortMode.None);
        foreach (var network in networks)
        {
            if (network.WaypointPositions == null) continue;
            foreach (var wp in network.WaypointPositions)
            {
                if (wp == null) continue;
                Vector2Int px = ToPixel(wp.position, minX, minZ, worldW, worldH, texW, texH);
                DrawCircle(tex, px, 3, new Color(1f, 0.8f, 0f));
            }
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

    static Color PillColorToColor(PillColor pillColor) => pillColor switch
    {
        PillColor.Green => Color.green,
        PillColor.Red   => Color.red,
        PillColor.Blue  => Color.blue,
        PillColor.Brown => new Color(0.55f, 0.27f, 0.07f),
        _               => Color.magenta,
    };

    static void DrawCircle(Texture2D tex, Vector2Int center, int radius, Color color)
    {
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy <= radius * radius)
            {
                int x = center.x + dx, y = center.y + dy;
                if ((uint)x < (uint)tex.width && (uint)y < (uint)tex.height)
                    tex.SetPixel(x, y, color);
            }
        }
    }

    static void DrawX(Texture2D tex, Vector2Int center, int arm, Color color)
    {
        for (int d = -arm; d <= arm; d++)
        for (int t = 0; t <= 1; t++) // 2 px thick so it stays visible
        {
            int x = center.x + d;
            if ((uint)x >= (uint)tex.width) continue;

            int y1 = center.y + d + t;
            int y2 = center.y - d + t;
            if ((uint)y1 < (uint)tex.height) tex.SetPixel(x, y1, color);
            if ((uint)y2 < (uint)tex.height) tex.SetPixel(x, y2, color);
        }
    }

    static void DrawSquare(Texture2D tex, Vector2Int center, int half, Color color)
    {
        for (int dy = -half; dy <= half; dy++)
        for (int dx = -half; dx <= half; dx++)
        {
            int x = center.x + dx, y = center.y + dy;
            if ((uint)x < (uint)tex.width && (uint)y < (uint)tex.height)
                tex.SetPixel(x, y, color);
        }
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
