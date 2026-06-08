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
            
            Vector3 start = WaypointPositions[i].position;
            Vector3 end;

            if (i + 1 != WaypointPositions.Length)
            {
                end = WaypointPositions[i + 1].position;
            }
            else
            {
                end = WaypointPositions[0].position;
            }


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