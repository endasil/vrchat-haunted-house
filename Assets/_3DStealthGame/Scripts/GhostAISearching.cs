    using Assets._3DStealthGame.Scripts;
    using Assets._3DStealthGame.Scripts.Enums;

    using TMPro;

    using UdonSharp;
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

        public Vector3 LastCorner;
        public Vector3 SecondToLastCorner;
// Base movement speed when patrolling
    public float defaultSpeed = 1f;

        // Movement speed when chasing a detected player
        public float playerFoundSpeed = 2f;

        // Multiplier applied to animation "velocity" parameter to scale animation with movement speed.
        public float animationSpeed = 0.5f;

        // Ignore positions where the agent would end up x meter from facing a wall.
        public float wallNearDistance = 2f;

        // ==============================
        // Patrol Settings
        // ==============================

        // Maximum random patrol distance
        public float maxWalkDistance = 20f;
        public float minWalkDistance = 5f;

        // Current AI behavior mode enum Walk / Chase etc
        public AIBehavior aiBehavior;

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

        private Animator _animator;      // Controls movement animation
        private TextMeshPro _indicator;  // Displays AI state symbol (! ? ~ .)


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

        // Start time of the sweep so PingPong has a stable origin.
        private float _idleSweepStartTime;

        // Indicates if the sweep phase has started (we rotate to base direction first, then sweep).
        private bool _idleSweepStarted;

        // Tracks which step of the look-around sequence we are in.
        private int _idleLookStep;

        // Target rotation used by the current look step.
        private Quaternion _idleLookTargetRotation;

        private bool _idleSweepCompleted;


        // One-time look-around tuning.
        public float lookAroundSweepAngle = 60f;          // degrees left/right from base direction
        public float lookAroundSweepDuration = 8f;      // seconds for one full cycle: center->right->center->left->center
        public float idleTurnSpeed = 120f;

    private void Start()
        {
            // Cache references to not have to look them up at runtime
            _animator = GetComponentInChildren<Animator>();
            _indicator = GetComponentInChildren<TextMeshPro>();

            // If navMeshAgent reference not set in inspector, try to find one on the same GameObject
            if (!navMeshAgent)
                navMeshAgent = (NavMeshAgent)GetComponent(typeof(NavMeshAgent));

            // Preallocate player cache, can't use lists in U# :(
            _playerCache = new VRCPlayerApi[80];

            // Make sure we can find a target right away.
            _lastRetargetTime = -999f;

        }

        private void Update()
        {
            // Only run AI logic on one client to make sure the ai behavior is the same for all players.
            if (!Networking.IsOwner(gameObject)) return;

            UpdateAnimation();

            if (navMeshAgent == null) return;

            UpdateRetargetingIfNeeded();

            // ==============================
            // Perform current AI behavior
            // ==============================

            if (aiBehavior == AIBehavior.FollowPlayer)
            {
                FollowPlayerBehavior();
                return;
            }


            if (aiBehavior == AIBehavior.RandomWalk)
            {
                RandomWalkBehavior();
                return;
            }

            if (aiBehavior == AIBehavior.WalkNChase)
            {
                WalkAndChaseBehavior();
                return;
            }
        }

        // ==============================
        // Update Subsystems
        // ==============================

        // Adjust animation speed with agent speed.
        private void UpdateAnimation()
        {
            // Update animation speed based on current velocity
            //if (_animator != null && navMeshAgent != null)
            //    _animator.SetFloat("Velocity", navMeshAgent.velocity.magnitude * animationSpeed);
        }

        private void UpdateRetargetingIfNeeded()
        {
            // Periodic retarget (handles join/leave)
            if (Time.time - _lastRetargetTime < retargetInterval) return;
            _lastRetargetTime = Time.time;

            if (aiBehavior != AIBehavior.FollowPlayer) return;

            if (!IsValidTargetPlayer(_currentTargetPlayer))
                _currentTargetPlayer = FindNearestValidPlayer();
        }

        // ==============================
        // Behavior: Follow Player
        // ==============================

        private void FollowPlayerBehavior()
        {
            EnsureAgentIsMoving();

            if (IsValidTargetPlayer(_currentTargetPlayer))
            {
                // Actively chasing player
                TurnTowardsSteeringTarget();
                navMeshAgent.speed = playerFoundSpeed;
                navMeshAgent.SetDestination(GetPlayerHeadPosition(_currentTargetPlayer));
                SetIndicatorText(AwarenessIndicator.SpottedPlayer);
                return;
            }

            // No target -> back to patrolling
            SetIndicatorText(AwarenessIndicator.SearchingForPlayers);
            navMeshAgent.speed = defaultSpeed;
            RandomWalkBehavior();
        }

        // ==============================
        // Behavior: Random Walk
        // ==============================
        private void RandomWalkBehavior()
        {
            bool isFacingArrivalDirection = RotateTowardsSteeringTargetWhenCloseToArrival();

            if (UpdateIdleLookAroundIfActive())
            {
                return;
            }

            bool isAtDestination = navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance && !navMeshAgent.pathPending;

            // Only begin idle look-around if we have arrived AND finished turning to face the arrival direction.
            if (isAtDestination && isFacingArrivalDirection)
                StartIdleLookAround();
        }


        // Patrol, but interrupt patrol if vision/hearing detects a player.
        public void WalkAndChaseBehavior()
        {
            EnsureAgentIsMoving();
            RotateTowardsSteeringTargetWhenCloseToArrival();

            // if idle look around is active, it will handle this frame
            if (UpdateIdleLookAroundIfActive())
            {
                return;
            }

            VRCPlayerApi detectedPlayer;
            bool detectedByVision;
            bool didDetectPlayer = TryDetectBestPlayer(out detectedPlayer, out detectedByVision);

            if (didDetectPlayer)
            {
                // When we detect a player, cancel idle and begin chase
                _currentTargetPlayer = detectedPlayer;

                CancelIdleLookAround();

                navMeshAgent.speed = playerFoundSpeed;
                navMeshAgent.SetDestination(GetPlayerHeadPosition(_currentTargetPlayer));

                _isInvestigating = true;
                SetIndicatorText(detectedByVision ? AwarenessIndicator.SpottedPlayer : AwarenessIndicator.HeardSomething);
                return;
            }

            // I wwe already started investigating, keep moving untui we reach the destination.
            if (_isInvestigating && navMeshAgent.hasPath && navMeshAgent.remainingDistance > navMeshAgent.stoppingDistance)
            {
                navMeshAgent.speed = playerFoundSpeed;
                SetIndicatorText(AwarenessIndicator.InvestigatingLastKnown);
                return;
            }

            _isInvestigating = false;
            navMeshAgent.speed = defaultSpeed;
            SetIndicatorText(AwarenessIndicator.SearchingForPlayers);

            // If a new patrol destination is needed, start look around logic which
            // will also select a new target.

            if (!navMeshAgent.hasPath || navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
            {
                StartIdleLookAround();
            }

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
        private void EnsureAgentIsMoving()
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.updateRotation = true;
        }

        // Prevent the navmesh agent from controlling the rotation and position of the npc 
        // so we can move it from code directly.
        private void EnsureAgentIsIdle()
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.updateRotation = false;
        }

        // Sets the indicator text based on logical AI awareness state instead of raw strings.
        private void SetIndicatorText(AwarenessIndicator awarenessIndicator)
        {
            if (_indicator == null) return;

            switch (awarenessIndicator)
            {
                case AwarenessIndicator.SearchingForPlayers:
                    _indicator.text = "?";
                    break;

                case AwarenessIndicator.HeardSomething:
                    _indicator.text = "~";
                    break;

                case AwarenessIndicator.SpottedPlayer:
                    _indicator.text = "!";
                    break;

                case AwarenessIndicator.InvestigatingLastKnown:
                    _indicator.text = ".";
                    break;
            }
        }
        
        // Smoothly rotates toward next steering target
        private void TurnTowardsSteeringTarget()
        {
            Vector3 directionToSteeringTarget = navMeshAgent.steeringTarget - transform.position;
            directionToSteeringTarget.y = 0f;

            if (directionToSteeringTarget.sqrMagnitude < 0.0001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(directionToSteeringTarget, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                navMeshAgent.angularSpeed * Time.deltaTime);
        }

        private bool RotateTowardsSteeringTargetWhenCloseToArrival()
        {
            if (_idleLookAroundState != IdleLookAroundState.NotIdle) return false;
            if (navMeshAgent.pathPending) return false;
            if (navMeshAgent.remainingDistance >= navMeshAgent.stoppingDistance + 1f) return false;

            Vector3 directionToSteeringTarget = navMeshAgent.steeringTarget - transform.position;
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

        // ==============================
        // Idle Look-Around (State Machine)
        // ==============================

        // Begins idle behavior: choose next destination, stop movement, face a sensible base direction.
        private void StartIdleLookAround()
        {
            Debug.Log("Start idle lookaround.");
            // Immediately pick the next destination so the ai can be turned towards it later.
            navMeshAgent.autoBraking = true;
            navMeshAgent.stoppingDistance = 0.4f;
            navMeshAgent.speed = defaultSpeed;
            navMeshAgent.SetDestination(GetRandomNavMeshPosition());
            _idleLookStep = 0;
            _idleSweepStarted = false;
            EnsureAgentIsIdle();

            // If we're facing a wall, use the path corner direction instead of forward.
            // This avoids idling while staring into a wall when we arrive.
            bool isWallInFront = Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, wallNearDistance);
            _idleBaseDirection = isWallInFront ? GetFacingDirectionToNextCorner() : transform.forward;

            // First phase: hold and look around the base direction.
            _idleLookAroundState = IdleLookAroundState.InitialHoldAndSweep;
            _idleSweepCompleted = false;
            _idleSweepStarted = false;

            SetIndicatorText(AwarenessIndicator.SearchingForPlayers);
        }

        // Return true if this logic did the work for this frame.
        private bool UpdateIdleLookAroundIfActive()
        {
            if (_idleLookAroundState == IdleLookAroundState.NotIdle)
                return false;

            EnsureAgentIsIdle();

            if (_idleLookAroundState == IdleLookAroundState.InitialHoldAndSweep)
            {
                var doInitialSweep = false;
                if (!doInitialSweep)
                {
                    _idleLookAroundState = IdleLookAroundState.SecondHold;
                    _idleBaseDirection = GetFacingDirectionToNextCorner();
                    _idleSweepStarted = false;
                    _idleSweepCompleted = false;
                    return true;
                }

            UpdateIdleHoldAndSweep(sweepDuration: lookAroundSweepDuration);

                if (Time.time >= _idleStateEndTime)
                {
                    _idleLookAroundState = IdleLookAroundState.SecondHold;
                    _idleBaseDirection = GetFacingDirectionToNextCorner();
                    _idleSweepStarted = false;
                    _idleSweepCompleted = false;
                }

                return true;
            }

            if (_idleLookAroundState == IdleLookAroundState.SecondHold)
            {
                UpdateIdleHoldAndSweep(sweepDuration: lookAroundSweepDuration);

                if (_idleSweepCompleted && Time.time >= _idleStateEndTime)
                {
                    CancelIdleLookAround();
                    EnsureAgentIsMoving();
                }

                return true;
            }

            return false;
        }
        private void CancelIdleLookAround()
        {
            _idleLookAroundState = IdleLookAroundState.NotIdle;
            _idleSweepStarted = false;
            _idleSweepCompleted = false;
        }

        // First idle phase: rotate to base direction, then perform exactly one smooth sweep cycle.
        private void UpdateIdleHoldAndSweep(float sweepDuration)
        {
            Quaternion baseRotation = Quaternion.LookRotation(_idleBaseDirection, Vector3.up);

            // Step 1: Rotate to base direction before sweeping.
            if (!_idleSweepStarted && !_idleSweepCompleted)
            {
                float angleToBase = Quaternion.Angle(transform.rotation, baseRotation);
                if (angleToBase > 5f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, baseRotation, idleTurnSpeed * Time.deltaTime);
                    return;
                }
                _idleSweepStarted = true;
                _idleSweepStartTime = Time.time;
                _idleStateEndTime = Time.time + sweepDuration + Random.Range(0.1f, 0.1f); 
            }

            // Step 2: Hold base direction after sweep completes.
            if (_idleSweepCompleted)
            {
                transform.rotation = baseRotation;
                return;
            }

            // Step 3: Drive sweep directly from sine wave scaled by sweepDuration.
            float normalizedTime = (Time.time - _idleSweepStartTime) / sweepDuration;

            if (normalizedTime >= 1f)
            {
                _idleSweepCompleted = true;
                transform.rotation = baseRotation;
                return;
            }

            float sweepAngle = Mathf.Sin(normalizedTime * Mathf.PI * 2f) * lookAroundSweepAngle;
            Quaternion sweepRotation = Quaternion.AngleAxis(sweepAngle, Vector3.up);
            transform.rotation = Quaternion.LookRotation(sweepRotation * _idleBaseDirection, Vector3.up);
        }

        // Second idle phase, no look around, just make sure we face the correct
        // rotation before leaving idle.
        private bool RotateToIdleBaseDirection(float rotateSpeed)
        {
            Quaternion baseRotation = Quaternion.LookRotation(_idleBaseDirection, Vector3.up);
            float angleToBase = Quaternion.Angle(transform.rotation, baseRotation);
            if (angleToBase > 5f)
            {
                transform.rotation =
                    Quaternion.RotateTowards(transform.rotation, baseRotation, rotateSpeed * Time.deltaTime);
                return false;
            }

            return true;
        }

        // Choose a target direction to face when stopping.
        // If it is already facing the target direction, return 
        // the current forward vector.
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

            LastCorner = path.corners[path.corners.Length - 1];
            SecondToLastCorner = path.corners[path.corners.Length - 2];

            Vector3 arrivalDirection = (LastCorner - SecondToLastCorner).normalized;
            Vector3 raycastOrigin = LastCorner + Vector3.up * 0.5f;
            Debug.DrawRay(raycastOrigin, arrivalDirection * 1.5f, Color.red, 999f);
            return Physics.Raycast(raycastOrigin, arrivalDirection, 1.5f);
        }

        // Returns a random reachable NavMesh position around the NPC,
        // with basic rejection to avoid wall-facing arrivals.
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
                        Debug.Log($"Rejected position {attempt} — wall ahead on arrival");
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

    }
