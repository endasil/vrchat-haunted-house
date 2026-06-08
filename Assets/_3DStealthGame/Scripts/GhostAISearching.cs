#pragma warning disable IDE0056
using Assets._3DStealthGame.Scripts.Enums;

using TMPro;

using UdonSharp;

using UnityEngine;
using UnityEngine.AI;

using VRC.SDKBase;
// ReSharper disable SuggestVarOrType_BuiltInTypes

public class GhostAISearching : UdonSharpBehaviour
{
    // ==============================
    // Navigation & Movement Settings
    // ==============================

    // NavMesh agent used for pathfinding and movement
    private NavMeshAgent _navMeshAgent;


    // Base movement speed when patrolling
    public float defaultSpeed = 1f;

    // Movement speed when chasing a detected player
    public float playerFoundSpeed = 2f;

    // Ignore positions where the agent would end up x meter from facing a wall.
    private readonly float _wallNearDistance = 1f;

    // ==============================
    // Patrol Settings
    // ==============================

    // Maximum random patrol distance
    public float maxWalkDistance = 20f;
    public float minWalkDistance = 5f;

    // ==============================
    // Vision Settings
    // ==============================

    public float sideVisionAngle = 45f; // half-angle vision cone
    public float visionLength = 10f;    // Maximum sight distance

    // ==============================
    // Hearing Settings
    // ==============================

    public float hearingRange = 3f;      // Radius for hearing-based detection

    // ==============================
    // Targeting
    // ==============================

    public float retargetInterval = 0.2f;            // How often to re-evaluate target

    //==============================
    // Component References
    //==============================

    public TextMeshPro Indicator;  // Displays AI state symbol (! ? ~ .)


    // ==============================
    // Snowball Interaction
    // ==============================

    public Snowball[] snowballs;
    private int[] _lastSnowballHitEventIds;

    // ==============================
    // Runtime State: Players & Target
    // ==============================

    private VRCPlayerApi[] _playerCache;
    private VRCPlayerApi _currentTargetPlayer;       // Currently targeted player
    private float _lastRetargetTime;                 // Time of last retarget check
    private bool _isInvestigating;        // True while chasing last known location

    // ==============================
    // Runtime State: Idle Look-Around
    // ==============================

    // Current idle state.
    private IdleLookAroundState _idleLookAroundState = IdleLookAroundState.NotIdle;

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
        // Cache references to not have to look them up at runtime
        Indicator = GetComponentInChildren<TextMeshPro>();

        // If navMeshAgent reference not set in inspector, try to find one on the same GameObject
        if (!_navMeshAgent)
            _navMeshAgent = GetComponent<NavMeshAgent>();

        // Only the owner drives the agent. On everyone else the transform comes from VRCObjectSync,
        // so a live agent here would just fight the synced position. Keep it off on non-owners.
        if (_navMeshAgent != null)
            _navMeshAgent.enabled = Networking.IsOwner(gameObject);

        // Preallocate player cache, can't use lists in U# :(
        _playerCache = new VRCPlayerApi[80];

        // Make sure we can find a target right away.
        _lastRetargetTime = -999f;

