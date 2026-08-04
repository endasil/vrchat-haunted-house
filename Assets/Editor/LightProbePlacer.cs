using System.Collections.Generic;
using Assets._3DStealthGame.Scripts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Builds the scene's light probes from the baked NavMesh.
//
// Every light in the house is baked, so probes are the only thing that carries that light onto
// the players, the ghosts, the pills and the doors. Without them all of those render against the
// flat ambient colour, which is nearly black.
//
// The NavMesh is the right source for the positions: it covers exactly where a player or a ghost
// can be, and it is already inset half a metre from the walls, so probes taken from it do not end
// up buried in geometry. Columns come from three places - a grid across the walkable area, a ring
// along the NavMesh outline (so someone walking next to a wall is not lit by the probe out in the
// middle of the room), and satellites around every light and door, which is where the brightness
// changes fastest.
//
// Open the scene, run Tools/Lighting/Generate Light Probes, then bake lighting. Re-running
// replaces the previous group.
public static class LightProbePlacer
{
    const float Spacing = 2.5f;        // grid step, and the step along the NavMesh outline
    const float BorderInset = 0.15f;   // pull outline points off the exact edge, toward the room
    const float MinDistance = 1.5f;    // columns closer together than this collapse into one
    const float FeatureOffset = 1.2f;  // how far from a light or door its satellites sit
    const float SnapDistance = 1.5f;   // how far a satellite may be pulled to reach the NavMesh
    const float ClearRadius = 0.2f;    // a probe this close to a collider is dropped
    const float RoomFillRadius = 5f;   // how far to fill around a light or door that missed the NavMesh
    const float StepTolerance = 0.5f;  // a fill point this much above the room floor is furniture, not floor

    // Heights above the walkable surface. The house has no ceilings and the walls are 2.297 m,
    // so the top layer stays 0.4 m below the wall top and keeps looking at the room, not the sky.
    static readonly float[] Heights = { 0.3f, 1.1f, 1.9f };

    const string GroupName = "Light Probes";
    const string ParentName = "World Holder";

    [MenuItem("Tools/Lighting/Generate Light Probes")]
    static void GenerateLightProbes()
    {
        var tri = NavMesh.CalculateTriangulation();
        if (tri.vertices.Length == 0)
        {
            Debug.LogError("LightProbePlacer: no NavMesh data found in scene.");
            return;
        }

        var parent = GameObject.Find(ParentName);
        if (parent == null)
        {
            Debug.LogError($"LightProbePlacer: no GameObject named \"{ParentName}\" in the scene to put the probes under.");
            return;
        }

        // Physics queries below read collider positions, which only match the transforms after a sync.
        Physics.SyncTransforms();

        var columns = new List<Vector3>();
        AddGridColumns(tri, columns);
        AddOutlineColumns(tri, columns);
        AddFeatureColumns(columns, out int featureColumns, out int filledColumns);

        var kept = Dedupe(columns);

        var world = new List<Vector3>(kept.Count * Heights.Length);
        int blocked = 0;
        foreach (var column in kept)
        {
            foreach (float height in Heights)
            {
                Vector3 p = column + Vector3.up * height;
                if (Physics.CheckSphere(p, ClearRadius, ~0, QueryTriggerInteraction.Ignore))
                {
                    blocked++;
                    continue;
                }
                world.Add(p);
            }
        }

        if (world.Count == 0)
        {
            Debug.LogError("LightProbePlacer: every candidate probe was inside a collider - nothing written.");
            return;
        }

        var group = ReplaceGroup(parent);

        // probePositions are local to the group, so convert even though World Holder sits at the origin.
        var local = new Vector3[world.Count];
        for (int i = 0; i < world.Count; i++)
            local[i] = group.transform.InverseTransformPoint(world[i]);
        group.probePositions = local;

        EditorSceneManager.MarkSceneDirty(group.gameObject.scene);
        Selection.activeGameObject = group.gameObject;

        var bounds = new Bounds(world[0], Vector3.zero);
        foreach (var p in world) bounds.Encapsulate(p);
        Debug.Log($"LightProbePlacer: {world.Count} probes in {kept.Count} columns " +
                  $"({featureColumns} from lights and doors before dedupe, {filledColumns} filling rooms off the NavMesh, " +
                  $"{blocked} dropped inside colliders). " +
                  $"Bounds X:{bounds.min.x:F1}-{bounds.max.x:F1} Y:{bounds.min.y:F1}-{bounds.max.y:F1} Z:{bounds.min.z:F1}-{bounds.max.z:F1}. " +
                  "Bake lighting to fill them in.");
    }

