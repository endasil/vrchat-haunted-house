#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable UNT0026
using Assets._3DStealthGame.Scripts.Enums;
using UdonSharp;
using UnityEngine;
using UnityEngine.AI;
using VRC.SDKBase;

namespace Assets._3DStealthGame.Scripts
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class DoorU : Resettable
    {
        private const float OwnershipTransferWaitSeconds = 1f;

        public float openDuration = 1f;
        [UdonSynced] private bool _openForGhosts;
        [UdonSynced] private int _openerPlayerId;
        private bool _opened;
        private bool _opening;
        private float _timer;
        private Quaternion _startRot;
        private Quaternion _targetRot;
        private NavMeshObstacle _navMeshObstacle;

        public PillColor PillColor;
        public float FloorAngle = 90;
        public override void Start()
        {
            _navMeshObstacle = GetComponent<NavMeshObstacle>();
            if (_navMeshObstacle == null)
            {
                Debug.LogError($"DoorU ({gameObject.name}): needs a NavMeshObstacle component on the same GameObject.", gameObject);
            }

            _startRot = transform.localRotation;
            _targetRot = Quaternion.Euler(FloorAngle, _startRot.eulerAngles.y, _startRot.eulerAngles.z);
            base.Start();
        }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player)
        {
            if (!player.isLocal || _opened || _opening) return;

            GameObject[] playerObjects = player.GetPlayerObjects();

            PlayerInventory playerInventory = playerObjects[0].GetComponent<PlayerInventory>();


            if (playerInventory != null)
            {
                if (playerInventory.HasPill(PillColor))
                {
                    float fallAngle = Mathf.Abs(FloorAngle);


                    // Translate the players position form world space to local space
                    // for easier comparsion of position.
                    var playerPosInLocalSpace = transform.InverseTransformPoint(player.GetPosition());
                    var playerInFrontOfDoor = playerPosInLocalSpace.z >= 0f;
                    
                    // Determine fall direction of door depending on which side the player is on 
                    // some doors can be approached from both sides.
                    if (playerInFrontOfDoor)
                    {
                        fallAngle = -fallAngle;
                    }

                    _targetRot = Quaternion.Euler(fallAngle, _startRot.eulerAngles.y, _startRot.eulerAngles.z);
                    _timer = 0f;
                    _opening = true;
                }
                else
                {
                    return;
                }
            }
            else
            {
                Debug.LogError($"{gameObject.name}: Unable to find PlayerInventory script on player object");
            }
        }

        void Update()
        {
            if (!_opening || _opened) return;

            _timer += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(_timer / openDuration);

            transform.localRotation = Quaternion.Slerp(_startRot, _targetRot, normalizedTime );

            if (normalizedTime  >= 1f)
            {
                _opened = true;
                _opening = false;
                OpenForGhosts();
            }
        }

        public override void ResetState()
        {
            _opening = false;
            _opened = false;
            _timer = 0f;
            transform.localRotation = _startRot;

            if (Networking.IsOwner(gameObject) && _openForGhosts)
            {
                CloseForGhosts();
            }
        }

        public override void OnDeserialization()
        {
            ApplyGhostPassage();
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (!_openForGhosts || player.playerId != _openerPlayerId) return;

            SendCustomEventDelayedSeconds(nameof(_CloseIfOpenerLeft), OwnershipTransferWaitSeconds);
        }

        public void _CloseIfOpenerLeft()
        {
            if (!Networking.IsOwner(gameObject) || !_openForGhosts) return;
            if (Utilities.IsValid(VRCPlayerApi.GetPlayerById(_openerPlayerId))) return;

            CloseForGhosts();
        }

        private void OpenForGhosts()
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            Networking.SetOwner(localPlayer, gameObject);
            _openForGhosts = true;
            _openerPlayerId = localPlayer.playerId;
            RequestSerialization();
            ApplyGhostPassage();
        }

        private void CloseForGhosts()
        {
            _openForGhosts = false;
            RequestSerialization();
            ApplyGhostPassage();
        }

        private void ApplyGhostPassage()
        {
            _navMeshObstacle.enabled = !_openForGhosts;
        }
    }
}