        // Snapshot current snowball hit counters so we don't react to hits that happened before we joined.
        if (snowballs != null)
        {
            _lastSnowballHitEventIds = new int[snowballs.Length];
            for (int i = 0; i < snowballs.Length; i++)
                if (snowballs[i] != null)
                    _lastSnowballHitEventIds[i] = snowballs[i].syncedGhostHitEventId;
        }

    }

    private void Update()
    {
        // Only run AI logic on one client to make sure the ai behavior is the same for all players.
        if (!Networking.IsOwner(gameObject)) return;


        if (_navMeshAgent == null) return;

        UpdateRetargetingIfNeeded();
        CheckSnowballHits();

        WalkAndChaseBehavior();

    }

    // ==============================
    // Update Subsystems
    // ==============================

    private void UpdateRetargetingIfNeeded()
    {
        // Periodic retarget (handles join/leave)
        if (Time.time - _lastRetargetTime < retargetInterval) return;
        _lastRetargetTime = Time.time;

        if (!IsValidTargetPlayer(_currentTargetPlayer))
            _currentTargetPlayer = FindNearestValidPlayer();
    }

    // Patrol, but interrupt patrol if vision/hearing detects a player.
    private void WalkAndChaseBehavior()
    {
        // Detection runs before idle look-around so the ghost can spot players during its sweep.
        bool didDetectPlayer = TryDetectBestPlayer(out var detectedPlayer, out var detectedByVision);

        if (didDetectPlayer)
        {
            Debug.Log("Detected player: " + detectedPlayer.displayName + " by " + (detectedByVision ? "vision" : "hearing"));
            // Only pick a new target if we don't already have one — stick to current target until patrol resumes.
            if (!IsValidTargetPlayer(_currentTargetPlayer))
                _currentTargetPlayer = detectedPlayer;
            else if (_currentTargetPlayer.playerId != detectedPlayer.playerId)
                Debug.LogWarning($"[GhostAI] Detected {detectedPlayer.displayName} but keeping existing target {_currentTargetPlayer.displayName}");

            CancelIdleLookAround();
            EnsureAgentCanMove();

            _navMeshAgent.speed = playerFoundSpeed;
            bool pathSet = _navMeshAgent.SetDestination(GetPlayerHeadPosition(_currentTargetPlayer));
            if (!pathSet)
                Debug.LogError($"[GhostAI] SetDestination FAILED. isStopped={_navMeshAgent.isStopped}, agentPos={transform.position}");
            else
                Debug.Log($"[GhostAI] Chasing {_currentTargetPlayer.displayName} — hasPath={_navMeshAgent.hasPath}, pathStatus={_navMeshAgent.pathStatus}, speed={_navMeshAgent.speed}, isStopped={_navMeshAgent.isStopped}");

            _isInvestigating = true;
            SetIndicatorText(detectedByVision ? AwarenessIndicator.SpottedPlayer : AwarenessIndicator.HeardSomething);
            return;
        }

        // Only rotate toward arrival direction during patrol, not while chasing.
        if (!_isInvestigating)
            RotateTowardsSteeringTargetWhenCloseToArrival();

        // if idle look around is active, it will handle this frame
        if (UpdateIdleLookAroundIfActive())
        {
            return;
        }

        EnsureAgentCanMove();

        // If we already started investigating, keep moving until we reach the destination.
        if (_isInvestigating && _navMeshAgent.hasPath && _navMeshAgent.remainingDistance > _navMeshAgent.stoppingDistance)
        {
            Debug.Log("Is investigating");
            _navMeshAgent.speed = playerFoundSpeed;
            SetIndicatorText(AwarenessIndicator.InvestigatingLastKnown);
            return;
        }

        if (_isInvestigating)
            Debug.Log($"[GhostAI] Investigation ended — hasPath={_navMeshAgent.hasPath}, pathStatus={_navMeshAgent.pathStatus}, remainingDist={_navMeshAgent.remainingDistance:F2}");

        _isInvestigating = false;
        _currentTargetPlayer = null;
        _navMeshAgent.speed = defaultSpeed;
        SetIndicatorText(AwarenessIndicator.SearchingForPlayers);

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

    private void CheckSnowballHits()
    {
        if (snowballs == null || _lastSnowballHitEventIds == null) return;
        for (int i = 0; i < snowballs.Length; i++)
        {
            if (snowballs[i] == null) continue;
            if (snowballs[i].syncedGhostHitEventId == _lastSnowballHitEventIds[i]) continue;
            _lastSnowballHitEventIds[i] = snowballs[i].syncedGhostHitEventId;
            HandleSnowballHit(snowballs[i].syncedThrowerPosition);
        }
    }

    private void HandleSnowballHit(Vector3 throwerPosition)
    {
        // Ignore if already chasing a player or investigating — no override.
        if (IsValidTargetPlayer(_currentTargetPlayer) || _isInvestigating) return;

        CancelIdleLookAround();
        EnsureAgentCanMove();
        _navMeshAgent.speed = defaultSpeed;
        _navMeshAgent.SetDestination(throwerPosition);
        _isInvestigating = true;
        SetIndicatorText(AwarenessIndicator.HeardSomething);
    }

    // ==============================
    // Player Validation & Targeting
    // ==============================

    private bool IsValidTargetPlayer(VRCPlayerApi playerCandidate)
    {
        return playerCandidate != null && playerCandidate.IsValid();
    }

    // Finds the player closest to this npc in world
    private VRCPlayerApi FindNearestValidPlayer()
    {
        int totalPlayerCount = VRCPlayerApi.GetPlayerCount();
        VRCPlayerApi.GetPlayers(_playerCache);

        VRCPlayerApi closestPlayer = null;
        float closestPlayerDistanceSquared = float.MaxValue;

        Vector3 agentPosition = transform.position;

        for (int playerIndex = 0; playerIndex < totalPlayerCount && playerIndex < _playerCache.Length; playerIndex++)
        {
            VRCPlayerApi playerCandidate = _playerCache[playerIndex];
            if (playerCandidate == null || !playerCandidate.IsValid()) continue;

            Vector3 playerPosition = playerCandidate.GetPosition();
            float squaredDistanceToPlayer = (playerPosition - agentPosition).sqrMagnitude;

            if (squaredDistanceToPlayer < closestPlayerDistanceSquared)
            {
                closestPlayerDistanceSquared = squaredDistanceToPlayer;
                closestPlayer = playerCandidate;
            }
        }

        return closestPlayer;
    }

    // Better to focus on the head than the root of the body since this is where the player experience that they are at.
    private Vector3 GetPlayerHeadPosition(VRCPlayerApi player)
    {
        VRCPlayerApi.TrackingData headTrackingData = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        return headTrackingData.position;
    }

    // ==============================
    // NavMeshAgent Helpers
    // ==============================

    // Allow the navmesh agent to move and rotate the npc
    private void EnsureAgentCanMove()
    {
        _navMeshAgent.isStopped = false;
        _navMeshAgent.updateRotation = true;
    }

    // Prevent the navmesh agent from controlling the rotation and position of the npc 
    // so we can move it from code directly.
    private void EnsureAgentIsIdle()
    {
        _navMeshAgent.isStopped = true;
        _navMeshAgent.updateRotation = false;
    }

    // Sets the indicator text based on logical AI awareness state instead of raw strings.
    private void SetIndicatorText(AwarenessIndicator awarenessIndicator)
    {
        if (Indicator == null) return;

        switch (awarenessIndicator)
        {
            case AwarenessIndicator.SearchingForPlayers:
                Indicator.text = "?";
                break;

            case AwarenessIndicator.HeardSomething:
                Indicator.text = "~";
                break;

            case AwarenessIndicator.SpottedPlayer:
                Indicator.text = "!";
                break;

            case AwarenessIndicator.InvestigatingLastKnown:
                Indicator.text = ".";
                break;
        }
    }

    // Smoothly rotates toward next steering target
    private void TurnTowardsSteeringTarget()
    {
        Vector3 directionToSteeringTarget = _navMeshAgent.steeringTarget - transform.position;
        directionToSteeringTarget.y = 0f;

        if (directionToSteeringTarget.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(directionToSteeringTarget, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            _navMeshAgent.angularSpeed * Time.deltaTime);
    }

    private bool RotateTowardsSteeringTargetWhenCloseToArrival()
    {
        if (_idleLookAroundState != IdleLookAroundState.NotIdle)
        {
            return false;
        }

        if (_navMeshAgent.pathPending)
        {
            Debug.Log("Path pending");
            return false;
        }

        if (_navMeshAgent.hasPath == false)
        {
            // Debug.Log("No path yet");
            return false;
        }

        if (_navMeshAgent.remainingDistance >= _navMeshAgent.stoppingDistance + 1f) return false;

        Vector3 directionToSteeringTarget = _navMeshAgent.steeringTarget - transform.position;
        // Debug.Log("direction to steering target" + directionToSteeringTarget);
        // Debug.Log("Angle to steering target: " + Vector3.Angle(transform.forward, directionToSteeringTarget));
        directionToSteeringTarget.y = 0f;
        if (directionToSteeringTarget.sqrMagnitude <= 0.0001f) return true;

        Quaternion facingRotation = Quaternion.LookRotation(directionToSteeringTarget, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, facingRotation, 240f * Time.deltaTime);

        var arrivalFacingAngleTolerance = 5f;
        float angleToFacingRotation = Quaternion.Angle(transform.rotation, facingRotation);
        return angleToFacingRotation <= arrivalFacingAngleTolerance;
    }


    // ==============================
    // Detection Logic
    // ==============================

    // Attempts to detect the best player via vision first, then hearing
    private bool TryDetectBestPlayer(out VRCPlayerApi bestDetectedPlayer, out bool bestDetectedByVision)
    {
        bestDetectedPlayer = null;
        bestDetectedByVision = false;

        int totalPlayerCount = VRCPlayerApi.GetPlayerCount();
        VRCPlayerApi.GetPlayers(_playerCache);

        Vector3 agentPosition = transform.position;

        float closestVisionDistanceSquared = float.MaxValue;
        float closestHearingDistanceSquared = float.MaxValue;

        VRCPlayerApi closestVisionPlayer = null;
        VRCPlayerApi closestHearingPlayer = null;

        for (int playerIndex = 0; playerIndex < totalPlayerCount && playerIndex < _playerCache.Length; playerIndex++)
        {
            VRCPlayerApi playerCandidate = _playerCache[playerIndex];
            if (playerCandidate == null || !playerCandidate.IsValid()) continue;

            Vector3 playerHeadPosition = GetPlayerHeadPosition(playerCandidate);
            Vector3 offsetToPlayer = playerHeadPosition - agentPosition;

            float squaredDistanceToPlayer = offsetToPlayer.sqrMagnitude;
            float distanceToPlayer = Mathf.Sqrt(squaredDistanceToPlayer);

            bool isInVisionRange = distanceToPlayer <= visionLength;
            bool isInHearingRange = distanceToPlayer <= hearingRange;

            if (isInVisionRange && IsWithinViewCone(playerHeadPosition))
            {
                if (squaredDistanceToPlayer < closestVisionDistanceSquared)
                {
                    closestVisionDistanceSquared = squaredDistanceToPlayer;
                    closestVisionPlayer = playerCandidate;
                }
                continue;
            }

            if (isInHearingRange && squaredDistanceToPlayer < closestHearingDistanceSquared)
            {
                closestHearingDistanceSquared = squaredDistanceToPlayer;
                closestHearingPlayer = playerCandidate;
            }
        }

        if (closestVisionPlayer != null)
        {
            bestDetectedPlayer = closestVisionPlayer;
            bestDetectedByVision = true;
            return true;
        }

        if (closestHearingPlayer != null)
        {
            bestDetectedPlayer = closestHearingPlayer;
            bestDetectedByVision = false;
            return true;
        }

        return false;
    }

    // Checks if target is inside view cone and nothing is in the way
    private bool IsWithinViewCone(Vector3 targetWorldPosition)
    {
        Vector3 raycastOrigin = transform.position + new Vector3(0f, 0.5f, 0f);
        Vector3 offsetToTarget = targetWorldPosition - raycastOrigin;

        float distanceToTarget = offsetToTarget.magnitude;
        if (distanceToTarget <= 0.0001f) return true;

        Vector3 directionToTarget = offsetToTarget / distanceToTarget;

        float cosineAngleToTarget = Vector3.Dot(directionToTarget, transform.forward);
        float angleToTargetDegrees = Mathf.Acos(Mathf.Clamp(cosineAngleToTarget, -1f, 1f)) * Mathf.Rad2Deg;

        if (angleToTargetDegrees > sideVisionAngle) return false;


        bool hitSomething = Physics.Raycast(
            raycastOrigin,
            directionToTarget,
            out var raycastHit,
            visionLength,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        if (!hitSomething)
        {
            // No obstruction detected within vision length
            return true;
        }

        // If the first hit is basically at the target distance, treat as visible.
        bool hitIsAtOrBeyondTarget = raycastHit.distance >= distanceToTarget - 0.2f;
        return hitIsAtOrBeyondTarget;
    }

    // ==============================
    // Idle Look-Around (State Machine)
    // ==============================

    private void PickDestinationAndStartLookAround()
    {
        Debug.Log("Start idle lookaround.");
        _navMeshAgent.autoBraking = true;
        _navMeshAgent.stoppingDistance = 0.4f;
        _navMeshAgent.speed = defaultSpeed;
        if (!TryGetRandomNavMeshPosition(out Vector3 nextDestination, out bool wallAhead))
        {
            Debug.Log($"StartIdleLookAround: no valid NavMesh position found, skipping sweep — will retry next frame. wallAhead: {wallAhead}");
            return;
        }

        _navMeshAgent.SetDestination(nextDestination);
        _targetPathIsPending = true;
        EnsureAgentIsIdle();

        _currentSweepAngle = Random.Range(minLookAroundSweepAngle, lookAroundSweepAngle);
        _idleLookStep = 0;
        _idleLookTargetAngle = 0f; // first target is center (base direction)
        _idleLookAroundState = IdleLookAroundState.LookAround;
        _idleSweepCompleted = false;
        _idlePausingAtEndpoint = false;

        SetIndicatorText(AwarenessIndicator.SearchingForPlayers);
    }


    // Return true if this logic did the work for this frame.
    private bool UpdateIdleLookAroundIfActive()
    {
        if (_idleLookAroundState == IdleLookAroundState.NotIdle)
        {
            //            Debug.Log("UpdateIdleLookAroundIfActive: Not idle");
            return false;
        }

        EnsureAgentIsIdle();

        if (_idleLookAroundState == IdleLookAroundState.LookAround)
        {
            UpdateIdleHoldAndSweep();

            if (_idleSweepCompleted && Time.time >= _idleStateEndTime)
            {
                CancelIdleLookAround();
                EnsureAgentCanMove();
            }

            return true;
        }

        return false;
    }

    private void CancelIdleLookAround()
    {
        _targetPathIsPending = false;
        _idleLookAroundState = IdleLookAroundState.NotIdle;
        _idleSweepCompleted = false;
        _idlePausingAtEndpoint = false;
    }

    // Sweep runs immediately from the ghost's current facing — no pre-rotation.
    // Timing is set by InitSweepTimingFromCurrentFacing before this is called.
    // Flow: current angle → first endpoint (pause) → center → second endpoint (pause) → center (done).
    private void UpdateIdleHoldAndSweep()
    {

        if (_targetPathIsPending)
        {
            // Wait until a path has been calculated before starting lookaround
            // this is because we want the look around to pass over the direction
            // it will eventually go.

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
                    Debug.Log("Case 1 reached center, sweep to " + (_firstSweepSign > 0 ? "right" : "left"));
                    _idleLookTargetAngle = _firstSweepSign * _currentSweepAngle;
                    break;
                case 2: // reached first endpoint, sweep to the opposite side
                    Debug.Log("case 2: sweep to " + (_firstSweepSign > 0 ? "left" : "right"));
                    _idleLookTargetAngle = -_firstSweepSign * _currentSweepAngle;
                    break;
                case 3: // reached left, return to center
                    Debug.Log("case 3: reached left, return to center");
                    _idleLookTargetAngle = 0f;
                    break;
                case 4: // back at center, done
                    Debug.Log("case 4: back at center, done");
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
        if (!NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, path))
            return false;

        if (path.corners.Length < 2)
            return false;

        var lastCorner = path.corners[path.corners.Length - 1];

        var secondToLastCorner = path.corners[path.corners.Length - 2];

        Vector3 arrivalDirection = (lastCorner - secondToLastCorner).normalized;
        Vector3 raycastOrigin = lastCorner;// + Vector3.up * 0.5f;
                                           // Debug.DrawRay(raycastOrigin, arrivalDirection * 1.5f, Color.red, 999f);

        var wallHit = Physics.Raycast(raycastOrigin, arrivalDirection, _wallNearDistance);
        return wallHit;
    }


    // Returns a random reachable NavMesh position around the NPC,
    // with basic rejection to avoid wall-facing arrivals.
    private bool TryGetRandomNavMeshPosition(out Vector3 position, out bool wallAhead)
    {
        wallAhead = false;
        Vector3 fallbackPosition = transform.position;
        bool hasFallback = false;
        position = transform.position; // Default to current position if no valid position is found
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float randomOffsetX = Random.Range(-maxWalkDistance, maxWalkDistance);
            float randomOffsetZ = Random.Range(-maxWalkDistance, maxWalkDistance);

            randomOffsetX = Mathf.Sign(randomOffsetX) * Mathf.Clamp(Mathf.Abs(randomOffsetX), minWalkDistance, maxWalkDistance);
            randomOffsetZ = Mathf.Sign(randomOffsetZ) * Mathf.Clamp(Mathf.Abs(randomOffsetZ), minWalkDistance, maxWalkDistance);

            Vector3 desiredWorldPosition = transform.position + new Vector3(randomOffsetX, 0, randomOffsetZ);

            float maxDistance = 2f;
            wallAhead = false;
            if (NavMesh.SamplePosition(desiredWorldPosition, out var navMeshHit, maxDistance, NavMesh.AllAreas))
            {
                if (!IsFacingWallOnArrival(navMeshHit.position))
                {
                    position = navMeshHit.position;
                    wallAhead = false;
                    Debug.Log("Found acceptable position " + position + " distance " + Vector3.Distance(transform.position, position));
                    return true; // No wall ahead, return this position
                }
                fallbackPosition = navMeshHit.position;
                hasFallback = true;
            }


            Debug.Log($"Sample position failed to get random position within {maxDistance} of {desiredWorldPosition} attempt:  {attempt}");
        }


        if (hasFallback)
        {
            position = fallbackPosition;
            wallAhead = true;
            return true;
        }

        return false;
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

    // When the previous owner leaves, a new client takes over driving the ghost. Turn the agent on
    // for the new owner (off for everyone else) and warp it to where the ghost currently is, since
    // it was disabled and never tracked the synced position. Mirrors AIPatrolU.OnOwnershipTransferred.
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (_navMeshAgent == null) return;

        _navMeshAgent.enabled = Networking.IsOwner(gameObject);

        if (_navMeshAgent.enabled)
        {
            _navMeshAgent.Warp(transform.position);
            _currentTargetPlayer = null;
            _isInvestigating = false;
            CancelIdleLookAround();
        }
    }

}