    // One global lattice across the whole NavMesh, so the grid lines up between triangles and
    // between rooms instead of restarting inside each triangle.
    static void AddGridColumns(NavMeshTriangulation tri, List<Vector3> columns)
    {
        for (int i = 0; i < tri.indices.Length; i += 3)
        {
            Vector3 a = tri.vertices[tri.indices[i]];
            Vector3 b = tri.vertices[tri.indices[i + 1]];
            Vector3 c = tri.vertices[tri.indices[i + 2]];

            int minX = Mathf.CeilToInt(Mathf.Min(a.x, b.x, c.x) / Spacing);
            int maxX = Mathf.FloorToInt(Mathf.Max(a.x, b.x, c.x) / Spacing);
            int minZ = Mathf.CeilToInt(Mathf.Min(a.z, b.z, c.z) / Spacing);
            int maxZ = Mathf.FloorToInt(Mathf.Max(a.z, b.z, c.z) / Spacing);

            for (int ix = minX; ix <= maxX; ix++)
            for (int iz = minZ; iz <= maxZ; iz++)
            {
                float x = ix * Spacing, z = iz * Spacing;
                if (TrySurfaceHeight(a, b, c, x, z, out float y))
                    columns.Add(new Vector3(x, y, z));
            }
        }
    }

    // An edge that belongs to only one triangle is on the NavMesh outline, which follows the walls
    // half a metre out. Vertices are keyed by position, not index, because neighbouring NavMesh
    // tiles repeat the vertices along their shared border with different indices.
    static void AddOutlineColumns(NavMeshTriangulation tri, List<Vector3> columns)
    {
        var edges = new Dictionary<(Vector3Int, Vector3Int), (Vector3 A, Vector3 B, Vector3 Centre, int Count)>();

        for (int i = 0; i < tri.indices.Length; i += 3)
        {
            Vector3 a = tri.vertices[tri.indices[i]];
            Vector3 b = tri.vertices[tri.indices[i + 1]];
            Vector3 c = tri.vertices[tri.indices[i + 2]];
            Vector3 centre = (a + b + c) / 3f;

            AddEdge(edges, a, b, centre);
            AddEdge(edges, b, c, centre);
            AddEdge(edges, c, a, centre);
        }

        foreach (var edge in edges.Values)
        {
            if (edge.Count != 1) continue;

            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(edge.A, edge.B) / Spacing));
            for (int s = 0; s <= steps; s++)
            {
                Vector3 p = Vector3.Lerp(edge.A, edge.B, (float)s / steps);
                Vector3 inward = edge.Centre - p;
                inward.y = 0f;
                if (inward.sqrMagnitude > 1e-6f)
                    p += inward.normalized * BorderInset;
                columns.Add(p);
            }
        }
    }

    static void AddEdge(Dictionary<(Vector3Int, Vector3Int), (Vector3, Vector3, Vector3, int)> edges,
                        Vector3 a, Vector3 b, Vector3 centre)
    {
        Vector3Int ka = Quantise(a), kb = Quantise(b);
        var key = Compare(ka, kb) <= 0 ? (ka, kb) : (kb, ka);
        if (edges.TryGetValue(key, out var existing))
            edges[key] = (existing.Item1, existing.Item2, existing.Item3, existing.Item4 + 1);
        else
            edges[key] = (a, b, centre, 1);
    }

    // Wall sconces sit inside the wall and doors sit in the doorway, so neither position is on the
    // NavMesh. Offsetting to four sides and snapping each one finds whichever rooms they open onto.
    // A feature where all four miss is in a room the ghosts cannot reach, which still needs probes
    // because players can be there, so it gets filled from the feature instead.
    static void AddFeatureColumns(List<Vector3> columns, out int satellites, out int filled)
    {
        satellites = 0;
        filled = 0;

        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (!light.enabled || light.type == LightType.Directional) continue;
            int added = AddSatellites(light.transform.position, columns);
            satellites += added;
            if (added == 0) filled += FillAroundFeature(light.transform.position, columns);
        }

        foreach (var door in Object.FindObjectsByType<DoorU>(FindObjectsSortMode.None))
        {
            int added = AddSatellites(door.transform.position, columns);
            satellites += added;
            if (added == 0) filled += FillAroundFeature(door.transform.position, columns);
        }
    }

    // The spawn room is walled off from the ghosts, so it is not on the NavMesh even though the
    // player starts there. Fill outwards from the feature on the same lattice, keeping only what
    // the room's own floor can see, which is what stops the fill leaking through a wall to the
    // ground plane outside the house.
    static int FillAroundFeature(Vector3 centre, List<Vector3> columns)
    {
        if (!Physics.Raycast(centre + Vector3.up * 0.5f, Vector3.down, out var under,
                             RoomFillRadius, ~0, QueryTriggerInteraction.Ignore))
            return 0;

        Vector3 eye = under.point + Vector3.up * 0.3f;
        columns.Add(under.point);
        int added = 1;

        int minX = Mathf.CeilToInt((centre.x - RoomFillRadius) / Spacing);
        int maxX = Mathf.FloorToInt((centre.x + RoomFillRadius) / Spacing);
        int minZ = Mathf.CeilToInt((centre.z - RoomFillRadius) / Spacing);
        int maxZ = Mathf.FloorToInt((centre.z + RoomFillRadius) / Spacing);

        for (int ix = minX; ix <= maxX; ix++)
        for (int iz = minZ; iz <= maxZ; iz++)
        {
            var above = new Vector3(ix * Spacing, under.point.y + 2f, iz * Spacing);
            if (!Physics.Raycast(above, Vector3.down, out var floor, 4f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (Mathf.Abs(floor.point.y - under.point.y) > StepTolerance) continue;
            if (Physics.Linecast(eye, floor.point + Vector3.up * 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
            columns.Add(floor.point);
            added++;
        }

        return added;
    }

    static int AddSatellites(Vector3 centre, List<Vector3> columns)
    {
        var offsets = new[]
        {
            new Vector3(FeatureOffset, 0f, 0f),
            new Vector3(-FeatureOffset, 0f, 0f),
            new Vector3(0f, 0f, FeatureOffset),
            new Vector3(0f, 0f, -FeatureOffset),
        };

        int added = 0;
        foreach (var offset in offsets)
        {
            if (!NavMesh.SamplePosition(centre + offset, out var hit, SnapDistance, NavMesh.AllAreas)) continue;
            columns.Add(hit.position);
            added++;
        }
        return added;
    }

    static List<Vector3> Dedupe(List<Vector3> columns)
    {
        var buckets = new Dictionary<Vector3Int, List<Vector3>>();
        var kept = new List<Vector3>();

        foreach (var p in columns)
        {
            var cell = new Vector3Int(
                Mathf.FloorToInt(p.x / MinDistance),
                Mathf.FloorToInt(p.y / MinDistance),
                Mathf.FloorToInt(p.z / MinDistance));

            bool tooClose = false;
            for (int dx = -1; dx <= 1 && !tooClose; dx++)
            for (int dy = -1; dy <= 1 && !tooClose; dy++)
            for (int dz = -1; dz <= 1 && !tooClose; dz++)
            {
                if (!buckets.TryGetValue(cell + new Vector3Int(dx, dy, dz), out var near)) continue;
                foreach (var other in near)
                {
                    if (Vector3.SqrMagnitude(other - p) < MinDistance * MinDistance) { tooClose = true; break; }
                }
            }
            if (tooClose) continue;

            if (!buckets.TryGetValue(cell, out var bucket))
            {
                bucket = new List<Vector3>();
                buckets[cell] = bucket;
            }
            bucket.Add(p);
            kept.Add(p);
        }

        return kept;
    }

    static LightProbeGroup ReplaceGroup(GameObject parent)
    {
        var previous = parent.transform.Find(GroupName);
        if (previous != null)
            Undo.DestroyObjectImmediate(previous.gameObject);

        var go = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(go, "Generate Light Probes");
        Undo.SetTransformParent(go.transform, parent.transform, "Generate Light Probes");
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return Undo.AddComponent<LightProbeGroup>(go);
    }

    // Barycentric test in the XZ plane, returning the height of the triangle above that point.
    static bool TrySurfaceHeight(Vector3 a, Vector3 b, Vector3 c, float x, float z, out float y)
    {
        y = 0f;
        float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
        if (Mathf.Abs(d) < 1e-6f) return false;

        float wa = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
        float wb = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
        float wc = 1f - wa - wb;
        if (wa < 0f || wb < 0f || wc < 0f) return false;

        y = wa * a.y + wb * b.y + wc * c.y;
        return true;
    }

    static Vector3Int Quantise(Vector3 v) =>
        new Vector3Int(Mathf.RoundToInt(v.x * 100f), Mathf.RoundToInt(v.y * 100f), Mathf.RoundToInt(v.z * 100f));

    static int Compare(Vector3Int a, Vector3Int b)
    {
        if (a.x != b.x) return a.x < b.x ? -1 : 1;
        if (a.y != b.y) return a.y < b.y ? -1 : 1;
        if (a.z != b.z) return a.z < b.z ? -1 : 1;
        return 0;
    }
}
