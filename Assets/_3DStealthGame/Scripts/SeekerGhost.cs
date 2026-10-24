#pragma warning disable IDE0056
using _3DStealthGame.Scripts.Constants;
using UnityEngine;
using UnityEngine.AI;

using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
// ReSharper disable SuggestVarOrType_BuiltInTypes

// The seeker ghost: select a random position on the navmesh and walk there.
// If it sees or hear a player, random patrol will be stopped, and it will 
// chase the player. when a player is seen or heard, investigates the last known position after
// losing them, then resumes patrol. 

[DisallowMultipleComponent]
public class SeekerGhost : GhostAgentBase
{
    // ==============================
    // Navigation & Movement Settings
    // ==============================

    // Base movement speed when patrolling
    public float defaultSpeed = 1f;

    // Movement speed when chasing a detected player
    public float playerFoundSpeed = 2f;

    // Ignore positions where the agent would end up x meter from facing a wall.
    private const float WallNearDistance = 1f;

    // While chasing, only request a new path once the player has moved this far
    // from the current path's destination.
    private const float ChaseRepathDistance = 1f;

    // Turn speed (deg/s) used to face the arrival direction when close to a patrol destination.
    private const float ArrivalTurnSpeed = 240f;

    // ==============================
    // Patrol Settings
    // ==============================

    // Maximum random patrol distance
    public float maxWalkDistance = 20f;
    public float minWalkDistance = 5f;

    // ==============================
    // Hearing Settings
    // ==============================

    public float hearingRange = 3f;      // Radius for hearing-based detection

    // ==============================
    // Runtime State: Target
    // ==============================

    private VRCPlayerApi _currentTargetPlayer;      // Currently targeted player
    private bool _isInvestigating;        // True while chasing last known location
    private bool _detectedPlayerLastFrame;
    private bool _isTurningInPlace;

    // ==============================
    // Runtime State: Idle Look-Around
    // ==============================

    // True while the idle look-around is running.
    private bool _isLookingAround;

    // The direction the NPC tries to face while idling (used as the sweep "center").
    private Vector3 _idleBaseDirection = Vector3.forward;

    // End time of the current idle state.
    private float _idleStateEndTime;

    // Tracks which step of the look-around sequence we are in.
    private int _idleLookStep;

    private bool _idleSweepCompleted;

    // One-time look-around tuning.
    public float minLookAroundSweepAngle = 20f;        // minimum sweep half-angle
    public float lookAroundSweepAngle = 60f;          // maximum sweep half-angle
    public float idleTurnSpeed = 120f;
    public float lookAroundEndpointPauseTime = 1.5f;  // seconds to hold at each sweep endpoint before continuing
    public float postSweepPauseDuration = 0.3f;       // seconds to hold at center after sweep completes, before walking

    private bool _idlePausingAtEndpoint;
    private float _idleEndpointPauseUntil;
    private bool _targetPathIsPending;
    private float _currentSweepAngle;
    private float _idleLookTargetAngle;
    // +1 = sweep right first, -1 = sweep left first. Set when _idleBaseDirection is resolved
    // so the sweep continues in the same direction the ghost was already rotating.
    private float _firstSweepSign = 1f;

    private void Start()
    {
        base.InitGhostAgent();
    }

    private void Update()
    {
        // Only run AI logic on one client to make sure the ai behavior is the same for all players.
        if (!_isOwner) return;

        if (_navMeshAgent == null) return;

        WalkAndChaseBehavior();
    }

