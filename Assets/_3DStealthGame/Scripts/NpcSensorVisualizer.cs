using UdonSharp;

using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class NpcSensorVisualizer : UdonSharpBehaviour
{
    [Header("Reference")]
    public SeekerGhost npc;

    [Header("Line Renderers (assign in Inspector)")]
    public LineRenderer outlineRenderer; // Draws the cone perimeter
    
    [Header("Cone Settings")]
    public int coneSegments = 40;
    public float coneHeight = 1f;

    void Update()
    {
        if (npc == null) return;

        float halfAngle = npc.visionHalfAngle;
        float length = npc.visionLength;
        Vector3 origin = npc.transform.position + Vector3.up * coneHeight;
        float forwardAngle = npc.transform.eulerAngles.y;

        // Outline: origin -> left tip -> arc -> right tip -> origin
        int outlineCount = coneSegments + 3;
        outlineRenderer.positionCount = outlineCount;
        outlineRenderer.SetPosition(0, origin);
        outlineRenderer.SetPosition(1, origin + AngleToDir(forwardAngle - halfAngle) * length);

        for (int i = 0; i < coneSegments; i++)
        {
            float t = (float)i / (coneSegments - 1);
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            outlineRenderer.SetPosition(2 + i, origin + AngleToDir(forwardAngle + angle) * length);
        }

        outlineRenderer.SetPosition(outlineCount - 1, origin);

    }

    private Vector3 AngleToDir(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
    }
}