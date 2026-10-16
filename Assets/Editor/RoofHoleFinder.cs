using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Finds places inside the house that have open floor but no roof above them,
// and adds roof planes that cover them.
public static class RoofHoleFinder
{
    const float CellSize = 0.25f;
    // Heights above the house floor, which ScanResult.floorY holds.
    const float SampleHeight = 1.2f;
    // A first hit from above that is higher than this is a wall or tall furniture.
    const float BlockedMinHeight = 1.0f;
    const float RoofMinHeight = 2.1f;
    const float RoofMaxHeight = 2.7f;
    const float InteriorRayLength = 40f;
    const int InteriorRayCount = 8;
    // For a cell in line with a doorway, 1 of its 8 rays passes through the doorway to the outside.
    const int InteriorMaxMisses = 1;
    const float PatchOverlap = 0.05f;
    const string OutputFolder = "Temp/ClaudeBridge";
    const string PatchName = "Roof Patch";

    enum Cell { Outside, NoFloor, Blocked, Roofed, Hole }

    class ScanResult
    {
        public Cell[,] cells;
        public Collider[,] floors;
        public Vector3 origin;
        public int countX;
        public int countZ;
        public float floorY;
        public float roofY;
    }

    [MenuItem("Haunted House/Roof Holes/Find")]
    static void Find()
    {
        ScanResult scan = Scan();
        Directory.CreateDirectory(OutputFolder);
        WriteImage(scan, Path.Combine(OutputFolder, "roofholes.png"));
        File.WriteAllText(Path.Combine(OutputFolder, "roofholes.txt"), BuildReport(scan));
        Debug.Log("RoofHoleFinder: wrote roofholes.png and roofholes.txt to " + OutputFolder);
    }

