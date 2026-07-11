#pragma warning disable IDE0090 // Use 'new(...)'
using UdonSharp;

using UnityEngine;
using UnityEngine.AI;

public class WaypointNetwork : UdonSharpBehaviour
{
    public Transform[] WaypointPositions;

#if UNITY_EDITOR
    public void OnDrawGizmos()
    {
        if (WaypointPositions == null) return;

        Gizmos.color = Color.yellow;

        for (int i = 0; i < WaypointPositions.Length; i++)
        {
            if (WaypointPositions[i] == null) continue;

            Transform endWaypoint = i + 1 != WaypointPositions.Length
                ? WaypointPositions[i + 1]
                : WaypointPositions[0];
            if (endWaypoint == null) continue;

            Vector3 start = WaypointPositions[i].position;
            Vector3 end = endWaypoint.position;


            NavMeshPath path = new NavMeshPath();

            bool success = NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path);
            if (!success)
            {
                Debug.LogWarning($"Path calculation failed from {start} to {end}");
            }

            // Draw lines between path corners
            {
                for (int j = 0; j < path.corners.Length - 1; j++)
                {
                    Gizmos.DrawLine(path.corners[j], path.corners[j + 1]);
                }
            }
        }
    }
#endif
}