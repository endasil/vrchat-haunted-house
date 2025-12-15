
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

// Automatically sync this objects position and rotation over network
[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class WaypointPatrolU : UdonSharpBehaviour
{
    public float moveSpeed = 1.0f;
    public Transform[] waypoints;
    private Rigidbody m_RigidBody;

    // Make sure late joiners get the current waypoint index.
    [UdonSynced]
    private int m_CurrentWaypointIndex;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        m_RigidBody = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        Transform currentWaypoint = waypoints[m_CurrentWaypointIndex];
        Vector3 currentToTarget = currentWaypoint.position - m_RigidBody.position;

        if (currentToTarget.magnitude <= 0.1f)
        {
            m_CurrentWaypointIndex = (m_CurrentWaypointIndex + 1) % waypoints.Length;
        }

        Quaternion forwardRotation = Quaternion.LookRotation(currentToTarget);
        m_RigidBody.MoveRotation(forwardRotation);
        m_RigidBody.MovePosition(m_RigidBody.position + currentToTarget.normalized * (moveSpeed * Time.deltaTime));
    }
}