    // Some floor planes have no roof above any part of them. Each one gets an upside-down
    // copy at the roof height. A rectangle patch then covers each hole that is left.
    [MenuItem("Haunted House/Roof Holes/Add Roof Patches")]
    static void AddPatches()
    {
        ScanResult scan = Scan();
        GameObject template = FindRoofTemplate();
        if (template == null)
        {
            throw new System.InvalidOperationException("RoofHoleFinder: no GameObject named Roof with a MeshRenderer in the open scene.");
        }

        Undo.SetCurrentGroupName("Add Roof Patches");
        int undoGroup = Undo.GetCurrentGroup();

        var log = new StringBuilder();
        foreach (KeyValuePair<Collider, Vector2Int> floor in CountCellsPerFloor(scan))
        {
            if (floor.Value.x == 0 || floor.Value.y > 0 || !IsFloorPlane(floor.Key, scan)) continue;

            Transform floorTransform = floor.Key.transform;
            GameObject roof = CreateRoof(template, floorTransform.parent, "Roof");
            roof.transform.SetPositionAndRotation(
                floorTransform.position + Vector3.up * (scan.roofY - scan.floorY),
                floorTransform.rotation * Quaternion.Euler(180f, 0f, 0f));
            roof.transform.localScale = floorTransform.localScale;
            log.AppendLine($"{PathOf(roof.transform)}  copy of {floorTransform.name}");
        }

        scan = Scan();

        foreach (RectInt rect in CoverHoles(scan))
        {
            float minX = scan.origin.x + rect.xMin * CellSize - CellSize / 2 - PatchOverlap;
            float maxX = scan.origin.x + rect.xMax * CellSize - CellSize / 2 + PatchOverlap;
            float minZ = scan.origin.z + rect.yMin * CellSize - CellSize / 2 - PatchOverlap;
            float maxZ = scan.origin.z + rect.yMax * CellSize - CellSize / 2 + PatchOverlap;

            Vector2Int centreCell = new Vector2Int(rect.xMin + rect.width / 2, rect.yMin + rect.height / 2);
            Collider floor = scan.floors[centreCell.x, centreCell.y];
            Transform parent = floor != null ? GroupOf(floor.transform) : null;

            GameObject patch = CreateRoof(template, parent, PatchName);
            // The Plane mesh is 10 m x 10 m.
            patch.transform.position = new Vector3((minX + maxX) / 2, scan.roofY, (minZ + maxZ) / 2);
            patch.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
            SetWorldScale(patch.transform, new Vector3((maxX - minX) / 10f, 1f, (maxZ - minZ) / 10f));

            log.AppendLine($"{PathOf(patch.transform)}  x {minX:F2}..{maxX:F2}  z {minZ:F2}..{maxZ:F2}");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Directory.CreateDirectory(OutputFolder);
        File.WriteAllText(Path.Combine(OutputFolder, "roofpatches.txt"), log.ToString());
        Debug.Log("RoofHoleFinder: added roof patches:\n" + log);
    }

    const float SeamStep = 0.05f;
    const float SeamProbeOffset = 0.02f;
    const float SeamMaxWidth = 0.5f;
    const float WallTopMaxDepth = 0.05f;

    // Scan tests one point per CellSize, so it can miss a gap narrower than CellSize.
    // FindSeamGaps tests points along the edges of every roof plane.
    [MenuItem("Haunted House/Roof Holes/Find Seams")]
    static void FindSeams()
    {
        Directory.CreateDirectory(OutputFolder);
        File.WriteAllText(Path.Combine(OutputFolder, "roofseams.txt"), BuildSeamReport(FindSeamGaps(Scan())));
        Debug.Log("RoofHoleFinder: wrote roofseams.txt to " + OutputFolder);
    }

    const float SeamStripOverlap = 0.03f;
    // The strip is above the roof planes, so where the two overlap, the camera below sees the roof.
    const float SeamStripLift = 0.002f;
    const string SeamStripName = "Roof Seam";

    [MenuItem("Haunted House/Roof Holes/Fill Seams")]
    static void FillSeams()
    {
        ScanResult scan = Scan();
        GameObject template = FindRoofTemplate();
        if (template == null)
        {
            throw new System.InvalidOperationException("RoofHoleFinder: no GameObject named Roof with a MeshRenderer in the open scene.");
        }

        Undo.SetCurrentGroupName("Fill Roof Seams");
        int undoGroup = Undo.GetCurrentGroup();

        var log = new StringBuilder();
        foreach (SeamGap gap in FindSeamGaps(scan))
        {
            // The same gap is found from the roof on each side of it. Skip it when an
            // earlier strip already covers it.
            Physics.SyncTransforms();
            Vector3 middle = (gap.edgeStart + gap.edgeEnd) / 2;
            float width = GapWidthAt(middle, gap.outward, scan);
            if (width == 0f) continue;

            Vector3 along = gap.edgeEnd - gap.edgeStart;
            Vector3 alongDirection = along.sqrMagnitude > 0f ? along.normalized : Vector3.Cross(gap.outward, Vector3.up);
            float length = along.magnitude + SeamStep + 2 * SeamStripOverlap;
            float depth = gap.width + 2 * SeamStripOverlap;
            Vector3 centre = middle + gap.outward * (gap.width / 2);
            centre.y = scan.roofY + SeamStripLift;

            Transform parent = gap.roof.transform.parent;
            GameObject strip = CreateRoof(template, parent, SeamStripName);
            // Rotated so the plane's x axis runs along the edge and its z axis runs across the gap.
            strip.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(gap.outward, Vector3.down));
            SetWorldScale(strip.transform, new Vector3(length / 10f, 1f, depth / 10f));
            log.AppendLine($"{PathOf(strip.transform)}  centre ({centre.x:F2}, {centre.z:F2}), {length:F2} m along {alongDirection.x:F0},{alongDirection.z:F0}, {depth:F2} m across");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Directory.CreateDirectory(OutputFolder);
        File.WriteAllText(Path.Combine(OutputFolder, "roofseamstrips.txt"), log.ToString());
        Debug.Log("RoofHoleFinder: added roof seam strips:\n" + log);
    }

    class SeamGap
    {
        public Collider roof;
        public Vector3 edgeStart;
        public Vector3 edgeEnd;
        public Vector3 outward;
        public float width;
    }

    static List<SeamGap> FindSeamGaps(ScanResult scan)
    {
        var gaps = new List<SeamGap>();
        bool hitBackfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = false;
        try
        {
            foreach (MeshCollider roof in Object.FindObjectsOfType<MeshCollider>())
            {
                if (!IsRoofPlane(roof, scan)) continue;

                // The Plane mesh corners are at x and z of -5 and 5.
                Vector3[] corners =
                {
                    roof.transform.TransformPoint(new Vector3(-5f, 0f, -5f)),
                    roof.transform.TransformPoint(new Vector3(5f, 0f, -5f)),
                    roof.transform.TransformPoint(new Vector3(5f, 0f, 5f)),
                    roof.transform.TransformPoint(new Vector3(-5f, 0f, 5f)),
                };
                Vector3 centre = roof.bounds.center;

                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = corners[i];
                    Vector3 b = corners[(i + 1) % 4];
                    Vector3 along = (b - a).normalized;
                    Vector3 outward = Vector3.Cross(Vector3.up, along).normalized;
                    if (Vector3.Dot(outward, (a + b) / 2 - centre) < 0) outward = -outward;

                    SeamGap open = null;
                    float length = Vector3.Distance(a, b);
                    for (float d = SeamStep / 2; d < length; d += SeamStep)
                    {
                        Vector3 edgePoint = a + along * d;
                        float width = GapWidthAt(edgePoint, outward, scan);
                        if (width > 0f)
                        {
                            if (open == null)
                            {
                                open = new SeamGap { roof = roof, edgeStart = edgePoint, outward = outward };
                                gaps.Add(open);
                            }
                            open.edgeEnd = edgePoint;
                            open.width = Mathf.Max(open.width, width);
                        }
                        else
                        {
                            open = null;
                        }
                    }
                }
            }
        }
        finally
        {
            Physics.queriesHitBackfaces = hitBackfaces;
        }
        return gaps;
    }

