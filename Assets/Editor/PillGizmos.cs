using Assets._3DStealthGame.Scripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;


public static class PillGizmos
{
    private static readonly Color[] PillColors = {
        Color.green,
        Color.red,
        new Color(0.2f, 0.5f, 1f),
        new Color(0.15f, 0.15f, 0.15f)
    };

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
    static void DrawPillGizmo(Pill pill, GizmoType gizmoType)
    {
        int typeIndex = (int)pill.PillColor;
        Color c = typeIndex < PillColors.Length ? PillColors[typeIndex] : Color.white;

        Gizmos.color = c;
        Gizmos.DrawWireSphere(pill.transform.position, 0.25f);

        Handles.color = c;
        Handles.Label(
            pill.transform.position + Vector3.up * 0.4f,
            pill.PillColor + " Pill"
        );

        var path = new NavMeshPath();
        foreach (DoorU door in Object.FindObjectsOfType<DoorU>())
        {
            if (door.PillColor != pill.PillColor) continue;
            if (!NavMesh.CalculatePath(pill.transform.position, door.transform.position, NavMesh.AllAreas, path))
            {
                Debug.LogWarning($"No NavMesh path from {pill.PillColor} Pill ({pill.name}) to {door.PillColor} Door ({door.name})", door);
                continue;
            }

            Gizmos.color = c;
            for (int i = 0; i < path.corners.Length - 1; i++)
                Gizmos.DrawLine(path.corners[i], path.corners[i + 1]);
        }
    }

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
    static void DrawDoorGizmo(DoorU door, GizmoType gizmoType)
    {
        int typeIndex = (int)door.PillColor;
        Color c = typeIndex < PillColors.Length ? PillColors[typeIndex] : Color.white;

        Gizmos.color = c;
        Gizmos.DrawWireCube(door.transform.position, new Vector3(0.5f, 1f, 0.1f));

        Handles.color = c;
        Handles.Label(
            door.transform.position + Vector3.up * 0.7f,
            door.PillColor.ToString() + " Door"
        );
    }
}
