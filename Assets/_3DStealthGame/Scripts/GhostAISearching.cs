using Assets._3DStealthGame.Scripts;

using TMPro;

using UdonSharp;
using UdonSharp.Localization;

using UnityEngine;
using UnityEngine.AI;

using VRC.SDKBase;

public class GhostAISearching : UdonSharpBehaviour
{
    // ==============================
    // Navigation & Movement Settings
    // ==============================

    // NavMesh agent used for pathfinding and movement
    public NavMeshAgent navMeshAgent;

    // Base movement speed when patrolling
    public float defaultSpeed = 1f;

    // Movement speed when chasing a detected player
    public float playerFoundSpeed = 2f;

    // Multiplier applied to animation "velocity" parameter to scale animation with movement speed.
    public float animationSpeed = 0.5f;

    //==============================
    // Component References
    //==============================
    
    private Animator animator;      // Controls movement animation
    private TextMeshPro indicator;  // Displays AI state symbol (! ? ~ .)


    // ==============================
    // Patrol Settings
    // ==============================

    public float maxWalkDistance = 20f; // Maximum random patrol distance
    public float minWalkDistance = 5f;

    public AIBehavior aiBehavior;       // Current AI behavior mode enum

    // ==============================
    // Vision Settings
    // ==============================
    
    public float sideVisionAngle = 45f; // half-angle vision cone
    public float visionLength = 10f;    // Maximum sight distance

    
    // ==============================
    // Hearing Settings
    // ==============================
    
    public float hearingRange = 3f;      // Radius for hearing-based detection
    private bool isInvestigating;        // True while chasing last known location

    // Idle look-around system
    public bool isWaiting = false;
    private Vector3 bestIdleDirection = Vector3.forward;
    private float secondPhaseEndWaitTime;
    private float initialHoldEndTime;

    // Targeting (multi-player safe)
    private VRCPlayerApi currentTargetPlayer;
    private bool currentTargetDetectedByVision;
    private float lastRetargetTime;

    public float retargetInterval = 0.2f;

    // Player cache (Udon requires preallocated arrays)
    private VRCPlayerApi[] playerCache;

    private float lookAroundPhaseStartTime;
    private bool lookAroundSweepStarted;

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        indicator = GetComponentInChildren<TextMeshPro>();

        if (!navMeshAgent)
            navMeshAgent = (NavMeshAgent)GetComponent(typeof(NavMeshAgent));

