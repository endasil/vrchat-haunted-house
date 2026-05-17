using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class KeyUGizmos
{
    private static readonly Color[] KeyColors = {
        Color.green,
        Color.red,
        new Color(0.2f, 0.5f, 1f),
        new Color(0.15f, 0.15f, 0.15f)
    };

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
    static void DrawKeyGizmo(KeyU pill, GizmoType gizmoType)
    {
        int typeIndex = (int)pill.pillColor;
        Color c = typeIndex < KeyColors.Length ? KeyColors[typeIndex] : Color.white;

        Gizmos.color = c;
        Gizmos.DrawWireSphere(pill.transform.position, 0.25f);

        Handles.color = c;
        Handles.Label(
            pill.transform.position + Vector3.up * 0.4f,
            pill.pillColor.ToString() + " Pill"
        );

        var path = new NavMeshPath();
        foreach (DoorU door in Object.FindObjectsOfType<DoorU>())
        {
            if (door.keyType != pill.pillColor) continue;
            if (!NavMesh.CalculatePath(pill.transform.position, door.transform.position, NavMesh.AllAreas, path))
            {
                Debug.LogWarning($"No NavMesh path from {pill.pillColor} Pill ({pill.name}) to {door.keyType} Door ({door.name})", door);
                continue;
            }

            // Debug.Log($"NavMesh path found from {KeyTypeHelper.GetName(key.keyType)} Pill ({key.name}) to {KeyTypeHelper.GetName(door.keyType)} Door ({door.name})", door);

            Gizmos.color = c;
            for (int i = 0; i < path.corners.Length - 1; i++)
                Gizmos.DrawLine(path.corners[i], path.corners[i + 1]);
        }
    }

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
    static void DrawDoorGizmo(DoorU door, GizmoType gizmoType)
    {
        int typeIndex = (int)door.keyType;
        Color c = typeIndex < KeyColors.Length ? KeyColors[typeIndex] : Color.white;

        Gizmos.color = c;
        Gizmos.DrawWireCube(door.transform.position, new Vector3(0.5f, 1f, 0.1f));

        Handles.color = c;
        Handles.Label(
            door.transform.position + Vector3.up * 0.7f,
            door.keyType.ToString() + " Door"
        );
    }
}
