using TMPro;

using UdonSharp;

using UnityEngine;
using UnityEngine.AI;

using VRC.SDKBase;

// Never attached to a GameObject directly, so it has no UdonSharpProgramAsset
// (same as Resettable).
public class GhostAgentBase : UdonSharpBehaviour
{
    // How far a point may be from the NavMesh and still get snapped onto it.
    protected const float NavMeshSampleRadius = 2f;

    // The vision raycast starts this far above transform.position.
    protected const float EyeHeight = 0.5f;

    // Speed in m/s below which hearing treats a player as standing still.
    private const float StandingStillSpeed = 0.1f;

    // Floating state symbol above the ghost (e.g. "!" when a player is spotted).
    // Looked up in children if not assigned in the inspector.
    public TextMeshPro Indicator;

    // Forward vision cone: half-angle in degrees (so 45 = a 90° cone) and how far
    // the ghost can see. Each ghost sets its own values in the inspector.
    public float visionHalfAngle = 45f;
    public float visionLength = 10f;

    // Angles in degrees between the ghost's forward and the player it turns toward.
    protected const float TurnInPlaceStartAngle = 90f;
    protected const float TurnInPlaceEndAngle = 10f;

    // Turn speed (deg/s) of the in-place turn toward a player.
    public float turnInPlaceSpeed = 240f;

    protected NavMeshAgent _navMeshAgent;

    // Preallocated for VRChat's max instance size; can't use lists in U#.
    protected VRCPlayerApi[] _playerCache;

    // Kept up to date by OnOwnershipTransferred.
    protected bool _isOwner;

    // VRCObjectSync sets the ghost's transform on clients that don't own the ghost.
    // An enabled NavMeshAgent would also set it, so only the owner enables the agent.
    protected void InitGhostAgent()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();
        if (_navMeshAgent == null)
            Debug.LogError($"{gameObject.name} is missing a NavMeshAgent component.");

        if (Indicator == null)
            Indicator = GetComponentInChildren<TextMeshPro>();

        if (Indicator == null)
        {
            Debug.LogError($"{gameObject.name}: Indicator needs to be assigned in the inspector.", gameObject);
        }

        _playerCache = new VRCPlayerApi[80];

        _isOwner = Networking.IsOwner(gameObject);
        
        if (_navMeshAgent != null)
            _navMeshAgent.enabled = _isOwner;
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        _isOwner = Networking.IsOwner(gameObject);

        if (_navMeshAgent == null) return;

        _navMeshAgent.enabled = _isOwner;

