using _3DStealthGame.Scripts.Constants;
using Assets._3DStealthGame.Scripts.Enums;

using UdonSharp;

using UnityEngine;
using UnityEngine.AI;

using VRC.SDKBase; 

// The butler ghost: a stately patroller with a narrow forward gaze.
//
// The butler walks a fixed,  waypoint loop and only has a narrow forward vision cone.
// If it sees you it stops, turns to face you, then marches straight at you. When it loses sight it returns to its exact route.
//
// The AI only runs on the owner (NavMeshAgent is disabled on everyone else, where
// the transform comes from VRCObjectSync). The actual "caught" is handled locally
// per client by an ObserverU trigger on the butler, not here.
//
// Shared vision/navigation/ownership plumbing lives in GhostAgentBase.
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class ButlerGhost : GhostAgentBase
{
    // ==============================
    // Patrol
    // ==============================

    public float moveSpeed = 1.0f;
    public WaypointNetwork WaypointNetwork;

    // ==============================
    // Gaze / chase tuning
    // ==============================

    // Half-angle of the forward vision cone (so 25 = a 50° cone). Deliberately
    // narrower than the eye ghost's 45° half-angle so you can slip behind it.
    public float visionHalfAngle = 25f;

    // How far the butler can see.
    public float visionLength = 8f;

    public float chaseSpeed = 2.5f;

    // After losing sight it keeps marching to your last-seen spot for this long
    // before giving up and returning to its route.
    public float loseSightGraceTime = 2f;

    // Brief "stop and turn to face you" beat when first spotting you.
    public float lockOnPauseTime = 0.4f;

    // ==============================
    // Internal state
    // ==============================

    [UdonSynced]
    private int _currentWaypointIndex;

    // Patrol = walking the loop (also covers walking back to the route after a
    // failed chase), LockOn = stopped and turning to face a spotted player,
    // Chase = marching at the player.
    private const int StatePatrol = 0;
    private const int StateLockOn = 1;
    private const int StateChase = 2;
    private int _state = StatePatrol;

    // True once Start validated the agent and waypoint network.
    private bool _initialized;

    private Vector3 _chaseTargetPos;
    private float _lostSightTime;
    private float _lockOnUntil;

    // Turn speed (deg/s) while facing a player.
    private const float TurnSpeed = 240f;

    // While chasing, only request a new path once the player has moved this far
    // from the current path's destination (avoids throwing away a path every frame).
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

        InitGhostAgent();
        if (_navMeshAgent == null) return;
        _navMeshAgent.speed = moveSpeed;
        SetIndicatorSymbol("");

        if (_state == StatePatrol)
        {
            SetDestinationToCurrentWaypoint();
        }

        EnsureAgentCanMove();

        _initialized = true;
    }

    void Update()
    {
        if (!_initialized || !_isOwner) return;

        switch (_state)
        {
            case StatePatrol:
                UpdatePatrol();
                break;
            case StateLockOn:
                UpdateLockOn();
                break;
            case StateChase:
                UpdateChase();
                break;
        }
    }

    // ==============================
    // States
    // ==============================

    private void UpdatePatrol()
    {
        // Spotted someone? Stop and lock on before marching.
        VRCPlayerApi spotted = FindClosestVisiblePlayer(visionHalfAngle, visionLength);
        if (spotted != null)
        {
            BeginLockOn(spotted);
            return;
        }

        AdvanceWaypointIfArrived();
    }

    private void UpdateLockOn()
    {
        // We may have lost them during the pause, so keep facing their last spot.
        VRCPlayerApi spotted = FindClosestVisiblePlayer(visionHalfAngle, visionLength);
        if (spotted != null)
            _chaseTargetPos = spotted.GetPosition();

        TurnTowardsWorldPosition(_chaseTargetPos, TurnSpeed);

        if (Time.time >= _lockOnUntil)
        {
            _state = StateChase;
            _navMeshAgent.speed = chaseSpeed;
            EnsureAgentCanMove();
            SetDestinationOnNavMesh(_chaseTargetPos);
        }
    }

    private void UpdateChase()
    {
        VRCPlayerApi spotted = FindClosestVisiblePlayer(visionHalfAngle, visionLength);
        if (spotted != null)
        {
            // Still visible, so refresh the target and only repath when they've
            // actually moved away from where we're already headed.
            _lostSightTime = -1f;
            Vector3 playerPos = spotted.GetPosition();

            if (!_navMeshAgent.hasPath
                || Vector3.Distance(_navMeshAgent.destination, playerPos) > ChaseRepathDistance)
            {
                _chaseTargetPos = playerPos;
                SetDestinationOnNavMesh(_chaseTargetPos);
            }
            return;
        }

        // Lost sight, so keep marching to the last-seen spot, then give up.
        if (_lostSightTime < 0f)
            _lostSightTime = Time.time;

        if (Time.time - _lostSightTime >= loseSightGraceTime)
            BeginReturn();
    }

    // Head back to the current waypoint and resume the normal patrol loop. The
    // patrol state handles both the walk back and the route itself (and can still
    // spot players on the way), so no separate "return" state is needed.
    private void BeginReturn()
    {
        _state = StatePatrol;
        _navMeshAgent.speed = moveSpeed;
        EnsureAgentCanMove();
        SetIndicatorSymbol("");
        SetDestinationToCurrentWaypoint();
    }

    private void BeginLockOn(VRCPlayerApi player)
    {
        _state = StateLockOn;
        _chaseTargetPos = player.GetPosition();
        _lockOnUntil = Time.time + lockOnPauseTime;
        _lostSightTime = -1f;
        EnsureAgentIsIdle();
        SetIndicatorSymbol(AwarenessIndicator.SpottedPlayer);
    }

    // ==============================
    // Patrol movement
    // ==============================

    // Mirrors the eye ghost's "do I need a destination?" check: skip while a path
    // is computing, skip while still travelling, otherwise advance and head on.
    private void AdvanceWaypointIfArrived()
    {
        Debug.Log($"{gameObject.name} AdvanceWaypointIfArrived: frameTime: {Time.frameCount} " +
                  $"stopped:{_navMeshAgent.isStopped} status: {_navMeshAgent.pathStatus} " +
                  $"hasPath:{_navMeshAgent.hasPath} pending:{_navMeshAgent.pathPending} " +
                  $"remaining:{_navMeshAgent.remainingDistance} speed:{_navMeshAgent.speed} " +
                  $"vel:{_navMeshAgent.velocity.magnitude} desired:{_navMeshAgent.desiredVelocity.magnitude}");

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
        Debug.Log($"{gameObject.name} ft{Time.frameCount} toWaypoint.sqrMagnitude {toWaypoint.sqrMagnitude} threshold * threshold: {threshold * threshold} ");
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
        Debug.Log($"{gameObject.name} SetDesitinationToCurrentWaypoint.");
        Transform wp = WaypointNetwork.WaypointPositions[_currentWaypointIndex];
        if (wp == null)
        {
            Debug.LogError($"{gameObject.name}: waypoint {_currentWaypointIndex} in '{WaypointNetwork.gameObject.name}' is null.");
            return;
        }

        if (!SetDestinationOnNavMesh(wp.position))
        {
            Debug.LogError($"{gameObject.name} frame {Time.frameCount}: waypoint {_currentWaypointIndex} at {wp.position} is not within {NavMeshSampleRadius}m of the NavMesh.");
        }
        else
        {
            Debug.Log($"{gameObject.name} f: {Time.frameCount} New dest: wp[{_currentWaypointIndex}] at {wp.position} real dist: {Vector3.Distance(gameObject.transform.position, wp.position)} agent.remain: {_navMeshAgent.remainingDistance} pathpending: {_navMeshAgent.pathPending} agent dest: {_navMeshAgent.destination} hasPath: {_navMeshAgent.hasPath}");
        }
    }

    // This client just took over driving the butler, so resume the patrol loop from
    // the synced waypoint index.
    protected override void OnBecameAgentOwner()
    {
        if (!_initialized) return;

        _state = StatePatrol;
        _navMeshAgent.speed = moveSpeed;
        EnsureAgentCanMove();
        SetIndicatorSymbol("");
        SetDestinationToCurrentWaypoint();
    }
}
