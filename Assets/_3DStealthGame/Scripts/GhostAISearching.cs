using Assets._3DStealthGame.Scripts;
using TMPro;

using UdonSharp;

using UnityEngine;
using UnityEngine.AI;

using VRC.SDKBase;

public class GhostAISearching : UdonSharpBehaviour
{
    // Navigation
    public NavMeshAgent navMeshAgent;

    // Speeds
    public float defaultSpeed = 1f;
    public float playerFoundSpeed = 2f;
    public float animationSpeed = 0.5f;

    // References
    private Animator animator;
    private TextMeshPro indicator;

    // Patrol behavior
    public float maxWalkDistance = 20f;
    public float minWalkDistance = 5f;

    public AIBehavior aiBehavior;

    // Vision parameters
    public float sideVisionAngle = 45f; // half-angle cone
    public float visionLength = 10f;

    // Hearing parameters
    public float hearingRange = 3f;
    private bool isInvestigating;

    // Idle look-around system
    public bool isWaiting = false;
    private Vector3 bestIdleDirection = Vector3.forward;
    private float waitEndTime;
    private float holdEndTime;

    // Targeting (multi-player safe)
    private VRCPlayerApi currentTargetPlayer;
    private bool currentTargetDetectedByVision;
    private float lastRetargetTime;

    public float retargetInterval = 0.2f;

    // Player cache (Udon requires preallocated arrays)
    private VRCPlayerApi[] playerCache;
    
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
            navMeshAgent.autoBraking = true;
            navMeshAgent.stoppingDistance = 0.4f;
            navMeshAgent.speed = defaultSpeed;
            navMeshAgent.SetDestination(GetRandomNavMeshPosition());
            StartIdleLookAround();
        }
    }

    private void StartIdleLookAround()
    {
        bestIdleDirection = transform.forward;
        isWaiting = true;
        navMeshAgent.isStopped = true;
        navMeshAgent.updateRotation = false;

        waitEndTime = Time.time + Random.Range(2f, 4f);
        holdEndTime = Time.time + Random.Range(1f, 3f);

        if (indicator != null) indicator.text = "?";
        navMeshAgent.speed = defaultSpeed;
    }

    private bool UpdateIdleLookAround()
    {
        if (!isWaiting) return false;

        if (Time.time < holdEndTime)
        {
            Quaternion holdRotation = Quaternion.LookRotation(bestIdleDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, holdRotation, 240f * Time.deltaTime);

            if (!navMeshAgent.isStopped)
            {
                navMeshAgent.isStopped = true;
                navMeshAgent.updateRotation = false;
            }

            return true;
        }

        Vector3 directionToNextCorner = GetFacingDirectionToNextCorner();
        if (directionToNextCorner.sqrMagnitude > 0.0001f)
            bestIdleDirection = directionToNextCorner.normalized;

        Quaternion lookRotation = Quaternion.LookRotation(bestIdleDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, 240f * Time.deltaTime);

        if (Time.time >= waitEndTime)
        {
            isWaiting = false;
            navMeshAgent.isStopped = false;
            navMeshAgent.updateRotation = true;
            return false;
        }

        return true;
    }

    private Vector3 GetFacingDirectionToNextCorner()
    {
        Vector3 directionToSteeringTarget = navMeshAgent.steeringTarget - transform.position;
        if (directionToSteeringTarget.sqrMagnitude > 0.0001f)
            return directionToSteeringTarget;

        Vector3 desiredVelocityDirection = navMeshAgent.desiredVelocity;
        if (desiredVelocityDirection.sqrMagnitude > 0.0001f)
            return desiredVelocityDirection;

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

    private Vector3 GetRandomNavMeshPosition()
    {
        float randomOffsetX = Random.Range(-maxWalkDistance, maxWalkDistance);
        float randomOffsetZ = Random.Range(-maxWalkDistance, maxWalkDistance);

        randomOffsetX = Mathf.Sign(randomOffsetX) * Mathf.Clamp(Mathf.Abs(randomOffsetX), minWalkDistance, maxWalkDistance);
        randomOffsetZ = Mathf.Sign(randomOffsetZ) * Mathf.Clamp(Mathf.Abs(randomOffsetZ), minWalkDistance, maxWalkDistance);

        Vector3 desiredWorldPosition = transform.position + new Vector3(randomOffsetX, 0, randomOffsetZ);

        NavMeshHit navMeshHit;
        if (NavMesh.SamplePosition(desiredWorldPosition, out navMeshHit, 6f, NavMesh.AllAreas))
            return navMeshHit.position;

        return transform.position;
    }
}
