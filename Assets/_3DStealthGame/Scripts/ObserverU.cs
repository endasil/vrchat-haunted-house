using Assets._3DStealthGame.Scripts.Enums;

using UdonSharp;

using UnityEngine;

using VRC.SDKBase;

public class ObserverU : UdonSharpBehaviour
{
    private bool _isPlayerInRange;
    private GameEndingU _gameEnding;

    // Which ghost this trigger belongs to.
    private GhostType _ghostType = GhostType.None;

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
        while (t != null && _ghostType == GhostType.None)
        {
            if (t.GetComponent<ButlerGhost>() != null)
                _ghostType = GhostType.Butler;
            else if (t.GetComponent<SeekerGhost>() != null)
                _ghostType = GhostType.Seeker;
            else if (t.GetComponent<GargoyleU>() != null)
                _ghostType = GhostType.Gargoyle;
            t = t.parent;
        }
        if (_ghostType == GhostType.None)
            Debug.LogError($"ObserverU on '{transform.root.name}': no ghost AI script found on any parent, death messages will use the fallback line");
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player.isLocal)
        {
            _isPlayerInRange = true;
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
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        Vector3 origin = transform.position;
        // Aim at roughly the torso (feet + 1m) so the ray doesn't hit the floor.
        Vector3 offset = localPlayer.GetPosition() + Vector3.up - origin;
        const int allLayers = ~0;
        if (Physics.Raycast(origin,
                offset.normalized,
                out RaycastHit hit, offset.magnitude,
                allLayers, QueryTriggerInteraction.Ignore))
        {
            // A ray that hits a player returns collider == null
			//  A non-null collider is a wall between the ghost and the player.
            if (hit.collider != null) return;
        }

        _gameEnding.CaughtPlayerBy(_ghostType);
    }

}
