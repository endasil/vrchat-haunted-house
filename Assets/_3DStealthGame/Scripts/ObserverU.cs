
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
            Debug.LogError($"ObserverU ({gameObject.name}): could not find a GameManager GameObject in the scene");
            return;
        }

        _gameEnding = gameManagerObj.GetComponent<GameEndingU>();
        if (_gameEnding == null)
            Debug.LogError($"ObserverU ({gameObject.name}): GameManager has no GameEndingU component");

        // Uses the ghost AI script on this object, or on the closest parent that has one.
        Transform t = transform;
        while (t != null && _ghostType == -1)
        {
            if (t.GetComponent<ButlerGhost>() != null)
                _ghostType = GameEndingU.GHOST_BUTLER;
            else if (t.GetComponent<SeekerGhost>() != null)
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

    // The trigger capsule on this object is the ghost's vision cone.
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
            // A ray that hits a player returns collider == null
			//  A non-null collider is a wall between the ghost and the player.
            Collider hitCollider = hit.collider;
            if (hitCollider != null)
                return;
        }

        _gameEnding.CaughtPlayerBy(_ghostType);
    }

}