    // Returns 0 when the point just outside the roof edge needs no roof. Otherwise returns
    // the distance from the edge to the next roof, or SeamMaxWidth when no roof is that close.
    static float GapWidthAt(Vector3 edgePoint, Vector3 outward, ScanResult scan)
    {
        for (float offset = SeamProbeOffset; offset <= SeamMaxWidth; offset += SeamProbeOffset)
        {
            Vector3 probe = edgePoint + outward * offset;
            var top = new Vector3(probe.x, scan.floorY + 5f, probe.z);
            if (!Physics.Raycast(top, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            {
                return offset == SeamProbeOffset ? 0f : offset;
            }
            // A wall top is 13 mm below the roof. A player can see the sky through a roof gap
            // above a wall top, by looking up through that 13 mm space.
            bool isWallTop = hit.point.y > scan.roofY - WallTopMaxDepth;
            if (hit.point.y > scan.floorY + BlockedMinHeight && !isWallTop)
            {
                return offset == SeamProbeOffset ? 0f : offset;
            }

            // The interior test runs its rays at SampleHeight, which is inside the wall here.
            var sample = new Vector3(probe.x, scan.floorY + SampleHeight, probe.z);
            if (!isWallTop && !IsInterior(sample))
            {
                return offset == SeamProbeOffset ? 0f : offset;
            }
            if (Physics.Raycast(sample, Vector3.up, out RaycastHit roof, RoofMaxHeight - SampleHeight, ~0, QueryTriggerInteraction.Ignore)
                && roof.point.y >= scan.floorY + RoofMinHeight)
            {
                return offset == SeamProbeOffset ? 0f : offset;
            }
        }
        return SeamMaxWidth;
    }

    static bool IsRoofPlane(MeshCollider collider, ScanResult scan)
    {
        if (collider.sharedMesh == null || collider.sharedMesh.name != "Plane") return false;
        return Mathf.Abs(collider.bounds.center.y - scan.roofY) < 0.05f && collider.transform.up.y < -0.99f;
    }

    static string BuildSeamReport(List<SeamGap> gaps)
    {
        var report = new StringBuilder();
        foreach (SeamGap gap in gaps)
        {
            report.AppendLine($"{PathOf(gap.roof.transform)}: edge ({gap.edgeStart.x:F2}, {gap.edgeStart.z:F2}) to ({gap.edgeEnd.x:F2}, {gap.edgeEnd.z:F2}), outward ({gap.outward.x:F0}, {gap.outward.z:F0}), gap up to {gap.width:F2} m");
        }
        if (gaps.Count == 0)
        {
            report.AppendLine("no seams");
        }
        return report.ToString();
    }

    static ScanResult Scan()
    {
        // In edit mode a collider stays at its old position for raycasts until this call.
        Physics.SyncTransforms();
        Bounds area = HouseBounds();
        var scan = new ScanResult
        {
            origin = new Vector3(area.min.x, 0f, area.min.z),
            countX = Mathf.CeilToInt(area.size.x / CellSize) + 1,
            countZ = Mathf.CeilToInt(area.size.z / CellSize) + 1,
        };
        scan.cells = new Cell[scan.countX, scan.countZ];
        scan.floors = new Collider[scan.countX, scan.countZ];

        // A roof plane faces down. With back faces off, a ray from above passes through
        // the roof, and a ray from below hits the roof's front face.
        bool hitBackfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = false;
        try
        {
            float topRayStart = area.max.y + 1f;
            var topHits = new RaycastHit?[scan.countX, scan.countZ];
            var topHeights = new List<float>();
            for (int ix = 0; ix < scan.countX; ix++)
            {
                for (int iz = 0; iz < scan.countZ; iz++)
                {
                    var start = new Vector3(scan.origin.x + ix * CellSize, topRayStart, scan.origin.z + iz * CellSize);
                    if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, topRayStart - area.min.y + 1f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        topHits[ix, iz] = hit;
                        topHeights.Add(hit.point.y);
                    }
                }
            }

            if (topHeights.Count == 0)
            {
                throw new System.InvalidOperationException("RoofHoleFinder: no ray from above hit a collider.");
            }
            // Most of the scanned area is open floor, so the median first hit is the floor height.
            topHeights.Sort();
            scan.floorY = topHeights[topHeights.Count / 2];

            var roofHeights = new List<float>();
            for (int ix = 0; ix < scan.countX; ix++)
            {
                for (int iz = 0; iz < scan.countZ; iz++)
                {
                    if (topHits[ix, iz] is RaycastHit top)
                    {
                        scan.floors[ix, iz] = top.collider;
                        scan.cells[ix, iz] = Classify(top, scan.floorY, roofHeights);
                    }
                    else
                    {
                        scan.cells[ix, iz] = Cell.NoFloor;
                    }
                }
            }

            if (roofHeights.Count == 0)
            {
                throw new System.InvalidOperationException($"RoofHoleFinder: no roof was found above the floor at y {scan.floorY:F3}.");
            }
            roofHeights.Sort();
            scan.roofY = roofHeights[roofHeights.Count / 2];
        }
        finally
        {
            Physics.queriesHitBackfaces = hitBackfaces;
        }
        return scan;
    }

    static Cell Classify(RaycastHit top, float floorY, List<float> roofHeights)
    {
        if (top.point.y > floorY + BlockedMinHeight)
        {
            return Cell.Blocked;
        }

        var sample = new Vector3(top.point.x, floorY + SampleHeight, top.point.z);
        if (!IsInterior(sample))
        {
            return Cell.Outside;
        }

        if (Physics.Raycast(sample, Vector3.up, out RaycastHit roof, RoofMaxHeight - SampleHeight, ~0, QueryTriggerInteraction.Ignore)
            && roof.point.y >= floorY + RoofMinHeight)
        {
            roofHeights.Add(roof.point.y);
            return Cell.Roofed;
        }

        return Cell.Hole;
    }

    static bool IsInterior(Vector3 sample)
    {
        int misses = 0;
        for (int i = 0; i < InteriorRayCount; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, i * 360f / InteriorRayCount, 0f) * Vector3.forward;
            if (!Physics.Raycast(sample, direction, InteriorRayLength, ~0, QueryTriggerInteraction.Ignore))
            {
                misses++;
            }
        }
        return misses <= InteriorMaxMisses;
    }

