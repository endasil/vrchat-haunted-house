using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-time tool to make the floors Quest-compatible.
//
// The floors use the custom Custom/FloorTriplanar_VRChat shader, which Quest strips out at
// build time and replaces with a UV-based fallback - that fallback stretches the texture
// because the floors are the built-in 10x10 Plane whose UVs span 0..1 across the whole
// (non-uniformly scaled) plane. This tool fixes both problems at once:
//
//   1. Bakes the tiling the triplanar shader did in world space into a per-floor copy of the
//      plane mesh (UV0 scaled per axis by 10 * lossyScale * _Tiling). UV2 keeps the untiled
//      0..1 coordinates so baked lightmaps stay correct.
//   2. Switches the 6 floor materials to VRChat/Mobile/Standard Lite (an allowed Quest shader).
//
// Materials stay shared, so the floors still static-batch into a low draw-call count.
//
// Open the scene with the floors in it (e.g. Assets/Scenes/haunted.unity), then run
// Tools/Floors/Bake UVs + Convert to Mobile Shader. Re-bake lighting afterwards.
public static class FloorUvBaker
{
    // The 6 floor materials, by asset GUID -> their triplanar _Tiling. The tiling is read live
    // from the material when it still has the property; these values are the fallback so the
    // tool stays re-runnable after the shader has already been swapped.
    static readonly Dictionary<string, float> FloorMaterialTiling = new Dictionary<string, float>
    {
        { "4bf20d2e9873d184994bbd333bc44061", 0.7f }, // Floor_WoodPlanks
        { "2137099b5a0689445ae94927dbce26fe", 0.4f }, // Floor_Bedroom
        { "eaab651cdb10ded47a6ffcd15d88afdb", 0.4f }, // Floor_DinningRoom
        { "b375857689bc8c749a819e002181bfc5", 0.7f }, // Floor_TIlesGreen
        { "b27874f6585173e4c8fe842a17ceebf2", 0.4f }, // Floor_TilesRedish
        { "5b97c3c88138b954291a448f87d9b5b2", 0.7f }, // Floor_WoodPlanks_Light
    };

    const string MobileShaderGuid = "0b7113dea2069fc4e8943843eff19f70"; // VRChat/Mobile/Standard Lite
    const string BakedMeshFolder = "Assets/_3DStealthGame/Art/Models/Floors_Baked";

