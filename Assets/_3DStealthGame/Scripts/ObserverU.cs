
#pragma warning disable IDE0090 // Use 'new(...)'
using UdonSharp;

using UnityEngine;

using VRC.SDKBase;

public class ObserverU : UdonSharpBehaviour
{
    private bool _isPlayerInRange;
    private GameEndingU _gameEnding;

    // Which ghost this trigger belongs to, worked out from the AI script on the
    // ghost root. Matches the ghost type ints in GameEndingU.
    private int _ghostType = -1;

    void Start()
    {
        var gameManagerObj = GameObject.Find("GameManager");
        if (gameManagerObj == null)
        {
            Debug.LogError("ObserverU: could not find a GameManager GameObject in the scene");
            return;
        }

        _gameEnding = gameManagerObj.GetComponent<GameEndingU>();
        if (_gameEnding == null)
            Debug.LogError("ObserverU: GameManager has no GameEndingU component");

        // Walk up the hierarchy and take the NEAREST ghost AI script.
        Transform t = transform;
        while (t != null && _ghostType == -1)
        {
            if (t.GetComponent<WaypointPatrolU>() != null)
                _ghostType = GameEndingU.GHOST_BUTLER;
            else if (t.GetComponent<GhostAISearching>() != null)
                _ghostType = GameEndingU.GHOST_SEEKER;
            else if (t.GetComponent<GargoyleU>() != null)
                _ghostType = GameEndingU.GHOST_GARGOYLE;
            t = t.parent;
        }
        if (_ghostType == -1)
            Debug.LogError($"ObserverU on '{transform.root.name}': no ghost AI script found on any parent, death messages will use the fallback line");
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player.isLocal)
        {
            _isPlayerInRange = true;
            // Catch right here too, not only in Update: the trigger can flicker
            // in/out every physics step (the ghost's rigidbody jitters the
            // collider), and then the exit clears the flag before Update ever
            // sees it.
            TryCatch();
        }
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (player.isLocal)
        {
            _isPlayerInRange = false;
        }
    }

    // The trigger capsule on this object is the ghost's vision cone. While the
    // player is inside it, cast a ray at them and look at what the ray meets
    // first: the player's own capsule (or nothing solid) means a clear view and
    // they're caught; anything else is a wall in between and they're safe.
    private void Update()
    {
        if (_isPlayerInRange)
            TryCatch();
    }

    private void TryCatch()
    {
        if (_gameEnding == null)
            return;

        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        if (localPlayer == null)
            return;

        Vector3 origin = transform.position;
        // Aim at roughly the torso (feet + 1m) so the ray doesn't hit the floor.
        Vector3 offset = localPlayer.GetPosition() + Vector3.up - origin;
        float distToPlayer = offset.magnitude;
        const int allLayers = ~0;
        if (distToPlayer > 0.0001f
            && Physics.Raycast(origin,
                offset / distToPlayer,
                out RaycastHit hit, distToPlayer,
                allLayers, QueryTriggerInteraction.Ignore))
        {
            // VRChat hides player colliders from Udon: a ray that hits a player
            // reports the hit but with collider == null. So null collider means
            // we hit the player. Only a real (non-null) collider on
            // a non-player layer is a wall in the way.
            Collider hitCollider = hit.collider;
            if (hitCollider != null)
                return;
        }

        _gameEnding.CaughtPlayerBy(_ghostType);
    }

}
