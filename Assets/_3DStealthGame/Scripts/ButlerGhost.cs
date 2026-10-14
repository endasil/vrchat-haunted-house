using _3DStealthGame.Scripts.Constants;

using UdonSharp;

using UnityEngine;
using UnityEngine.AI;

using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class ButlerGhost : GhostAgentBase
{
    // ==============================
    // Patrol
    // ==============================

    public float moveSpeed = 1.0f;
    public WaypointNetwork WaypointNetwork;
    public float chaseSpeed = 2.5f;


    public float snowballChaseTime = 10f;

    // ==============================
    // Internal state
    // ==============================

    [UdonSynced]
    private int _currentWaypointIndex;

    private const int StatePatrol = 0;
    private const int StateTurnTowardsPlayer = 1;
    private const int StateChase = 2;
    private int _state = StatePatrol;

    // True once Start validated the agent and waypoint network.
    private bool _initialized;

    // The player who threw the snowball. Null while patrolling.
    private VRCPlayerApi _chaseTargetPlayer;
    private float _chaseEndTime;

    // While chasing, request a new path only once the player has moved this far
    // from the current destination.
    private const float ChaseRepathDistance = 1f;

    void Start()
    {
        if (WaypointNetwork == null)
        {
            Debug.LogError($"{gameObject.name} does not have a WaypointNetwork assigned in the inspector.");
            return;
        }

        if (WaypointNetwork.WaypointPositions.Length == 0)
        {
            Debug.LogError($"{gameObject.name} does not have any waypoints assigned.");
            return;
        }

        base.InitGhostAgent();
        if (_navMeshAgent == null) return;
        _navMeshAgent.speed = moveSpeed;

        if (_isOwner)
        {
            SetDestinationToCurrentWaypoint();
            base.EnsureAgentCanMove();
        }

        _initialized = true;
    }

    void Update()
    {
        if (!_initialized || !_isOwner) return;

        switch (_state)
        {
            case StatePatrol:
                SetNextWaypointIfArrived();
                break;
            case StateTurnTowardsPlayer:
                UpdateTurnTowardsPlayer();
                break;
            case StateChase:
                UpdateChase();
                break;
        }
    }

    // ==============================
    // Snowball Hit Handling
    // ==============================

    // Called over the network by a snowball that hit this butler
    // (Snowball.NotifyButlerHit targets NetworkEventTarget.Owner).
    // NetworkCallable attribute allows network SendNetworkEvent to
    // pass up to 8 arguments to the function. Accepted variable types
    // are the one that can be udon synced:
    // bool, char, byte, sbyte, short, ushort, int, uint, long, ulong, float, double
    // Just having this a public function instead of network event did not work
    // because only that specific client that called the function runs the code,
    // networkevents mean that all clients run their copy of the function.
    [NetworkCallable]
    public void OnSnowballHit(int throwerPlayerId)
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (!_initialized) return;

        VRCPlayerApi thrower = VRCPlayerApi.GetPlayerById(throwerPlayerId);
        if (thrower == null || !thrower.IsValid())
        {
            Debug.LogError($"{gameObject.name}: snowball hit from player id {throwerPlayerId}, who is not a valid player.");
            return;
        }
        
        _chaseTargetPlayer = thrower;
        _chaseEndTime = Time.time + snowballChaseTime;
        base.SetIndicatorSymbol(AwarenessIndicator.SpottedPlayer);

        if (base.HorizontalAngleToPosition(thrower.GetPosition()) > TurnInPlaceStartAngle)
        {
            _state = StateTurnTowardsPlayer;
            base.EnsureAgentIsIdle();
        }
        else
        {
            BeginChase();
        }
    }

    // ==============================
    // States
    // ==============================

    private void UpdateTurnTowardsPlayer()
    {
        if (!IsChaseTargetValid() || Time.time >= _chaseEndTime)
        {
            BeginPatrol();
            return;
        }

        Vector3 playerPosition = _chaseTargetPlayer.GetPosition();
        base.TurnTowardsWorldPosition(playerPosition, turnInPlaceSpeed);

        if (base.HorizontalAngleToPosition(playerPosition) <= TurnInPlaceEndAngle)
        {
            BeginChase();
        }
    }

    private void BeginChase()
    {
        _state = StateChase;
        _navMeshAgent.speed = chaseSpeed;
        base.EnsureAgentCanMove();
        base.SetDestinationOnNavMesh(_chaseTargetPlayer.GetPosition());
    }

    private void UpdateChase()
    {
        if (!IsChaseTargetValid() || Time.time >= _chaseEndTime)
        {
            BeginPatrol();
            return;
        }

        if (_navMeshAgent.pathPending) return;

        Vector3 playerPosition = _chaseTargetPlayer.GetPosition();
        if (!_navMeshAgent.hasPath
            || Vector3.Distance(_navMeshAgent.destination, playerPosition) > ChaseRepathDistance)
        {
            base.SetDestinationOnNavMesh(playerPosition);
        }
    }

    private bool IsChaseTargetValid()
    {
        return _chaseTargetPlayer != null && _chaseTargetPlayer.IsValid();
    }

    private void BeginPatrol()
    {
        _state = StatePatrol;
        _chaseTargetPlayer = null;
        _navMeshAgent.speed = moveSpeed;
        base.EnsureAgentCanMove();
        base.SetIndicatorSymbol("");
        SetDestinationToCurrentWaypoint();
    }

    // ==============================
    // Patrol movement
    // ==============================

    private void SetNextWaypointIfArrived()
    {
        if (_navMeshAgent.pathPending)
        {
            Debug.Log($"{gameObject.name} PathPending.");
            return;
        }
        Transform wp = WaypointNetwork.WaypointPositions[_currentWaypointIndex];
        if (wp == null)
        {
            Debug.LogError($"{gameObject.name} Waypoint is null!");
            return;
        }

        // Real distance to waypoint, _navMeshAgent.remainingDistance is 0 while calculating waypoint.
        Vector3 toWaypoint = wp.position - transform.position;
        toWaypoint.y = 0;
        float threshold = _navMeshAgent.stoppingDistance + 0.5f;

		
		// Not sure if this comparsion is actually faster than
		// using squareroot, should test and se...
        if (toWaypoint.sqrMagnitude  < threshold * threshold)
        {
            Debug.Log($"{gameObject.name} reached waypoint. Advance.");
            _currentWaypointIndex = (_currentWaypointIndex + 1) % WaypointNetwork.WaypointPositions.Length;
            SetDestinationToCurrentWaypoint();
        }
        else if (_navMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            Debug.LogWarning($"{gameObject.name} path invalid, requesting new waypoint {_currentWaypointIndex}.");
            SetDestinationToCurrentWaypoint();
        }
    }

    private void SetDestinationToCurrentWaypoint()
    {
        Transform wp = WaypointNetwork.WaypointPositions[_currentWaypointIndex];
        if (wp == null)
        {
            Debug.LogError($"{gameObject.name}: waypoint {_currentWaypointIndex} in '{WaypointNetwork.gameObject.name}' is null.");
            return;
        }

        if (!base.SetDestinationOnNavMesh(wp.position))
        {
            Debug.LogError($"{gameObject.name} frame {Time.frameCount}: waypoint {_currentWaypointIndex} at {wp.position} is not within {NavMeshSampleRadius}m of the NavMesh.");
        }
    }

    protected override void OnBecameAgentOwner()
    {
        if (!_initialized) return;
        BeginPatrol();
    }
}