    // Patrol, but interrupt patrol if vision/hearing detects a player.
    private void WalkAndChaseBehavior()
    {
        // Detection runs before idle look-around so the ghost can spot players during its sweep.
        bool didDetectPlayer = TryDetectBestPlayer(out var detectedPlayer, out var detectedByVision);

        if (didDetectPlayer)
        {
            _currentTargetPlayer = detectedPlayer;

            CancelIdleLookAround();

            Vector3 playerPosition = _currentTargetPlayer.GetPosition();

            // Only a new detection starts the turn. Mid-chase the path can bend around a wall,
            // so the player can be more than 90 degrees off while the ghost follows the path.
            float angleToPlayer = base.HorizontalAngleToPosition(playerPosition);
            if (!_detectedPlayerLastFrame && angleToPlayer > TurnInPlaceStartAngle)
            {
                _isTurningInPlace = true;
            }
            else if (_isTurningInPlace && angleToPlayer <= TurnInPlaceEndAngle)
            {
                _isTurningInPlace = false;
            }
            _detectedPlayerLastFrame = true;

            if (_isTurningInPlace)
            {
                base.EnsureAgentIsIdle();
                base.TurnTowardsWorldPosition(playerPosition, turnInPlaceSpeed);
            }
            else
            {
                base.EnsureAgentCanMove();
            }

            _navMeshAgent.speed = playerFoundSpeed;

            if (!_navMeshAgent.pathPending)
            {
                bool needNewPath = !_navMeshAgent.hasPath
                    || Vector3.Distance(_navMeshAgent.destination, playerPosition) > ChaseRepathDistance;

                if (needNewPath)
                {
                    bool pathSet = base.SetDestinationOnNavMesh(playerPosition);
                    if (!pathSet)
                    {
                        Debug.LogError($"{gameObject.name} SetDestination failed to find a path to the player with samplePosition. player pos: {playerPosition} from agent at: agentPos={transform.position}");
                    }
                }
            }

            // The rotation looked odd when the player is close, so use manual rotation when within hearing distance.
            float distanceToTarget = Vector3.Distance(transform.position, base.GetPlayerHeadPosition(_currentTargetPlayer));
            if (!_isTurningInPlace && distanceToTarget <= hearingRange)
            {
                _navMeshAgent.updateRotation = false;
                base.TurnTowardsWorldPosition(playerPosition, _navMeshAgent.angularSpeed);
            }

            _isInvestigating = true;
            base.SetIndicatorSymbol(detectedByVision ? AwarenessIndicator.SpottedPlayer : AwarenessIndicator.HeardSomething);
            return;
        }

        _detectedPlayerLastFrame = false;
        _isTurningInPlace = false;

        // Only rotate toward arrival direction during patrol
        if (!_isInvestigating)
        {
            RotateTowardsSteeringTargetWhenCloseToArrival();
        }

        if (UpdateIdleLookAroundIfActive())
        {
            return;
        }

        base.EnsureAgentCanMove();

        // If we already started investigating, keep moving until we reach the destination.
        // While a path is pending, hasPath and remainingDistance still describe the previous path.
        if (_isInvestigating && 
            (_navMeshAgent.pathPending || (_navMeshAgent.hasPath && _navMeshAgent.remainingDistance > _navMeshAgent.stoppingDistance)))
        {
            _navMeshAgent.speed = playerFoundSpeed;
            base.SetIndicatorSymbol(AwarenessIndicator.InvestigatingLastKnown);
            return;
        }

        _isInvestigating = false;
        _currentTargetPlayer = null;
        _navMeshAgent.speed = defaultSpeed;
        base.SetIndicatorSymbol("");

        // If a new patrol destination is needed, start look around logic which
        // will also select a new target.
        if (!_navMeshAgent.hasPath || _navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance)
        {
            PickDestinationAndStartLookAround();
        }
    }

    // ==============================
    // Snowball Hit Handling
    // ==============================

    // Called over the network by a snowball that hit this ghost
    // (Snowball.NotifyGhostHit targets NetworkEventTarget.Owner).
    [NetworkCallable]
    public void OnSnowballHit(Vector3 throwerPosition)
    {
        if (!_isOwner) return;
        if (_navMeshAgent == null) return;

        // Ignore this if already chasing a player or investigating, don't override that.
        if (_isInvestigating) return;

        CancelIdleLookAround();
        base.EnsureAgentCanMove();
        _navMeshAgent.speed = defaultSpeed;
        base.SetDestinationOnNavMesh(throwerPosition);
        _isInvestigating = true;
        base.SetIndicatorSymbol(AwarenessIndicator.HeardSomething);
    }