    // Skips colliders wider than 60 m, so a large ground plane does not make the scanned area larger.
    static Bounds HouseBounds()
    {
        bool hasBounds = false;
        var area = new Bounds();
        foreach (Collider collider in Object.FindObjectsOfType<Collider>())
        {
            if (collider.isTrigger) continue;
            Bounds b = collider.bounds;
            if (b.size.x > 60f || b.size.z > 60f) continue;
            if (!hasBounds)
            {
                area = b;
                hasBounds = true;
            }
            else
            {
                area.Encapsulate(b);
            }
        }

        if (!hasBounds)
        {
            throw new System.InvalidOperationException("RoofHoleFinder: the open scene has no colliders.");
        }
        return area;
    }

    // Greedy rectangles that start on a Hole cell. A rectangle can also cover Blocked cells,
    // because the scan has no roof result for a wall or tall furniture.
    static List<RectInt> CoverHoles(ScanResult scan)
    {
        var used = new bool[scan.countX, scan.countZ];
        var rects = new List<RectInt>();
        for (int iz = 0; iz < scan.countZ; iz++)
        {
            for (int ix = 0; ix < scan.countX; ix++)
            {
                if (scan.cells[ix, iz] != Cell.Hole || used[ix, iz]) continue;

                int endX = ix;
                while (endX + 1 < scan.countX && IsFreeToCover(scan, used, endX + 1, iz))
                {
                    endX++;
                }
                while (scan.cells[endX, iz] == Cell.Blocked)
                {
                    endX--;
                }

                int endZ = iz;
                while (endZ + 1 < scan.countZ && RowIsFreeToCover(scan, used, ix, endX, endZ + 1))
                {
                    endZ++;
                }

                for (int x = ix; x <= endX; x++)
                {
                    for (int z = iz; z <= endZ; z++)
                    {
                        used[x, z] = true;
                    }
                }
                rects.Add(new RectInt(ix, iz, endX - ix + 1, endZ - iz + 1));
            }
        }
        return rects;
    }

