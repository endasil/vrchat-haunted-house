using TMPro;

using UdonSharp;

using UnityEngine;
using UnityEngine.AI;

using VRC.SDKBase;

// Shared plumbing for the ghost AIs (seeker and butler): NavMesh agent ownership,
// player scanning with a vision cone + line-of-sight check, steering helpers and
// the floating indicator text. Each ghost keeps its own state machine in its own
// class; this base only holds what used to be duplicated between them.
//
// Never attached to a GameObject directly, so it has no UdonSharpProgramAsset
// (same as Resettable).
public class GhostAgentBase : UdonSharpBehaviour
{
    // How far a point may be from the NavMesh and still get snapped onto it.
    protected const float NavMeshSampleRadius = 2f;

    // Height above the ghost's pivot the vision raycast originates from.
    protected const float EyeHeight = 0.5f;

    // Floating state symbol above the ghost (e.g. "!" when a player is spotted).
    // Looked up in children if not assigned in the inspector. Null is fine.
    public TextMeshPro Indicator;

    // Forward vision cone: half-angle in degrees (so 45 = a 90° cone) and how far
    // the ghost can see. Each ghost sets its own values in the inspector.
    public float visionHalfAngle = 45f;
    public float visionLength = 10f;

    protected NavMeshAgent _navMeshAgent;

    // Preallocated for VRChat's max instance size; can't use lists in U#.
    protected VRCPlayerApi[] _playerCache;

    // Cached so Update doesn't pay for Networking.IsOwner every frame. Kept up to
    // date by OnOwnershipTransferred.
    protected bool _isOwner;

    // Cache components, preallocate the player cache and hand the NavMeshAgent to
    // the owner only. On everyone else the transform comes from VRCObjectSync, so
    // a live agent here would just fight the synced position.
    protected void InitGhostAgent()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();
        if (_navMeshAgent == null)
            Debug.LogError($"{gameObject.name} is missing a NavMeshAgent component.");

        if (Indicator == null)
            Indicator = GetComponentInChildren<TextMeshPro>();

        _playerCache = new VRCPlayerApi[80];

        _isOwner = Networking.IsOwner(gameObject);
        if (_navMeshAgent != null)
            _navMeshAgent.enabled = _isOwner;
    }

    // When the previous owner leaves, a new client takes over driving the ghost.
    // Turn the agent on for the new owner (off for everyone else) and warp it to
    // where the ghost currently is, since it was disabled and never tracked the
    // synced position. Subclasses reset their state machine in OnBecameAgentOwner.
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        _isOwner = Networking.IsOwner(gameObject);

        if (_navMeshAgent == null) return;

        _navMeshAgent.enabled = _isOwner;

        if (_isOwner)
        {
            _navMeshAgent.Warp(transform.position);
            OnBecameAgentOwner();
        }
    }

    // Hook for subclasses: this client just took over the AI, reset the state
    // machine to something sane.
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
    // facing or walls (used for hearing); null if nobody is in range.
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

            float distanceSquared = (GetPlayerHeadPosition(candidate) - agentPosition).sqrMagnitude;

            if (distanceSquared > maxDistanceSquared) continue;
            if (distanceSquared >= closestDistanceSquared) continue;

            closestDistanceSquared = distanceSquared;
            closest = candidate;
        }

        return closest;
    }

    // Inside the cone angle and nothing blocking the view. The ray is cast only
    // up to just short of the target, so any hit means something is in the way.
    // NOTE: ~0 includes the ghost's own layer; a solid (non-trigger) collider on
    // the ghost itself can block its own view.
    protected bool IsWithinViewCone(Vector3 targetWorldPosition, float coneHalfAngleDeg)
    {
        Vector3 raycastOrigin = transform.position + new Vector3(0f, EyeHeight, 0f);
        Vector3 offsetToTarget = targetWorldPosition - raycastOrigin;

        float distanceToTarget = offsetToTarget.magnitude;
        if (distanceToTarget <= 0.0001f) return true;

        Vector3 directionToTarget = offsetToTarget / distanceToTarget;

        if (Vector3.Angle(transform.forward, directionToTarget) > coneHalfAngleDeg) return false;

        return !Physics.Raycast(
            raycastOrigin,
            directionToTarget,
            distanceToTarget - 0.2f,
            ~0,
            QueryTriggerInteraction.Ignore);
    }

    // ==============================
    // NavMeshAgent helpers
    // ==============================

    // Snap a world point onto the NavMesh before pathing to it. Points off the
    // mesh (like a player's head 1.5m up) otherwise map onto a nearby wall edge
    // and produce a useless partial path. Returns false if nothing on the mesh
    // is within range.
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

    // ==============================
    // Indicator
    // ==============================

    protected void SetIndicatorSymbol(string symbol)
    {
        if (Indicator == null) return;
        Indicator.text = symbol;
    }
}