        playerCache = new VRCPlayerApi[80];
        lastRetargetTime = -999f;
    }

    private void Update()
    {
        if (animator != null && navMeshAgent != null)
            animator.SetFloat("Velocity", navMeshAgent.velocity.magnitude * animationSpeed);

        if (navMeshAgent == null) return;

        // Periodic retarget (handles join/leave without hard references)
        if (Time.time - lastRetargetTime >= retargetInterval)
        {
            lastRetargetTime = Time.time;

            if (aiBehavior == AIBehavior.FollowPlayer)
            {
                if (!IsValidTargetPlayer(currentTargetPlayer))
                    currentTargetPlayer = FindNearestValidPlayer();
            }
        }

        if (aiBehavior == AIBehavior.FollowPlayer)
        {
            if (navMeshAgent.isStopped && !isWaiting)
            {
                navMeshAgent.isStopped = false;
                navMeshAgent.updateRotation = true;
            }

            if (IsValidTargetPlayer(currentTargetPlayer))
            {
                TurnTowardsSteeringTarget();
                navMeshAgent.speed = playerFoundSpeed;
                navMeshAgent.SetDestination(GetPlayerHeadPosition(currentTargetPlayer));
                if (indicator != null) indicator.text = "!";
            }
            else
            {
                if (indicator != null) indicator.text = "?";
                navMeshAgent.speed = defaultSpeed;
                RandomWalk();
            }

            return;
        }

        if (aiBehavior == AIBehavior.RandomWalk)
        {
            RandomWalk();
            return;
        }

        if (aiBehavior == AIBehavior.WalkNChase)
        {
            if (navMeshAgent.isStopped && !isWaiting)
            {
                navMeshAgent.isStopped = false;
                navMeshAgent.updateRotation = true;
            }

            WalkNChase();
        }
    }

    public override void OnPlayerLeft(VRCPlayerApi playerThatLeft)
    {
        if (currentTargetPlayer != null &&
            playerThatLeft != null &&
            currentTargetPlayer.playerId == playerThatLeft.playerId)
        {
            currentTargetPlayer = null;
            isInvestigating = false;
        }
    }

    private bool IsValidTargetPlayer(VRCPlayerApi playerCandidate)
    {
        // If you want the AI to also target the local player, remove "!playerCandidate.isLocal"
        return playerCandidate != null && playerCandidate.IsValid() && !playerCandidate.isLocal;
    }

    private VRCPlayerApi FindNearestValidPlayer()
    {
        int totalPlayerCount = VRCPlayerApi.GetPlayerCount();
        VRCPlayerApi.GetPlayers(playerCache);

        VRCPlayerApi closestPlayer = null;
        float closestPlayerDistanceSquared = float.MaxValue;

        Vector3 agentPosition = transform.position;

        for (int playerIndex = 0; playerIndex < totalPlayerCount && playerIndex < playerCache.Length; playerIndex++)
        {
            VRCPlayerApi playerCandidate = playerCache[playerIndex];
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

    private Vector3 GetPlayerHeadPosition(VRCPlayerApi player)
    {
        VRCPlayerApi.TrackingData headTrackingData = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        return headTrackingData.position;
    }

    private void TurnTowardsSteeringTarget()
    {
        Vector3 directionToSteeringTarget = navMeshAgent.steeringTarget - transform.position;
        directionToSteeringTarget.y = 0f;

        if (directionToSteeringTarget.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(directionToSteeringTarget, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            navMeshAgent.angularSpeed * Time.deltaTime
        );
    }

    private bool _isFirstPhase = false;

    private void RandomWalk()
    {
        if (!isWaiting && !navMeshAgent.pathPending)
        {
            if (navMeshAgent.remainingDistance < navMeshAgent.stoppingDistance + 1f)
            {
                Vector3 directionToSteeringTarget = navMeshAgent.steeringTarget - transform.position;
                if (directionToSteeringTarget.sqrMagnitude > 0.0001f)
                {
                    Quaternion facingRotation = Quaternion.LookRotation(directionToSteeringTarget, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, facingRotation, 240f * Time.deltaTime);
                }
            }
        }

        if (UpdateIdleLookAround()) return;

        if (navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance && !navMeshAgent.pathPending)
        {
            StartIdleLookAround();
        }
    }

    private void StartIdleLookAround()
    {
        navMeshAgent.autoBraking = true;
        navMeshAgent.stoppingDistance = 0.4f;
        navMeshAgent.speed = defaultSpeed;
        navMeshAgent.SetDestination(GetRandomNavMeshPosition());


        isWaiting = true;
        navMeshAgent.isStopped = true;
        navMeshAgent.updateRotation = false;
            
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, out RaycastHit hit, 1f))
        {
            Debug.LogError($"Object ahead: {hit.collider.gameObject.name}, changing idle direction.");
            bestIdleDirection = GetFacingDirectionToNextCorner();
            // initialHoldEndTime = Time.time;
        }
        else
        {
            bestIdleDirection = transform.forward;

        }
        var initialWaitTime = Random.Range(3.5f, 4f);
        initialHoldEndTime = Time.time + initialWaitTime;
        if (indicator != null) indicator.text = "?";
        navMeshAgent.speed = defaultSpeed;
        _isFirstPhase = true;

    }


    private bool UpdateIdleLookAround()
    {
        // The npc is moving, nothing to do here. 
        if (!isWaiting) return false;

        // During the initial hold time, rotate to best idle direction first, then sweep
        if (Time.time < initialHoldEndTime)
        {
            Quaternion holdRotation = Quaternion.LookRotation(bestIdleDirection, Vector3.up);
            float angleToTarget = Quaternion.Angle(transform.rotation, holdRotation);

            if (angleToTarget > 5f && lookAroundSweepStarted== false)
            {
                // Not facing base direction yet, rotate towards it first
                transform.rotation = Quaternion.RotateTowards(transform.rotation, holdRotation, 240f * Time.deltaTime);
                lookAroundSweepStarted = false;
            }
            else
            {
                // Facing base direction, now sweep
                if (!lookAroundSweepStarted)
                {
                    Debug.Log("Facing base direction, now sweep");
                    lookAroundSweepStarted = true;
                    lookAroundPhaseStartTime = Time.time;
                }
                ApplyLookAroundSweep(bestIdleDirection, 240f);
            }

            Debug.Log($"Initial wait Remaining: {initialHoldEndTime - Time.time} IsFirstPhase: {_isFirstPhase}");

            // Reconfirm stopped state if changed externally, like ai mode in inspector
            if (!navMeshAgent.isStopped)
            {
                navMeshAgent.isStopped = true;
                navMeshAgent.updateRotation = false;
            }

            return true;
        }

        if (_isFirstPhase)
        {
            Debug.Log("Switched phase");
            _isFirstPhase = false;
            secondPhaseEndWaitTime = Time.time + Random.Range(1f, 3f);
            bestIdleDirection = GetFacingDirectionToNextCorner();
            lookAroundSweepStarted = false;
        }

        Quaternion lookRotation = Quaternion.LookRotation(bestIdleDirection, Vector3.up);
        float angleToBase = Quaternion.Angle(transform.rotation, lookRotation);

        if (angleToBase > 5f)
        {
            // Not facing base direction yet, rotate towards it first
            transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, 140f * Time.deltaTime);
            lookAroundSweepStarted = false;
        }
        else
        {
            // Facing base direction, now sweep
            if (!lookAroundSweepStarted)
            {
                lookAroundSweepStarted = true;
                lookAroundPhaseStartTime = Time.time;
            }
            // ApplyLookAroundSweep(bestIdleDirection, 140f);

            if (Time.time >= secondPhaseEndWaitTime && Quaternion.Angle(transform.rotation, lookRotation) < 5f)
            {
                Debug.Log($"Time.time >= secondPhasewaitTime returning {secondPhaseEndWaitTime - Time.time}");
                isWaiting = false;
                navMeshAgent.isStopped = false;
                navMeshAgent.updateRotation = true;
                return true;
            }
            else
            {
                Debug.Log($"SecondPhase: Time.time < waitEndTime. Remaining: {secondPhaseEndWaitTime - Time.time} left. Rotating. ");
            }
        }

        return true;
    }
    private Vector3 GetFacingDirectionToNextCorner()
    {
        Vector3 directionToSteeringTarget = navMeshAgent.steeringTarget - transform.position;
        if (directionToSteeringTarget.sqrMagnitude > 0.0001f)
        {
            Debug.Log("returning directionToSteeringTarget");
            return directionToSteeringTarget.normalized;
        }

        Vector3 desiredVelocityDirection = navMeshAgent.desiredVelocity;
        if (desiredVelocityDirection.sqrMagnitude > 0.0001f)
        {
            Debug.LogError("returning desiredVelocityDirection");

            return desiredVelocityDirection.normalized;
        }

        Debug.LogError("transform.forward");

        return transform.forward;
    }

    public void WalkNChase()
    {
        if (!isWaiting && !navMeshAgent.pathPending)
        {
            if (navMeshAgent.remainingDistance < navMeshAgent.stoppingDistance + 1f)
            {
                Vector3 directionToSteeringTarget = navMeshAgent.steeringTarget - transform.position;
                if (directionToSteeringTarget.sqrMagnitude > 0.0001f)
                {
                    Quaternion facingRotation = Quaternion.LookRotation(directionToSteeringTarget, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, facingRotation, 240f * Time.deltaTime);
                }
            }
        }

        if (UpdateIdleLookAround()) return;

        VRCPlayerApi detectedPlayer;
        bool detectedByVision;

        bool playerDetected = TryDetectBestPlayer(out detectedPlayer, out detectedByVision);

        if (playerDetected)
        {
            currentTargetPlayer = detectedPlayer;
            currentTargetDetectedByVision = detectedByVision;

            isWaiting = false;
            navMeshAgent.isStopped = false;
            navMeshAgent.updateRotation = true;

            navMeshAgent.speed = playerFoundSpeed;
            navMeshAgent.SetDestination(GetPlayerHeadPosition(currentTargetPlayer));

            isInvestigating = true;

            if (indicator != null) indicator.text = detectedByVision ? "!" : "~";
            return;
        }

        if (isInvestigating && navMeshAgent.hasPath && navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
        {
            navMeshAgent.speed = playerFoundSpeed;
            if (indicator != null) indicator.text = ".";
            return;
        }

        isInvestigating = false;
        navMeshAgent.speed = defaultSpeed;
        if (indicator != null) indicator.text = "?";

        if (!navMeshAgent.hasPath || navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
        {
            navMeshAgent.autoBraking = true;
            navMeshAgent.stoppingDistance = 0.4f;
            navMeshAgent.SetDestination(GetRandomNavMeshPosition());
            StartIdleLookAround();
        }
    }

    private bool TryDetectBestPlayer(out VRCPlayerApi bestDetectedPlayer, out bool bestDetectedByVision)
    {
        bestDetectedPlayer = null;
        bestDetectedByVision = false;

        int totalPlayerCount = VRCPlayerApi.GetPlayerCount();
        VRCPlayerApi.GetPlayers(playerCache);

        Vector3 agentPosition = transform.position;

        float closestVisionDistanceSquared = float.MaxValue;
        float closestHearingDistanceSquared = float.MaxValue;

        VRCPlayerApi closestVisionPlayer = null;
        VRCPlayerApi closestHearingPlayer = null;

        for (int playerIndex = 0; playerIndex < totalPlayerCount && playerIndex < playerCache.Length; playerIndex++)
        {
            VRCPlayerApi playerCandidate = playerCache[playerIndex];
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

            if (isInHearingRange)
            {
                if (squaredDistanceToPlayer < closestHearingDistanceSquared)
                {
                    closestHearingDistanceSquared = squaredDistanceToPlayer;
                    closestHearingPlayer = playerCandidate;
                }
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

        RaycastHit raycastHit;
        bool hitSomething = Physics.Raycast(
            raycastOrigin,
            directionToTarget,
            out raycastHit,
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

    private bool IsFacingWallOnArrival(Vector3 destination)
    {
        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, path))
            return false;

        if (path.corners.Length < 2)
            return false;

        Vector3 lastCorner = path.corners[path.corners.Length - 1];
        Vector3 secondToLastCorner = path.corners[path.corners.Length - 2];

        Vector3 arrivalDirection = (lastCorner - secondToLastCorner).normalized;
        Vector3 raycastOrigin = lastCorner + Vector3.up * 0.5f;

        return Physics.Raycast(raycastOrigin, arrivalDirection, 1f);
    }

    private Vector3 GetRandomNavMeshPosition()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float randomOffsetX = Random.Range(-maxWalkDistance, maxWalkDistance);
            float randomOffsetZ = Random.Range(-maxWalkDistance, maxWalkDistance);

            randomOffsetX = Mathf.Sign(randomOffsetX) *
                            Mathf.Clamp(Mathf.Abs(randomOffsetX), minWalkDistance, maxWalkDistance);
            randomOffsetZ = Mathf.Sign(randomOffsetZ) *
                            Mathf.Clamp(Mathf.Abs(randomOffsetZ), minWalkDistance, maxWalkDistance);

            Vector3 desiredWorldPosition = transform.position + new Vector3(randomOffsetX, 0, randomOffsetZ);

            NavMeshHit navMeshHit;
            if (NavMesh.SamplePosition(desiredWorldPosition, out navMeshHit, 6f, NavMesh.AllAreas))
            {
                if (IsFacingWallOnArrival(navMeshHit.position))
                {
                    Debug.Log($"Rejected position {attempt} — wall ahead on arrival");
                    continue;
                }
                return navMeshHit.position;
            }


            Debug.Log($"Failed to get random position {attempt}");

        }

        Debug.LogError("Failed to get random position");
        
        return transform.position;

    }

    private void ApplyLookAroundSweep(Vector3 baseDirection, float rotateSpeed)
    {
        // Sweep left for first half of period, right for second half
        float sweepPeriod = 1.8f;
        float elapsed = Time.time - lookAroundPhaseStartTime;
        float t = Mathf.PingPong(elapsed, sweepPeriod) / sweepPeriod; // 0→1→0 loop

        // -30 to +30 degrees, starts center, goes left first
        float sweepAngle = Mathf.Lerp(-60f, 60f, t);

        Quaternion sweepRotation = Quaternion.AngleAxis(sweepAngle, Vector3.up);
        Vector3 sweptDirection = sweepRotation * baseDirection;

        Quaternion sweepTargetRotation = Quaternion.LookRotation(sweptDirection, Vector3.up);
        Debug.Log($"{transform.rotation} Pre rotation");

        transform.rotation = Quaternion.RotateTowards(transform.rotation, sweepTargetRotation, rotateSpeed * Time.deltaTime);
        Quaternion.Angle(transform.rotation, sweepTargetRotation);
        Debug.Log($"{transform.rotation} Post rotation");
    }
}