        if (_isOwner)
        {
            // The agent was disabled while VRCObjectSync set the transform,
            // so the agent's own position is out of date. Not having this
            // sometimes causes npcs to appear on strange positions int
            // teh world.
            _navMeshAgent.Warp(transform.position);
            OnBecameAgentOwner();
        }
    }

    protected virtual void OnBecameAgentOwner() { }

    // ==============================
    // Player scanning
    // ==============================

    // Players experience themselves at their head, so detection measures there.
    protected Vector3 GetPlayerHeadPosition(VRCPlayerApi player)
    {
        return player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
    }

    // Returns the closest player whose head is within maxDistance, inside the
    // forward cone and with a clear line of sight; null if nobody qualifies.
    protected VRCPlayerApi FindClosestVisiblePlayer(float coneHalfAngleDeg, float maxDistance)
    {
        int totalPlayerCount = VRCPlayerApi.GetPlayerCount();
        VRCPlayerApi.GetPlayers(_playerCache);

        Vector3 agentPosition = transform.position;
        float maxDistanceSquared = maxDistance * maxDistance;
        VRCPlayerApi closest = null;
        float closestDistanceSquared = float.MaxValue;

        for (int i = 0; i < totalPlayerCount && i < _playerCache.Length; i++)
        {
            VRCPlayerApi candidate = _playerCache[i];
            if (candidate == null || !candidate.IsValid()) continue;

            Vector3 headPosition = GetPlayerHeadPosition(candidate);
            float distanceSquared = (headPosition - agentPosition).sqrMagnitude;
            
            if (distanceSquared > maxDistanceSquared) continue;
            if (distanceSquared >= closestDistanceSquared) continue;
            if (!IsWithinViewCone(headPosition, coneHalfAngleDeg)) continue;

            closestDistanceSquared = distanceSquared;
            closest = candidate;
        }

        return closest;
    }

    // Returns the closest player whose head is within maxDistance, regardless of
    // facing (used for hearing); null if nobody is in range. A moving player is heard
    // through walls, a player standing still only with a clear line of sight.
    protected VRCPlayerApi FindClosestPlayerWithin(float maxDistance)
    {
        int totalPlayerCount = VRCPlayerApi.GetPlayerCount();
        VRCPlayerApi.GetPlayers(_playerCache);

        Vector3 agentPosition = transform.position;
        float maxDistanceSquared = maxDistance * maxDistance;
        VRCPlayerApi closest = null;
        float closestDistanceSquared = float.MaxValue;

        for (int i = 0; i < totalPlayerCount && i < _playerCache.Length; i++)
        {
            VRCPlayerApi candidate = _playerCache[i];
            if (candidate == null || !candidate.IsValid()) continue;

            Vector3 headPosition = GetPlayerHeadPosition(candidate);
            float distanceSquared = (headPosition - agentPosition).sqrMagnitude;

            if (distanceSquared > maxDistanceSquared) continue;
            if (distanceSquared >= closestDistanceSquared) continue;

            bool isStandingStill = candidate.GetVelocity().magnitude < StandingStillSpeed;
            if (isStandingStill && !HasLineOfSight(headPosition)) continue;

            closestDistanceSquared = distanceSquared;
            closest = candidate;
        }

        return closest;
    }

    // Inside the cone angle and nothing blocking the view.
    protected bool IsWithinViewCone(Vector3 targetWorldPosition, float coneHalfAngleDeg)
    {
        Vector3 raycastOrigin = transform.position + new Vector3(0f, EyeHeight, 0f);
        Vector3 offsetToTarget = targetWorldPosition - raycastOrigin;

        float distanceToTarget = offsetToTarget.magnitude;
        if (distanceToTarget <= 0.0001f) return true;

        Vector3 directionToTarget = offsetToTarget / distanceToTarget;

        if (Vector3.Angle(transform.forward, directionToTarget) > coneHalfAngleDeg) return false;

        return HasLineOfSight(targetWorldPosition);
    }

    // The ray is cast only up to just short of the target, so any hit means
    // something is in the way.
    // NOTE: ~0 includes the ghost's own layer; a solid (non-trigger) collider on
    // the ghost itself can block its own view.
    private bool HasLineOfSight(Vector3 targetWorldPosition)
    {
        Vector3 raycastOrigin = transform.position + new Vector3(0f, EyeHeight, 0f);
        Vector3 offsetToTarget = targetWorldPosition - raycastOrigin;

        float distanceToTarget = offsetToTarget.magnitude;
        if (distanceToTarget <= 0.0001f) return true;

        return !Physics.Raycast(raycastOrigin, offsetToTarget / distanceToTarget,
            distanceToTarget - 0.2f,
            ~0,
            QueryTriggerInteraction.Ignore);
    }

    // ==============================
    // NavMeshAgent helpers
    // ==============================

    // Snap a world point onto the nearest NavMesh before trying to find a path to it.
    // Returns false if nothing on the mesh is within range.
    protected bool SetDestinationOnNavMesh(Vector3 worldPoint)
    {
        if (NavMesh.SamplePosition(worldPoint, out var navMeshHit, NavMeshSampleRadius, NavMesh.AllAreas))
        {
            return _navMeshAgent.SetDestination(navMeshHit.position);
        }

        return false;
    }

    // Allow the navmesh agent to move and rotate the npc.
    protected void EnsureAgentCanMove()
    {
        _navMeshAgent.isStopped = false;
        _navMeshAgent.updateRotation = true;
    }

    // Prevent the navmesh agent from controlling the rotation and position of the
    // npc so we can move it from code directly.
    protected void EnsureAgentIsIdle()
    {
        _navMeshAgent.isStopped = true;
        _navMeshAgent.updateRotation = false;
    }

    // Smoothly rotate (horizontal only) toward a fixed world point. Used at close
    // range, where the agent's velocity-based rotation freezes or snaps.
    protected void TurnTowardsWorldPosition(Vector3 worldPosition, float degreesPerSecond)
    {
        Vector3 direction = worldPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRotation, degreesPerSecond * Time.deltaTime);
    }

    protected float HorizontalAngleToPosition(Vector3 worldPosition)
    {
        Vector3 direction = worldPosition - transform.position;
        direction.y = 0f;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        return Vector3.Angle(forward, direction);
    }

    // ==============================
    // Indicator
    // ==============================

    protected void SetIndicatorSymbol(string symbol)
    {
        Indicator.text = symbol;
    }
}
