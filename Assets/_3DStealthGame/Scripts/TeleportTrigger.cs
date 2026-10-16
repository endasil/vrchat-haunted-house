using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Assets._3DStealthGame.Scripts
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TeleportTrigger : UdonSharpBehaviour
    {
        public Transform destination;

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (!player.isLocal) return;

            if (destination == null)
            {
                Debug.LogError($"TeleportTrigger ({gameObject.name}): destination needs to be assigned in the inspector.");
                return;
            }

            player.TeleportTo(destination.position, destination.rotation);
        }
    }
}
