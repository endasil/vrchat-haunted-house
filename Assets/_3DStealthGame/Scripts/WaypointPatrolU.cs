
using UdonSharp;
using UnityEngine;
using UnityEngine.AI;
using VRC.SDKBase;

[RequireComponent(typeof(NavMeshAgent))]
public class WaypointPatrolU : UdonSharpBehaviour
{
    public float moveSpeed = 1.0f;
    public WaypointNetwork WaypointNetwork;

    private NavMeshAgent _navMeshAgent;

    [UdonSynced]
    private int _currentWaypointIndex;

    private const float NavMeshSampleRadius = 2f;

    void Start()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();
        if (_navMeshAgent == null)
        {
            Debug.LogError($"{gameObject.name} is missing a NavMeshAgent component.");
            return;
        }

        if (WaypointNetwork == null)
        {
            Debug.LogError($"{gameObject.name} does not have a WaypointNetwork assigned in the inspector.");
            return;
        }

        _navMeshAgent.speed = moveSpeed;
        _navMeshAgent.enabled = Networking.IsOwner(gameObject);
    }

    void Update()
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (_navMeshAgent == null || !_navMeshAgent.enabled) return;
        if (WaypointNetwork == null || WaypointNetwork.WaypointPositions == null) return;
        if (WaypointNetwork.WaypointPositions.Length == 0) return;

        if (!_navMeshAgent.hasPath && !_navMeshAgent.pathPending)
        {
            SetDestinationToCurrentWaypoint();
            return;
        }

        if (_navMeshAgent.pathPending) return;

        if (_navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance)
        {
            _currentWaypointIndex = (_currentWaypointIndex + 1) % WaypointNetwork.WaypointPositions.Length;
            SetDestinationToCurrentWaypoint();
        }
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (_navMeshAgent == null) return;

        _navMeshAgent.enabled = Networking.IsOwner(gameObject);

        if (_navMeshAgent.enabled)
        {
            _navMeshAgent.Warp(transform.position);
            SetDestinationToCurrentWaypoint();
        }
    }

    private void SetDestinationToCurrentWaypoint()
    {
        Transform wp = WaypointNetwork.WaypointPositions[_currentWaypointIndex];
        if (wp == null) return;

        if (NavMesh.SamplePosition(wp.position, out var hit, NavMeshSampleRadius, NavMesh.AllAreas))
            _navMeshAgent.SetDestination(hit.position);
    }
}