    static bool RowIsFreeToCover(ScanResult scan, bool[,] used, int fromX, int toX, int z)
    {
        bool hasHole = false;
        for (int x = fromX; x <= toX; x++)
        {
            if (!IsFreeToCover(scan, used, x, z)) return false;
            if (scan.cells[x, z] == Cell.Hole) hasHole = true;
        }
        // A row with only Blocked cells is a wall top, which needs no roof.
        return hasHole;
    }

    static bool IsFreeToCover(ScanResult scan, bool[,] used, int x, int z)
    {
        Cell cell = scan.cells[x, z];
        return !used[x, z] && (cell == Cell.Hole || cell == Cell.Blocked);
    }

    static GameObject FindRoofTemplate()
    {
        foreach (MeshRenderer renderer in Object.FindObjectsOfType<MeshRenderer>())
        {
            if (renderer.gameObject.name == "Roof" && renderer.GetComponent<MeshFilter>() != null)
            {
                return renderer.gameObject;
            }
        }
        return null;
    }

    // Instantiate copies the renderer settings, the collider and the static flags of the template.
    static GameObject CreateRoof(GameObject template, Transform parent, string name)
    {
        GameObject roof = Object.Instantiate(template, parent);
        roof.name = name;
        Undo.RegisterCreatedObjectUndo(roof, "Add Roof");
        return roof;
    }

    static void SetWorldScale(Transform transform, Vector3 worldScale)
    {
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(worldScale.x / parentScale.x, worldScale.y / parentScale.y, worldScale.z / parentScale.z);
    }