    // Called over the network by a snowball that hit something near this ghost
    // (Snowball.AlertSeekersNearImpact targets NetworkEventTarget.Owner).
    [NetworkCallable]
    public void OnSnowballImpact(Vector3 impactPosition)
    {
        if (!_isOwner) return;
        if (_navMeshAgent == null) return;

        // The ghost ignores impacts while it chases a player it sees or hears.
        if (_detectedPlayerLastFrame) return;

        if (!CanSeeOrHearPosition(impactPosition)) return;

        // False when no NavMesh point is within NavMeshSampleRadius of the impact, for example a hit high on a wall.
        if (!base.SetDestinationOnNavMesh(impactPosition)) return;

        CancelIdleLookAround();
        base.EnsureAgentCanMove();
        _isInvestigating = true;
        base.SetIndicatorSymbol(AwarenessIndicator.HeardSomething);
    }

    private bool CanSeeOrHearPosition(Vector3 worldPosition)
    {
        float distance = Vector3.Distance(transform.position, worldPosition);
        if (distance <= hearingRange) return true;

        return distance <= visionLength && base.IsWithinViewCone(worldPosition, visionHalfAngle);
    }

    private void RotateTowardsSteeringTargetWhenCloseToArrival()
    {
        if (_isLookingAround) return;

        if (_navMeshAgent.pathPending) return;

        if (!_navMeshAgent.hasPath) return;

        if (_navMeshAgent.remainingDistance >= _navMeshAgent.stoppingDistance + 1f) return;

        base.TurnTowardsWorldPosition(_navMeshAgent.steeringTarget, ArrivalTurnSpeed);
    }

    // ==============================
    // Detection Logic
    // ==============================

    // Attempts to detect the best player via vision first, then hearing.
    private bool TryDetectBestPlayer(out VRCPlayerApi bestDetectedPlayer, out bool bestDetectedByVision)
    {
        bestDetectedPlayer = base.FindClosestVisiblePlayer(visionHalfAngle, visionLength);
        if (bestDetectedPlayer != null)
        {
            bestDetectedByVision = true;
            return true;
        }

        bestDetectedPlayer = base.FindClosestPlayerWithin(hearingRange);
        bestDetectedByVision = false;
        return bestDetectedPlayer != null;
    }

    // ==============================
    // Idle Look-Around (State Machine)
    // ==============================

    private void PickDestinationAndStartLookAround()
    {
        _navMeshAgent.stoppingDistance = 0.4f;
        if (!TryGetRandomNavMeshPosition(out Vector3 nextDestination))
        {
            return;
        }

        _navMeshAgent.SetDestination(nextDestination);
        _targetPathIsPending = true;
        base.EnsureAgentIsIdle();

        _currentSweepAngle = Random.Range(minLookAroundSweepAngle, lookAroundSweepAngle);
        _idleLookStep = 0;
        _idleLookTargetAngle = 0f; // first target is center (base direction)
        _isLookingAround = true;
        _idleSweepCompleted = false;
        _idlePausingAtEndpoint = false;
    }

    // Return true if this logic did the work for this frame.
    private bool UpdateIdleLookAroundIfActive()
    {
        if (!_isLookingAround) return false;

        base.EnsureAgentIsIdle();

        UpdateIdleHoldAndSweep();

        if (_idleSweepCompleted && Time.time >= _idleStateEndTime)
        {
            CancelIdleLookAround();
            base.EnsureAgentCanMove();
        }

        return true;
    }

    private void CancelIdleLookAround()
    {
        _targetPathIsPending = false;
        _isLookingAround = false;
        _idleSweepCompleted = false;
        _idlePausingAtEndpoint = false;
    }