    [MenuItem("Tools/Floors/Bake UVs + Convert to Mobile Shader")]
    static void BakeAndConvert()
    {
        // Resolve the floor materials and capture each one's triplanar _Tiling BEFORE we swap
        // the shader (the property goes away once the material is on Standard Lite).
        var floorMaterials = new HashSet<Material>();
        var tilingByMaterial = new Dictionary<Material, float>();
        foreach (var entry in FloorMaterialTiling)
        {
            string path = AssetDatabase.GUIDToAssetPath(entry.Key);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"FloorUvBaker: floor material with GUID {entry.Key} not found.");
                return;
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Debug.LogError($"FloorUvBaker: could not load material at {path}.");
                return;
            }
            floorMaterials.Add(mat);
            // Prefer the live triplanar _Tiling; fall back to the known value after conversion.
            tilingByMaterial[mat] = mat.HasProperty("_Tiling") ? mat.GetFloat("_Tiling") : entry.Value;
        }

        // Find every floor renderer in the open scene.
        var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (!Directory.Exists(BakedMeshFolder))
            Directory.CreateDirectory(BakedMeshFolder);

        // Reuse one baked mesh for floors that share size + tiling.
        var meshCache = new Dictionary<string, Mesh>();
        int bakedCount = 0;

        foreach (var renderer in renderers)
        {
            Material floorMat = FindFloorMaterial(renderer, floorMaterials);
            if (floorMat == null) continue;

            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogError($"FloorUvBaker: floor '{renderer.name}' has no MeshFilter/mesh.", renderer);
                return;
            }

            Mesh source = meshFilter.sharedMesh;
            // The UV math assumes the built-in 10x10 Plane (UVs 0..1 across 10 units).
            Vector3 size = source.bounds.size;
            if (Mathf.Abs(size.x - 10f) > 0.1f || Mathf.Abs(size.z - 10f) > 0.1f)
            {
                Debug.LogWarning($"FloorUvBaker: skipping '{renderer.name}' - mesh '{source.name}' is {size.x:F1}x{size.z:F1}, not the built-in 10x10 Plane the UV math assumes.", renderer);
                continue;
            }

            float tiling = tilingByMaterial[floorMat];
            Vector3 s = renderer.transform.lossyScale;
            // Plane is 10 units wide; triplanar repeats every 1/_Tiling metres.
            float uvScaleX = 10f * Mathf.Abs(s.x) * tiling;
            float uvScaleY = 10f * Mathf.Abs(s.z) * tiling;

            string key = $"{uvScaleX:F3}_{uvScaleY:F3}";
            if (!meshCache.TryGetValue(key, out Mesh baked))
            {
                baked = BakeMesh(source, uvScaleX, uvScaleY);
                string assetPath = $"{BakedMeshFolder}/Floor_{uvScaleX:F2}x{uvScaleY:F2}.asset";
                AssetDatabase.CreateAsset(baked, AssetDatabase.GenerateUniqueAssetPath(assetPath));
                meshCache[key] = baked;
            }

            meshFilter.sharedMesh = baked;
            EditorUtility.SetDirty(meshFilter);
            if (PrefabUtility.IsPartOfPrefabInstance(meshFilter))
                PrefabUtility.RecordPrefabInstancePropertyModifications(meshFilter);
            bakedCount++;
        }

        // Swap the materials to the mobile shader.
        Shader mobile = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(MobileShaderGuid));
        if (mobile == null)
        {
            Debug.LogError($"FloorUvBaker: VRChat/Mobile/Standard Lite shader (GUID {MobileShaderGuid}) not found. Meshes were baked; materials not converted.");
            return;
        }
        foreach (var mat in floorMaterials)
            ConvertMaterial(mat, mobile);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"FloorUvBaker: baked {bakedCount} floor(s) into {meshCache.Count} mesh(es) and converted {floorMaterials.Count} materials to {mobile.name}. Save the scene and re-bake lighting.");
    }

    // Returns the floor material on this renderer, or null if it isn't a floor.
    static Material FindFloorMaterial(MeshRenderer renderer, HashSet<Material> floorMaterials)
    {
        foreach (var mat in renderer.sharedMaterials)
            if (mat != null && floorMaterials.Contains(mat))
                return mat;
        return null;
    }

    // Copies the source plane mesh, scaling UV0 for tiling and keeping untiled UV2 for lightmaps.
    static Mesh BakeMesh(Mesh source, float uvScaleX, float uvScaleY)
    {
        var baked = new Mesh
        {
            name = source.name,
            vertices = source.vertices,
            normals = source.normals,
            tangents = source.tangents,
            triangles = source.triangles,
        };

        Vector2[] uv = source.uv;
        var tiled = new Vector2[uv.Length];
        for (int i = 0; i < uv.Length; i++)
            tiled[i] = new Vector2(uv[i].x * uvScaleX, uv[i].y * uvScaleY);
        baked.uv = tiled;   // UV0: tiled, used by albedo/normal/metallic/occlusion
        baked.uv2 = uv;     // UV2: untiled 0..1, used by baked lightmaps

        baked.RecalculateBounds();
        return baked;
    }

    // Switches a floor material to the mobile shader, carrying the albedo into _MainTex.
    static void ConvertMaterial(Material mat, Shader mobile)
    {
        // _MainTexture (triplanar) -> _MainTex (Standard Lite). Other maps share names and carry over.
        Texture albedo = mat.HasProperty("_MainTexture") ? mat.GetTexture("_MainTexture") : null;
        mat.shader = mobile;
        if (albedo != null)
            mat.SetTexture("_MainTex", albedo);
        // Keep tiling in the mesh, not the material.
        mat.SetTextureScale("_MainTex", Vector2.one);
        mat.SetTextureOffset("_MainTex", Vector2.zero);
        // Assigning the shader in code doesn't enable the Standard keywords, so the normal/metallic
        // maps would be ignored. Turn them on based on which maps the material actually has.
        if (mat.GetTexture("_BumpMap") != null) mat.EnableKeyword("_NORMALMAP");
        if (mat.GetTexture("_MetallicGlossMap") != null) mat.EnableKeyword("_METALLICGLOSSMAP");
        EditorUtility.SetDirty(mat);
    }
}
