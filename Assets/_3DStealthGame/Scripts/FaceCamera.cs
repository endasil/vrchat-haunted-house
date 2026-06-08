using UnityEngine;

using UdonSharp;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FaceCamera : UdonSharpBehaviour
{
    private VRCPlayerApi _localPlayer;

    void Start()
    {
        _localPlayer = Networking.LocalPlayer;
    }

    void LateUpdate()
    {
        if (_localPlayer == null || !_localPlayer.IsValid()) return;

        Vector3 playerPos = _localPlayer.GetPosition();
        Vector3 direction = transform.position - playerPos;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }
}