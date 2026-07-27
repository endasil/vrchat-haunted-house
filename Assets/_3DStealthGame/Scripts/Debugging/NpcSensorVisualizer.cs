using UdonSharp;

using UnityEngine;
using UnityEngine.AI;

namespace _3DStealthGame.Scripts.Debugging
{
    [RequireComponent(typeof(LineRenderer))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NpcSensorVisualizer : UdonSharpBehaviour
    {
        [Header("Reference")]
        public GhostAgentBase npc;

        [Header("Line Renderers (assign in Inspector)")]
        public LineRenderer outlineRenderer; // Draws the cone perimeter
        public LineRenderer hearingRenderer; // Draws the hearing radius circle (seeker only)

        [Header("Cone Settings")]
        public int coneSegments = 40;
        public float coneHeight = 1f;
        public float lineWidth = 0.02f;

        [Header("Hearing Settings")]
        public int hearingSegments = 48;

        // Set when npc is a SeekerGhost; the base ghost has no hearing.
        private SeekerGhost seeker;

        void Start()
        {
            if (outlineRenderer == null)
            {
                outlineRenderer = GetComponent<LineRenderer>();
            }
            if (outlineRenderer != null)
            {
                outlineRenderer.widthMultiplier = lineWidth;
            }
            if (npc == null)
            {
                npc = GetComponent<GhostAgentBase>();
            }

            if (npc != null)
            {
                seeker = npc.GetComponent<SeekerGhost>();
            }
            if (hearingRenderer != null)
            {
                hearingRenderer.widthMultiplier = lineWidth;
                hearingRenderer.gameObject.SetActive(seeker != null);
            }
        }

        void Update()
        {
            if (npc == null)
            {
                Debug.LogError("NpcSensorVisualizer has no npc assigned", this);
                enabled = false;
                return;
            }
            if (outlineRenderer == null)
            {
                Debug.LogError("NpcSensorVisualizer has no outlineRenderer assigned", this);
                enabled = false;
                return;
            }

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

            if (seeker != null && hearingRenderer != null)
            {
                float radius = seeker.hearingRange;
                int count = hearingSegments + 1;
                hearingRenderer.positionCount = count;
                for (int i = 0; i < count; i++)
                {
                    float angle = 360f * (float)i / hearingSegments;
                    hearingRenderer.SetPosition(i, origin + AngleToDir(angle) * radius);
                }
            }
        }

        private Vector3 AngleToDir(float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }
    }
}