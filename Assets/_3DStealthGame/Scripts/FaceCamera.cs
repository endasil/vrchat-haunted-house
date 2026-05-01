using UnityEngine;

using UdonSharp;
using VRC.SDKBase;

public class FaceCamera : UdonSharpBehaviour
{
    private VRCPlayerApi localPlayer;

    void Start()
    {
        localPlayer = Networking.LocalPlayer;
    }

    void LateUpdate()
    {
        if (localPlayer == null || !localPlayer.IsValid()) return;

        Vector3 playerPos = localPlayer.GetPosition();
        Vector3 direction = transform.position - playerPos;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }
}