    // Sweep runs immediately from the ghost's current facing, with no pre-rotation.
    // Flow: current angle → first endpoint (pause) → center → second endpoint (pause) → center (done).
    private void UpdateIdleHoldAndSweep()
    {
        if (_targetPathIsPending)
        {
            // The sweep is centred on steeringTarget, which belongs to the previous path while pathPending is true.
            if (_navMeshAgent.pathPending) return;

            _targetPathIsPending = false;
            Vector3 destOffset = _navMeshAgent.steeringTarget - transform.position;
            destOffset.y = 0f;
            _idleBaseDirection = destOffset.sqrMagnitude > 0.01f ? destOffset.normalized : transform.forward;

            // Determine which side of center the ghost is currently on so the sweep
            // continues in the same direction it was already rotating toward center.
            // Positive signed angle = ghost is clockwise (right) of base = rotating left → sweep left first.
            float signedAngleFromBase = Vector3.SignedAngle(_idleBaseDirection, transform.forward, Vector3.up);
            _firstSweepSign = signedAngleFromBase > 0f ? -1f : 1f;
        }

        if (_idleSweepCompleted)
        {
            transform.rotation = Quaternion.LookRotation(_idleBaseDirection, Vector3.up);
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(
            Quaternion.AngleAxis(_idleLookTargetAngle, Vector3.up) * _idleBaseDirection, Vector3.up);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRotation, idleTurnSpeed * Time.deltaTime);

        if (Quaternion.Angle(transform.rotation, targetRotation) < 2f)
        {
            // Pause at endpoints (steps 1 and 2), but not at center (steps 0 and 3)
            if ((_idleLookStep == 1 || _idleLookStep == 2) && !_idlePausingAtEndpoint)
            {
                _idlePausingAtEndpoint = true;
                _idleEndpointPauseUntil = Time.time + lookAroundEndpointPauseTime;
                return;
            }

            if (_idlePausingAtEndpoint && Time.time < _idleEndpointPauseUntil) return;
            _idlePausingAtEndpoint = false;
            _idleLookStep++;

            switch (_idleLookStep)
            {
                case 1: // reached center, continue in the direction we were already rotating
                    _idleLookTargetAngle = _firstSweepSign * _currentSweepAngle;
                    break;
                case 2: // reached first endpoint, sweep to the opposite side
                    _idleLookTargetAngle = -_firstSweepSign * _currentSweepAngle;
                    break;
                case 3: // reached the second endpoint, return to center
                    _idleLookTargetAngle = 0f;
                    break;
                case 4: // back at center, done
                    _idleSweepCompleted = true;
                    _idleStateEndTime = Time.time + postSweepPauseDuration;
                    break;
            }
        }
    }

    // ==============================
    // Patrol Destination Selection
    // ==============================

    // Check if the AI would end on a place with a wall within 1
    // meter in front of it.
    private bool IsFacingWallOnArrival(Vector3 destination)
    {
        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, path)) return false;

        if (path.corners.Length < 2) return false;

        var lastCorner = path.corners[path.corners.Length - 1];

        var secondToLastCorner = path.corners[path.corners.Length - 2];

        Vector3 arrivalDirection = (lastCorner - secondToLastCorner).normalized;

        return Physics.Raycast(lastCorner, arrivalDirection, WallNearDistance);
    }

    // Returns a random reachable NavMesh position around the NPC,
    // with basic rejection to avoid wall-facing arrivals.
    private bool TryGetRandomNavMeshPosition(out Vector3 position)
    {
        // Default to current position if no valid position is found.
        position = transform.position;
        bool hasFallback = false;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            float randomOffsetX = Random.Range(-maxWalkDistance, maxWalkDistance);
            float randomOffsetZ = Random.Range(-maxWalkDistance, maxWalkDistance);

            randomOffsetX = Mathf.Sign(randomOffsetX) * Mathf.Clamp(Mathf.Abs(randomOffsetX), minWalkDistance, maxWalkDistance);
            randomOffsetZ = Mathf.Sign(randomOffsetZ) * Mathf.Clamp(Mathf.Abs(randomOffsetZ), minWalkDistance, maxWalkDistance);

            Vector3 desiredWorldPosition = transform.position + new Vector3(randomOffsetX, 0, randomOffsetZ);

            if (NavMesh.SamplePosition(desiredWorldPosition, out var navMeshHit, NavMeshSampleRadius, NavMesh.AllAreas))
            {
                position = navMeshHit.position;
                if (!IsFacingWallOnArrival(position)) return true;

                hasFallback = true;
            }
        }

        return hasFallback;
    }

    // ==============================
    // VRChat Player Events
    // ==============================

    public override void OnPlayerLeft(VRCPlayerApi playerThatLeft)
    {
        if (_currentTargetPlayer != null &&
            playerThatLeft != null &&
            _currentTargetPlayer.playerId == playerThatLeft.playerId)
        {
            _currentTargetPlayer = null;
            _isInvestigating = false;
        }
    }

    protected override void OnBecameAgentOwner()
    {
        _currentTargetPlayer = null;
        _isInvestigating = false;
        _detectedPlayerLastFrame = false;
        _isTurningInPlace = false;
        CancelIdleLookAround();
    }
}
