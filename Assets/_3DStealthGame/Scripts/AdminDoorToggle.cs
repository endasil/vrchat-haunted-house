using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Assets._3DStealthGame.Scripts
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class AdminDoorToggle : UdonSharpBehaviour
    {
        public GameObject door;
        public GameObject fireplaceTeleport;
        public AdminList adminList;

        [UdonSynced] private bool _doorActive = true;

        private void Start()
        {
            if (door == null || fireplaceTeleport == null || adminList == null)
            {
                Debug.LogError($"AdminDoorToggle ({gameObject.name}): door, fireplaceTeleport and adminList need to be assigned in the inspector.");
                return;
            }
            // Only update the door state for the owner 
            // (instance master at first) should update 
            // this state at start to avoid having other
            // people joining changing the state.
            if (Networking.IsOwner(gameObject))
            {
                _doorActive = door.activeSelf;
                RequestSerialization();
            }
            ShowOrHideDoorAndFireplace();

            // The button stays active for everyone, so OnDeserialization still runs for players who are not admins.
            if (!adminList.IsAdmin(Networking.LocalPlayer))
            {
                GetComponent<Renderer>().enabled = false;
                GetComponent<Collider>().enabled = false;
            }
        }

        public override void Interact()
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (!adminList.IsAdmin(localPlayer)) return;

            Networking.SetOwner(localPlayer, gameObject);
            _doorActive = !_doorActive;
            ShowOrHideDoorAndFireplace();
            RequestSerialization();
        }

        public override void OnDeserialization()
        {
            ShowOrHideDoorAndFireplace();
        }

        private void ShowOrHideDoorAndFireplace()
        {
            door.SetActive(_doorActive);
            fireplaceTeleport.SetActive(_doorActive);
        }
    }
}