    // The ancestor two levels below the scene root that holds this transform,
    // for example "World Holder/Level A/Corridor Slim".
    static Transform GroupOf(Transform transform)
    {
        Transform current = transform;
        while (current.parent != null && current.parent.parent != null && current.parent.parent.parent != null)
        {
            current = current.parent;
        }
        return current.parent != null && current.parent.parent != null ? current : null;
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

    static void WriteImage(ScanResult scan, string path)
    {
        const int pixelsPerCell = 4;
        var texture = new Texture2D(scan.countX * pixelsPerCell, scan.countZ * pixelsPerCell);
        for (int ix = 0; ix < scan.countX; ix++)
        {
            for (int iz = 0; iz < scan.countZ; iz++)
            {
                Color color = ColorOf(scan.cells[ix, iz]);
                for (int px = 0; px < pixelsPerCell; px++)
                {
                    for (int pz = 0; pz < pixelsPerCell; pz++)
                    {
                        texture.SetPixel(ix * pixelsPerCell + px, iz * pixelsPerCell + pz, color);
                    }
                }
            }
        }

        // A white tick every 5 m on both axes.
        for (int ix = 0; ix < scan.countX; ix++)
        {
            float x = scan.origin.x + ix * CellSize;
            if (Mathf.Abs(x / 5f - Mathf.Round(x / 5f)) * 5f >= CellSize / 2) continue;
            for (int p = 0; p < texture.height; p += 2)
            {
                texture.SetPixel(ix * pixelsPerCell, p, Color.white);
            }
        }
        for (int iz = 0; iz < scan.countZ; iz++)
        {
            float z = scan.origin.z + iz * CellSize;
            if (Mathf.Abs(z / 5f - Mathf.Round(z / 5f)) * 5f >= CellSize / 2) continue;
            for (int p = 0; p < texture.width; p += 2)
            {
                texture.SetPixel(p, iz * pixelsPerCell, Color.white);
            }
        }

        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
    }

    static Color ColorOf(Cell cell)
    {
        switch (cell)
        {
            case Cell.Hole: return Color.red;
            case Cell.Roofed: return new Color(0.2f, 0.6f, 0.2f);
            case Cell.Blocked: return new Color(0.3f, 0.3f, 0.6f);
            case Cell.Outside: return new Color(0.35f, 0.35f, 0.35f);
            default: return Color.black;
        }
    }

    static string BuildReport(ScanResult scan)
    {
        var report = new StringBuilder();
        report.AppendLine($"origin x {scan.origin.x:F2} z {scan.origin.z:F2}, cell {CellSize} m, {scan.countX} x {scan.countZ} cells");
        report.AppendLine($"floor y {scan.floorY:F3}, roof y (median of roof hits) {scan.roofY:F3}");
        report.AppendLine("image: x grows to the right, z grows upward (image row 0 is the bottom). White ticks every 5 m.");
        report.AppendLine();

        var seen = new bool[scan.countX, scan.countZ];
        var stack = new Stack<Vector2Int>();
        int holeIndex = 0;
        for (int ix = 0; ix < scan.countX; ix++)
        {
            for (int iz = 0; iz < scan.countZ; iz++)
            {
                if (scan.cells[ix, iz] != Cell.Hole || seen[ix, iz]) continue;

                int count = 0;
                int minX = ix, maxX = ix, minZ = iz, maxZ = iz;
                var groups = new Dictionary<string, int>();
                stack.Push(new Vector2Int(ix, iz));
                seen[ix, iz] = true;
                while (stack.Count > 0)
                {
                    Vector2Int c = stack.Pop();
                    count++;
                    minX = Mathf.Min(minX, c.x);
                    maxX = Mathf.Max(maxX, c.x);
                    minZ = Mathf.Min(minZ, c.y);
                    maxZ = Mathf.Max(maxZ, c.y);

                    Collider floor = scan.floors[c.x, c.y];
                    string floorPath = floor != null ? PathOf(floor.transform) : "(none)";
                    groups.TryGetValue(floorPath, out int n);
                    groups[floorPath] = n + 1;

                    foreach (Vector2Int d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                    {
                        Vector2Int next = c + d;
                        if (next.x < 0 || next.y < 0 || next.x >= scan.countX || next.y >= scan.countZ) continue;
                        if (seen[next.x, next.y] || scan.cells[next.x, next.y] != Cell.Hole) continue;
                        seen[next.x, next.y] = true;
                        stack.Push(next);
                    }
                }

                holeIndex++;
                float area = count * CellSize * CellSize;
                report.AppendLine($"hole {holeIndex}: {area:F2} m2, x {scan.origin.x + minX * CellSize:F2}..{scan.origin.x + maxX * CellSize:F2}, z {scan.origin.z + minZ * CellSize:F2}..{scan.origin.z + maxZ * CellSize:F2}");
                foreach (KeyValuePair<string, int> group in groups)
                {
                    report.AppendLine($"    floor {group.Key}: {group.Value} cells");
                }
            }
        }

        if (holeIndex == 0)
        {
            report.AppendLine("no holes");
        }

        report.AppendLine();
        report.AppendLine("floors under a hole (hole cells / roofed cells / floor plane size):");
        foreach (KeyValuePair<Collider, Vector2Int> floor in CountCellsPerFloor(scan))
        {
            if (floor.Value.x == 0) continue;
            report.AppendLine($"    {PathOf(floor.Key.transform)}: {floor.Value.x} / {floor.Value.y} / {floor.Key.bounds.size.x:F2} x {floor.Key.bounds.size.z:F2}{(IsFloorPlane(floor.Key, scan) ? "" : " (not a floor plane)")}");
        }
        return report.ToString();
    }

    // x is the number of Hole cells, y the number of Roofed cells, for each collider the ray from above hit first.
    static Dictionary<Collider, Vector2Int> CountCellsPerFloor(ScanResult scan)
    {
        var counts = new Dictionary<Collider, Vector2Int>();
        for (int ix = 0; ix < scan.countX; ix++)
        {
            for (int iz = 0; iz < scan.countZ; iz++)
            {
                Collider floor = scan.floors[ix, iz];
                if (floor == null) continue;
                counts.TryGetValue(floor, out Vector2Int count);
                if (scan.cells[ix, iz] == Cell.Hole) count.x++;
                if (scan.cells[ix, iz] == Cell.Roofed) count.y++;
                counts[floor] = count;
            }
        }
        return counts;
    }

    static bool IsFloorPlane(Collider collider, ScanResult scan)
    {
        var meshCollider = collider as MeshCollider;
        if (meshCollider == null || meshCollider.sharedMesh == null || meshCollider.sharedMesh.name != "Plane") return false;
        return Mathf.Abs(collider.bounds.center.y - scan.floorY) < 0.05f && collider.bounds.size.y < 0.01f;
    }
}
