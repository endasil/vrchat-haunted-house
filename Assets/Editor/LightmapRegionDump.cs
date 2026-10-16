using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Saves the part of the baked lightmap that the selected renderer uses, with the renderer's
// lightmap UV triangles drawn on it in white, to Temp/ClaudeBridge/lightmap_<name>.png.
public static class LightmapRegionDump
{
    const int PixelsPerTexel = 8;
    const string OutputFolder = "Temp/ClaudeBridge";

    [MenuItem("Haunted House/Debug/Dump Lightmap Region")]
    static void DumpSelected()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            throw new System.InvalidOperationException("LightmapRegionDump: nothing is selected.");
        }

        var renderer = selected.GetComponent<MeshRenderer>();
        var filter = selected.GetComponent<MeshFilter>();
        if (renderer == null || filter == null || renderer.lightmapIndex < 0)
        {
            throw new System.InvalidOperationException($"LightmapRegionDump: {selected.name} has no lightmapped MeshRenderer.");
        }

        Texture2D lightmap = LightmapSettings.lightmaps[renderer.lightmapIndex].lightmapColor;
        Vector4 scaleOffset = renderer.lightmapScaleOffset;
        int size = lightmap.width;
        int minX = Mathf.FloorToInt(scaleOffset.z * size);
        int minY = Mathf.FloorToInt(scaleOffset.w * size);
        int width = Mathf.CeilToInt(scaleOffset.x * size);
        int height = Mathf.CeilToInt(scaleOffset.y * size);

        Color[] texels = ReadTexels(lightmap);
        var output = new Texture2D(width * PixelsPerTexel, height * PixelsPerTexel);
        var report = new StringBuilder();
        report.AppendLine($"{selected.name}: lightmap {renderer.lightmapIndex} ({size} x {size}), texels x {minX}..{minX + width - 1}, y {minY}..{minY + height - 1}");

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int sx = Mathf.Clamp(minX + x, 0, size - 1);
                int sy = Mathf.Clamp(minY + y, 0, size - 1);
                Color c = texels[sy * size + sx];
                // The lightmap holds HDR values. Dividing by 2 keeps values up to 2 visible.
                Color shown = new Color(c.r / 2f, c.g / 2f, c.b / 2f, 1f);
                for (int px = 0; px < PixelsPerTexel; px++)
                {
                    for (int py = 0; py < PixelsPerTexel; py++)
                    {
                        output.SetPixel(x * PixelsPerTexel + px, y * PixelsPerTexel + py, shown);
                    }
                }
            }
        }

        Mesh mesh = filter.sharedMesh;
        Vector2[] uv2 = mesh.uv2 != null && mesh.uv2.Length > 0 ? mesh.uv2 : mesh.uv;
        int[] triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            for (int e = 0; e < 3; e++)
            {
                Vector2 a = ToPixel(uv2[triangles[i + e]], scaleOffset, size, minX, minY);
                Vector2 b = ToPixel(uv2[triangles[i + (e + 1) % 3]], scaleOffset, size, minX, minY);
                DrawLine(output, a, b, Color.white);
            }
        }

        output.Apply();
        Directory.CreateDirectory(OutputFolder);
        string safeName = string.Join("_", selected.name.Split(Path.GetInvalidFileNameChars()));
        File.WriteAllBytes(Path.Combine(OutputFolder, $"lightmap_{safeName}.png"), output.EncodeToPNG());
        Object.DestroyImmediate(output);

        foreach (Light light in Object.FindObjectsOfType<Light>())
        {
            float distance = Vector3.Distance(light.transform.position, renderer.bounds.ClosestPoint(light.transform.position));
            if (distance > light.range) continue;
            report.AppendLine($"  {light.name} ({light.transform.parent?.name}): {light.type} {light.lightmapBakeType}, spot angle {light.spotAngle}, range {light.range}, intensity {light.intensity}, distance {distance:F2}, forward {light.transform.forward}, shadows {light.shadows}, baked shadow radius {light.shadowRadius}, culling mask {light.cullingMask}");
        }
        File.WriteAllText(Path.Combine(OutputFolder, $"lightmap_{safeName}.txt"), report.ToString());
        Debug.Log(report.ToString());
    }

    static int _savedLightmapIndex = -1;
    static MeshRenderer _savedRenderer;

    // 65535 is the lightmap index Unity uses for a renderer with no lightmap.
    [MenuItem("Haunted House/Debug/Lightmap Off For Selection")]
    static void LightmapOff()
    {
        _savedRenderer = Selection.activeGameObject.GetComponent<MeshRenderer>();
        _savedLightmapIndex = _savedRenderer.lightmapIndex;
        _savedRenderer.lightmapIndex = 65535;
    }

    [MenuItem("Haunted House/Debug/Lightmap Back On")]
    static void LightmapBackOn()
    {
        _savedRenderer.lightmapIndex = _savedLightmapIndex;
    }

    // Writes every collider above and below the selected object's position, nearest first.
    [MenuItem("Haunted House/Debug/Colliders Above And Below Selection")]
    static void CollidersAboveAndBelow()
    {
        Physics.SyncTransforms();
        Vector3 start = Selection.activeGameObject.transform.position + Vector3.up * 0.5f;
        var report = new StringBuilder();
        foreach (Vector3 direction in new[] { Vector3.up, Vector3.down })
        {
            RaycastHit[] hits = Physics.RaycastAll(start, direction, 20f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                report.AppendLine($"{(direction.y > 0 ? "above" : "below")} y {hit.point.y:F3}: {PathOf(hit.collider.transform)}");
            }
        }
        Directory.CreateDirectory(OutputFolder);
        File.WriteAllText(Path.Combine(OutputFolder, "colliders_vertical.txt"), report.ToString());
    }

    static string PathOf(Transform transform)
    {
        string path = transform.name;
        for (Transform t = transform.parent; t != null; t = t.parent)
        {
            path = t.name + "/" + path;
        }
        return path;
    }

    [MenuItem("Haunted House/Debug/Make Sign Quads Static")]
    static void MakeSignQuadsStatic()
    {
        GameObject signs = GameObject.Find("Start Room Signs");
        foreach (MeshRenderer renderer in signs.GetComponentsInChildren<MeshRenderer>())
        {
            string name = renderer.gameObject.name;
            if (name != "FrameQuad" && name != "BoardQuad") continue;

            Undo.RecordObject(renderer.gameObject, "Make Sign Quads Static");
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
            renderer.receiveGI = ReceiveGI.Lightmaps;
            Debug.Log($"Static: {PathOf(renderer.transform)}");
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(signs.scene);
    }

    [MenuItem("Haunted House/Debug/Bake")]
    static void Bake()
    {
        UnityEditor.Lightmapping.Bake();
    }

    static Light _disabledLight;

    // Turns off the Light on the selected object and bakes. Bake Light Back On turns it on and bakes again.
    [MenuItem("Haunted House/Debug/Bake Without Selected Light")]
    static void BakeWithoutSelectedLight()
    {
        _disabledLight = Selection.activeGameObject.GetComponent<Light>();
        _disabledLight.enabled = false;
        UnityEditor.Lightmapping.Bake();
    }

    [MenuItem("Haunted House/Debug/Bake Light Back On")]
    static void BakeLightBackOn()
    {
        _disabledLight.enabled = true;
        UnityEditor.Lightmapping.Bake();
    }

    // The lightmap texture is not readable from script, so the GPU copies it into a readable texture.
    static Color[] ReadTexels(Texture2D lightmap)
    {
        var target = RenderTexture.GetTemporary(lightmap.width, lightmap.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        Graphics.Blit(lightmap, target);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        var readable = new Texture2D(lightmap.width, lightmap.height, TextureFormat.RGBAHalf, false, true);
        readable.ReadPixels(new Rect(0, 0, lightmap.width, lightmap.height), 0, 0);
        readable.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        Color[] texels = readable.GetPixels();
        Object.DestroyImmediate(readable);
        return texels;
    }

    static Vector2 ToPixel(Vector2 uv, Vector4 scaleOffset, int size, int minX, int minY)
    {
        float lx = (uv.x * scaleOffset.x + scaleOffset.z) * size - minX;
        float ly = (uv.y * scaleOffset.y + scaleOffset.w) * size - minY;
        return new Vector2(lx * PixelsPerTexel, ly * PixelsPerTexel);
    }

    static void DrawLine(Texture2D texture, Vector2 a, Vector2 b, Color color)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(a, b)) + 1;
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
            int x = Mathf.RoundToInt(p.x);
            int y = Mathf.RoundToInt(p.y);
            if (x >= 0 && y >= 0 && x < texture.width && y < texture.height)
            {
                texture.SetPixel(x, y, color);
            }
        }
    }
}
